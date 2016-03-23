using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public class Dialog_NameBlueprint : Dialog_Rename
    {
        #region Fields

        private Blueprint _blueprint;

        #endregion Fields

        #region Constructors

        public Dialog_NameBlueprint( Blueprint blueprint ) : base()
        {
            _blueprint = blueprint;
            curName = blueprint.name;
        }

        #endregion Constructors

        #region Properties

        protected override int MaxNameLength => 24;

        #endregion Properties

        #region Methods

        protected override AcceptanceReport NameIsValid( string newName )
        {
            // always ok if we didn't change anything
            if ( newName == _blueprint.name )
                return true;

            // otherwise check for used symbols and uniqueness
            AcceptanceReport validName = Blueprint.IsValidBlueprintName( newName );
            if ( !validName.Accepted )
                return validName;

            // finally, if we're renaming an already exported blueprint, check if the new name doesn't already exist
            if ( _blueprint.exported && !Controller.TryRenameFile( _blueprint, newName ) )
                return new AcceptanceReport( "Fluffy.Blueprints.ExportedBlueprintWithThatNameAlreadyExists".Translate( newName ) );

            // if all checks are passed, return true.
            return true;
        }

        protected override void SetName( string name )
        {
            _blueprint.name = name;
        }

        #endregion Methods
    }
}