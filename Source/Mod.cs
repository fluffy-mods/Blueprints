// Mod.cs
// Copyright Karel Kroeze, 2020-2020

using UnityEngine;
using Verse;

namespace Blueprints {
    public class Mod: Verse.Mod {
        public static Settings Settings { get; private set; }
        public Mod(ModContentPack content) : base(content) {
            Settings = GetSettings<Settings>();
        }

        public override void DoSettingsWindowContents(Rect canvas) {
            Settings.DoWindowContents(canvas);
        }

        public override string SettingsCategory() {
            return "Fluffy.Blueprints".Translate();
        }
    }
}
