using HarmonyLib;

namespace EnemySoundFixes.Patches.Enemies
{
    [HarmonyPatch(typeof(ButlerEnemyAI))]
    static class ButlerPatches
    {
        [HarmonyPatch(nameof(ButlerEnemyAI.Update))]
        [HarmonyPostfix]
        static void ButlerEnemyAI_Post_Update(ButlerEnemyAI __instance)
        {
            if (__instance.isEnemyDead && __instance.buzzingAmbience.isPlaying && __instance.creatureAnimator.GetBool("popFinish"))
            {
                __instance.buzzingAmbience.Stop();
                Plugin.Logger.LogDebug("Butler: Stop buzzing (bugs are free)");
            }
        }
    }
}
