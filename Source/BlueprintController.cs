﻿//using CommunityCoreLibrary;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Blueprints
{
    public class BlueprintController : WorldComponent
    {
        public const   string           BlueprintSaveExtension = ".xml";
        private static string _blueprintSaveLocation;
        private static BlueprintController _instance;

        private List<Blueprint>  _blueprints = new List<Blueprint>();
        private List<Designator> _designators;
        private bool             _initialized;

        public BlueprintController( World world ) : base( world )
        {
            _instance = this;
        }

        public static BlueprintController Instance => _instance;

        public static string BlueprintSaveLocation
        {
            get
            {
                if ( _blueprintSaveLocation == null ) {
                    _blueprintSaveLocation = GetSaveLocation();
                }

                return _blueprintSaveLocation;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            Initialize();
        }

        public static void Add( Blueprint blueprint )
        {
            if ( !Instance._initialized ) {
                Initialize();
            }

            Instance._blueprints.Add( blueprint );

            Designator_Blueprint designator = new Designator_Blueprint( blueprint );
            Instance._designators.Add( designator );

            // select the new designator
            Find.DesignatorManager.Select( designator );
        }

        public static void Remove( Designator_Blueprint designator, bool removeFromDisk )
        {
            if ( !Instance._initialized ) {
                Initialize();
            }

            Instance._blueprints.Remove( designator.Blueprint );
            Instance._designators.Remove( designator );

            if ( removeFromDisk ) {
                DeleteXML( designator.Blueprint );
            }
        }

        public static Blueprint FindBlueprint( string name )
        {
            if ( !Instance._initialized ) {
                Initialize();
            }

            return Instance._blueprints.FirstOrDefault( blueprint => blueprint.name == name );
        }

        public static Designator_Blueprint FindDesignator( string name )
        {
            if ( !Instance._initialized ) {
                Initialize();
            }

            return Instance._designators.FirstOrDefault( designator =>
                                                    ( designator as Designator_Blueprint )?.Blueprint.name == name ) as
                Designator_Blueprint;
        }

        public static void Initialize()
        {
            if ( Instance._initialized ) {
                return;
            }

            // do harmony patches
            Harmony harmony = new Harmony( "fluffy.blueprints" );
            harmony.PatchAll( Assembly.GetExecutingAssembly() );

            // find our designation category.
            DesignationCategoryDef desCatDef = DefDatabase<DesignationCategoryDef>.GetNamed( "Blueprints" );
            if ( desCatDef == null ) {
                throw new Exception( "Blueprints designation category not found" );
            }

            // reset list of designators in blueprints tab.
            Instance._designators = desCatDef.AllResolvedDesignators;
            Instance._designators.Clear();
            Instance._designators.Add( new Designator_CreateBlueprint() );

            foreach (Blueprint blueprint in Instance._blueprints ) {
                Instance._designators.Add( new Designator_Blueprint( blueprint ) );
            }

            // done!
            Instance._initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look( ref _blueprints, "Blueprints" );
        }

        private static void DeleteXML( Blueprint blueprint )
        {
            File.Delete( FullFilePath( blueprint.name ) );
        }

        internal static List<FileInfo> GetSavedFilesList()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo( BlueprintSaveLocation );

            IOrderedEnumerable<FileInfo> files = from f in directoryInfo.GetFiles()
                        where f.Extension == BlueprintSaveExtension
                        orderby f.LastWriteTime descending
                        select f;

            return files.ToList();
        }

        private static string GetSaveLocation()
        {
            // Get method "FolderUnderSaveData" from GenFilePaths, which is private (NonPublic) and static.
            MethodInfo Folder = typeof( GenFilePaths ).GetMethod( "FolderUnderSaveData",
                                                           BindingFlags.NonPublic |
                                                           BindingFlags.Static );
            if ( Folder == null ) {
                throw new Exception( "Blueprints :: FolderUnderSaveData is null [reflection]" );
            }

            // Call "FolderUnderSaveData" from null parameter, since this is a static method.
            return (string) Folder.Invoke( null, new object[] {"Blueprints"} );
        }

        private static string FullFilePath( string name )
        {
#if DEBUG
            Log.Message( Path.Combine( BlueprintSaveLocation, name + BlueprintSaveExtension ) );
#endif
            return Path.Combine( BlueprintSaveLocation, name + BlueprintSaveExtension );
        }

        internal static bool FileExists( string name )
        {
            return File.Exists( FullFilePath( name ) );
        }

        internal static bool TryRenameFile( Blueprint blueprint, string newName )
        {
            if ( !FileExists( newName ) )
            {
                RenameFile( blueprint, newName );
                return true;
            }

            return false;
        }

        private static void RenameFile( Blueprint blueprint, string newName )
        {
            DeleteXML( blueprint );
            blueprint.name = newName;
            SaveToXML( blueprint );
        }

        internal static Blueprint LoadFromXML( string name )
        {
            // set up empty blueprint
            Blueprint blueprint = new Blueprint();

#if DEBUG
            Log.Message( "Attempting to load from: " + name );
#endif

            // load stuff
            try
            {
                Scribe.loader.InitLoading( BlueprintSaveLocation + "/" + name );
                ScribeMetaHeaderUtility.LoadGameDataHeader( ScribeMetaHeaderUtility.ScribeHeaderMode.Map, true );
                Scribe.EnterNode( "Blueprint" );
                blueprint.ExposeData();
                Scribe.ExitNode();
            }
            catch ( Exception e )
            {
                Log.Error( "Exception while loading blueprint: " + e );
            }
            finally
            {
                // done loading
                Scribe.loader.FinalizeLoading();
                Scribe.mode = LoadSaveMode.Inactive;
            }

            // if a def used in the blueprint doesn't exist, exposeData will throw an error,
            // which is fine. in addition, it'll set the field to null - which may result in problems down the road.
            // Make sure each item in the blueprint has a def set, if not - remove it.
            blueprint.contents = blueprint.contents.Where( item => item.BuildableDef != null ).ToList();

            // return blueprint.
            return blueprint;
        }

        public static void SaveToXML( Blueprint blueprint )
        {
            try
            {
                try
                {
                    Scribe.saver.InitSaving( FullFilePath( blueprint.name ), "Blueprint" );
                }
                catch ( Exception ex )
                {
                    GenUI.ErrorDialog( "ProblemSavingFile".Translate( ex.ToString() ) );
                    throw;
                }

                ScribeMetaHeaderUtility.WriteMetaHeader();
                Scribe_Deep.Look( ref blueprint, "Blueprint" );
            }
            catch ( Exception ex2 )
            {
                Log.Error( "Exception while saving blueprint: " + ex2 );
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
            }

            // set exported flag.
            blueprint.exported = true;
        }
    }
}
