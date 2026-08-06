using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Apollo.RtMidi.Devices;
using Apollo.RtMidi.Devices.Infos;
using Apollo.Structures;

namespace Apollo.Core {
    // Owns the Teensy (midiLED) connection and sends strip colours as SysEx. A single sender thread
    // coalesces frames (latest wins) and only transmits changed LEDs, so fast animations never flood
    // the wire. The render thread only touches stateLock, never blocking on a send holding portLock.
    public static class UnderlightsConnector {
        public const string PortIdentifier = "MIDI LED";

        static readonly byte[] SysExClear = new byte[] { 0xF0, 0x7D, 0x01, 0xF7 };

        static readonly object stateLock = new object();
        static readonly object portLock = new object();

        static IMidiOutputDevice output;
        static volatile bool connected;

        static Color[] pending;
        static bool clearRequested;
        static bool senderRunning;

        static Color[] displayed;

        public static bool Connected => connected;

        public static bool IsUnderlightsPort(string name)
            => name != null && name.ToUpper().Contains(PortIdentifier);

        public static void Connect(IMidiOutputDeviceInfo info) {
            if (connected || info == null) return;

            lock (portLock) {
                output = info.CreateDevice();
                output.Open();
            }

            lock (stateLock) {
                connected = true;
                pending = null;
                clearRequested = true;
                EnsureSender();
            }

            Program.Log("Underlights Connected");
        }

        public static void Disconnect() {
            if (!connected) return;

            lock (stateLock) {
                connected = false;
                pending = null;
            }

            lock (portLock) {
                if (output == null) return;

                try {
                    if (output.IsOpen) {
                        output.Send(SysExClear);
                        output.Close();
                    }
                    output.Dispose();
                } catch {}

                output = null;
            }

            Program.Log("Underlights Disconnected");
        }

        public static void SendStrip(IReadOnlyList<Color> strip) {
            if (!connected || strip == null) return;

            Color[] copy = new Color[strip.Count];
            for (int i = 0; i < strip.Count; i++) copy[i] = strip[i]?? new Color(0);

            lock (stateLock) {
                if (!connected) return;

                pending = copy;
                EnsureSender();
            }
        }

        public static void Clear() {
            if (!connected) return;

            lock (stateLock) {
                if (!connected) return;

                clearRequested = true;
                pending = null;
                EnsureSender();
            }
        }

        static void EnsureSender() {
            if (senderRunning) return;

            senderRunning = true;
            Task.Run(SenderLoop);
        }

        static void SenderLoop() {
            while (true) {
                Color[] target;
                bool doClear;

                lock (stateLock) {
                    if (!connected) {
                        senderRunning = false;
                        return;
                    }

                    target = pending;
                    pending = null;
                    doClear = clearRequested;
                    clearRequested = false;

                    if (target == null && !doClear) {
                        senderRunning = false;
                        return;
                    }
                }

                if (doClear) {
                    displayed = null;
                    SendRaw(SysExClear);
                }

                if (target != null) {
                    byte[] diff = BuildDiff(target);
                    if (diff != null) SendRaw(diff);
                }
            }
        }

        static byte[] BuildDiff(Color[] target) {
            if (displayed == null || displayed.Length != target.Length)
                displayed = new Color[target.Length];

            List<byte> message = new() { 0xF0, 0x7D, 0x00 };
            bool changed = false;

            for (int i = 0; i < target.Length && i < 128; i++) {
                Color color = target[i]?? new Color(0);

                if (displayed[i] == null || displayed[i] != color) {
                    message.Add((byte)i);
                    message.Add(color.Red);
                    message.Add(color.Green);
                    message.Add(color.Blue);

                    displayed[i] = color;
                    changed = true;
                }
            }

            if (!changed) return null;

            message.Add(0xF7);
            return message.ToArray();
        }

        static void SendRaw(byte[] message) {
            lock (portLock) {
                if (output == null) return;
                try {
                    output.Send(message);
                } catch {}
            }
        }
    }
}
