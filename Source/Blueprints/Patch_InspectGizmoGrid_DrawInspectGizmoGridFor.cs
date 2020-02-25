// Patch_InspectGizmoGrid_DrawInspectGizmoGridFor.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Blueprints
{
    [HarmonyPatch( typeof( InspectGizmoGrid ), nameof( InspectGizmoGrid.DrawInspectGizmoGridFor ) )]
    public class Patch_InspectGizmoGrid_DrawInspectGizmoGridFor
    {
        public static FieldInfo gizmoList = AccessTools.Field( typeof( InspectGizmoGrid ), "gizmoList" );

        public static FieldInfo objList = AccessTools.Field( typeof( InspectGizmoGrid ), "objList" );

        public static Command_CreateBlueprintFromSelected blueprint => new Command_CreateBlueprintFromSelected();

        public static IEnumerable<CodeInstruction> Transpiler( IEnumerable<CodeInstruction> _instructions )
        {
            var instructions = _instructions.ToList();

            var clearInfo = AccessTools.Method( typeof( List<object> ), "Clear" );
            var blueprintGetter = AccessTools
                             .Property( typeof( Patch_InspectGizmoGrid_DrawInspectGizmoGridFor ), nameof( blueprint ) )
                             .GetGetMethod();
            var addInfo = AccessTools.Method( typeof( List<Gizmo> ), nameof( List<Gizmo>.Add ) );

            for ( int i = 0; i < instructions.Count; i++ )
            {
                yield return instructions[i];

                if ( i > 0 && instructions[i - 1].LoadsField( objList ) && instructions[i].Calls( clearInfo ) )
                {
                    yield return new CodeInstruction( OpCodes.Ldsfld, gizmoList );
                    yield return new CodeInstruction( OpCodes.Call, blueprintGetter );
                    yield return new CodeInstruction( OpCodes.Callvirt, addInfo );
                }
            }
        }
    }
}