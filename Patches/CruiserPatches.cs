using HarmonyLib;
using System.Collections;
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
        static Coroutine twistingKey;

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.RevCarClientRpc))]
        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.TryIgnition), MethodType.Enumerator)]
        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.SetIgnition))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VehicleController_Trans_EngineRev(IEnumerable<CodeInstruction> instructions)
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

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.SetVehicleAudioProperties))]
        [HarmonyPrefix]
        static void VehicleController_Pre_SetVehicleAudioProperties(VehicleController __instance, AudioSource audio, ref bool audioActive)
        {
            if (audioActive && ((audio == __instance.extremeStressAudio && __instance.magnetedToShip) || ((audio == __instance.rollingAudio || audio == __instance.skiddingAudio) && (__instance.magnetedToShip || (!__instance.FrontLeftWheel.isGrounded && !__instance.FrontRightWheel.isGrounded && !__instance.BackLeftWheel.isGrounded && !__instance.BackRightWheel.isGrounded)))))
                audioActive = false;
        }

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.SetVehicleAudioProperties))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VehicleController_Trans_SetVehicleAudioProperties(IEnumerable<CodeInstruction> instructions)
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

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.LateUpdate))]
        [HarmonyPostfix]
        static void VehicleController_Post_LateUpdate(VehicleController __instance)
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

            if (twistingKey != null && __instance.keyIgnitionCoroutine == null)
            {
                __instance.StopCoroutine(twistingKey);
                twistingKey = null;
            }
        }

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.TryIgnition))]
        [HarmonyPrefix]
        static void VehicleController_Pre_TryIgnition(VehicleController __instance, bool isLocalDriver)
        {
            if (!__instance.keyIsInIgnition && isLocalDriver && __instance.vehicleID == 0)
                twistingKey = __instance.StartCoroutine(TwistKey(__instance));
        }

        static IEnumerator TwistKey(VehicleController vehicleController)
        {
            yield return new WaitForSeconds(0.85f);
            if (vehicleController.keyIgnitionCoroutine != null && vehicleController.currentDriver != null)
                vehicleController.currentDriver.movementAudio.PlayOneShot(vehicleController.twistKey);
            twistingKey = null;
            yield break;
        }

        [HarmonyPatch(typeof(VehicleController), nameof(VehicleController.Awake))]
        [HarmonyPrefix]
        static void VehicleController_Post_Awake(VehicleController __instance)
        {
            if (__instance.vehicleID != 0 || References.cruiserDashboardButton == null || References.sfx == null)
                return;

            Transform triggers = __instance.transform.Find("Triggers");
            if (triggers != null)
            {
                //Transform radar = triggers.Find("Radio");
                foreach (Transform button in new Transform[]{
                    triggers.Find("ChangeChannel (1)"),
                    triggers.Find("ChangeChannel (2)"),
                    triggers.Find("ChangeChannel (3)"),
                    /*radar?.Find("TurnOnRadio"),
                    radar?.Find("ChangeChannel")*/})
                {
                    if (button == null || !button.TryGetComponent(out InteractTrigger interactTrigger) || button.GetComponent<AudioSource>() != null)
                        continue;

                    AudioSource audioSource = interactTrigger.gameObject.AddComponent<AudioSource>();
                    audioSource.clip = References.cruiserDashboardButton;
                    audioSource.outputAudioMixerGroup = References.sfx;
                    audioSource.spatialBlend = 1f;
                    audioSource.spread = 33f;
                    audioSource.rolloffMode = AudioRolloffMode.Linear;
                    audioSource.minDistance = 4f;
                    audioSource.maxDistance = 15f;
                    interactTrigger.onInteract.AddListener(delegate
                    {
                        audioSource.Play();
                    });
                    Plugin.Logger.LogDebug($"Cruiser: Dashboard button (\"{button.name}\")");
                }
            }
        }
    }
}
