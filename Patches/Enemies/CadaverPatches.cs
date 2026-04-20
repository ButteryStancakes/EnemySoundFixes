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
    }
}
