using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class BrackenPatches
    {
        [HarmonyPatch(typeof(FlowermanAI), nameof(FlowermanAI.HitEnemy))]
        [HarmonyPrefix]
        static void FlowermanAIPreHitEnemy(FlowermanAI __instance, int force, bool playHitSFX)
        {
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead && __instance.enemyHP <= force;
        }

        [HarmonyPatch(typeof(FlowermanAI), nameof(FlowermanAI.KillEnemy))]
        [HarmonyPostfix]
        static void FlowermanAIPostKillEnemy(FlowermanAI __instance, bool destroy)
        {
            // happens after creatureSFX.Stop()
            if (GeneralPatches.playHitSound)
            {
                GeneralPatches.playHitSound = false;
                if (!destroy)
                {
                    __instance.creatureSFX.PlayOneShot(__instance.enemyType.hitBodySFX);
                    Plugin.Logger.LogInfo("Bracken: Play hit sound on death");
                }
            }
        }
    }
}
