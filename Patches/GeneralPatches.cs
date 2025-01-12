using DunGen;
using DunGen.Graph;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UIElements;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class GeneralPatches
    {
        internal static bool playHitSound;

        static bool patchedDoorSfx;

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

            EnemyType mouthDog = null;
            foreach (SpawnableEnemyWithRarity enemy in allEnemies)
            {
                switch (enemy.enemyType.name)
                {
                    case "BaboonHawk":
                        if (References.baboonTakeDamage == null)
                        {
                            References.baboonTakeDamage = enemy.enemyType.hitBodySFX;
                            Plugin.Logger.LogDebug("Cached baboon hawk damage sound");
                        }
                        enemy.enemyType.hitBodySFX = null;
                        Plugin.Logger.LogDebug("Overwritten baboon hawk damage sound");
                        enemy.enemyType.enemyPrefab.GetComponent<BaboonBirdAI>().dieSFX = enemy.enemyType.deathSFX;
                        Plugin.Logger.LogDebug("Overwritten missing baboon hawk death sound");
                        break;
                    case "CaveDweller":
                        CaveDwellerAI caveDwellerAI = enemy.enemyType.enemyPrefab.GetComponent<CaveDwellerAI>();
                        caveDwellerAI.clickingAudio1.volume = 0f;
                        caveDwellerAI.clickingAudio2.volume = 0f;
                        Plugin.Logger.LogDebug("Fix maneater clicking volume");
                        break;
                    case "Centipede":
                        enemy.enemyType.enemyPrefab.GetComponent<CentipedeAI>().creatureSFX.loop = true;
                        Plugin.Logger.LogDebug("Loop snare flea walking and clinging");
                        break;
                    case "Crawler":
                        if (Plugin.configThumperNoThunder.Value)
                        {
                            EnemyBehaviourState searching = enemy.enemyType.enemyPrefab.GetComponent<CrawlerAI>().enemyBehaviourStates.FirstOrDefault(enemyBehaviourState => enemyBehaviourState.name == "searching");
                            if (searching != null)
                            {
                                searching.VoiceClip = null;
                                searching.playOneShotVoice = false;
                                Plugin.Logger.LogDebug("Remove thunder sound from thumper");
                            }
                        }
                        break;
                    case "ForestGiant":
						ForestGiantAI forestGiantAI = enemy.enemyType.enemyPrefab.GetComponent<ForestGiantAI>();
                        forestGiantAI.creatureSFX.spatialBlend = 1f;
                        Plugin.Logger.LogDebug("Fix forest giant global audio volume");
                        enemy.enemyType.hitBodySFX = StartOfRound.Instance.footstepSurfaces.FirstOrDefault(footstepSurface => footstepSurface.surfaceTag == "Wood").hitSurfaceSFX;
                        Plugin.Logger.LogDebug("Overwritten missing forest giant hit sound");
                        forestGiantAI.giantBurningAudio.volume = 0f;
                        Plugin.Logger.LogDebug("Fix forest giant burning volume fade");
                        break;
                    case "MouthDog":
                        mouthDog = enemy.enemyType;
                        break;
                }

                if (References.hitEnemyBody == null && enemy.enemyType.hitBodySFX.name == "HitEnemyBody")
                {
                    References.hitEnemyBody = enemy.enemyType.hitBodySFX;
                    Plugin.Logger.LogDebug("Cached generic damage sound");
                }
            }

            if (References.hitEnemyBody != null)
            {
                if (mouthDog != null)
                {
                    mouthDog.hitBodySFX = References.hitEnemyBody;
                    Plugin.Logger.LogDebug("Overwritten missing eyeless dog hit sound");
                }
            }
        }

        [HarmonyPatch(typeof(AnimatedObjectTrigger), nameof(AnimatedObjectTrigger.Start))]
        [HarmonyPostfix]
        static void AnimatedObjectTriggerPostStart(AnimatedObjectTrigger __instance)
        {
            if (__instance.playAudiosInSequence && __instance.playParticle == null)
            {
                __instance.playParticleOnTimesTriggered = -1;
                Plugin.Logger.LogDebug($"\"{__instance.name}.AnimatedObjectTrigger\" doesn't have particles attached");
            }
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.OnCollideWithEnemy))]
        [HarmonyPatch(typeof(BushWolfEnemy), nameof(BushWolfEnemy.OnCollideWithEnemy))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EnemyTransOnCollideWithEnemy(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 2; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == References.HIT_ENEMY)
                {
                    codes.RemoveAt(i - 2);
                    codes.InsertRange(i - 2,
                    [
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Ldfld, References.IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ceq)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler: Don't play hit sound when attacking dead enemy");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(StormyWeather), "PlayThunderEffects")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TransPlayThunderEffects(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            FieldInfo shipCreakSFX = AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.shipCreakSFX));
            for (int i = 5; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == References.PLAY_RANDOM_CLIP && codes[i - 1].opcode == OpCodes.Ldc_I4 && (int)codes[i - 1].operand == 1000 && codes[i - 5].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 5].operand == shipCreakSFX)
                {
                    codes[i - 1].opcode = OpCodes.Ldc_I4_6;
                    Plugin.Logger.LogDebug("Transpiler (Stormy weather): No \"Hey\" when ship is struck");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.PlayAudioAtTeleportPositions))]
        [HarmonyPrefix]
        static bool EntranceTeleportPlayAudioAtTeleportPositions(EntranceTeleport __instance, AudioSource ___exitPointAudio)
        {
            if (__instance.doorAudios == null || __instance.doorAudios.Length < 1)
                return false;

            AudioClip doorAudio = __instance.doorAudios[Random.Range(0, __instance.doorAudios.Length)];

            if (__instance.entrancePointAudio != null)
            {
                __instance.entrancePointAudio.PlayOneShot(doorAudio);
                WalkieTalkie.TransmitOneShotAudio(__instance.entrancePointAudio, doorAudio);
            }
            if (___exitPointAudio != null)
            {
                ___exitPointAudio.PlayOneShot(doorAudio);
                WalkieTalkie.TransmitOneShotAudio(___exitPointAudio, doorAudio);
            }

            return false;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        static void PostFinishGeneratingNewLevelClientRpc()
        {
            if (!patchedDoorSfx)
            {
                patchedDoorSfx = true;

                if (Plugin.configFixDoorSounds.Value)
                {
                    string cabinDoor = null;
                    if (StartOfRound.Instance.currentLevel.sceneName == "Level5Rend")
                        cabinDoor = "/Environment/Map/SnowCabin/FancyDoorMapModel/SteelDoor (1)/DoorMesh/Cube";
                    else if (StartOfRound.Instance.currentLevel.sceneName == "Level10Adamance")
                        cabinDoor = "/Environment/SnowCabin/FancyDoorMapModel/SteelDoor (1)/DoorMesh/Cube";

                    if (!string.IsNullOrEmpty(cabinDoor) && References.woodenDoorOpen != null && References.woodenDoorOpen.Length > 0 && References.woodenDoorClose != null && References.woodenDoorClose.Length > 0)
                    {
                        AnimatedObjectTrigger door = GameObject.Find(cabinDoor)?.GetComponent<AnimatedObjectTrigger>();
                        if (door != null)
                        {
                            door.boolFalseAudios = References.woodenDoorClose;
                            door.boolTrueAudios = References.woodenDoorOpen;
                            Plugin.Logger.LogDebug("Overwritten cabin door sounds");
                        }
                    }

                    foreach (AnimatedObjectTrigger animatedObjectTrigger in Object.FindObjectsOfType<AnimatedObjectTrigger>())
                    {
                        if (animatedObjectTrigger.thisAudioSource != null)
                        {
                            Renderer rend = animatedObjectTrigger.transform.parent?.GetComponent<Renderer>();
                            if (animatedObjectTrigger.name == "PowerBoxDoor" || animatedObjectTrigger.thisAudioSource.name == "storage door" || (rend != null && rend.sharedMaterials.Length == 7))
                            {
                                AudioClip[] temp = (AudioClip[])animatedObjectTrigger.boolFalseAudios.Clone();
                                animatedObjectTrigger.boolFalseAudios = (AudioClip[])animatedObjectTrigger.boolTrueAudios.Clone();
                                animatedObjectTrigger.boolTrueAudios = temp;
                                Plugin.Logger.LogDebug($"{animatedObjectTrigger.name}: AnimatedObjectTrigger audios");
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndOfGameClientRpc))]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect))]
        [HarmonyPostfix]
        static void ResetLoadState()
        {
            patchedDoorSfx = false;
        }

        [HarmonyPatch(typeof(RoundManager), "Awake")]
        [HarmonyPostfix]
        static void RoundManagerPostAwake(RoundManager __instance)
        {
            if (References.woodenDoorOpen == null || References.woodenDoorOpen.Length < 1 || References.woodenDoorClose == null || References.woodenDoorClose.Length < 1)
            {
                IndoorMapType manor = __instance.dungeonFlowTypes.FirstOrDefault(dungeonFlowType => dungeonFlowType.dungeonFlow?.name == "Level2Flow");
                if (manor != null)
                {
                    foreach (GraphNode node in manor.dungeonFlow.Nodes)
                    {
                        foreach (TileSet tileSet in node.TileSets)
                        {
                            if (tileSet.name == "Level2HallwayTiles")
                            {
                                GameObject manorStartRoom = tileSet.TileWeights.Weights.FirstOrDefault(weight => weight.Value?.name == "ManorStartRoom")?.Value;
                                if (manorStartRoom != null)
                                {
                                    // fun!
                                    AnimatedObjectTrigger manorDoor = manorStartRoom.transform.Find("Doorways")?.GetComponentInChildren<Doorway>()?.ConnectorPrefabWeights?.FirstOrDefault(prefab => prefab.GameObject.name == "FancyDoorMapSpawn")?.GameObject.GetComponent<SpawnSyncedObject>()?.spawnPrefab?.GetComponentInChildren<AnimatedObjectTrigger>();

                                    if (manorDoor != null)
                                    {
                                        References.woodenDoorClose = manorDoor.boolFalseAudios;
                                        References.woodenDoorOpen = manorDoor.boolTrueAudios;
                                        Plugin.Logger.LogDebug("Cached wooden door sounds");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
