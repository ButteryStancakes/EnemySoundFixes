using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class ForestKeeperPatches
    {
        const float TIME_PLAY_AUDIO_2 = (178 - 46) / 60f; // frame 178 at 60 fps - PlayAudio2 event, PlayAudio1 at frame 46

        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.Update))]
        [HarmonyPostfix]
        static void ForestGiantAI_Post_Update(ForestGiantAI __instance)
        {
            if (__instance.stunNormalizedTimer > 0f || __instance.isEnemyDead || __instance.currentBehaviourStateIndex == 2)
            {
                PlayAudioAnimationEvent playAudioAnimationEvent = __instance.animationContainer.GetComponent<PlayAudioAnimationEvent>();
                AudioSource closeWideSFX = playAudioAnimationEvent.audioToPlay;
                if (closeWideSFX.isPlaying && closeWideSFX.clip != null)
                {
                    closeWideSFX.clip = null;
                    closeWideSFX.Stop();
                    Plugin.Logger.LogDebug("Forest keeper: Stop chewing (eating animation interrupted)");
                }
                ParticleSystem bloodParticle = playAudioAnimationEvent.particle;
                if (bloodParticle.isEmitting)
                {
                    bloodParticle.Stop();
                    Plugin.Logger.LogDebug("Forest keeper: Stop spraying blood from mouth (eating animation interrupted)");
                }
            }
        }

        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.StopKillAnimation))]
        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.EatPlayerAnimation), MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ForestGiantAI_Trans_Animation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == References.CREATURE_VOICE)
                {
                    for (int j = i - 1; j <= i + 1; j++)
                    {
                        codes[j].opcode = OpCodes.Nop;
                        codes[j].operand = null;
                    }
                    Plugin.Logger.LogDebug("Transpiler (Forest Keeper): Don't interrupt voice");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.AnimationEventA))]
        [HarmonyPostfix]
        static void ForestGiantAI_Post_AnimationEventA(ForestGiantAI __instance)
        {
            __instance.creatureSFX.PlayOneShot(__instance.giantFall);
            Plugin.Logger.LogDebug("Forest keeper: Fallen down");
        }

        [HarmonyPatch(typeof(PlayAudioAnimationEvent), nameof(PlayAudioAnimationEvent.PlayAudio2))]
        [HarmonyPrefix]
        static bool PlayAudioAnimationEvent_Pre_PlayAudio2(PlayAudioAnimationEvent __instance)
        {
            if (__instance.audioClip2.name == "FGiantEatPlayerSFX")
            {
                ForestGiantAI forestGiantAI = __instance.GetComponent<EnemyAnimationEvent>().mainScript as ForestGiantAI;
                if (forestGiantAI.inSpecialAnimationWithPlayer != null && forestGiantAI.inSpecialAnimationWithPlayer.inAnimationWithEnemy == forestGiantAI)
                {
                    __instance.audioToPlay.PlayOneShot(__instance.audioClip2);
                    Plugin.Logger.LogDebug("Forest keeper: Play bite sound effect with overlap");
                }
                else
                    Plugin.Logger.LogDebug("Forest keeper: Don't bite (player was teleported)");

                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(PlayAudioAnimationEvent), nameof(PlayAudioAnimationEvent.PlayParticle))]
        [HarmonyPrefix]
        static bool PlayAudioAnimationEvent_Pre_PlayParticle(PlayAudioAnimationEvent __instance)
        {
            if (__instance.audioClip2 != null && __instance.audioClip2.name == "FGiantEatPlayerSFX")
            {
                EnemyAI enemyAI = __instance.GetComponent<EnemyAnimationEvent>().mainScript;
                if (enemyAI.inSpecialAnimationWithPlayer == null || enemyAI.inSpecialAnimationWithPlayer.inAnimationWithEnemy != enemyAI)
                {
                    Plugin.Logger.LogDebug("Forest keeper: Don't spray blood (player was teleported)");
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.CancelSpecialAnimationWithPlayer))]
        [HarmonyPrefix]
        static void EnemyAI_Pre_CancelSpecialAnimationWithPlayer(EnemyAI __instance)
        {
            if (__instance is ForestGiantAI)
            {
                if (__instance.inSpecialAnimationWithPlayer != null && __instance.inSpecialAnimationWithPlayer.inAnimationWithEnemy == __instance && !__instance.inSpecialAnimationWithPlayer.isPlayerDead)
                {
                    PlayAudioAnimationEvent playAudioAnimationEvent = (__instance as ForestGiantAI).animationContainer.GetComponent<PlayAudioAnimationEvent>();
                    AudioSource closeWideSFX = playAudioAnimationEvent.audioToPlay;
                    if (closeWideSFX.isPlaying && closeWideSFX.clip?.name == "Roar" && closeWideSFX.time > TIME_PLAY_AUDIO_2)
                    {
                        //float time = closeWideSFX.time;
                        closeWideSFX.Stop();
                        // this didn't seem to work as expected
                        /*try
                        {
                            closeWideSFX.Play();
                            closeWideSFX.time = time;
                        }
                        catch (System.Exception e)
                        {
                            Plugin.Logger.LogError("Forest keeper: Failed to seek audio");
                            Plugin.Logger.LogError(e);
                            closeWideSFX.Stop();
                        }*/
                        Plugin.Logger.LogDebug("Forest keeper: Stop chewing (player was teleported)");
                        ParticleSystem bloodParticle = playAudioAnimationEvent.particle;
                        if (bloodParticle.isEmitting)
                        {
                            bloodParticle.Stop();
                            Plugin.Logger.LogDebug("Forest keeper: Stop spraying blood from mouth (player was teleported)");
                        }
                    }
                }
            }
        }
    }
}
