// Copyright Karel Kroeze, 2021-2021.
// Blueprints/Blueprints/Patch_InspectGizmoGrid_DrawInspectGizmoGridFor.cs

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Blueprints
{
    [HarmonyPatch(typeof(InspectGizmoGrid), nameof(InspectGizmoGrid.DrawInspectGizmoGridFor))]
    public class Patch_InspectGizmoGrid_DrawInspectGizmoGridFor
    {
        public static MethodInfo addInfo = AccessTools.Method(typeof(List<Gizmo>), nameof(List<Gizmo>.Add));

        public static MethodInfo clearInfo = AccessTools.Method(typeof(List<object>), "Clear");
        public static FieldInfo  gizmoList = AccessTools.Field(typeof(InspectGizmoGrid), "gizmoList");
        public static FieldInfo  objList   = AccessTools.Field(typeof(InspectGizmoGrid), "objList");

        public static MethodInfo blueprintGetter = AccessTools
                                                  .Property(typeof(Patch_InspectGizmoGrid_DrawInspectGizmoGridFor),
                                                            nameof(BlueprintCopy))
                                                  .GetGetMethod();


        public static Command_CreateBlueprintCopyFromSelected BlueprintCopy =>
            new Command_CreateBlueprintCopyFromSelected();

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> _instructions)
        {
            List<CodeInstruction> instructions = _instructions.ToList();


            for (int i = 0; i < instructions.Count; i++)
            {
                yield return instructions[i];

                if (i > 0 && instructions[i - 1].LoadsField(objList)
                          && instructions[i].Calls(clearInfo)
                          && instructions[i + 1].LoadsField(gizmoList))
                {
                    Debug.Message("injecting blueprint gizmo");
                    yield return new CodeInstruction(OpCodes.Ldsfld, gizmoList);
                    yield return new CodeInstruction(OpCodes.Call, blueprintGetter);
                    yield return new CodeInstruction(OpCodes.Callvirt, addInfo);
                }
            }
        }
    }
}
