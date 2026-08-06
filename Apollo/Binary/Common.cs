using System;

using Apollo.Core;
using Apollo.Devices;
using Apollo.Elements;
using Apollo.Elements.Launchpads;
using Apollo.Helpers;
using Apollo.Structures;
using Apollo.Undo;

namespace Apollo.Binary {
    public static class Common {
        // Far above stock's version line so stock Apollo always treats mod files as "newer" and
        // warns instead of mis-reading them. CompatibleVersion is the stock version exports target.
        public const int version = 1000;
        public const int CompatibleVersion = 32;

        public static readonly Type[] id = new[] {
            typeof(Preferences),
            typeof(Copyable),

            typeof(Project),
            typeof(Track),
            typeof(Chain),
            typeof(Device),
            typeof(Launchpad),

            typeof(Group),
            typeof(Copy),
            typeof(Delay),
            typeof(Fade),
            typeof(Flip),
            typeof(Hold),
            typeof(KeyFilter),
            typeof(Layer),
            typeof(Move),
            typeof(Multi),
            typeof(Output),
            typeof(MacroFilter),
            typeof(Switch),
            typeof(Paint),
            typeof(Pattern),
            typeof(Preview),
            typeof(Rotate),
            typeof(Tone),

            typeof(Color),
            typeof(Frame),
            typeof(Length),
            typeof(Offset),
            typeof(Time),

            typeof(Choke),
            typeof(ColorFilter),
            typeof(Clear),
            typeof(LayerFilter),
            typeof(Loop),
            typeof(Refresh),
            typeof(UndoManager),

            typeof(Underlights)
        };
    }
}