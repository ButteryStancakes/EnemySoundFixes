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
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == References.PLAY_ONE_SHOT && codes[i - 3].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 3].operand == References.ENGINE_AUDIO_1)
                {
                    codes.InsertRange(i - 4, [
                        new CodeInstruction(codes[i - 4].opcode, codes[i - 4].operand),
                        new CodeInstruction(OpCodes.Ldfld, References.ENGINE_AUDIO_1),
                        new CodeInstruction(OpCodes.Callvirt, References.STOP),
                    ]);
                    Plugin.Logger.LogDebug("Transpiler (Cruiser): Reset engine rev sounds");
                    return codes;
                }
            }

            Plugin.Logger.LogError("Cruiser transpiler failed");
            return codes;
        }

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.PlayCollisionAudio))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VehicleControllerTransPlayCollisionAudio(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            FieldInfo audio1Type = AccessTools.Field(typeof(VehicleController), "audio1Type"), audio1Time = AccessTools.Field(typeof(VehicleController), "audio1Time"), audio2Type = AccessTools.Field(typeof(VehicleController), "audio2Type"), audio2Time = AccessTools.Field(typeof(VehicleController), "audio2Time");
            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == References.REALTIME_SINCE_STARTUP && codes[i + 2].opcode == OpCodes.Ldfld)
                {
                    if ((FieldInfo)codes[i + 2].operand == audio1Type)
                    {
                        Plugin.Logger.LogDebug("Transpiler (Cruiser): Fix timestamp check for collision audio (#1)");
                        codes[i + 2].operand = audio1Time;
                    }
                    else if ((FieldInfo)codes[i + 2].operand == audio2Type)
                    {
                        Plugin.Logger.LogDebug("Transpiler (Cruiser): Fix timestamp check for collision audio (#2)");
                        codes[i + 2].operand = audio2Time;
                    }
                }
            }

            return codes;
        }
    }
}
