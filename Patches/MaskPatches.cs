using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class MaskPatches
    {
        [HarmonyPatch(typeof(RandomPeriodicAudioPlayer), "Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> RandomPeriodicAudioPlayerTransUpdate(IEnumerable<CodeInstruction> instructions)
        {
            if (!Plugin.configFixMasks.Value)
                return instructions;

            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Add)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Call && (MethodInfo)codes[j].operand == References.REALTIME_SINCE_STARTUP)
                        {
                            codes[i].opcode = OpCodes.Nop;
                            codes[j].opcode = OpCodes.Nop;
                            Plugin.Logger.LogDebug("Transpiler: Fix periodic mask audio intervals");
                            return codes;
                        }
                    }
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemyPostStart(MaskedPlayerEnemy __instance)
        {
            if (!Plugin.configBetterMimicSteps.Value)
                return;

            AudioSource playerMovementAudio = GameNetworkManager.Instance?.localPlayerController?.movementAudio;
            if (playerMovementAudio == null)
                return;

            __instance.movementAudio.transform.localPosition = new Vector3(0f, 0.278f, 0f);
            __instance.movementAudio.volume = playerMovementAudio.volume;
            __instance.movementAudio.dopplerLevel = playerMovementAudio.dopplerLevel;
            __instance.movementAudio.spread = playerMovementAudio.spread;
            __instance.movementAudio.rolloffMode = AudioRolloffMode.Custom;
            foreach (AudioSourceCurveType audioSourceCurveType in System.Enum.GetValues(typeof(AudioSourceCurveType)))
                __instance.movementAudio.SetCustomCurve(audioSourceCurveType, playerMovementAudio.GetCustomCurve(audioSourceCurveType));
            Plugin.Logger.LogInfo("Mimic: Footsteps match players");
        }
    }
}
