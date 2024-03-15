using RimWorld;
using Verse;

namespace Blueprints
{
    public static class Extensions
    {
        public static bool IsValidBlueprintTerrain( this TerrainDef terrain )
        {
            return terrain.designationCategory != null;
        }

        public static bool IsValidBlueprintThing( this Thing thing )
        {
            if ( thing is RimWorld.Blueprint blueprint )
                return blueprint.def.entityDefToBuild.designationCategory != null &&
                       thing.Faction                                      == Faction.OfPlayer;
            if ( thing is Frame frame )
                return frame.def.entityDefToBuild.designationCategory != null &&
                       thing.Faction                                  == Faction.OfPlayer;
            return thing.def.designationCategory != null && thing.Faction == Faction.OfPlayer;
        }
    }
}