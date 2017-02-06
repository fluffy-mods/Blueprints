using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace Blueprints
{
    [StaticConstructorOnStartup]
    public class Resources
    {
        #region Fields

        public static readonly Texture2D RotLeftTex = ContentFinder<Texture2D>.Get("UI/Widgets/RotLeft", true);
        public static readonly Texture2D RotRightTex = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight", true);
        // todo; figure out blueprint flipping
        // public static readonly Texture2D FlipTex = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight", true);
        public static Color ghostBlue = new Color( .25f, .50f, .50f, .5f );
        public static Color ghostGrey = new Color( .8f, .8f, .8f, .5f );
        public static Color ghostRed = new Color( .5f, .08f, .08f, .5f );
        public static Texture2D Icon_AddBlueprint;
        public static Texture2D Icon_Blueprint;
        public static Texture2D Icon_Edit;
        private static readonly Material _mouseOverBracketMaterial = MaterialPool.MatFrom("UI/Overlays/MouseoverBracketTex", ShaderDatabase.MetaOverlay);
        private static Dictionary<Color, Material> _ghostFloors = new Dictionary<Color, Material>();
        private static FieldInfo designator_place_placingRotation_FI = typeof( Designator_Place).GetField( "placingRot", BindingFlags.Instance | BindingFlags.NonPublic );

        #endregion Fields

        #region Methods

        public static IntVec3 CenterPosition( IntVec3 bottomLeft, IntVec2 size, Rot4 rotation )
        {
            return bottomLeft + new IntVec3( ( size.x - 1 ) / 2, 0, ( size.z - 1 ) / 2 );
        }

        public static void DesignatorRotate( Designator_Place designator, RotationDirection direction )
        {
            Rot4 rotation = GetDesignatorRotation( designator );
            rotation.Rotate( direction );
            SetDesignatorRotation( designator, rotation );
        }

        public static void DottedLine( float x, float y, float width, float dashLength = 10f, float gapLength = 5f )
        {
            float curX = x;
            while ( curX < x + width )
            {
                Widgets.DrawLineHorizontal( curX, y, dashLength );
                curX += dashLength + gapLength;
            }
        }

        public static Rot4 GetDesignatorRotation( Designator_Place designator )
        {
            return (Rot4)designator_place_placingRotation_FI.GetValue( designator );
        }

        public static Color ghostColor( PlacementReport placementReport )
        {
            switch ( placementReport )
            {
                case PlacementReport.CanNotPlace:
                    return ghostRed;

                case PlacementReport.CanPlace:
                    return ghostBlue;

                case PlacementReport.Alreadyplaced:
                    return ghostGrey;

                default:
                    return Color.white;
            }
        }

        public static Material ghostFloorMaterial( PlacementReport placementReport )
        {
            Color color = ghostColor( placementReport );
            Material ghost;
            if ( _ghostFloors.TryGetValue( color, out ghost ) )
                return ghost;

            ghost = new Material( _mouseOverBracketMaterial );
            ghost.color = color;
            _ghostFloors.Add( color, ghost );
            return ghost;
        }

        public static void LogNull( object obj, string name )
        {
            Log.Message( name + ": " + ( obj == null ? "NULL" : obj ) );
        }

        public static void SetDesignatorRotation( Designator_Place designator, Rot4 rotation )
        {
            designator_place_placingRotation_FI.SetValue( designator, rotation );
        }

        #endregion Methods
    }
}