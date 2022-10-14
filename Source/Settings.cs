// Copyright Karel Kroeze, -2020

using BetterKeybinding;
using UnityEngine;
using Verse;

namespace Blueprints
{
    public class Settings : ModSettings
    {
        private KeyBind _copyKey;

        public KeyBind CopyKey
        {
            get
            {
                _copyKey??=new KeyBind( "Fluffy.Blueprints.CopyKey".Translate(), KeyCode.C, EventModifiers.Control );
                return _copyKey;
            }
        }

        public void DoWindowContents( Rect canvas )
        {
            var options = new Listing_Standard();
            options.Begin( canvas );
            CopyKey.Draw( options.GetRect( 30 ) );
            options.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look( ref _copyKey, "copyKey" );
        }
    }
}