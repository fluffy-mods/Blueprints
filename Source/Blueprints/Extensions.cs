using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Blueprints
{
    public static class Extensions
    {
        #region Methods

        public static bool IsValidBlueprintTerrain( this TerrainDef terrain )
        {
            return !terrain.designationCategory.NullOrEmpty();
        }

        public static bool IsValidBlueprintThing( this Thing thing )
        {
            return !thing.def.designationCategory.NullOrEmpty() && thing.Faction == Faction.OfPlayer;
        }

        #endregion Methods
    }
}