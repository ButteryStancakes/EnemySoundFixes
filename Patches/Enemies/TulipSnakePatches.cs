using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EnemySoundFixes.Patches.Enemies
{
    [HarmonyPatch(typeof(FlowerSnakeEnemy))]
    static class TulipSnakePatches
    {
        [HarmonyPatch(nameof(FlowerSnakeEnemy.Update))]
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

        [HarmonyPatch(nameof(FlowerSnakeEnemy.StopLeapOnLocalClient))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemy_Post_StopLeapOnLocalClient(FlowerSnakeEnemy __instance, bool landOnGround)
        {
            if (landOnGround && !__instance.isEnemyDead)
            {
                __instance.flappingAudio.pitch = Random.Range(0.8f, 1.2f);
                Plugin.Logger.LogDebug("Tulip snake: Reroll scurry pitch (landed from leap)");
            }
        }

        [HarmonyPatch(nameof(FlowerSnakeEnemy.StopClingingOnLocalClient))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemy_Post_StopClingingOnLocalClient(FlowerSnakeEnemy __instance)
        {
            if (!__instance.isEnemyDead)
            {
                __instance.flappingAudio.pitch = Random.Range(0.8f, 1.2f);
                Plugin.Logger.LogDebug("Tulip snake: Reroll scurry pitch (dismounted player)");
            }
        }

        [HarmonyPatch(nameof(FlowerSnakeEnemy.HitEnemy))]
        [HarmonyPrefix]
        static void FlowerSnakeEnemy_Pre_HitEnemy(FlowerSnakeEnemy __instance, bool playHitSFX)
        {
            // so tulip snake can play hit sound when killed
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead;
        }

        [HarmonyPatch(nameof(FlowerSnakeEnemy.KillEnemy))]
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

        [HarmonyPatch(nameof(FlowerSnakeEnemy.MakeChuckleClientRpc))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> FlowerSnakeEnemy_Trans_MakeChuckleClientRpc(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            FieldInfo audioClips = AccessTools.Field(typeof(EnemyType), nameof(EnemyType.audioClips));
            for (int i = 5; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == References.PLAY_RANDOM_CLIP && codes[i - 1].opcode == OpCodes.Ldc_I4_5 && codes[i - 5].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 5].operand == audioClips)
                {
                    codes[i - 1].opcode = OpCodes.Ldc_I4_4;
                    Plugin.Logger.LogDebug("Transpiler (Tulip snake): Remove wingflap from chuckle pool");
                    break;
                }
            }

            return codes;
        }
    }
}
