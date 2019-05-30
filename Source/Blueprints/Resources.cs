using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Blueprints
{
    [StaticConstructorOnStartup]
    public class Resources
    {
        // todo; figure out blueprint flipping
        // public static readonly Texture2D FlipTex = ContentFinder<Texture2D>.Get("UI/Widgets/RotRight", true);
        public static Color ghostBlue = new Color( .25f, .50f, .50f, .5f );
        public static Color ghostGrey = new Color( .8f, .8f, .8f, .5f );
        public static Color ghostRed  = new Color( .5f, .08f, .08f, .5f );

        public static Texture2D Icon_AddBlueprint,
                                Icon_Blueprint,
                                Icon_Edit,
                                RotLeftTex,
                                RotRightTex,
                                FlipTex;

        private static readonly Material _mouseOverBracketMaterial =
            MaterialPool.MatFrom( "UI/Overlays/MouseoverBracketTex", ShaderDatabase.MetaOverlay );

        private static readonly Dictionary<Color, Material> _ghostFloors = new Dictionary<Color, Material>();

        private static readonly FieldInfo designator_place_placingRotation_FI =
            typeof( Designator_Place ).GetField( "placingRot", BindingFlags.Instance | BindingFlags.NonPublic );

        static Resources()
        {
            Icon_AddBlueprint = ContentFinder<Texture2D>.Get( "Icons/AddBlueprint" );
            Icon_Blueprint    = ContentFinder<Texture2D>.Get( "Icons/Blueprint" );
            Icon_Edit         = ContentFinder<Texture2D>.Get( "Icons/Edit" );
            RotLeftTex        = ContentFinder<Texture2D>.Get( "UI/Widgets/RotLeft" );
            RotRightTex       = ContentFinder<Texture2D>.Get( "UI/Widgets/RotRight" );
            FlipTex           = ContentFinder<Texture2D>.Get( "Icons/Flip" );
        }

        public static IntVec3 Offset( IntVec2 size, Rot4 from, Rot4 to )
        {
            var alt = AltitudeLayer.Blueprint.AltitudeFor();
            return ( GenThing.TrueCenter( IntVec3.Zero, from, size, alt ) -
                     GenThing.TrueCenter( IntVec3.Zero, to, size, alt ) ).ToIntVec3();
        }

        public static IntVec3 CenterPosition( IntVec3 bottomLeft, IntVec2 size, Rot4 rotation )
        {
            return bottomLeft + new IntVec3( ( size.x - 1 ) / 2, 0, ( size.z - 1 ) / 2 );
        }

        public static Color GhostColor( PlacementReport placementReport )
        {
            switch ( placementReport )
            {
                case PlacementReport.CanNotPlace:
                    return ghostRed;

                case PlacementReport.CanPlace:
                    return ghostBlue;

                case PlacementReport.AlreadyPlaced:
                    return ghostGrey;

                default:
                    return Color.white;
            }
        }

        public static Material GhostFloorMaterial( PlacementReport placementReport )
        {
            var      color = GhostColor( placementReport );
            Material ghost;
            if ( _ghostFloors.TryGetValue( color, out ghost ) )
                return ghost;

            ghost       = new Material( _mouseOverBracketMaterial );
            ghost.color = color;
            _ghostFloors.Add( color, ghost );
            return ghost;
        }

        public static void SetDesignatorRotation( Designator_Place designator, Rot4 rotation )
        {
            designator_place_placingRotation_FI.SetValue( designator, rotation );
        }
    }
}