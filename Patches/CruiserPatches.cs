using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class CruiserPatches
    {
        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.RevCarClientRpc))]
        [HarmonyPatch(typeof(VehicleController), "TryIgnition", MethodType.Enumerator)]
        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.SetIgnition))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VehicleControllerTransEngineRev(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 4; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == References.PLAY_ONE_SHOT && codes[i - 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 3].operand == References.ENGINE_AUDIO_1)
                {
                    codes.InsertRange(i - 4, [
                        new CodeInstruction(codes[i - 4].opcode, codes[i - 4].operand),
                        new CodeInstruction(OpCodes.Ldfld, References.ENGINE_AUDIO_1),
                        new CodeInstruction(OpCodes.Callvirt, References.STOP),
                    ]);
                    Plugin.Logger.LogDebug("Transpiler (Cruiser): Reset engine rev sounds");
                    break;
                }
            }

            return codes;
        }
    }
}
