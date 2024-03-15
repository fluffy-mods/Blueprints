using RimWorld;
using Verse;

namespace Blueprints {
    public class Dialog_NameBlueprint: Dialog_Rename<Blueprint> {
        public Dialog_NameBlueprint(Blueprint renaming) : base(renaming) {
        }

        protected override int MaxNameLength => 24;

        protected override AcceptanceReport NameIsValid(string newName) {
            // always ok if we didn't change anything
            if (newName == renaming.name) {
                return true;
            }

            // otherwise check for used symbols and uniqueness
            AcceptanceReport validName = Blueprint.IsValidBlueprintName( newName );
            if (!validName.Accepted) {
                return validName;
            }

            // finally, if we're renaming an already exported blueprint, check if the new name doesn't already exist
            if (renaming.exported && !BlueprintController.TryRenameFile(renaming, newName)) {
                return new AcceptanceReport(
                    "Fluffy.Blueprints.ExportedBlueprintWithThatNameAlreadyExists".Translate(newName));
            }

            // if all checks are passed, return true.
            return true;
        }

    }
}
