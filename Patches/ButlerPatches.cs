using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class ButlerPatches
    {
        [HarmonyPatch(typeof(ButlerEnemyAI), nameof(ButlerEnemyAI.Update))]
        [HarmonyPostfix]
        static void ButlerEnemyAIPostUpdate(ButlerEnemyAI __instance)
        {
            if (__instance.isEnemyDead && __instance.buzzingAmbience.isPlaying && __instance.creatureAnimator.GetBool("popFinish"))
            {
                __instance.buzzingAmbience.Stop();
                Plugin.Logger.LogDebug("Butler: Stop buzzing (bugs are free)");
            }
        }
    }
}
