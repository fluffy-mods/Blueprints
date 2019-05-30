using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;

namespace Blueprints
{
    public class Designator_CreateBlueprint : Designator
    {
        public Designator_CreateBlueprint()
        {
            icon             = Resources.Icon_AddBlueprint;
            defaultLabel     = "Fluffy.Blueprints.Create".Translate();
            defaultDesc      = "Fluffy.Blueprints.CreateHelp".Translate();
            useMouseIcon     = true;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundSucceeded   = SoundDefOf.Designate_PlanAdd;
            tutorTag         = "Blueprint";
        }

        public override int DraggableDimensions => 2;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                var options = new List<FloatMenuOption>();

                foreach ( var file in Controller.GetSavedFilesList() )
                {
                    var name = Path.GetFileNameWithoutExtension( file.Name );
                    if ( Controller.FindBlueprint( name ) == null )
                        options.Add( new FloatMenuOption( "Fluffy.Blueprints.LoadFromXML".Translate( name ),
                                                          delegate
                                                          {
                                                              Controller.Add( Controller.LoadFromXML( file.Name ) );
                                                          } ) );
                }

                if ( options.NullOrEmpty() )
                    Messages.Message( "Fluffy.Blueprints.NoStoredBlueprints".Translate(),
                                      MessageTypeDefOf.RejectInput );
                return options;
            }
        }

        public override AcceptanceReport CanDesignateCell( IntVec3 loc )
        {
            var things = loc.GetThingList( Map );
            return loc.InBounds( Map ) &&
                   ( loc.GetTerrain( Map ).IsValidBlueprintTerrain() ||
                     !things.NullOrEmpty() &&
                     things.Any( thing => thing.IsValidBlueprintThing() ) );
        }

        public override void RenderHighlight( List<IntVec3> dragCells )
        {
            DesignatorUtility.RenderHighlightOverSelectableCells( this, dragCells );
        }

        public override void DesignateMultiCell( IEnumerable<IntVec3> cells )
        {
            // bail out if empty
            if ( cells == null || cells.Count() == 0 )
            {
                Messages.Message( "Fluffy.Blueprints.CannotCreateBluePrint_NothingSelected".Translate(),
                                  MessageTypeDefOf.RejectInput );
                return;
            }

            // get list of buildings in the cells, note that this includes frames and blueprints, and so _may include floors!_
            var things = new List<Thing>( cells.SelectMany( cell => cell.GetThingList( Map )
                                                                        .Where( thing => thing
                                                                                   .IsValidBlueprintThing() ) )
                                               .Distinct() );

            // get list of creatable terrains
            var terrains = new List<Pair<TerrainDef, IntVec3>>();
            terrains.AddRange( cells.Select( cell => new Pair<TerrainDef, IntVec3>( cell.GetTerrain( Map ), cell ) )
                                    .Where( p => p.First.IsValidBlueprintTerrain() ) );

            // get edges of blueprint area
            // (might be bigger than selected region, but never smaller).
            var allCells = cells.Concat( things.SelectMany( thing => thing.OccupiedRect().Cells ) );

            var left   = allCells.Min( cell => cell.x );
            var top    = allCells.Max( cell => cell.z );
            var right  = allCells.Max( cell => cell.x );
            var bottom = allCells.Min( cell => cell.z );

            // total size ( +1 because x = 2 ... x = 4 => 4 - 2 + 1 cells )
            var size = new IntVec2( right - left + 1, top - bottom + 1 );

            // fetch origin for default (North) orientation
            var origin = Resources.CenterPosition( new IntVec3( left, 0, bottom ), size, Rot4.North );

            // create list of buildables
            var buildables = new List<BuildableInfo>();
            foreach ( var thing in things )
                buildables.Add( new BuildableInfo( thing, origin ) );
            foreach ( var terrain in terrains )
                buildables.Add( new BuildableInfo( terrain.First, terrain.Second, origin ) );

            // try to get a decent default name: check if selection contains only a single room - if so, that's a decent name.
            var    room        = origin.GetRoom( Map );
            string defaultName = null;
            if ( room != null && room.Role != RoomRoleDefOf.None )
                defaultName = room.Role.LabelCap;
            // TODO: multiple (same) rooms, etc.

            // add to controller - controller handles adding to designations
            var blueprint = new Blueprint( buildables, size, defaultName );
            Controller.Add( blueprint );

            blueprint.Debug();
        }
    }
}