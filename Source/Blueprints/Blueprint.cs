using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public class Blueprint : IExposable
    {
        #region Fields

        public List<BuildableInfo> contents;
        public bool exported;
        public string name;

        // regex for valid file names. Should allow all 'normal' characters, where normal (i.e. \w) differs per localization.
        private static Regex _validNameRegex = new Regex(@"^[\w]+$");

        private List<BuildableInfo> _availableContents;
        private List<BuildableDef> _buildables;
        private List<ThingCount> _costlist;
        private Dictionary<BuildableDef, List<BuildableInfo>> _groupedBuildables;
        private IntVec2 _size;

        #endregion Fields

        #region Constructors

        public Blueprint()
        {
            // empty for Scribe;
            exported = true; // loaded from scribe - so must have been exported.
        }

        public Blueprint(List<BuildableInfo> contents, IntVec2 size, string defaultName = null)
        {
            // input
            this.contents = contents;
            name = defaultName;
            _size = size;

            // provide reference to this blueprint in all contents
            foreach (var item in contents)
                item.blueprint = this;

            // just created, so not exported yet
            exported = false;

            // 'orrible default name
            if (name == null || !CouldBeValidBlueprintName(name))
                name = "Fluffy.Blueprints.DefaultBlueprintName".Translate();

            // increment numeric suffix until we have a unique name
            int i = 1;
            while (Controller.FindBlueprint(name + "_" + i) != null)
                i++;

            // set name
            name = name + "_" + i;

            // ask for name
            Find.WindowStack.Add(new Dialog_NameBlueprint(this));
        }

        #endregion Constructors

        #region Properties

        public List<BuildableInfo> AvailableContents
        {
            get
            {
                if (_availableContents == null)
                    // check if vanilla designator for buildable is visible (i.e. prerequisites are met).
                    _availableContents = contents.Where(item => item.Designator.Visible)
                                                 .ToList();
                return _availableContents;
            }
        }

        public List<BuildableDef> Buildables
        {
            get
            {
                if (_buildables == null)
                    _buildables = AvailableContents.Select(item => item.BuildableDef)
                                          .ToList();
                return _buildables;
            }
        }

        public List<ThingCount> CostListAdjusted
        {
            get
            {
                if (_costlist == null)
                    _costlist = CreateCostList();
                return _costlist;
            }
        }

        public Dictionary<BuildableDef, List<BuildableInfo>> GroupedBuildables
        {
            get
            {
                // return cached
                if (_groupedBuildables != null)
                    return _groupedBuildables;

                // create and cache list
                Dictionary<BuildableDef, List<BuildableInfo>> dict = new Dictionary<BuildableDef, List<BuildableInfo>>();

                foreach (var buildable in Buildables.Distinct())
                {
                    dict.Add(buildable, AvailableContents.Where(bi => bi.BuildableDef == buildable).ToList());
                }
                _groupedBuildables = dict;
                return dict;
            }
        }

        #endregion Properties

        #region Methods

        public static bool CouldBeValidBlueprintName(string name)
        {
            return _validNameRegex.IsMatch(name);
        }

        public static AcceptanceReport IsValidBlueprintName(string name)
        {
            if (!CouldBeValidBlueprintName(name))
                return new AcceptanceReport("Fluffy.Blueprints.InvalidBlueprintName".Translate(name));

            // TODO: figure out why this doesn't work
            if (Controller.FindBlueprint(name) != null)
                return new AcceptanceReport("Fluffy.Blueprints.NameAlreadyTaken".Translate(name));

            return true;
        }

        public bool CanPlace(IntVec3 origin)
        {
            return CanPlace(origin, Rot4.North);
        }

        public bool CanPlace(IntVec3 origin, Rot4 rotation)
        {
            // check each individual element for placement issues
            bool canPlace = true;
            foreach (var item in AvailableContents)
            {
                if (item.CanPlace(origin) == PlacementReport.CanNotPlace)
                    canPlace = false;
            }
            return canPlace;
        }

        public void Debug()
        {
            Log.Message(_size.ToString());

            foreach (BuildableInfo thing in contents)
            {
                Log.Message(thing.ToString());
                Log.Message($"Available: {AvailableContents.Contains(thing)}");
            }
        }

        public void DrawGhost(IntVec3 origin)
        {
            foreach (var item in AvailableContents)
            {
                item.DrawGhost(origin);
            }
        }

        public void DrawStuffMenu(BuildableDef buildable)
        {
            ThingDef thing = buildable as ThingDef;
            if (thing == null || thing.costStuffCount <= 0 || thing.stuffCategories.NullOrEmpty())
                return;

            var stuffOptions = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.IsStuff &&
                                                                                        thing.stuffCategories.Any(cat =>
                                                                                          def.stuffProps.categories.Contains(cat)));

            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (var stuff in stuffOptions)
            {
                options.Add(new FloatMenuOption(stuff.LabelCap + " (" + Find.VisibleMap.resourceCounter.GetCount(stuff) + ")", delegate
            { SetStuffFor(buildable, stuff); }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref contents, "BuildableThings", LookMode.Deep, new object[] { this });
            Scribe_Values.Look(ref name, "Name");
            Scribe_Values.Look(ref _size, "Size");
        }

        // called when the parent designator is selected, this resets the buildables cache
        // so that newly unlocked research can be properly applied.
        public void RecacheBuildables()
        {
            _availableContents = null;
            _buildables = null;
            _groupedBuildables = null;
            _costlist = null;
        }

        public void Rotate(RotationDirection direction)
        {
            _size = _size.Rotated();

            foreach (var item in contents)
                item.Rotate(direction);
        }

        //public void Flip()
        //{
        //    foreach ( var item in contents )
        //        item.Flip();
        //}

        protected internal bool ShouldLinkWith(IntVec3 position, ThingDef thingDef)
        {
            // get things at neighbouring position
            var ThingsAtPosition = AvailableContents.Where(item => item.Position == position && item.BuildableDef is ThingDef).Select(item => item.BuildableDef as ThingDef);

            // if there's nothing there, there's nothing to link with
            if (ThingsAtPosition.Count() < 1)
                return false;

            // loop over things to see if any of the things at the neighbouring location share a linkFlag with the thingDef we're looking at
            foreach (var thing in ThingsAtPosition)
            {
                if ((thing.graphicData.linkFlags & thingDef.graphicData.linkFlags) != LinkFlags.None)
                    return true;
            }

            // nothing stuck, return false
            return false;
        }

        private List<ThingCount> CreateCostList()
        {
            // set up a temporary dictionary to make adding costs easier
            Dictionary<ThingDef, int> costdict = new Dictionary<ThingDef, int>();

            // loop over all buildables
            foreach (var item in AvailableContents)
            {
                foreach (var cost in item.BuildableDef.CostListAdjusted(item.Stuff, false))
                {
                    // add up all construction costs
                    if (costdict.ContainsKey(cost.thingDef))
                        costdict[cost.thingDef] += cost.count;
                    else
                        costdict.Add(cost.thingDef, cost.count);
                }
            }

            // return a list of thingcounts, in descending cost order.
            return costdict.Select(pair => new ThingCount(pair.Key, pair.Value)).OrderByDescending(tc => tc.Count).ToList();
        }

        private void SetStuffFor(BuildableDef buildableDef, ThingDef stuff)
        {
            // get all buildables of this type
            var buildables = contents.Where(bi => bi.BuildableDef == buildableDef);

            // set them to use the new stuff def
            foreach (var buildable in buildables)
            {
                buildable.Stuff = stuff;
            }

            // reset caches
            RecacheBuildables();
        }

        #endregion Methods
    }
}
