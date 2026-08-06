using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using Apollo.Core;
using Apollo.DeviceViewers;
using Apollo.Elements;
using Apollo.Enums;
using Apollo.Rendering;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Devices {
    // Mirrors the grid's edge onto an Ambilight-style LED strip (a Teensy running midiLED). Square
    // copies the outer ring of the 8x8 grid; Around copies the surrounding function buttons. Bypass
    // controls whether signals continue down the chain. Signals are fed into the shared
    // UnderlightsStrip compositor, so multiple devices blend by depth instead of colliding.
    public class Underlights: Device {
        // Edge buttons in XY index space (row * 10 + col; row 0 = bottom, col 0 = left), ordered
        // along the direction the strip travels on that side.
        static readonly byte[] SquareRight = { 88, 78, 68, 58, 48, 38, 28, 18 };  // top -> bottom
        static readonly byte[] SquareBottom = { 18, 17, 16, 15, 14, 13, 12, 11 }; // right -> left
        static readonly byte[] SquareLeft = { 11, 21, 31, 41, 51, 61, 71, 81 };   // bottom -> top
        static readonly byte[] SquareTop = { 81, 82, 83, 84, 85, 86, 87, 88 };    // left -> right

        static readonly byte[] AroundRight = { 89, 79, 69, 59, 49, 39, 29, 19 };
        static readonly byte[] AroundBottom = { 8, 7, 6, 5, 4, 3, 2, 1 };
        static readonly byte[] AroundLeft = { 10, 20, 30, 40, 50, 60, 70, 80 };
        static readonly byte[] AroundTop = { 91, 92, 93, 94, 95, 96, 97, 98 };

        readonly List<int>[] map = new List<int>[101];
        bool dirty = true;

        UnderlightsType _mode;
        public UnderlightsType Mode {
            get => _mode;
            set {
                _mode = value;
                dirty = true;

                if (Viewer?.SpecificViewer != null) ((UnderlightsViewer)Viewer.SpecificViewer).SetMode(Mode);

                if (Purpose == PurposeType.Active) UnderlightsStrip.Clear();
            }
        }

        bool _bypass;
        public bool Bypass {
            get => _bypass;
            set {
                _bypass = value;

                if (Viewer?.SpecificViewer != null) ((UnderlightsViewer)Viewer.SpecificViewer).SetBypass(Bypass);
            }
        }

        protected override object[] CloneParameters(PurposeType purpose)
            => new object[] { Mode, Bypass };

        public Underlights(UnderlightsType mode = UnderlightsType.Square, bool bypass = true): base("underlights") {
            Mode = mode;
            Bypass = bypass;

            if (Purpose == PurposeType.Active)
                Preferences.UnderlightsChanged += MarkDirty;
        }

        void MarkDirty() => dirty = true;

        static int SampleIndex(int led, int count, int sourceLength) {
            int index = (int)((led + 0.5) / count * sourceLength);
            return Math.Min(index, sourceLength - 1);
        }

        void AddSegment(byte[] side, int count, ref int offset) {
            for (int k = 0; k < count; k++) {
                int led = offset + k;
                if (led >= 128) break;

                byte grid = side[SampleIndex(k, count, side.Length)];
                (map[grid] ??= new List<int>()).Add(led);
            }

            offset += count;
        }

        void BuildMap() {
            for (int i = 0; i < map.Length; i++) map[i] = null;

            bool around = Mode == UnderlightsType.Around;

            // Clockwise from the top-right corner: right, bottom, left, top.
            int offset = 0;
            AddSegment(around? AroundRight : SquareRight, Preferences.UnderlightsRight, ref offset);
            AddSegment(around? AroundBottom : SquareBottom, Preferences.UnderlightsBottom, ref offset);
            AddSegment(around? AroundLeft : SquareLeft, Preferences.UnderlightsLeft, ref offset);
            AddSegment(around? AroundTop : SquareTop, Preferences.UnderlightsTop, ref offset);

            dirty = false;
        }

        public override void MIDIProcess(List<Signal> n) {
            if (Purpose == PurposeType.Active && UnderlightsConnector.Connected) {
                if (dirty) BuildMap();

                n.ForEach(signal => {
                    List<int> leds = map[signal.Index];
                    if (leds != null)
                        foreach (int led in leds)
                            UnderlightsStrip.Enter(led, signal.Color, signal.Layer, signal.BlendingMode, signal.BlendingRange);
                });

                Heaven.PlsTick();
            }

            if (Bypass) InvokeExit(n);
        }

        protected override void Stopped() {
            if (Purpose == PurposeType.Active) UnderlightsStrip.Clear();
        }

        public override void Dispose() {
            if (Disposed) return;

            if (Purpose == PurposeType.Active)
                Preferences.UnderlightsChanged -= MarkDirty;

            base.Dispose();
        }

        public class ModeUndoEntry: EnumSimplePathUndoEntry<Underlights, UnderlightsType> {
            protected override void Action(Underlights item, UnderlightsType element) => item.Mode = element;

            public ModeUndoEntry(Underlights underlights, UnderlightsType u, UnderlightsType r, IEnumerable source)
            : base("Underlights Mode", underlights, u, r, source) {}

            ModeUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }

        public class BypassUndoEntry: SimplePathUndoEntry<Underlights, bool> {
            protected override void Action(Underlights item, bool element) => item.Bypass = element;

            public BypassUndoEntry(Underlights underlights, bool u, bool r)
            : base($"Underlights Bypass Changed to {(r? "Enabled" : "Disabled")}", underlights, u, r) {}

            BypassUndoEntry(BinaryReader reader, int version)
            : base(reader, version) {}
        }
    }
}
