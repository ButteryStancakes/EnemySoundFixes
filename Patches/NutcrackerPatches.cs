using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class NutcrackerPatches
    {
        [HarmonyPatch(typeof(NutcrackerEnemyAI), nameof(NutcrackerEnemyAI.KillEnemy))]
        [HarmonyPostfix]
        static void NutcrackerEnemyAIPostKillEnemy(NutcrackerEnemyAI __instance, bool destroy)
        {
            if (destroy)
                return;

            // stop the marching music
            __instance.creatureVoice.loop = false;
            __instance.creatureVoice.clip = null;
            __instance.creatureVoice.pitch = 1f;

            // can't just assign dieSFX because it gets canceled by creatureVoice.Stop() in original KillEnemy()
            __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
            Plugin.Logger.LogInfo("Nutcracker: Played death sound");
        }
    }
}
