using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class SapsuckerPatches
    {
        [HarmonyPatch(typeof(GiantKiwiAI), nameof(GiantKiwiAI.Update))]
        [HarmonyPostfix]
        static void GiantKiwiAIPostUpdate(GiantKiwiAI __instance)
        {
            if (__instance.isEnemyDead && __instance.creatureSFX.isPlaying)
            {
                __instance.creatureSFX.Stop();
                Plugin.Logger.LogDebug("Sapsucker: Stop snoring (dead)");
            }
        }
    }
}
