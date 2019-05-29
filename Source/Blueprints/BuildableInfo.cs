// BuildableInfo.cs
// Copyright Karel Kroeze, 2018-2019

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public enum PlacementReport
    {
        CanNotPlace,
        CanPlace,
        AlreadyPlaced
    }

    public class BuildableInfo : IEquatable<BuildableInfo>, IExposable
    {
        private readonly Dictionary<int, Material> _cachedMaterials = new Dictionary<int, Material>();

        private BuildableDef     _buildableDef;
        private Designator_Build _designator;
        private IntVec3          _position;
        private Rot4             _rotation;
        private ThingDef         _stuff;
        private TerrainDef       _terrainDef;
        private ThingDef         _thingDef;

        public Blueprint blueprint;

        public BuildableInfo( Blueprint blueprint )
        {
            this.blueprint = blueprint;
            // scribe
        }

        public BuildableInfo( Thing thing, IntVec3 origin )
        {
            if ( thing is RimWorld.Blueprint blueprint )
                Init( blueprint, origin );
            else if ( thing is Frame frame )
                Init( frame, origin );
            else
                Init( thing, origin );
        }

        public BuildableInfo( TerrainDef terrain, IntVec3 position, IntVec3 origin )
        {
            Init( terrain, position, origin );
        }

        public BuildableDef BuildableDef
        {
            get
            {
                if ( _buildableDef == null )
                {
                    if ( _thingDef != null )
                        _buildableDef = _thingDef;
                    else if ( _terrainDef != null )
                        _buildableDef = _terrainDef;
                    else
                        Log.ErrorOnce( "Blueprints :: No thingDef or terrainDef set!", GetHashCode() * 123 );
                }

                return _buildableDef;
            }
        }

        public Designator_Build Designator
        {
            get
            {
                if ( _designator == null )
                    _designator = CreateDesignatorCopy();
                return _designator;
            }
        }

        public IntVec3 Position
        {
            get
            {
                if ( BuildableDef is TerrainDef )
                    return _position;
                if ( Rotatable )
                    return _position;
                if ( Centered || !Square )
                    return _position;

                // offset position
                var size = _thingDef.Size;
                var pos  = IntVec3.Zero;
                var alt  = AltitudeLayer.Blueprint.AltitudeFor();
                var offset = GenThing.TrueCenter( pos, Rot4.North, size, alt ) -
                             GenThing.TrueCenter( pos, _rotation, size, alt );
                return _position - offset.ToIntVec3();
            }
        }

        public ThingDef Stuff
        {
            get => _stuff;
            set
            {
                _stuff = value;
                Designator.SetStuffDef( value );
            }
        }

        public bool Rotatable
        {
            get
            {
                if ( BuildableDef is ThingDef thingDef ) return thingDef.rotatable;
                return false;
            }
        }

        public bool Square
        {
            get
            {
                if ( BuildableDef is ThingDef thingDef ) return thingDef.Size.x == thingDef.Size.z;
                return true;
            }
        }

        public bool Centered
        {
            get
            {
                if ( BuildableDef == null )
                    throw new InvalidOperationException( "cannot get centered status without a buildable set" );

                if ( BuildableDef is TerrainDef )
                    return true;

                // if width or height are even, the pivot cannot possibly be centered.
                if ( _thingDef.Size.x % 2 == 0 || _thingDef.Size.z % 2 == 0 )
                    return false;
                return true;
            }
        }

        // should only be used to compare info's in the same blueprint
        public bool Equals( BuildableInfo other )
        {
            return _thingDef == other._thingDef && _position == other._position;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look( ref _thingDef, "ThingDef" );
            Scribe_Defs.Look( ref _stuff, "Stuff" );
            Scribe_Defs.Look( ref _terrainDef, "TerrainDef" );
            Scribe_Values.Look( ref _position, "Position" );
            Scribe_Values.Look( ref _rotation, "Rotation" );
        }

        public PlacementReport CanPlace( IntVec3 origin )
        {
            // get rotated cell position
            var cell = origin + Position;

            // if out of bounds, we clearly can't place it
            if ( !cell.InBounds( Find.CurrentMap ) )
                return PlacementReport.CanNotPlace;

            // if the designator's check passes, we can safely assume it's OK to build here
            if ( Designator.CanDesignateCell( cell ).Accepted )
                return PlacementReport.CanPlace;

            // otherwise, check if the same thing (or it's blueprint/frame stages) already exists here
            // terrain and thing both have bluePrint and frame in thinglist, as are things. Terrains are not a thing, and retrieved with GetTerrain().
            var cellDefs = cell.GetThingList( Find.CurrentMap ).Select( thing => thing.def ).ToList();
            if ( cellDefs.Contains( BuildableDef as ThingDef )                    ||
                 cell.GetTerrain( Find.CurrentMap ) == BuildableDef as TerrainDef ||
                 cellDefs.Contains( BuildableDef.blueprintDef )                   ||
                 cellDefs.Contains( BuildableDef.frameDef ) )
                return PlacementReport.AlreadyPlaced;

            // finally, default to returning false.
            return PlacementReport.CanNotPlace;
        }

        public Designator_Build CreateDesignatorCopy()
        {
            // create a new copy
            var designator = new Designator_Build( BuildableDef );

            // apply stuffdef & rotation
            if ( _thingDef != null )
            {
                // set stuff def
                if ( _stuff == null )
                    designator.SetStuffDef( GenStuff.DefaultStuffFor( _thingDef ) );
                else
                    designator.SetStuffDef( _stuff );

                // set rotation through reflection
                Resources.SetDesignatorRotation( designator, _rotation );
            }

            return designator;
        }

        public void Designate( IntVec3 origin )
        {
            Designator.DesignateSingleCell( origin + Position );
        }

        public void DrawGhost( IntVec3 origin )
        {
            var cell = origin + Position;
            if ( _thingDef != null )
            {
                // normal thingdef graphic
                if ( _thingDef.graphicData.linkFlags == LinkFlags.None )
                {
                    GhostDrawer.DrawGhostThing( cell, Rotatable ? _rotation : Rot4.North, _thingDef, null,
                                                Resources.GhostColor( CanPlace( origin ) ),
                                                AltitudeLayer.Blueprint );
                }

                // linked thingdef graphic
                else
                {
                    Material material;
                    var      color = Resources.GhostColor( CanPlace( origin ) );
                    var      hash  = color.GetHashCode() * _rotation.GetHashCode();
                    if ( !_cachedMaterials.TryGetValue( hash, out material ) )
                    {
                        // get a colored version (ripped from GhostDrawer.DrawGhostThing)
                        var graphic = (Graphic_Linked) _thingDef.graphic.GetColoredVersion( ShaderDatabase.Transparent,
                                                                                            color,
                                                                                            Color.white );

                        // atlas contains all possible link graphics
                        var atlas = graphic.MatSingle;

                        // loop over cardinal directions, and set the correct bits (e.g. 1, 2, 4, 8).
                        var linkInt = 0;
                        var dirInt  = 1;
                        for ( var i = 0; i < 4; i++ )
                        {
                            if ( blueprint.ShouldLinkWith( Position + GenAdj.CardinalDirections[i], _thingDef ) )
                                linkInt += dirInt;
                            dirInt *= 2;
                        }

                        // translate int to bitmask (flags)
                        var linkSet = (LinkDirections) linkInt;

                        // get and cache the final material
                        material = MaterialAtlasPool.SubMaterialFromAtlas( atlas, linkSet );
                        _cachedMaterials.Add( hash, material );
                    }

                    // draw the thing.
                    var position = cell.ToVector3ShiftedWithAltitude( AltitudeLayer.MetaOverlays );
                    Graphics.DrawMesh( MeshPool.plane10, position, Quaternion.identity, material, 0 );
                }
            }
            else
            {
                var position = cell.ToVector3ShiftedWithAltitude( AltitudeLayer.MetaOverlays );
                Graphics.DrawMesh( MeshPool.plane10, position, Quaternion.identity,
                                   Resources.ghostFloorMaterial( CanPlace( origin ) ), 0 );
            }
        }

        public void Plan( IntVec3 origin )
        {
            // only plan wall (or things that link with walls) designations
            if ( _thingDef == null || ( _thingDef.graphicData.linkFlags & LinkFlags.Wall ) != LinkFlags.Wall )
                return;

            // don't add plan if already there
            if ( Find.CurrentMap.designationManager.DesignationAt( origin + Position, DesignationDefOf.Plan ) != null )
                return;

            // add plan designation
            Find.CurrentMap.designationManager.AddDesignation(
                new Designation( origin + Position, DesignationDefOf.Plan ) );
        }

        public void Rotate( RotationDirection direction )
        {
            // update position within blueprint
            // for a clock wise rotation
            if ( direction == RotationDirection.Clockwise )
                _position = _position.RotatedBy( Rot4.East );

            // counter clock wise is the reverse
            else
                _position = _position.RotatedBy( Rot4.West );

            // update rotation of item
            // if there's no thingdef, there's no point.
            if ( _thingDef == null )
                return;

            // always keep track of rotation internally
            var previousRotation = _rotation;
            _rotation.Rotate( direction );

            // if rotatable or a linked building (e.g. walls, sandbags), rotate.
            if ( Rotatable || _thingDef.graphicData.Linked )
                // rotate designator only if it makes sense
                Resources.SetDesignatorRotation( Designator, _rotation );

            // if the pivot of a non-rotatable thing is not in it's center, try to offset it's position.
            if ( !Rotatable && !Centered )
            {
                if ( !Square )
                {
                    // throw message - don't deal.
                }
                else
                {
                    // we'll try to offset the location
                    if ( _thingDef.hasInteractionCell )
                    {
                        // throw message - interaction cell might become inaccessible.
                    }
                }
            }
        }

        public void Flip()
        {
        }

        public override string ToString()
        {
            return BuildableDef.LabelCap + " _pos: " + _position + ", rot: " + _rotation + ", rotPos: " + Position +
                   ", cat: "             + BuildableDef.designationCategory;
        }

        private void Init( RimWorld.Blueprint blueprint, IntVec3 origin )
        {
            if ( blueprint.def.entityDefToBuild is TerrainDef terrain )
                Init( terrain, blueprint.Position, origin );
            else if ( blueprint is Blueprint_Build bp_build )
                Init( blueprint.def.entityDefToBuild as ThingDef, bp_build.stuffToUse, blueprint.Position,
                      blueprint.Rotation, origin );
            else if ( blueprint is Blueprint_Install bp_install )
                Init( blueprint.def.entityDefToBuild as ThingDef, bp_install.MiniToInstallOrBuildingToReinstall.Stuff,
                      blueprint.Position, blueprint.Rotation, origin );
        }

        private void Init( Frame frame, IntVec3 origin )
        {
            if ( frame.def.entityDefToBuild is TerrainDef terrain )
                Init( terrain, frame.Position, origin );
            else
                Init( frame.def.entityDefToBuild as ThingDef, frame.Stuff, frame.Position, frame.Rotation, origin );
        }

        private void Init( Thing thing, IntVec3 origin )
        {
            _thingDef   = thing.def;
            _terrainDef = null;
            _position   = thing.Position - origin;
            _rotation   = thing.Rotation;
            _stuff      = thing.Stuff;
        }

        private void Init( ThingDef thingdef, ThingDef stuffDef, IntVec3 position, Rot4 rotation, IntVec3 origin )
        {
            _thingDef   = thingdef;
            _terrainDef = null;
            _position   = position - origin;
            _rotation   = rotation;
            _stuff      = stuffDef;
        }

        private void Init( TerrainDef terrain, IntVec3 position, IntVec3 origin )
        {
            _thingDef   = null;
            _terrainDef = terrain;
            _position   = position - origin;
            _rotation   = Rot4.Invalid;
            _stuff      = null;
        }
    }
}