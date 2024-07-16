using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class HoardingBugPatches
    {
        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.KillEnemy))]
        [HarmonyPostfix]
        static void HoarderBugAIPostKillEnemy(HoarderBugAI __instance)
        {
            // creatureVoice.Stop() gets called in original KillEnemy()
            __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
            Plugin.Logger.LogInfo("Hoarding bug: Played backup death sound");
        }
    }
}
