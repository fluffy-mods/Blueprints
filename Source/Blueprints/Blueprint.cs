using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using RimWorld;
using Verse;

namespace Blueprints
{
    public class Blueprint : IExposable
    {
        // regex for valid file names. Should allow all 'normal' characters, where normal (i.e. \w) differs per localization.
        private static readonly Regex ValidNameRegex = new Regex( @"^[\w]+$" );

        private List<BuildableInfo>                           _availableContents;
        private List<BuildableDef>                            _buildables;
        private List<ThingDefCount>                           _costlist;
        private Dictionary<BuildableDef, List<BuildableInfo>> _groupedBuildables;
        private IntVec2                                       _size;

        public List<BuildableInfo> contents;
        public bool                exported;
        public string              name;

        public Blueprint()
        {
            // empty for Scribe;
        }

        public Blueprint( List<BuildableInfo> contents, IntVec2 size, string defaultName = null, bool temporary = false )
        {
            // input
            this.contents = contents;
            name          = defaultName;
            _size         = size;

            // provide reference to this blueprint in all contents
            foreach ( var item in contents )
                item.blueprint = this;

            // just created, so not exported yet
            exported = false;

            // 'orrible default name
            if ( name == null || !CouldBeValidBlueprintName( name ) )
                name = "Fluffy.Blueprints.DefaultBlueprintName".Translate();

            // increment numeric suffix until we have a unique name
            var i = 1;
            while ( BlueprintController.FindBlueprint( name + "_" + i ) != null )
                i++;

            // set name
            name = name + "_" + i;

            // ask for name
            if (!temporary)
                Find.WindowStack.Add( new Dialog_NameBlueprint( this ) );
        }

        public static void Create( IEnumerable<IntVec3> cells, Map map )
        {
            // bail out if empty
            if (cells == null || !cells.Any())
            {
                Messages.Message("Fluffy.Blueprints.CannotCreateBluePrint_NothingSelected".Translate(),
                                  MessageTypeDefOf.RejectInput);
                return;
            }

            // get list of buildings in the cells, note that this includes frames and blueprints, and so _may include floors!_
            var things = new List<Thing>(cells.SelectMany(cell => cell.GetThingList(map)
                                                                      .Where(thing => thing.IsValidBlueprintThing()))
                                               .Distinct());

            // get list of creatable terrains
            var terrains = new List<Pair<TerrainDef, IntVec3>>();
            terrains.AddRange(cells.Select(cell => new Pair<TerrainDef, IntVec3>(cell.GetTerrain( map ), cell))
                                    .Where(p => p.First.IsValidBlueprintTerrain()));

            // get edges of blueprint area
            // (might be bigger than selected region, but never smaller).
            var allCells = cells.Concat(things.SelectMany(thing => thing.OccupiedRect().Cells));

            var left = allCells.Min(cell => cell.x);
            var top = allCells.Max(cell => cell.z);
            var right = allCells.Max(cell => cell.x);
            var bottom = allCells.Min(cell => cell.z);

            // total size ( +1 because x = 2 ... x = 4 => 4 - 2 + 1 cells )
            var size = new IntVec2(right - left + 1, top - bottom + 1);

            // fetch origin for default (North) orientation
            var origin = Resources.CenterPosition(new IntVec3(left, 0, bottom), size, Rot4.North);

            // create list of buildables
            var buildables = new List<BuildableInfo>();
            foreach (var thing in things)
                buildables.Add(new BuildableInfo(thing, origin));
            foreach (var terrain in terrains)
                buildables.Add(new BuildableInfo(terrain.First, terrain.Second, origin));

            // try to get a decent default name: get rooms for occupied cells, then see if there is only one type.
            var rooms = allCells.Select(c => c.GetRoom(map))
                                .Where(r => r != null && r.Role != RoomRoleDefOf.None)
                                .Distinct()
                                .GroupBy(r => r.Role.LabelCap);

#if DEBUG
            foreach (var room in rooms)
                Blueprints.Debug.Message($"{room.Count()}x {room.Key}");
#endif

            // only one type of room
            string defaultName = null;
            if (rooms.Count() == 1)
            {
                var room = rooms.First();
                defaultName = room.Count() > 1
                    ? "Fluffy.Blueprints.Plural".Translate(room.Key)
                    : room.Key;
            }

            // add to controller - controller handles adding to designations
            var blueprint = new Blueprint(buildables, size, defaultName);
            BlueprintController.Add(blueprint);

            blueprint.Debug();
        }

