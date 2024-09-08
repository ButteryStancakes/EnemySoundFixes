using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class HoardingBugPatches
    {
        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.KillEnemy))]
        [HarmonyPostfix]
        static void HoarderBugAIPostKillEnemy(HoarderBugAI __instance, bool destroy)
        {
            // happens after creatureSFX.Stop()
            if (GeneralPatches.playHitSound)
            {
                GeneralPatches.playHitSound = false;
                if (!destroy)
                {
                    __instance.creatureSFX.PlayOneShot(__instance.enemyType.hitBodySFX);
                    Plugin.Logger.LogDebug("Hoarding bug: Play hit sound on death");
                }
            }

            if (!destroy)
            {
                // happens after creatureVoice.Stop()
                __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
                Plugin.Logger.LogDebug("Hoarding bug: Played backup death sound");
            }
        }

        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.HitEnemy))]
        [HarmonyPrefix]
        static void HoarderBugAIPreHitEnemy(HoarderBugAI __instance, int force, bool playHitSFX)
        {
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead && __instance.enemyHP <= force;
        }
    }
}
