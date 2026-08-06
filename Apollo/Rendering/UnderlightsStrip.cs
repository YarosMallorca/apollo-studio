using System;
using System.Collections.Generic;
using System.Linq;

using Apollo.Core;
using Apollo.Enums;
using Apollo.Structures;

namespace Apollo.Rendering {
    // Shared compositor for the physical Underlights strip. Every Underlights device maps its grid
    // signals onto strip-LED indices and feeds them here, so contributions from different chains
    // blend by Layer (like a Launchpad's Screen). Snapshotting rides Screen.Rendered to stay
    // FPS-limited.
    public static class UnderlightsStrip {
        const int MaxLeds = 128;

        class Contribution {
            public Color Color;
            public int Layer;
            public BlendingType Mode;
            public int Range;
        }

        class StripPixel {
            readonly SortedList<int, Contribution> _signals = new();
            readonly object locker = new object();

            public StripPixel() => Clear();

            public void Clear() {
                lock (locker) {
                    _signals.Clear();

                    // Base black layer, like Screen.Pixel: without it a lone non-Normal blend (e.g.
                    // a Layer set to Screen) has nothing below to composite against and is skipped.
                    _signals.Add(10000, new Contribution { Color = new Color(0), Layer = -100, Mode = BlendingType.Normal, Range = 200 });
                }
            }

            public void Enter(Color color, int layer, BlendingType mode, int range) {
                lock (locker) {
                    int key = -layer;

                    if (color.Lit) _signals[key] = new Contribution { Color = color.Clone(), Layer = layer, Mode = mode, Range = range };
                    else _signals.Remove(key);
                }
            }

            public Color GetColor() {
                lock (locker) {
                    Color ret = new Color(0);

                    for (int i = 0; i < _signals.Count; i++) {
                        Contribution signal = _signals.Values[i];
                        if (signal.Mode != BlendingType.Normal && ((i == _signals.Count - 1)? true : signal.Layer - _signals.Values[i + 1].Layer > signal.Range))
                            continue;

                        if (signal.Mode == BlendingType.Mask) break;

                        ret.Mix(signal.Color, (i == 0)? false : (_signals.Values[i - 1].Mode == BlendingType.Multiply && _signals.Values[i - 1].Layer - signal.Layer <= _signals.Values[i - 1].Range));

                        if (signal.Mode == BlendingType.Normal) break;
                    }

                    return ret;
                }
            }
        }

        static readonly StripPixel[] pixels = Enumerable.Range(0, MaxLeds).Select(_ => new StripPixel()).ToArray();

        static UnderlightsStrip() {
            Screen.Rendered += Snapshot;
        }

        public static void Enter(int led, Color color, int layer, BlendingType mode, int range) {
            if (0 <= led && led < MaxLeds)
                pixels[led].Enter(color, layer, mode, range);
        }

        public static void Clear() {
            foreach (StripPixel pixel in pixels) pixel.Clear();

            UnderlightsConnector.Clear();
        }

        static int StripLength() {
            int length = Preferences.UnderlightsTop + Preferences.UnderlightsRight + Preferences.UnderlightsBottom + Preferences.UnderlightsLeft;
            return Math.Min(length, MaxLeds);
        }

        static void Snapshot() {
            if (!UnderlightsConnector.Connected) return;

            int length = StripLength();

            List<Color> strip = new(length);
            for (int i = 0; i < length; i++)
                strip.Add(pixels[i].GetColor());

            UnderlightsConnector.SendStrip(strip);
        }
    }
}
