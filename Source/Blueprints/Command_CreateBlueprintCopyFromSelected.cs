using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public class Command_CreateBlueprintCopyFromSelected : Command
    {
        public Command_CreateBlueprintCopyFromSelected()
        {
            icon             = Resources.Icon_AddBlueprint;
            defaultLabel     = "Fluffy.Blueprints.Copy".Translate();
            defaultDesc      = "Fluffy.Blueprints.CopyHelp".Translate();
            tutorTag         = "Blueprint";
        }

        public override bool Visible => MapComponent_Copy.Valid;

        public override void ProcessInput( Event ev )
        {
            base.ProcessInput( ev );
            Blueprint.Create( Find.Selector.SelectedObjects.OfType<Thing>().Where( b => b.IsValidBlueprintThing() ), true );
        }

        public override GizmoResult GizmoOnGUI( Vector2 topLeft, float maxWidth )
        {
            var result = base.GizmoOnGUI( topLeft, maxWidth );
            var labelRect = new Rect( topLeft.x + 5, topLeft.y + 5, maxWidth, 18 );
            Text.Font = GameFont.Tiny;
            var label = Mod.Settings.CopyKey.Label;
            var size = Text.CalcSize( label );
            Widgets.DrawBoxSolid( new Rect( topLeft + new Vector2( 3, 3 ), size + new Vector2( 5, 0) ),
                                  new Color( 0f, 0f, 0f, .4f ) );
            Widgets.Label( labelRect, label );
            Text.Font = GameFont.Small;
            return result;
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                var options = new List<FloatMenuOption>();
                options.Add( new FloatMenuOption("Fluffy.Blueprints.CreateFromSelection".Translate(), () =>
                {
                    Blueprint.Create( Find.Selector.SelectedObjects.OfType<Thing>()
                                         .Where( b => b.IsValidBlueprintThing() ) );
                } ) );
                return options;
            }
        }
    }
}