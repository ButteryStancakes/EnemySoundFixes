using HarmonyLib;

namespace EnemySoundFixes.Patches.Enemies
{
    [HarmonyPatch(typeof(GiantKiwiAI))]
    static class GiantSapsuckerPatches
    {
        [HarmonyPatch(nameof(GiantKiwiAI.Update))]
        [HarmonyPostfix]
        static void GiantKiwiAI_Post_Update(GiantKiwiAI __instance)
        {
            if (__instance.isEnemyDead && __instance.creatureSFX.isPlaying)
            {
                __instance.creatureSFX.Stop();
                Plugin.Logger.LogDebug("Sapsucker: Stop snoring (dead)");
            }
        }
    }
}
