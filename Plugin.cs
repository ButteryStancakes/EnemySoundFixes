using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using GameNetcodeStuff;
using BepInEx.Configuration;

namespace EnemySoundFixes
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.enemysoundfixes", PLUGIN_NAME = "Enemy Sound Fixes", PLUGIN_VERSION = "1.5.0";
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<bool> configFixMasks, configThumperNoThunder, configBetterMimicSteps;

        void Awake()
        {
            Logger = base.Logger;

            configFixMasks = Config.Bind(
                "Misc",
                "FixMasks",
                true,
                "(Host only, requires game restart) Fixes masks' broken audio intervals.\nDisabling this is useful if you use a voice mimicking mod (Skinwalkers, Mirage, etc.) or just find the masks to be too noisy.");

            configBetterMimicSteps = Config.Bind(
                "Misc",
                "BetterMimicSteps",
                false,
                "Mimic footstep volume and distance are altered to sound more accurate to actual players.");

            configThumperNoThunder = Config.Bind(
                "Misc",
                "ThumperNoThunder",
                true,
                "Thumpers no longer play thunder sound effects from their voice when they stop chasing after players.");

            // migrate from previous version if necessary
            if (configFixMasks.Value)
            {
                bool dontFixMasks = Config.Bind("Misc", "DontFixMasks", false, "Legacy setting, use \"FixMasks\" instead").Value;
                if (dontFixMasks)
                    configFixMasks.Value = false;
                Config.Remove(Config["Misc", "DontFixMasks"].Definition);
                Config.Save();
            }

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }

    [HarmonyPatch]
    class EnemySoundFixesPatches
    {
        static readonly FieldInfo CREATURE_VOICE = AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.creatureVoice));
        static readonly FieldInfo IS_ENEMY_DEAD = AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.isEnemyDead));
        static readonly MethodInfo REALTIME_SINCE_STARTUP = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.realtimeSinceStartup));
        static readonly MethodInfo PLAY_ONE_SHOT = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.PlayOneShot), [typeof(AudioClip)]);
        static readonly MethodInfo HIT_ENEMY = AccessTools.Method(typeof(EnemyAI), nameof(EnemyAI.HitEnemy));
        static readonly MethodInfo DAMAGE_PLAYER = AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer));

        const float TIME_PLAY_AUDIO_2 = 178f / 60f; // frame 178 at 60 fps - PlayAudio2 event
        const float TIME_DROP_CARRIED_BODY = 5.01f; // 2s + 3.01s - MouthDogAI.KillPlayer enumerator

        static AudioClip baboonTakeDamage/*, hitEnemyBody*/;

        static Dictionary<MouthDogAI, (float Pitch, float Time)> dogPitches = [];

        [HarmonyPatch(typeof(QuickMenuManager), "Start")]
        [HarmonyPostfix]
        static void QuickMenuManagerPostStart(QuickMenuManager __instance)
        {
            List<SpawnableEnemyWithRarity> allEnemies =
            [
                .. __instance.testAllEnemiesLevel.Enemies,
                .. __instance.testAllEnemiesLevel.OutsideEnemies,
                .. __instance.testAllEnemiesLevel.DaytimeEnemies,
            ];

            EnemyType mouthDog = null/*, flowerSnake = null*/;
            AudioClip hitEnemyBody = null;
            foreach (SpawnableEnemyWithRarity enemy in allEnemies)
            {
                switch (enemy.enemyType.name)
                {
                    case "BaboonHawk":
                        if (baboonTakeDamage == null)
                        {
                            baboonTakeDamage = enemy.enemyType.hitBodySFX;
                            Plugin.Logger.LogInfo("Cached baboon hawk damage sound");
                        }
                        enemy.enemyType.hitBodySFX = null;
                        Plugin.Logger.LogInfo("Overwritten baboon hawk damage sound");
                        enemy.enemyType.enemyPrefab.GetComponent<BaboonBirdAI>().dieSFX = enemy.enemyType.deathSFX;
                        Plugin.Logger.LogInfo("Overwritten missing baboon hawk death sound");
                        break;
                    case "Centipede":
                        enemy.enemyType.enemyPrefab.GetComponent<CentipedeAI>().creatureSFX.loop = true;
                        Plugin.Logger.LogInfo("Loop snare flea walking and clinging");
                        break;
                    case "Crawler":
                        if (Plugin.configThumperNoThunder.Value)
                        {
                            EnemyBehaviourState searching = enemy.enemyType.enemyPrefab.GetComponent<CrawlerAI>().enemyBehaviourStates.FirstOrDefault(enemyBehaviourState => enemyBehaviourState.name == "searching");
                            if (searching != null)
                            {
                                searching.VoiceClip = null;
                                searching.playOneShotVoice = false;
                                Plugin.Logger.LogInfo("Remove thunder sound from thumper");
                            }
                        }
                        break;
                    case "ForestGiant":
                        enemy.enemyType.enemyPrefab.GetComponent<ForestGiantAI>().creatureSFX.spatialBlend = 1f;
                        Plugin.Logger.LogInfo("Fix forest giant global audio volume");
                        enemy.enemyType.hitBodySFX = StartOfRound.Instance.footstepSurfaces.FirstOrDefault(footstepSurface => footstepSurface.surfaceTag == "Wood").hitSurfaceSFX;
                        Plugin.Logger.LogInfo("Overwritten missing forest giant hit sound");
                        break;
                    case "MouthDog":
                        mouthDog = enemy.enemyType;
                        break;
                    /*case "FlowerSnake":
                        flowerSnake = enemy.enemyType;
                        break;*/
                }

                if (hitEnemyBody == null && enemy.enemyType.hitBodySFX.name == "HitEnemyBody")
                {
                    hitEnemyBody = enemy.enemyType.hitBodySFX;
                    Plugin.Logger.LogInfo("Cached generic damage sound");
                }
            }

            if (hitEnemyBody != null)
            {
                if (mouthDog != null)
                {
                    mouthDog.hitBodySFX = hitEnemyBody;
                    Plugin.Logger.LogInfo("Overwritten missing eyeless dog hit sound");
                }
                /*if (flowerSnake != null)
                {
                    flowerSnake.hitBodySFX = hitEnemyBody;
                    Plugin.Logger.LogInfo("Overwritten missing tulip snake hit sound");
                }*/
            }
        }

        [HarmonyPatch(typeof(NutcrackerEnemyAI), nameof(NutcrackerEnemyAI.KillEnemy))]
        [HarmonyPostfix]
        static void NutcrackerEnemyAIPostKillEnemy(NutcrackerEnemyAI __instance)
        {
            __instance.creatureVoice.loop = false;
            __instance.creatureVoice.clip = null;
            __instance.creatureVoice.pitch = 1f;
            // workaround for creatureVoice.Stop()
            __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
            Plugin.Logger.LogInfo("Nutcracker: Played backup death sound");
        }

        [HarmonyPatch(typeof(HoarderBugAI), nameof(HoarderBugAI.KillEnemy))]
        [HarmonyPostfix]
        static void HoarderBugAIPostKillEnemy(HoarderBugAI __instance)
        {
            // workaround for creatureVoice.Stop()
            __instance.creatureVoice.PlayOneShot(__instance.enemyType.deathSFX);
            Plugin.Logger.LogInfo("Hoarding bug: Played backup death sound");
        }

        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.HitEnemy))]
        [HarmonyPostfix]
        static void BaboonBirdAIPostHitEnemy(BaboonBirdAI __instance, bool playHitSFX)
        {
            if (playHitSFX)
            {
                if (!__instance.isEnemyDead)
                {
                    if (baboonTakeDamage != null)
                    {
                        __instance.creatureVoice.PlayOneShot(baboonTakeDamage);
                        Plugin.Logger.LogInfo("Baboon hawk: Ouch");
                    }
                }
                /*else if (hitEnemyBody != null)
                {
                    __instance.creatureSFX.PlayOneShot(hitEnemyBody);
                    Plugin.Logger.LogInfo("Baboon hawk: Hit body");
                }*/
            }
        }

        [HarmonyPatch(typeof(CentipedeAI), "delayedShriek", MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CentipedeAITransDelayedShriek(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            Label label = generator.DefineLabel();

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_1 && codes[i + 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 1].operand == CREATURE_VOICE)
                {
                    codes[i].labels.Add(label);
                    codes.InsertRange(i,
                    [
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Ldfld, IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Brfalse, label),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ret)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler: Patched Centipede shriek (added isEnemyDead check)");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.Update))]
        [HarmonyPrefix]
        static void CentipedeAIPreUpdate(CentipedeAI __instance)
        {
            if (__instance.creatureSFX.isPlaying && __instance.creatureSFX.clip.name == "CentipedeWalk" && (__instance.isEnemyDead || __instance.currentBehaviourStateIndex != 2))
            {
                __instance.creatureSFX.Stop();
                Plugin.Logger.LogInfo("Snare flea: Stop walking while dead, clinging to player, or sneaking away");
            }
        }
        
        // on second thought, even though it's dubious whether Zeekerss *intended* this clip to play at 1.7x pitch, it *does* nevertheless always play at 1.7x pitch in vanilla
        /*[HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.KillEnemy))]
        [HarmonyPostfix]
        static void CentipedeAIPostKillEnemy(CentipedeAI __instance)
        {
            __instance.creatureVoice.pitch = Random.value > 0.5f ? 1f : 1.7f;
            Plugin.Logger.LogInfo("Snare flea: Randomize death screech pitch");
        }*/

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayAudioOfCurrentState))]
        [HarmonyPostfix]
        static void EnemyAIPostPlayAudioOfCurrentState(EnemyAI __instance)
        {
            if (__instance is CentipedeAI && __instance.currentBehaviourStateIndex == 1 && __instance.creatureVoice.pitch > 1f)
            {
                __instance.creatureVoice.pitch = 1f;
                Plugin.Logger.LogInfo("Snare flea: Reset \"voice\" pitch for attacking again");
            }
        }

        [HarmonyPatch(typeof(ForestGiantAI), nameof(ForestGiantAI.Update))]
        [HarmonyPostfix]
        static void ForestGiantAIPostUpdate(ForestGiantAI __instance)
        {
            if (__instance.stunNormalizedTimer > 0f || __instance.isEnemyDead || __instance.currentBehaviourStateIndex == 2)
            {
                PlayAudioAnimationEvent playAudioAnimationEvent = __instance.animationContainer.GetComponent<PlayAudioAnimationEvent>();
                AudioSource closeWideSFX = playAudioAnimationEvent.audioToPlay;
                if (closeWideSFX.isPlaying)
                {
                    closeWideSFX.Stop();
                    Plugin.Logger.LogInfo("Forest keeper: Stop chewing (eating animation interrupted)");
                }
                ParticleSystem bloodParticle = playAudioAnimationEvent.particle;
                if (bloodParticle.isEmitting)
                {
                    bloodParticle.Stop();
                    Plugin.Logger.LogInfo("Forest keeper: Stop spraying blood from mouth (eating animation interrupted)");
                }
            }
        }

        [HarmonyPatch(typeof(ForestGiantAI), "StopKillAnimation")]
        [HarmonyPatch(typeof(ForestGiantAI), "EatPlayerAnimation", MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ForestGiantAITransAnimation(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldfld && (FieldInfo)codes[i].operand == CREATURE_VOICE)
                {
                    for (int j = i - 1; j <= i + 1; j++)
                    {
                        codes[j].opcode = OpCodes.Nop;
                        codes[j].operand = null;
                    }
                    Plugin.Logger.LogDebug("Transpiler: Don't interrupt forest keeper voice");
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
                ForestGiantAI forestGiantAI = __instance.GetComponent<EnemyAnimationEvent>().mainScript as ForestGiantAI;
                if (forestGiantAI.inSpecialAnimationWithPlayer != null && forestGiantAI.inSpecialAnimationWithPlayer.inAnimationWithEnemy == forestGiantAI)
                {
                    __instance.audioToPlay.PlayOneShot(__instance.audioClip2);
                    Plugin.Logger.LogInfo("Forest keeper: Play bite sound effect with overlap");
                }
                else
                    Plugin.Logger.LogInfo("Forest keeper: Don't bite (player was teleported)");

                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(PlayAudioAnimationEvent), nameof(PlayAudioAnimationEvent.PlayParticle))]
        [HarmonyPrefix]
        static bool PlayAudioAnimationEventPrePlayParticle(PlayAudioAnimationEvent __instance)
        {
            if (__instance.audioClip2 != null && __instance.audioClip2.name == "FGiantEatPlayerSFX")
            {
                EnemyAI enemyAI = __instance.GetComponent<EnemyAnimationEvent>().mainScript;
                if (enemyAI.inSpecialAnimationWithPlayer == null || enemyAI.inSpecialAnimationWithPlayer.inAnimationWithEnemy != enemyAI)
                {
                    Plugin.Logger.LogInfo("Forest keeper: Don't spray blood (player was teleported)");
                    return false;
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.CancelSpecialAnimationWithPlayer))]
        [HarmonyPrefix]
        static void EnemyAIPreCancelSpecialAnimationWithPlayer(EnemyAI __instance)
        {
            if (__instance is ForestGiantAI)
            {
                if (__instance.inSpecialAnimationWithPlayer != null && __instance.inSpecialAnimationWithPlayer.inAnimationWithEnemy == __instance && !__instance.inSpecialAnimationWithPlayer.isPlayerDead)
                {
                    PlayAudioAnimationEvent playAudioAnimationEvent = (__instance as ForestGiantAI).animationContainer.GetComponent<PlayAudioAnimationEvent>();
                    AudioSource closeWideSFX = playAudioAnimationEvent.audioToPlay;
                    if (closeWideSFX.isPlaying && closeWideSFX.clip?.name == "Roar" && closeWideSFX.time >= TIME_PLAY_AUDIO_2)
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
                        Plugin.Logger.LogInfo("Forest keeper: Stop chewing (player was teleported)");
                        ParticleSystem bloodParticle = playAudioAnimationEvent.particle;
                        if (bloodParticle.isEmitting)
                        {
                            bloodParticle.Stop();
                            Plugin.Logger.LogInfo("Forest keeper: Stop spraying blood from mouth (player was teleported)");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPrefix]
        static void PlayerControllerBPreDamagePlayer(CauseOfDeath causeOfDeath, ref bool fallDamage)
        {
            if (causeOfDeath == CauseOfDeath.Gravity && !fallDamage)
            {
                fallDamage = true;
                Plugin.Logger.LogInfo("Player: Treat Gravity damage as fall damage");
            }
        }

        [HarmonyPatch(typeof(RandomPeriodicAudioPlayer), "Update")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> RandomPeriodicAudioPlayerTransUpdate(IEnumerable<CodeInstruction> instructions)
        {
            if (!Plugin.configFixMasks.Value)
                return instructions;

            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Add)
                {
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Call && (MethodInfo)codes[j].operand == REALTIME_SINCE_STARTUP)
                        {
                            codes[i].opcode = OpCodes.Nop;
                            codes[j].opcode = OpCodes.Nop;
                            Plugin.Logger.LogDebug("Transpiler: Fix periodic mask audio intervals");
                            return codes;
                        }
                    }
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(AnimatedObjectTrigger), nameof(AnimatedObjectTrigger.Start))]
        [HarmonyPostfix]
        static void AnimatedObjectTriggerPostStart(AnimatedObjectTrigger __instance)
        {
            if (__instance.playAudiosInSequence && __instance.playParticle == null)
            {
                __instance.playParticleOnTimesTriggered = -1;
                Plugin.Logger.LogWarning($"\"{__instance.name}.AnimatedObjectTrigger\" doesn't have particles attached");
            }
        }

        [HarmonyPatch(typeof(ButlerEnemyAI), nameof(ButlerEnemyAI.Update))]
        [HarmonyPostfix]
        static void ButlerEnemyAIPostUpdate(ButlerEnemyAI __instance)
        {
            if (__instance.isEnemyDead && __instance.buzzingAmbience.isPlaying && __instance.creatureAnimator.GetBool("popFinish"))
            {
                __instance.buzzingAmbience.Stop();
                Plugin.Logger.LogInfo("Butler: Stop buzzing (bugs are free)");
            }
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.KillEnemy))]
        [HarmonyPostfix]
        static void MouthDogAIPostKillEnemy(MouthDogAI __instance)
        {
            __instance.creatureVoice.mute = true;
            Plugin.Logger.LogInfo("Eyeless dog: Don't start breathing after death");
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.Start))]
        [HarmonyPostfix]
        static void MouthDogAIPostStart(MouthDogAI __instance)
        {
            System.Random random = new(/*(int)__instance.serverPosition.x + (int)__instance.serverPosition.y +*/ StartOfRound.Instance.randomMapSeed /*+ Object.FindObjectsOfType<MouthDogAI>().Length*/ + (int)__instance.NetworkObjectId);
            if (random.Next(10) < 2)
                __instance.creatureVoice.pitch = 0.6f + (0.7f * (float)random.NextDouble());
            else
                __instance.creatureVoice.pitch = 0.9f + (0.2f * (float)random.NextDouble());
            Plugin.Logger.LogInfo("Eyeless dog: Reroll voice pitch (seeded random)");
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.KillPlayerClientRpc))]
        [HarmonyPrefix]
        static void MouthDogAIPreKillPlayerClientRpc(MouthDogAI __instance)
        {
            if (!dogPitches.ContainsKey(__instance))
            {
                dogPitches.Add(__instance, (__instance.creatureVoice.pitch, Time.timeSinceLevelLoad + TIME_DROP_CARRIED_BODY));
                Plugin.Logger.LogInfo($"Eyeless dog #{__instance.GetInstanceID()}: Cached {__instance.creatureVoice.pitch}x voice pitch (kill animation will start)");
            }
            else
                Plugin.Logger.LogWarning($"Eyeless dog #{__instance.GetInstanceID()}: Tried to initiate kill animation before ending previous kill animation");
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.Update))]
        [HarmonyPostfix]
        static void MouthDogAIPostUpdate(MouthDogAI __instance, bool ___inKillAnimation)
        {
            if (!__instance.isEnemyDead)
            {
                if (dogPitches.Count > 0 && !___inKillAnimation && dogPitches.TryGetValue(__instance, out (float Pitch, float Time) dogPitch) && Time.timeSinceLevelLoad >= dogPitch.Time)
                {
                    dogPitches.Remove(__instance);
                    Plugin.Logger.LogInfo($"Eyeless dog #{__instance.GetInstanceID()}: Reset voice pitch now that kill sound is done ({__instance.creatureVoice.pitch}x -> {dogPitch.Pitch}x)");
                    __instance.creatureVoice.pitch = dogPitch.Pitch;
                }
                if (!__instance.creatureVoice.isPlaying /*&& __instance.currentBehaviourStateIndex < 2*/)
                    __instance.creatureVoice.Play();
            }
        }

        [HarmonyPatch(typeof(EnemyAI), "SubtractFromPowerLevel")]
        [HarmonyPostfix]
        static void EnemyAIPostSubtractFromPowerLevel(EnemyAI __instance)
        {
            MouthDogAI mouthDogAI = __instance as MouthDogAI;
            if (mouthDogAI != null && dogPitches.Remove(mouthDogAI))
                Plugin.Logger.LogInfo($"Eyeless dog #{__instance.GetInstanceID()}: Died mid kill animation (clean up cached reference)");
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.ResetEnemyVariables))]
        [HarmonyPostfix]
        static void RoundManagerPostResetEnemyVariables()
        {
            dogPitches.Clear();
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.Update))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemyPostUpdate(FlowerSnakeEnemy __instance)
        {
            if (__instance.flappingAudio.isPlaying)
            {
                if (__instance.isEnemyDead)
                {
                    __instance.flappingAudio.Stop();
                    __instance.flappingAudio.mute = true;
                    Plugin.Logger.LogInfo("Tulip snake: Stop making noise while dead");
                }
                else if (__instance.flappingAudio.clip == __instance.enemyType.audioClips[9])
                {
                    if (__instance.clingingToPlayer != null)
                    {
                        __instance.flappingAudio.Stop();
                        Plugin.Logger.LogInfo("Tulip snake: Stop scurrying (latched to player)");
                    }
                }
                else if (__instance.clingingToPlayer == null)
                {
                    __instance.flappingAudio.Stop();
                    Plugin.Logger.LogInfo("Tulip snake: Stop flapping (no longer clinging)");
                }
            }
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.StopLeapOnLocalClient))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemyPostStopLeapOnLocalClient(FlowerSnakeEnemy __instance, bool landOnGround)
        {
            if (landOnGround && !__instance.isEnemyDead)
            {
                __instance.flappingAudio.pitch = Random.Range(0.8f, 1.2f);
                Plugin.Logger.LogInfo("Tulip snake: Reroll scurry pitch (landed from leap)");
            }
        }

        [HarmonyPatch(typeof(FlowerSnakeEnemy), nameof(FlowerSnakeEnemy.StopClingingOnLocalClient))]
        [HarmonyPostfix]
        static void FlowerSnakeEnemyPostStopClingingOnLocalClient(FlowerSnakeEnemy __instance)
        {
            if (!__instance.isEnemyDead)
            {
                __instance.flappingAudio.pitch = Random.Range(0.8f, 1.2f);
                Plugin.Logger.LogInfo("Tulip snake: Reroll scurry pitch (dismounted player)");
            }
        }

        [HarmonyPatch(typeof(MouthDogAI), "enterChaseMode", MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MouthDogAITransEnterChaseMode(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == PLAY_ONE_SHOT && codes[i - 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 1].operand == AccessTools.Field(typeof(MouthDogAI), nameof(MouthDogAI.breathingSFX)))
                {
                    for (int j = i - 4; j <= i; j++)
                        codes[j].opcode = OpCodes.Nop;
                    Plugin.Logger.LogDebug("Transpiler: Fixed dog's overlapping breathing");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.OnCollideWithEnemy))]
        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.OnCollideWithEnemy))]
        [HarmonyPatch(typeof(BushWolfEnemy), nameof(BushWolfEnemy.OnCollideWithEnemy))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EnemyTransOnCollideWithEnemy(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == HIT_ENEMY)
                {
                    codes.RemoveAt(i - 2);
                    codes.InsertRange(i - 2,
                    [
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Ldfld, IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ceq)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler: Don't play hit sound when attacking dead enemy");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayerFromOtherClientClientRpc))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> PlayerControllerBTransDamagePlayerFromOtherClientClientRpc(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 3; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == DAMAGE_PLAYER)
                {
                    for (int j = i - 1; j > 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Ldarg_1 && codes[j + 1].opcode == OpCodes.Ldc_I4_1)
                        {
                            codes[j + 1].opcode = OpCodes.Ldc_I4_0; // hasDamageSFX: false
                            Plugin.Logger.LogDebug("Transpiler: Melee weapons don't stack hit sounds on players");
                            break;
                        }
                    }
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(BushWolfEnemy), "HitTongueLocalClient")]
        [HarmonyPostfix]
        static void BushWolfEnemyPostHitTongueLocalClient(BushWolfEnemy __instance)
        {
            // need to call this again because it gets stopped by CancelReelingPlayerIn
            __instance.creatureVoice.PlayOneShot(__instance.hitBushWolfSFX);
            Plugin.Logger.LogInfo("Kidnapper fox: Bit my tongue");
        }

        [HarmonyPatch(typeof(BushWolfEnemy), nameof(BushWolfEnemy.Update))]
        [HarmonyPostfix]
        static void BushWolfEnemyPostUpdate(BushWolfEnemy __instance, bool ___dragging)
        {
            if ((!___dragging || __instance.isEnemyDead || __instance.stunNormalizedTimer > 0f) && __instance.creatureVoice.isPlaying && __instance.creatureVoice.clip == __instance.snarlSFX)
            {
                __instance.creatureVoice.clip = null;
                Plugin.Logger.LogInfo("Kidnapper fox: Cancel snarl (failsafe)");
            }
        }

        [HarmonyPatch(typeof(BushWolfEnemy), "CancelReelingPlayerIn")]
        [HarmonyPrefix]
        static void BushWolfEnemyPreCancelReelingPlayerIn(BushWolfEnemy __instance, ref bool ___dragging)
        {
            if (___dragging && __instance.isEnemyDead)
            {
                ___dragging = false;
                Plugin.Logger.LogInfo("Kidnapper fox: Don't let dragging interrupt death voice");
            }
        }

        [HarmonyPatch(typeof(BushWolfEnemy), nameof(BushWolfEnemy.HitEnemy))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BushWolfEnemyTransHitEnemy(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo cancelReelingPlayerIn = AccessTools.Method(typeof(BushWolfEnemy), "CancelReelingPlayerIn");
            for (int i = codes.Count - 1; i >= 0; i--)
            {
                Label label = generator.DefineLabel();
                codes[i].labels.Add(label);
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == cancelReelingPlayerIn)
                {
                    codes.InsertRange(i + 1, [
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Brtrue, label)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler: Kidnapper fox doesn't cry when dead");
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        [HarmonyPostfix]
        static void MaskedPlayerEnemyPostStart(MaskedPlayerEnemy __instance)
        {
            if (!Plugin.configBetterMimicSteps.Value)
                return;

            AudioSource playerMovementAudio = GameNetworkManager.Instance?.localPlayerController?.movementAudio;
            if (playerMovementAudio == null)
                return;

            __instance.movementAudio.transform.localPosition = new Vector3(0f, 0.278f, 0f);
            __instance.movementAudio.volume = playerMovementAudio.volume;
            __instance.movementAudio.dopplerLevel = playerMovementAudio.dopplerLevel;
            __instance.movementAudio.spread = playerMovementAudio.spread;
            __instance.movementAudio.rolloffMode = AudioRolloffMode.Custom;
            foreach (AudioSourceCurveType audioSourceCurveType in System.Enum.GetValues(typeof(AudioSourceCurveType)))
                __instance.movementAudio.SetCustomCurve(audioSourceCurveType, playerMovementAudio.GetCustomCurve(audioSourceCurveType));
            Plugin.Logger.LogInfo("Mimic: Footsteps match players");
        }
    }
}