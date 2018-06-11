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
    public enum PlacementReport
    {
        CanNotPlace,
        CanPlace,
        Alreadyplaced
    }

    public class BuildableInfo : IEquatable<BuildableInfo>, IExposable
    {
        #region Fields

        public Blueprint blueprint;

        private static FieldInfo _entDefFieldInfo = typeof(Designator_Build).GetField("entDef",
                                                                              BindingFlags.NonPublic |
                                                                              BindingFlags.Instance);

        private BuildableDef _buildableDef;
        private Dictionary<int, Material> _cachedMaterials = new Dictionary<int, Material>();
        private Designator_Build _designator;
        private IntVec3 _position;
        private Dictionary<Rot4, IntVec3> _rotatedPositions = new Dictionary<Rot4, IntVec3>();
        private Rot4 _rotation;
        private ThingDef _stuff;
        private TerrainDef _terrainDef;
        private ThingDef _thingDef;

        #endregion Fields

        #region Constructors

        public BuildableInfo(Blueprint blueprint)
        {
            this.blueprint = blueprint;
            // rest is filled in by scribe.
        }

        public BuildableInfo(Thing thing, IntVec3 origin)
        {
            if (thing is RimWorld.Blueprint)
                Init(thing as RimWorld.Blueprint, origin);
            else if (thing is Frame)
                Init(thing as Frame, origin);
            else
                Init(thing, origin);
        }

        public BuildableInfo(TerrainDef terrain, IntVec3 position, IntVec3 origin)
        {
            Init(terrain, position, origin);
        }

        #endregion Constructors

        #region Properties

        public BuildableDef BuildableDef
        {
            get
            {
                if (_buildableDef == null)
                {
                    if (_thingDef != null)
                        _buildableDef = _thingDef;
                    else if (_terrainDef != null)
                        _buildableDef = _terrainDef;
                    else
                        Log.ErrorOnce("Blueprints :: No thingDef or terrainDef set!", this.GetHashCode() * 123);
                }
                return _buildableDef;
            }
        }

        public Designator_Build Designator
        {
            get
            {
                if (_designator == null)
                    _designator = CreateLocalDesignatorCopy();
                return _designator;
            }
        }

        public IntVec3 Position => _position;

        public ThingDef Stuff
        {
            get
            {
                return _stuff;
            }
            set
            {
                _stuff = value;
                Designator.SetStuffDef(value);
            }
        }

        #endregion Properties

        #region Methods

        public PlacementReport CanPlace(IntVec3 origin)
        {
            // get rotated cell position
            var cell = origin + Position;

            // if the designator's check passes, we can safely assume it's OK to build here
            if (Designator.CanDesignateCell(cell).Accepted)
                return PlacementReport.CanPlace;

            // otherwise, check if the same thing (or it's blueprint/frame stages) already exists here
            // terrain and thing both have bluePrint and frame in thinglist, as are things. Terrains are not a thing, and retrieved with GetTerrain().
            var cellDefs = cell.GetThingList(Find.CurrentMap).Select(thing => thing.def).ToList();
            if (cellDefs.Contains(BuildableDef as ThingDef) ||
                 cell.GetTerrain(Find.CurrentMap) == BuildableDef as TerrainDef ||
                 cellDefs.Contains(BuildableDef.blueprintDef) ||
                 cellDefs.Contains(BuildableDef.frameDef))
                return PlacementReport.Alreadyplaced;

            // finally, default to returning false.
            return PlacementReport.CanNotPlace;
        }

        // todo; figure out blueprint flipping
        //public void Flip()
        //{
        //    _position.x = -_position.x;
        //}
        public Designator_Build CreateLocalDesignatorCopy()
        {
            // create a new copy
            var designator = new Designator_Build(BuildableDef);

            // apply stuffdef & rotation
            if (_thingDef != null)
            {
                // set stuff def
                if (_stuff == null)
                    designator.SetStuffDef(GenStuff.DefaultStuffFor(_thingDef));
                else
                    designator.SetStuffDef(_stuff);

                // set rotation through reflection
                Resources.SetDesignatorRotation(designator, _rotation);
            }

            return designator;
        }

        public void Designate(IntVec3 origin)
        {
            Designator.DesignateSingleCell(origin + Position);
        }

        public void DrawGhost(IntVec3 origin)
        {
            IntVec3 cell = origin + Position;
            if (_thingDef != null)
            {
                // normal thingdef graphic
                if (_thingDef.graphicData.linkFlags == LinkFlags.None)
                {
                    GhostDrawer.DrawGhostThing(cell, _rotation, _thingDef, null, Resources.ghostColor(CanPlace(origin)), AltitudeLayer.Blueprint);
                }

                // linked thingdef graphic
                else
                {
                    Material material;
                    Color color = Resources.ghostColor(CanPlace(origin));
                    int hash = color.GetHashCode() * _rotation.GetHashCode();
                    if (!_cachedMaterials.TryGetValue(hash, out material))
                    {
                        // get a colored version (ripped from GhostDrawer.DrawGhostThing)
                        Graphic_Linked graphic = (Graphic_Linked)_thingDef.graphic.GetColoredVersion(ShaderDatabase.Transparent,
                                                                            color,
                                                                            Color.white);

                        // atlas contains all possible link graphics
                        Material atlas = graphic.MatSingle;

                        // loop over cardinal directions, and set the correct bits (e.g. 1, 2, 4, 8).
                        int linkInt = 0;
                        int dirInt = 1;
                        for (int i = 0; i < 4; i++)
                        {
                            if (blueprint.ShouldLinkWith(Position + GenAdj.CardinalDirections[i], _thingDef))
                            {
                                linkInt += dirInt;
                            }
                            dirInt *= 2;
                        }

                        // translate int to bitmask (flags)
                        LinkDirections linkSet = (LinkDirections)linkInt;

                        // get and cache the final material
                        material = MaterialAtlasPool.SubMaterialFromAtlas(atlas, linkSet);
                        _cachedMaterials.Add(hash, material);
                    }

                    // draw the thing.
                    Vector3 position = (cell).ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                    Graphics.DrawMesh(MeshPool.plane10, position, Quaternion.identity, material, 0);
                }
            }
            else
            {
                Vector3 position = (cell).ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                Graphics.DrawMesh(MeshPool.plane10, position, Quaternion.identity, Resources.ghostFloorMaterial(CanPlace(origin)), 0);
            }
        }

        // should only be used to compare info's in the same blueprint
        public bool Equals(BuildableInfo other)
        {
            return _thingDef == other._thingDef && _position == other._position;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref _thingDef, "ThingDef");
            Scribe_Defs.Look(ref _stuff, "Stuff");
            Scribe_Defs.Look(ref _terrainDef, "TerrainDef");
            Scribe_Values.Look(ref _position, "Position");
            Scribe_Values.Look(ref _rotation, "Rotation");
        }

        public void Plan(IntVec3 origin)
        {
            // only plan wall (or things that link with walls) designations
            if (_thingDef == null || (_thingDef.graphicData.linkFlags & LinkFlags.Wall) != LinkFlags.Wall)
                return;

            // don't add plan if already there
            if (Find.CurrentMap.designationManager.DesignationAt(origin + Position, DesignationDefOf.Plan) != null)
                return;

            // add plan designation
            Find.CurrentMap.designationManager.AddDesignation(new Designation(origin + Position, DesignationDefOf.Plan));
        }

        public void Rotate(RotationDirection direction)
        {
            // update position within blueprint
            // for a clock wise rotation
            if (direction == RotationDirection.Clockwise)
                _position = _position.RotatedBy(Rot4.East);

            // counter clock wise is the reverse
            else
                _position = _position.RotatedBy(Rot4.West);

            // update rotation of item
            // if there's no thingdef, there's no point.
            if (_thingDef == null)
                return;

            // if it's not rotatable AND not a linked building (e.g. walls, sandbags), don't rotate.
            if (!_thingDef.rotatable && !_thingDef.graphicData.Linked)
                return;
            // NOTE: this may prove problematic with oddly sized items that are not rotatable - their relative positions may change.

            // internal rotation
            _rotation.Rotate(direction);

            // reflect changes in designator
            Resources.SetDesignatorRotation(Designator, _rotation);
        }

        public override string ToString()
        {
            return BuildableDef.LabelCap + " _pos: " + _position + ", rot: " + _rotation + ", rotPos: " + Position + ", cat: " + BuildableDef.designationCategory;
        }

        private void Init(RimWorld.Blueprint blueprint, IntVec3 origin)
        {
            if (blueprint.def.entityDefToBuild is TerrainDef)
                Init(blueprint.def.entityDefToBuild as TerrainDef, blueprint.Position, origin);
            else if (blueprint is Blueprint_Build)
                Init(blueprint.def.entityDefToBuild as ThingDef, (blueprint as Blueprint_Build).stuffToUse, blueprint.Position, blueprint.Rotation, origin);
            else if (blueprint is Blueprint_Install)
                Init(blueprint.def.entityDefToBuild as ThingDef, (blueprint as Blueprint_Install).MiniToInstallOrBuildingToReinstall.Stuff, blueprint.Position, blueprint.Rotation, origin);
        }

        private void Init(Frame frame, IntVec3 origin)
        {
            if (frame.def.entityDefToBuild is TerrainDef)
                Init(frame.def.entityDefToBuild as TerrainDef, frame.Position, origin);
            else
                Init(frame.def.entityDefToBuild as ThingDef, frame.Stuff, frame.Position, frame.Rotation, origin);
        }

        private void Init(Thing thing, IntVec3 origin)
        {
            _thingDef = thing.def;
            _terrainDef = null;
            _position = thing.Position - origin;
            _rotation = thing.Rotation;
            _stuff = thing.Stuff;
        }

        private void Init(ThingDef thingdef, ThingDef stuffDef, IntVec3 position, Rot4 rotation, IntVec3 origin)
        {
            _thingDef = thingdef;
            _terrainDef = null;
            _position = position - origin;
            _rotation = rotation;
            _stuff = stuffDef;
        }

        private void Init(TerrainDef terrain, IntVec3 position, IntVec3 origin)
        {
            _thingDef = null;
            _terrainDef = terrain;
            _position = position - origin;
            _rotation = Rot4.Invalid;
            _stuff = null;
        }

        #endregion Methods
    }
}
