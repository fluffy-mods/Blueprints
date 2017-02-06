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
            return terrain.designationCategory != null;
        }
        
        public static bool IsValidBlueprintThing( this Thing thing )
        {
            if ( thing is RimWorld.Blueprint )
                return ( thing as RimWorld.Blueprint ).def.entityDefToBuild.designationCategory != null && thing.Faction == Faction.OfPlayer;
            if ( thing is RimWorld.Frame )
                return ( thing as RimWorld.Frame ).def.entityDefToBuild.designationCategory != null && thing.Faction == Faction.OfPlayer;
            return thing.def.designationCategory != null && thing.Faction == Faction.OfPlayer;
        }

        #endregion Methods
    }
}
