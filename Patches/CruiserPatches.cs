using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

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

        [HarmonyPatch(typeof(VehicleController), "SetVehicleAudioProperties")]
        [HarmonyPrefix]
        static void VehicleControllerPreSetVehicleAudioProperties(VehicleController __instance, AudioSource audio, ref bool audioActive)
        {
            if (audioActive && ((audio == __instance.extremeStressAudio && __instance.magnetedToShip) || ((audio == __instance.rollingAudio || audio == __instance.skiddingAudio) && (__instance.magnetedToShip || (!__instance.FrontLeftWheel.isGrounded && !__instance.FrontRightWheel.isGrounded && !__instance.BackLeftWheel.isGrounded && !__instance.FrontLeftWheel.isGrounded)))))
                audioActive = false;
        }

        [HarmonyPatch(typeof(VehicleController), "SetVehicleAudioProperties")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VehicleControllerTransSetVehicleAudioProperties(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo volume = AccessTools.DeclaredPropertyGetter(typeof(AudioSource), nameof(AudioSource.volume));
            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == volume && codes[i + 1].opcode == OpCodes.Ldc_R4 && (float)codes[i + 1].operand == 0f && codes[i - 2].opcode == OpCodes.Bne_Un)
                {
                    codes[i + 1].operand = 0.001f;
                    codes[i + 2].opcode = OpCodes.Bge;
                    Plugin.Logger.LogDebug("Transpiler (Cruiser): Stop audio source when volume is close enough to zero");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(VehicleController), "LateUpdate")]
        [HarmonyPostfix]
        static void VehicleControllerPostLateUpdate(VehicleController __instance)
        {
            if (__instance.magnetedToShip && (StartOfRound.Instance.inShipPhase || !StartOfRound.Instance.shipDoorsEnabled) && Plugin.configSpaceMutesCruiser.Value > CruiserMute.Nothing)
            {
                __instance.hornAudio.mute = true;
                __instance.engineAudio1.mute = true;
                __instance.engineAudio2.mute = true;
                //__instance.rollingAudio.mute = true;
                //__instance.skiddingAudio.mute = true;
                __instance.turbulenceAudio.mute = true;
                //__instance.hoodFireAudio.mute = true;
                //__instance.extremeStressAudio.mute = true;
                if (Plugin.configSpaceMutesCruiser.Value == CruiserMute.NotRadio)
                {
                    __instance.radioAudio.mute = false;
                    __instance.radioInterference.mute = false;
                }
                else
                {
                    __instance.radioAudio.mute = true;
                    __instance.radioInterference.mute = true;
                }
                __instance.pushAudio.mute = true;
            }
            else
            {
                __instance.hornAudio.mute = false;
                __instance.engineAudio1.mute = false;
                __instance.engineAudio2.mute = false;
                //__instance.rollingAudio.mute = false;
                //__instance.skiddingAudio.mute = false;
                __instance.turbulenceAudio.mute = false;
                //__instance.hoodFireAudio.mute = false;
                //__instance.extremeStressAudio.mute = false;
                __instance.radioAudio.mute = false;
                __instance.radioInterference.mute = false;
                __instance.pushAudio.mute = false;
            }
        }
    }
}
