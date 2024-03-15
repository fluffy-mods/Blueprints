using Verse;

namespace Blueprints
{
    public class Dialog_NameBlueprint : Dialog_Rename
    {
        private readonly Blueprint _blueprint;

        public Dialog_NameBlueprint( Blueprint blueprint )
        {
            _blueprint = blueprint;
            curName    = blueprint.name;
        }

        protected override int MaxNameLength => 24;

        protected override AcceptanceReport NameIsValid( string newName )
        {
            // always ok if we didn't change anything
            if ( newName == _blueprint.name )
                return true;

            // otherwise check for used symbols and uniqueness
            var validName = Blueprint.IsValidBlueprintName( newName );
            if ( !validName.Accepted )
                return validName;

            // finally, if we're renaming an already exported blueprint, check if the new name doesn't already exist
            if ( _blueprint.exported && !BlueprintController.TryRenameFile( _blueprint, newName ) )
                return new AcceptanceReport(
                    "Fluffy.Blueprints.ExportedBlueprintWithThatNameAlreadyExists".Translate( newName ) );

            // if all checks are passed, return true.
            return true;
        }

        protected override void SetName( string name )
        {
            _blueprint.name = name;
        }
    }
}