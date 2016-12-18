using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Blueprints
{
    [StaticConstructorOnStartup]
    public class Designator_CreateBlueprint : Designator
    {
        #region Public Constructors

        public Designator_CreateBlueprint()
        {
            // Silly A13+ workaround
            LongEventHandler.ExecuteWhenFinished( delegate
            {
                Resources.Icon_AddBlueprint = ContentFinder<Texture2D>.Get( "Icons/AddBlueprint", true );
                Resources.Icon_Blueprint = ContentFinder<Texture2D>.Get( "Icons/Blueprint", true );
                Resources.Icon_Edit = ContentFinder<Texture2D>.Get( "Icons/Edit", true );

                icon = Resources.Icon_AddBlueprint;
            } );

            defaultLabel = "Fluffy.Blueprints.Create".Translate();
            defaultDesc = "Fluffy.Blueprints.CreateHelp".Translate();
            useMouseIcon = true;
        }

        #endregion Public Constructors

        #region Public Properties

        public override int DraggableDimensions
        {
            get
            {
                return 2;
            }
        }

        #endregion Public Properties

        #region Public Methods

        public override AcceptanceReport CanDesignateCell( IntVec3 loc )
        {
            var things = loc.GetThingList( Map );
            return loc.InBounds( Map ) &&
                ( loc.GetTerrain( Map ).IsValidBlueprintTerrain() ||
                ( !things.NullOrEmpty() &&
                   things.Any( thing => thing.IsValidBlueprintThing() ) ) );
        }

        public override void ProcessInput( Event ev )
        {
            if ( ev.button == 1 )
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();

                foreach ( var file in Controller.GetSavedFilesList() )
                {
                    string name = Path.GetFileNameWithoutExtension( file.FileInfo.Name );
                    if ( Controller.FindBlueprint( name ) == null )
                    {
                        options.Add( new FloatMenuOption( "Fluffy.Blueprints.LoadFromXML".Translate( name ), delegate
                        { Controller.Add( Controller.LoadFromXML( file ) ); } ) );
                    }
                }

                if ( options.NullOrEmpty() )
                    Messages.Message( "Fluffy.Blueprints.NoStoredBlueprints".Translate(), MessageSound.RejectInput );
                else
                    Find.WindowStack.Add( new FloatMenu( options ) );
                return;
            }

            base.ProcessInput( ev );
        }

        public override void DesignateMultiCell( IEnumerable<IntVec3> cells )
        {
            // bail out if empty
            if ( cells == null || cells.Count() == 0 )
            {
                Messages.Message( "Fluffy.Blueprints.CannotCreateBluePrint_NothingSelected".Translate(), MessageSound.RejectInput );
                return;
            }

            // get list of buildings in the cells
            List<Thing> things = new List<Thing>( cells.SelectMany( cell => cell.GetThingList( Map )
                                                       .Where( thing => thing.IsValidBlueprintThing() ) )
                                                       .Distinct() );

            // get list of creatable terrains
            List<Pair<TerrainDef, IntVec3>> terrains = new List<Pair<TerrainDef, IntVec3>>();
            terrains.AddRange( cells.Select( cell => new Pair<TerrainDef, IntVec3>( cell.GetTerrain( Map ), cell ) )
                                    .Where( p => p.First.IsValidBlueprintTerrain() ) );

            // get edges of blueprint area
            // (might be bigger than selected region, but never smaller).
            var allCells = cells.Concat( things.SelectMany( thing => thing.OccupiedRect().Cells ) );

            int left = allCells.Min( cell => cell.x );
            int top = allCells.Max( cell => cell.z );
            int right = allCells.Max( cell => cell.x );
            int bottom = allCells.Min( cell => cell.z );

            // total size ( +1 because x = 2 ... x = 4 => 4 - 2 + 1 cells )
            IntVec2 size = new IntVec2( right - left + 1, top - bottom + 1 );

            // fetch origin for default (North) orientation
            IntVec3 origin = Resources.CenterPosition( new IntVec3( left, 0, bottom ), size, Rot4.North );

            // create list of buildables
            List<BuildableInfo> buildables = new List<BuildableInfo>();
            foreach ( var thing in things )
            {
                buildables.Add( new BuildableInfo( thing, origin ) );
            }
            foreach ( var terrain in terrains )
            {
                buildables.Add( new BuildableInfo( terrain.First, terrain.Second, origin ) );
            }

            // try to get a decent default name: check if selection contains only a single room - if so, that's a decent name.
            Room room = origin.GetRoom( Map );
            string defaultName = null;
            if ( room != null && room.Role != RoomRoleDefOf.None )
                defaultName = room.Role.LabelCap;
            // TODO: multiple (same) rooms, etc.

            // add to controller - controller handles adding to designations
            Blueprint blueprint = new Blueprint( buildables, size, defaultName );
            Controller.Add( blueprint );

#if DEBUG
            blueprint.Debug();
#endif
        }

        #endregion Public Methods
    }
}
