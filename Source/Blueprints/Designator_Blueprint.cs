using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Blueprints
{
    public class Designator_Blueprint : Designator
    {
        #region Fields

        private Blueprint _blueprint;

        private float _lineOffset = -4f;
        private float _middleMouseDownTime;
        private float _panelHeight = 999f;
        private Vector2 _scrollPosition = Vector2.zero;

        #endregion Fields

        #region Constructors

        public Designator_Blueprint( Blueprint blueprint )
        {
            _blueprint = blueprint;
            icon = Resources.Icon_Blueprint;
        }

        #endregion Constructors

        #region Properties

        public Blueprint Blueprint => _blueprint;
        public override int DraggableDimensions => 0;
        public override string Label => _blueprint.name;

        #endregion Properties

        #region Methods

        public override AcceptanceReport CanDesignateCell( IntVec3 loc )
        {
            // always return true - we're looking at the larger blueprint in SelectedUpdate() and DesignateSingleCell() instead.
            return true;
        }

        // note that even though we're designating a blueprint, as far as the game is concerned we're only designating the _origin_ cell
        public override void DesignateSingleCell( IntVec3 origin )
        {
            bool somethingSucceeded = false;
            bool planningMode = Event.current.shift;

            // looping through cells, place where needed.
            foreach ( var item in Blueprint.contents )
            {
                PlacementReport placementReport = item.CanPlace( origin );
                if ( planningMode && placementReport != PlacementReport.Alreadyplaced )
                {
                    item.Plan( origin );
                    somethingSucceeded = true;
                }
                if ( !planningMode && placementReport == PlacementReport.CanPlace )
                {
                    item.Designate( origin );
                    somethingSucceeded = true;
                }
            }

            // TODO: Add succeed/failure sounds, failure reasons
            Finalize( somethingSucceeded );
        }

        // copy-pasta from RimWorld.Designator_Place, with minor changes.
        public override void DoExtraGuiControls( float leftX, float bottomY )
        {
            Rect winRect = new Rect( leftX, bottomY - 90f, 200f, 90f );
            HandleRotationShortcuts();

            Find.WindowStack.ImmediateWindow( 73095, winRect, WindowLayer.GameUI, delegate
            {
                RotationDirection rotationDirection = RotationDirection.None;
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Medium;
                Rect rect = new Rect( winRect.width / 2f - 64f - 5f, 15f, 64f, 64f );
                if ( Widgets.ImageButton( rect, Resources.RotLeftTex ) )
                {
                    SoundDefOf.AmountDecrement.PlayOneShotOnCamera();
                    rotationDirection = RotationDirection.Counterclockwise;
                    Event.current.Use();
                }
                Widgets.Label( rect, KeyBindingDefOf.DesignatorRotateLeft.MainKeyLabel );
                Rect rect2 = new Rect( winRect.width / 2f + 5f, 15f, 64f, 64f );
                if ( Widgets.ImageButton( rect2, Resources.RotRightTex ) )
                {
                    SoundDefOf.AmountIncrement.PlayOneShotOnCamera();
                    rotationDirection = RotationDirection.Clockwise;
                    Event.current.Use();
                }
                Widgets.Label( rect2, KeyBindingDefOf.DesignatorRotateRight.MainKeyLabel );
                if ( rotationDirection != RotationDirection.None )
                {
                    Blueprint.Rotate( rotationDirection );
                }
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }, true, false, 1f );
        }

        public override void DrawPanelReadout( ref float curY, float width )
        {
            // we need the window's size to be able to do a scrollbar, but are not directly given that size.
            Rect outRect = ArchitectCategoryTab.InfoRect.AtZero();

            // our window actually starts from curY
            outRect.yMin = curY;

            // our contents height is given by final curY - which we conveniently saved
            Rect viewRect = new Rect( 0f, 0f, width, _panelHeight );

            // if contents are larger than available canvas, leave some room for scrollbar
            if ( viewRect.height > outRect.height )
            {
                outRect.width -= 16f;
                width -= 16f;
            }

            // since we're going to work in new GUI group (through the scrollview), we'll need to keep track of
            // our own relative Y position.
            float oldY = curY;
            curY = 0f;

            // start scrollrect
            Widgets.BeginScrollView( outRect, ref _scrollPosition, viewRect );

            // list of objects to be build
            Text.Font = GameFont.Tiny;
            foreach ( var buildables in Blueprint.GroupedBuildables )
            {
                // count
                float curX = 5f;
                Widgets.Label( new Rect( 5f, curY, width * .2f, 100f ), buildables.Value.Count + "x" );
                curX += width * .2f;

                // stuff selector
                float height = 0f;
                if ( buildables.Value.First().Stuff != null )
                {
                    string label = buildables.Value.First().Stuff.LabelAsStuff.CapitalizeFirst() + " " + buildables.Key.label;

                    Rect iconRect = new Rect( curX, curY, 12f, 12f );
                    curX += 16f;

                    height = Text.CalcHeight( label, width - curX ) + _lineOffset;
                    Rect labelRect = new Rect( curX, curY, width - curX, height );
                    Rect buttonRect = new Rect( curX - 16f, curY, width - curX + 16f, height + _lineOffset );

                    if ( Mouse.IsOver( buttonRect ) )
                    {
                        GUI.DrawTexture( buttonRect, TexUI.HighlightTex );
                        GUI.color = GenUI.MouseoverColor;
                    }
                    GUI.DrawTexture( iconRect, Resources.Icon_Edit );
                    GUI.color = Color.white;
                    Widgets.Label( labelRect, label );
                    if ( Widgets.InvisibleButton( buttonRect ) )
                        Blueprint.DrawStuffMenu( buildables.Key );
                }
                else
                {
                    // label
                    float labelWidth = width - curX;
                    string label = buildables.Key.LabelCap;
                    height = Text.CalcHeight( label, labelWidth ) + _lineOffset;
                    Widgets.Label( new Rect( curX, curY, labelWidth, height ), label );
                }

                // next line
                curY += height + _lineOffset;
            }

            // complete cost list
            curY += 12f;
            Text.Font = GameFont.Small;
            Widgets.Label( new Rect( 0f, curY, width, 24f ), "Fluffy.Blueprint.Cost".Translate() );
            curY += 24f;

            Text.Font = GameFont.Tiny;
            List<ThingCount> costlist = Blueprint.CostListAdjusted;
            for ( int i = 0; i < costlist.Count; i++ )
            {
                ThingCount thingCount = costlist[i];
                Texture2D image;
                if ( thingCount.thingDef == null )
                {
                    image = TexUI.UnknownThing;
                }
                else
                {
                    image = thingCount.thingDef.uiIcon;
                }
                GUI.DrawTexture( new Rect( 0f, curY, 20f, 20f ), image );
                if ( thingCount.thingDef != null && thingCount.thingDef.resourceReadoutPriority != ResourceCountPriority.Uncounted && Find.ResourceCounter.GetCount( thingCount.thingDef ) < thingCount.count )
                {
                    GUI.color = Color.red;
                }
                Widgets.Label( new Rect( 26f, curY + 2f, 50f, 100f ), thingCount.count.ToString() );
                GUI.color = Color.white;
                string text;
                if ( thingCount.thingDef == null )
                {
                    text = "(" + "UnchosenStuff".Translate() + ")";
                }
                else
                {
                    text = thingCount.thingDef.LabelCap;
                }
                float height = Text.CalcHeight( text, width - 60f ) - 2f;
                Widgets.Label( new Rect( 60f, curY + 2f, width - 60f, height ), text );
                curY += height + _lineOffset;
            }

            Widgets.EndScrollView();

            _panelHeight = curY;

            // need to give some extra offset to properly align description.
            // also, add back in our internal offset
            curY += 28f + oldY;
        }

        public override bool GroupsWith( Gizmo other )
        {
            return this.Blueprint == ( other as Designator_Blueprint ).Blueprint;
        }

        public override void ProcessInput( Event ev )
        {
            // float menu on right click
            if ( ev.button == 1 )
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();

                // rename
                options.Add( new FloatMenuOption( "Fluffy.Blueprints.Rename".Translate(), delegate
                { Find.WindowStack.Add( new Dialog_NameBlueprint( this.Blueprint ) ); } ) );

                // delete blueprint
                options.Add( new FloatMenuOption( "Fluffy.Blueprints.Remove".Translate(), delegate
                { Controller.Remove( this, false ); } ) );

                // delete blueprint and remove from disk
                if ( this.Blueprint.exported )
                {
                    options.Add( new FloatMenuOption( "Fluffy.Blueprints.RemoveAndDeleteXML".Translate(), delegate
                    { Controller.Remove( this, true ); } ) );
                }

                // store to xml
                else
                {
                    options.Add( new FloatMenuOption( "Fluffy.Blueprints.SaveToXML".Translate(), delegate
                    { Controller.SaveToXML( this.Blueprint ); } ) );
                }

                Find.WindowStack.Add( new FloatMenu( options ) );
                return;
            }

            base.ProcessInput( ev );
        }

        public override void SelectedUpdate()
        {
            GenDraw.DrawNoBuildEdgeLines();
            if ( !ArchitectCategoryTab.InfoRect.Contains( GenUI.AbsMousePosition() ) )
            {
                IntVec3 origin = Gen.MouseCell();
                _blueprint.DrawGhost( origin );
            }
        }

        // Copy-pasta from RimWorld.HandleRotationShortcuts()
        private void HandleRotationShortcuts()
        {
            RotationDirection rotationDirection = RotationDirection.None;
            if ( Event.current.button == 2 )
            {
                if ( Event.current.type == EventType.MouseDown )
                {
                    Event.current.Use();
                    _middleMouseDownTime = Time.realtimeSinceStartup;
                }
                if ( Event.current.type == EventType.MouseUp && Time.realtimeSinceStartup - _middleMouseDownTime < 0.15f )
                {
                    rotationDirection = RotationDirection.Clockwise;
                }
            }
            if ( KeyBindingDefOf.DesignatorRotateRight.KeyDownEvent )
            {
                rotationDirection = RotationDirection.Clockwise;
            }
            if ( KeyBindingDefOf.DesignatorRotateLeft.KeyDownEvent )
            {
                rotationDirection = RotationDirection.Counterclockwise;
            }
            if ( rotationDirection == RotationDirection.Clockwise )
            {
                SoundDefOf.AmountIncrement.PlayOneShotOnCamera();
                Blueprint.Rotate( RotationDirection.Clockwise );
            }
            if ( rotationDirection == RotationDirection.Counterclockwise )
            {
                SoundDefOf.AmountDecrement.PlayOneShotOnCamera();
                Blueprint.Rotate( RotationDirection.Counterclockwise );
            }
        }

        #endregion Methods
    }
}