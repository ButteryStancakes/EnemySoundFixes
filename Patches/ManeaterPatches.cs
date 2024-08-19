using HarmonyLib;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class ManeaterPatches
    {
        [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.KillEnemy))]
        [HarmonyPostfix]
        static void CaveDwellerAIPostKillEnemy(CaveDwellerAI __instance, bool destroy)
        {
            if (destroy)
                return;

            foreach (AudioSource maneaterAudio in new AudioSource[]{
                __instance.clickingAudio1,
                __instance.clickingAudio2,
                __instance.walkingAudio,
                __instance.screamAudio,
                __instance.screamAudioNonDiagetic
            })
            {
                maneaterAudio.Stop();
                maneaterAudio.mute = true;
            }
            __instance.creatureVoice.Stop();
            __instance.creatureVoice.PlayOneShot(__instance.dieSFX);
        }
    }
}
