using HarmonyLib;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class TulipSnakePatches
    {
        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.Update))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemy_Post_Update(FlowerSnakeEnemy __instance, bool ___flapping)
        {
            if (__instance.flappingAudio.isPlaying)
            {
                if (__instance.isEnemyDead)
                {
                    __instance.flappingAudio.Stop();
                    __instance.flappingAudio.mute = true;
                    Plugin.Logger.LogDebug("Tulip snake: Stop making noise while dead");
                }
                else if (!Plugin.INSTALLED_SOUND_API)
                {
                    if (__instance.flappingAudio.clip == __instance.enemyType.audioClips[9])
                    {
                        if (__instance.clingingToPlayer != null)
                        {
                            __instance.flappingAudio.Stop();
                            Plugin.Logger.LogDebug("Tulip snake: Stop scurrying (latched to player)");
                        }
                    }
                    else if (__instance.clingingToPlayer == null)
                    {
                        __instance.flappingAudio.Stop();
                        Plugin.Logger.LogDebug("Tulip snake: Stop flapping (no longer clinging)");
                    }
                }

                if (___flapping)
                    __instance.flappingAudio.volume = 0.85f; // v70
            }
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.StopLeapOnLocalClient))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemy_Post_StopLeapOnLocalClient(FlowerSnakeEnemy __instance, bool landOnGround)
        {
            if (landOnGround && !__instance.isEnemyDead)
            {
                __instance.flappingAudio.pitch = Random.Range(0.8f, 1.2f);
                Plugin.Logger.LogDebug("Tulip snake: Reroll scurry pitch (landed from leap)");
            }
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.StopClingingOnLocalClient))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemy_Post_StopClingingOnLocalClient(FlowerSnakeEnemy __instance)
        {
            if (!__instance.isEnemyDead)
            {
                __instance.flappingAudio.pitch = Random.Range(0.8f, 1.2f);
                Plugin.Logger.LogDebug("Tulip snake: Reroll scurry pitch (dismounted player)");
            }
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.HitEnemy))]
        [HarmonyPrefix]
        static void FlowerSnakeEnemy_Pre_HitEnemy(FlowerSnakeEnemy __instance, bool playHitSFX)
        {
            // so tulip snake can play hit sound when killed
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead;
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.KillEnemy))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemy_Post_KillEnemy(FlowerSnakeEnemy __instance, bool destroy)
        {
            // happens after creatureSFX.Stop()
            if (GeneralPatches.playHitSound)
            {
                GeneralPatches.playHitSound = false;
                if (!destroy && References.hitEnemyBody != null)
                {
                    __instance.creatureSFX.PlayOneShot(References.hitEnemyBody);
                    Plugin.Logger.LogDebug("Tulip snake: Squish");
                }
            }
        }
    }
}
