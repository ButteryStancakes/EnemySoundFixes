using HarmonyLib;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    static class HoardingBugPatches
    {
        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.KillEnemy))]
        [HarmonyPostfix]
        static void HoarderBugAI_Post_KillEnemy(HoarderBugAI __instance, bool destroy)
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
                AudioClip clip = Random.value > 0.5f ? __instance.enemyType.deathSFX : __instance.dieSFX;
                __instance.creatureVoice.pitch = Random.Range(0.94f, 1.06f);
                __instance.creatureVoice.PlayOneShot(clip);
                WalkieTalkie.TransmitOneShotAudio(__instance.creatureVoice, clip, 0.85f);
                Plugin.Logger.LogDebug("Hoarding bug: Played backup death sound");
            }
        }

        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.HitEnemy))]
        [HarmonyPrefix]
        static void HoarderBugAI_Pre_HitEnemy(HoarderBugAI __instance, int force, bool playHitSFX)
        {
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead && __instance.enemyHP <= force;
        }
    }
}
