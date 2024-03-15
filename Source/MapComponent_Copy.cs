// MapComponent_Copy.cs
// Copyright Karel Kroeze, 2020-2020

using System.Linq;
using UnityEngine;
using Verse;

namespace Blueprints {
    public class MapComponent_Copy: MapComponent {
        public MapComponent_Copy(Map map) : base(map) {
        }

        public static bool Valid =>
            Find.Selector.SelectedObjects.OfType<Thing>().Count(b => b.IsValidBlueprintThing()) > 2;

        public override void MapComponentOnGUI() {
            if (Mod.Settings.CopyKey.JustPressed && Valid) {
                Event.current.Use();
                Blueprint.Create(Find.Selector.SelectedObjects.OfType<Thing>().Where(b => b.IsValidBlueprintThing()),
                                  true);
            }
        }
    }
}
