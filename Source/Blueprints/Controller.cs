//using CommunityCoreLibrary;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public static class Controller
    {
        #region Private Fields

        private static readonly string _blueprintSaveExtension = ".xml";
        private static List<Blueprint> _blueprints;
        private static string _blueprintSaveLocation;
        private static List<Designator> _designators;
        private static bool _initialized = false;

        #endregion Private Fields

        #region Public Properties

        public static string BlueprintSaveLocation
        {
            get
            {
                if ( _blueprintSaveLocation == null )
                    _blueprintSaveLocation = GetSaveLocation();
                return _blueprintSaveLocation;
            }
        }

        #endregion Public Properties

        #region Public Methods

        public static void Add( Blueprint blueprint, bool init = false )
        {
            if ( !init && !_initialized )
                Initialize();
            _blueprints.Add( blueprint );
            var designator = new Designator_Blueprint( blueprint );
            _designators.Add( designator );

            // select the new designator
            DesignatorManager.Select( designator );
        }

        public static void Remove( Designator_Blueprint designator, bool removeFromDisk )
        {
            _blueprints.Remove( designator.Blueprint );
            _designators.Remove( designator );

            if ( removeFromDisk )
                DeleteXML( designator.Blueprint );
        }

        public static int Count( bool includeCreator = false )
        {
            if ( !_initialized )
                Initialize();
            return _designators.Count - ( includeCreator ? 0 : 1 );
        }

        public static Blueprint FindBlueprint( string name )
        {
            if ( !_initialized )
                Initialize();
            return _blueprints.FirstOrDefault( blueprint => blueprint.name == name );
        }

        public static Designator_Blueprint FindDesignator( string name )
        {
            if ( !_initialized )
                Initialize();
            return _designators.FirstOrDefault( designator => ( designator as Designator_Blueprint )?.Blueprint.name == name ) as Designator_Blueprint;
        }

        // TODO: Call from bootstrap
        public static void Initialize()
        {
            if ( _initialized )
                return;

            // start blueprints list
            _blueprints = new List<Blueprint>();

            // find our designation category.
            DesignationCategoryDef desCatDef = DefDatabase<DesignationCategoryDef>.GetNamed( "Blueprints" );
            if ( desCatDef == null )
                throw new Exception( "Blueprints designation category not found" );

            // create internal designators list as a reference to list in the category def.
            FieldInfo _designatorsFI = typeof( DesignationCategoryDef ).GetField( "resolvedDesignators", BindingFlags.NonPublic | BindingFlags.Instance );
            _designators = _designatorsFI.GetValue( desCatDef ) as List<Designator>;

            // done!
            _initialized = true;
        }

        #endregion Public Methods

        #region Private Methods

        private static void DeleteXML( Blueprint blueprint )
        {
            File.Delete( FullFilePath( blueprint.name ) );
        }

        private static string FullSaveLocation( string name )
        {
            return BlueprintSaveLocation + "/" + name + _blueprintSaveExtension;
        }

        internal static List<SaveFileInfo> GetSavedFilesList()
        {
            DirectoryInfo directoryInfo = new DirectoryInfo( BlueprintSaveLocation );

            // raw files
            IOrderedEnumerable<FileInfo> files = from f in directoryInfo.GetFiles()
                                                 where f.Extension == _blueprintSaveExtension
                                                 orderby f.LastWriteTime descending
                                                 select f;

            // convert to RW save files - mostly for the headers
            List<SaveFileInfo> blueprintXMLs = new List<SaveFileInfo>();
            foreach ( FileInfo file in files )
            {
                try
                {
                    blueprintXMLs.Add( new SaveFileInfo( file ) );
                }
                catch ( Exception ex )
                {
                    Log.Error( "Exception loading " + file.Name + ": " + ex );
                }
            }

            return blueprintXMLs;
        }

        private static string GetSaveLocation()
        {
            // Get method "FolderUnderSaveData" from GenFilePaths, which is private (NonPublic) and static.
            MethodInfo Folder = typeof( GenFilePaths ).GetMethod( "FolderUnderSaveData",
                                                                 BindingFlags.NonPublic |
                                                                 BindingFlags.Static );
            if ( Folder == null )
                throw new Exception( "Blueprints :: FolderUnderSaveData is null [reflection]" );

            // Call "FolderUnderSaveData" from null parameter, since this is a static method.
            return (string)Folder.Invoke( null, new object[] { "Blueprints" } );
        }

        private static string FullFilePath( string name )
        {
#if DEBUG
            Log.Message( Path.Combine( _blueprintSaveLocation, name + _blueprintSaveExtension ) );
#endif
            return Path.Combine( _blueprintSaveLocation, name + _blueprintSaveExtension );
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

        internal static Blueprint LoadFromXML( SaveFileInfo file )
        {
            // set up empty blueprint
            Blueprint blueprint = new Blueprint();

#if DEBUG
            Log.Message( "Attempting to load from: " + file.FileInfo.FullName );
#endif

            // load stuff
            try
            {
                Scribe.InitLoading( BlueprintSaveLocation + "/" + file.FileInfo.Name );
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
                Scribe.FinalizeLoading();
                Scribe.mode = LoadSaveMode.Inactive;
            }

            // if a def used in the blueprint doesn't exist, exposeData will throw an error,
            // which is fine. in addition, it'll set the field to null - which may result in problems down the road.
            // Make sure each item in the blueprint has a def set, if not - remove it.
            // This check itself will throw another error, which is also fine. User will have to resolve the issue manually.
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
                    Scribe.InitWriting( FullSaveLocation( blueprint.name ), "Blueprint" );
                }
                catch ( Exception ex )
                {
                    GenUI.ErrorDialog( "ProblemSavingFile".Translate( ex.ToString() ) );
                    throw;
                }
                ScribeMetaHeaderUtility.WriteMetaHeader();

                Scribe_Deep.LookDeep( ref blueprint, "Blueprint" );
            }
            catch ( Exception ex2 )
            {
                Log.Error( "Exception while saving blueprint: " + ex2 );
            }
            finally
            {
                Scribe.FinalizeWriting();
            }

            // set exported flag.
            blueprint.exported = true;
        }

        #endregion Private Methods
    }
}