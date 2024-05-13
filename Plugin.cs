﻿using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EnemySoundFixes
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.enemysoundfixes", PLUGIN_NAME = "Enemy Sound Fixes", PLUGIN_VERSION = "1.1.0";
        internal static new ManualLogSource Logger;

        void Awake()
        {
            Logger = base.Logger;

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class EnemySoundFixesPatches
    {
        static AudioClip /*hitEnemyBody,*/ baboonTakeDamage;
        static readonly FieldInfo CREATURE_VOICE = typeof(EnemyAI).GetField(nameof(EnemyAI.creatureVoice), BindingFlags.Instance | BindingFlags.Public);

        [HarmonyPatch(typeof(QuickMenuManager), "Start")]
        [HarmonyPostfix]
        static void QuickMenuManagerPostStart(QuickMenuManager __instance)
        {
            /*if (hitEnemyBody == null)
            {
                SpawnableEnemyWithRarity enemy = __instance.testAllEnemiesLevel.Enemies.FirstOrDefault(spawnableEnemyWithRarity => spawnableEnemyWithRarity.enemyType.hitBodySFX.name == "HitEnemyBody");
                if (enemy != null)
                {
                    hitEnemyBody = enemy.enemyType.hitBodySFX;
                    Plugin.Logger.LogInfo("Cached generic damage sound");
                }
            }*/
            SpawnableEnemyWithRarity baboon = __instance.testAllEnemiesLevel.OutsideEnemies.FirstOrDefault(spawnableEnemyWithRarity => spawnableEnemyWithRarity.enemyType.name == "BaboonHawk");
            if (baboon != null)
            {
                if (baboonTakeDamage == null)
                {
                    baboonTakeDamage = baboon.enemyType.hitBodySFX;
                    Plugin.Logger.LogInfo("Cached baboon hawk damage sound");
                }
                baboon.enemyType.hitBodySFX = null; //hitEnemyBody
                Plugin.Logger.LogInfo("Overwritten baboon hawk damage sound");
                baboon.enemyType.enemyPrefab.GetComponent<BaboonBirdAI>().dieSFX = baboon.enemyType.deathSFX;
                Plugin.Logger.LogInfo("Overwritten missing baboon hawk death sound");
            }
            SpawnableEnemyWithRarity centipede = __instance.testAllEnemiesLevel.Enemies.FirstOrDefault(spawnableEnemyWithRarity => spawnableEnemyWithRarity.enemyType.name == "Centipede");
            if (centipede != null)
            {
                centipede.enemyType.enemyPrefab.GetComponent<CentipedeAI>().creatureSFX.loop = true;
                Plugin.Logger.LogInfo("Loop snare flea walking and clinging");
            }
            SpawnableEnemyWithRarity giant = __instance.testAllEnemiesLevel.OutsideEnemies.FirstOrDefault(spawnableEnemyWithRarity => spawnableEnemyWithRarity.enemyType.name == "ForestGiant");
            if (giant != null)
            {
                giant.enemyType.enemyPrefab.GetComponent<ForestGiantAI>().creatureSFX.spatialBlend = 1f;
                Plugin.Logger.LogInfo("Fix forest giant global audio volume");
                giant.enemyType.hitBodySFX = StartOfRound.Instance.footstepSurfaces.FirstOrDefault(footstepSurface => footstepSurface.surfaceTag == "Wood").hitSurfaceSFX;
                Plugin.Logger.LogInfo("Overwritten missing forest giant hit sound");
            }
        }

        [HarmonyPatch(typeof(NutcrackerEnemyAI), nameof(NutcrackerEnemyAI.KillEnemy))]
        [HarmonyPostfix]
        static void NutcrackerEnemyAIPostKillEnemy(NutcrackerEnemyAI __instance)
        {
            __instance.creatureVoice.loop = false;
            __instance.creatureVoice.clip = null;
            __instance.creatureVoice.pitch = 1f;
            // temporary workaround for creatureVoice.Stop()
            __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
            Plugin.Logger.LogInfo("Nutcracker: Played backup death sound");
        }

        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.KillEnemy))]
        [HarmonyPostfix]
        static void HoarderBugAIPostKillEnemy(HoarderBugAI __instance)
        {
            // temporary workaround for creatureVoice.Stop()
            __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
            Plugin.Logger.LogInfo("Hoarding Bug: Played backup death sound");
        }

        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.HitEnemy))]
        [HarmonyPostfix]
        static void BaboonBirdAIPostHitEnemy(BaboonBirdAI __instance)
        {
            if (!__instance.isEnemyDead && baboonTakeDamage != null)
            {
                __instance.creatureVoice.PlayOneShot(baboonTakeDamage);
                Plugin.Logger.LogInfo("Baboon hawk: Ouch");
            }
        }

        [HarmonyPatch(typeof(CentipedeAI), "delayedShriek", MethodType.Enumerator)]
        [HarmonyTranspiler()]
        static IEnumerable<CodeInstruction> CentipedeAITransDelayedShriek(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            Label label = generator.DefineLabel();

            for (int i = 0; i < codes.Count - 1; i++)
            {
                //Plugin.Logger.LogInfo(codes[i]);
                if (codes[i].opcode == OpCodes.Ldloc_1 && codes[i + 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 1].operand == CREATURE_VOICE)
                {
                    codes[i].labels.Add(label);
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldloc_1, null));
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldfld, typeof(EnemyAI).GetField(nameof(EnemyAI.isEnemyDead), BindingFlags.Instance | BindingFlags.Public)));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Brfalse, label));
                    codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldc_I4_0, null));
                    codes.Insert(i + 4, new CodeInstruction(OpCodes.Ret, null));
                    Plugin.Logger.LogInfo("Transpiler: Patched Centipede shriek (added isEnemyDead check)");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.Update))]
        [HarmonyPrefix]
        static void CentipedeAIPreUpdate(CentipedeAI __instance)
        {
            if (__instance.creatureSFX.isPlaying && __instance.creatureSFX.clip.name == "CentipedeWalk" && (__instance.isEnemyDead || __instance.currentBehaviourState.name != "attacking"))
            {
                __instance.creatureSFX.Stop();
                Plugin.Logger.LogInfo("Snare flea: Stop walking while dead, clinging to player, or sneaking away");
            }
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.KillEnemy))]
        [HarmonyPostfix]
        static void CentipedeAIPostKillEnemy(CentipedeAI __instance)
        {
            __instance.creatureVoice.pitch = Random.value > 0.5f ? 1f : 1.7f;
            Plugin.Logger.LogInfo("Snare flea: Randomize death screech pitch");
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayAudioOfCurrentState))]
        [HarmonyPostfix]
        static void EnemyAIPostPlayAudioOfCurrentState(EnemyAI __instance)
        {
            if (__instance is CentipedeAI && __instance.currentBehaviourState.name == "hiding" && __instance.creatureVoice.pitch > 1f)
            {
                __instance.creatureVoice.pitch = 1f;
                Plugin.Logger.LogInfo("Snare flea: Reset \"voice\" pitch for attacking again");
            }
        }

        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.Update))]
        [HarmonyPostfix]
        static void ForestGiantAIPostUpdate(ForestGiantAI __instance)
        {
            if (__instance.stunNormalizedTimer > 0f || __instance.isEnemyDead || __instance.currentBehaviourState.name == "Burning")
            {
                PlayAudioAnimationEvent playAudioAnimationEvent = __instance.animationContainer.GetComponent<PlayAudioAnimationEvent>();
                if (playAudioAnimationEvent != null)
                {
                    AudioSource closeWideSFX = playAudioAnimationEvent.audioToPlay;
                    if (closeWideSFX.isPlaying)
                    {
                        closeWideSFX.Stop();
                        Plugin.Logger.LogInfo("Forest keeper: Stop chewing non-existent player");
                    }
                    ParticleSystem bloodParticle = playAudioAnimationEvent.particle;
                    if (bloodParticle.isEmitting)
                    {
                        bloodParticle.Stop();
                        Plugin.Logger.LogInfo("Forest keeper: Don't spray blood from mouth");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ForestGiantAI), "StopKillAnimation")]
        [HarmonyPatch(typeof(ForestGiantAI), "EatPlayerAnimation", MethodType.Enumerator)]
        [HarmonyTranspiler()]
        static IEnumerable<CodeInstruction> ForestGiantAITransAnimation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count - 1; i++)
            {
                //Plugin.Logger.LogInfo(codes[i]);
                if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == CREATURE_VOICE)
                {
                    for (int j = i - 1; j <= i + 1; j++)
                    {
                        codes[j].opcode = OpCodes.Nop;
                        codes[j].operand = null;
                    }
                    Plugin.Logger.LogInfo("Transpiler: Don't interrupt forest keeper voice");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.AnimationEventA))]
        [HarmonyPostfix]
        static void ForestGiantAIPostAnimationEventA(ForestGiantAI __instance)
        {
            __instance.creatureSFX.PlayOneShot(__instance.giantFall);
            Plugin.Logger.LogInfo("Forest keeper: Fallen down");
        }

        [HarmonyPatch(typeof(PlayAudioAnimationEvent), nameof(PlayAudioAnimationEvent.PlayAudio2))]
        [HarmonyPrefix]
        static bool PlayAudioAnimationEventPrePlayAudio2(PlayAudioAnimationEvent __instance)
        {
            if (__instance.audioClip2.name == "FGiantEatPlayerSFX")
            {
                __instance.audioToPlay.PlayOneShot(__instance.audioClip2);
                Plugin.Logger.LogInfo("Forest keeper: Play bite sound effect with overlap");
                return false;
            }
            return true;
        }
    }
}