        public static void Create( IEnumerable<Thing> things, bool temporary = false )
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
            var blueprint = new Blueprint( buildables, size, "Selection", temporary );
            if ( temporary )
                Find.DesignatorManager.Select( new Designator_Blueprint( blueprint ) );
            else
                BlueprintController.Add( blueprint );

            blueprint.Debug();
        }

        public List<BuildableInfo> AvailableContents
        {
            get
            {
                if ( _availableContents == null )
                    // check if vanilla designator for buildable is visible (i.e. prerequisites are met).
                    _availableContents = contents.Where( item => item.Designator.Visible )
                                                 .ToList();
                return _availableContents;
            }
        }

        public List<BuildableDef> Buildables
        {
            get
            {
                if ( _buildables == null )
                    _buildables = AvailableContents.Select( item => item.BuildableDef )
                                                   .ToList();
                return _buildables;
            }
        }

        public List<ThingDefCount> CostListAdjusted
        {
            get
            {
                if ( _costlist == null )
                    _costlist = CreateCostList();
                return _costlist;
            }
        }

        public Dictionary<BuildableDef, List<BuildableInfo>> GroupedBuildables
        {
            get
            {
                // return cached
                if ( _groupedBuildables != null )
                    return _groupedBuildables;

                // create and cache list
                var dict = new Dictionary<BuildableDef, List<BuildableInfo>>();

                foreach ( var buildable in Buildables.Distinct() )
                    dict.Add( buildable, AvailableContents.Where( bi => bi.BuildableDef == buildable ).ToList() );
                _groupedBuildables = dict;
                return dict;
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look( ref contents, "BuildableThings", LookMode.Deep, this );
            Scribe_Values.Look( ref name, "Name" );
            Scribe_Values.Look( ref _size, "Size" );
            Scribe_Values.Look( ref exported, "Exported", false );
        }

        public static bool CouldBeValidBlueprintName( string name )
        {
            return ValidNameRegex.IsMatch( name );
        }

        public static AcceptanceReport IsValidBlueprintName( string name )
        {
            if ( !CouldBeValidBlueprintName( name ) )
                return new AcceptanceReport( "Fluffy.Blueprints.InvalidBlueprintName".Translate( name ) );

            // TODO: figure out why this doesn't work
            if ( BlueprintController.FindBlueprint( name ) != null )
                return new AcceptanceReport( "Fluffy.Blueprints.NameAlreadyTaken".Translate( name ) );

            return true;
        }

        public bool CanPlace( IntVec3 origin )
        {
            return CanPlace( origin, Rot4.North );
        }

        public bool CanPlace( IntVec3 origin, Rot4 rotation )
        {
            // check each individual element for placement issues
            foreach ( var item in AvailableContents )
                if ( item.CanPlace( origin ) == PlacementReport.CanNotPlace )
                    return false;
            return true;
        }
        
        [Conditional("DEBUG")]
        public void Debug()
        {
            Log.Message( _size.ToString() );

            foreach ( var thing in contents )
            {
                Log.Message( thing.ToString() );
                Log.Message( $"Available: {AvailableContents.Contains( thing )}" );
            }
        }

        public void DrawGhost( IntVec3 origin )
        {
            foreach ( var item in AvailableContents )
                item.DrawGhost( origin );
        }

        public void DrawStuffMenu( BuildableDef buildable )
        {
            var thing = buildable as ThingDef;
            if ( thing == null || thing.costStuffCount <= 0 || thing.stuffCategories.NullOrEmpty() )
                return;

            var stuffOptions = DefDatabase<ThingDef>.AllDefsListForReading.Where( def => def.IsStuff &&
                                                                                         thing.stuffCategories.Any(
                                                                                             cat =>
                                                                                                 def.stuffProps
                                                                                                    .categories
                                                                                                    .Contains(
                                                                                                         cat ) ) );

            var options = new List<FloatMenuOption>();
            foreach ( var stuff in stuffOptions )
                options.Add( new FloatMenuOption(
                                 stuff.LabelCap + " (" + Find.CurrentMap.resourceCounter.GetCount( stuff ) + ")",
                                 delegate { SetStuffFor( buildable, stuff ); } ) );
            Find.WindowStack.Add( new FloatMenu( options ) );
        }

        // called when the parent designator is selected, this resets the buildables cache
        // so that newly unlocked research can be properly applied.
        public void RecacheBuildables()
        {
            _availableContents = null;
            _buildables        = null;
            _groupedBuildables = null;
            _costlist          = null;
        }

        private HashSet<FailReason> _failReasonsMentioned = new HashSet<FailReason>();
        public void Rotate( RotationDirection direction )
        {
            _size = _size.Rotated();
            foreach ( var item in contents )
            {
                var success = item.Rotate( direction );
                if ( !success && !_failReasonsMentioned.Contains( success ) )
                {
                    Messages.Message( success.reason, MessageTypeDefOf.RejectInput, false );
                    _failReasonsMentioned.Add( success );
                }
            }
        }

        public void Flip()
        {
            foreach ( var item in contents )
            {
                var success = item.Flip();
                if ( !success && _failReasonsMentioned.Contains( success ) )
                {
                    Messages.Message( success.reason, MessageTypeDefOf.RejectInput, false );
                    _failReasonsMentioned.Add( success );
                }
            }
        }

        protected internal bool ShouldLinkWith( IntVec3 position, ThingDef thingDef )
        {
            // get things at neighbouring position
            var thingsAtPosition = AvailableContents
                                  .Where( item => item.Position == position && item.BuildableDef is ThingDef )
                                  .Select( item => item.BuildableDef as ThingDef );

            // if there's nothing there, there's nothing to link with
            if ( !thingsAtPosition.Any() )
                return false;

            // loop over things to see if any of the things at the neighbouring location share a linkFlag with the thingDef we're looking at
            foreach ( var thing in thingsAtPosition )
                if ( ( thing.graphicData.linkFlags & thingDef.graphicData.linkFlags ) != LinkFlags.None )
                    return true;

            // nothing stuck, return false
            return false;
        }

        private List<ThingDefCount> CreateCostList()
        {
            // set up a temporary dictionary to make adding costs easier
            var costdict = new Dictionary<ThingDef, int>();

            // loop over all buildables
            foreach ( var item in AvailableContents )
            {
                foreach ( var cost in item.BuildableDef.CostListAdjusted( item.Stuff, false ) )
                {
                    if ( cost?.thingDef == null )
                        continue;
                    // add up all construction costs
                    if ( costdict.ContainsKey( cost.thingDef ) )
                        costdict[cost.thingDef] += cost.count;
                    else
                        costdict.Add( cost.thingDef, cost.count );
                }
            }

            // return a list of thingcounts, in descending cost order.
            return costdict.Select( pair => new ThingDefCount( pair.Key, pair.Value ) )
                           .OrderByDescending( tc => tc.Count ).ToList();
        }

        private void SetStuffFor( BuildableDef buildableDef, ThingDef stuff )
        {
            // get all buildables of this type
            var buildables = contents.Where( bi => bi.BuildableDef == buildableDef );

            // set them to use the new stuff def
            foreach ( var buildable in buildables ) buildable.Stuff = stuff;

            // reset caches
            RecacheBuildables();
        }
    }
}