using HarmonyLib;
using UnityEngine;

namespace EnemySoundFixes.Patches.Enemies
{
    [HarmonyPatch]
    static class CadaverPatches
    {
        static MoldSpreadManager moldSpreadManager;

        [HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.Start))]
        [HarmonyPostfix]
        static void CadaverGrowthAI_Post_Start(CadaverGrowthAI __instance)
        {
            if (moldSpreadManager == null)
                moldSpreadManager = Object.FindAnyObjectByType<MoldSpreadManager>();

            if (moldSpreadManager != null && __instance.destroyAudio != null && __instance.destroyAudio.clip == null)
                __instance.destroyAudio.clip = moldSpreadManager.destroyAudio?.clip;
        }

        [HarmonyPatch(typeof(CadaverBloomAI), nameof(CadaverBloomAI.KillEnemy))]
        [HarmonyPostfix]
        static void CadaverBloomAI_Post_KillEnemy(CadaverBloomAI __instance, bool destroy)
        {
            if (destroy)
                return;

            __instance.creatureSFX.clip = null; // walking sound
            foreach (AudioSource cadaverAudio in new AudioSource[]{
                __instance.breathingSFX,
                __instance.burstSource
            })
            {
                cadaverAudio.Stop();
                cadaverAudio.mute = true;
            }
        }
    }
}
