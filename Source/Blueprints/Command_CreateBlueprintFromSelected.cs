using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public class Command_CreateBlueprintFromSelected : Command
    {
        public Command_CreateBlueprintFromSelected()
        {
            icon             = Resources.Icon_AddBlueprint;
            defaultLabel     = "Fluffy.Blueprints.CreateFromSelection".Translate();
            defaultDesc      = "Fluffy.Blueprints.CreateFromSelectionHelp".Translate();
            tutorTag         = "Blueprint";
        }

        public override bool Visible
        {
            get
            {
                return Find.Selector.SelectedObjects.OfType<Thing>().Count( b => b.IsValidBlueprintThing() ) > 2;
            }
        }

        public override void ProcessInput( Event ev )
        {
            base.ProcessInput( ev );
            CreateBlueprint( Find.Selector.SelectedObjects.OfType<Thing>().Where( b => b.IsValidBlueprintThing() ) );
        }

        public void CreateBlueprint( IEnumerable<Thing> things )
        {
            // get edges of blueprint area
            // (might be bigger than selected region, but never smaller).
            var cells = things.SelectMany( thing => thing.OccupiedRect().Cells );

            var left   = cells.Min( cell => cell.x );
            var top    = cells.Max( cell => cell.z );
            var right  = cells.Max( cell => cell.x );
            var bottom = cells.Min( cell => cell.z );

            // total size ( +1 because x = 2 ... x = 4 => 4 - 2 + 1 cells )
            var size = new IntVec2( right - left + 1, top - bottom + 1 );

            // fetch origin for default (North) orientation
            var origin = Resources.CenterPosition( new IntVec3( left, 0, bottom ), size, Rot4.North );

            // create list of buildables
            var buildables = new List<BuildableInfo>();
            foreach ( var thing in things )
                buildables.Add( new BuildableInfo( thing, origin ) );
            
            // add to controller - controller handles adding to designations
            var blueprint = new Blueprint( buildables, size, "Selection" );
            BlueprintController.Add( blueprint );

            blueprint.Debug();
        }
    }
}