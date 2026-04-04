using DunGen;
using DunGen.Graph;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Audio;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    static class GeneralPatches
    {
        internal static bool playHitSound;

        static bool patchedDoorSfx;

        [HarmonyPatch(typeof(QuickMenuManager), nameof(QuickMenuManager.Start))]
        [HarmonyPostfix]
        static void QuickMenuManager_Post_Start(QuickMenuManager __instance)
        {
            AudioClip stunFlowerman = null;
            try
            {
                AssetBundle sfxBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "enemysoundfixes"));
                stunFlowerman = sfxBundle.LoadAsset<AudioClip>("StunFlowerman");
                sfxBundle.Unload(false);
            }
            catch
            {
                Plugin.Logger.LogError("Encountered some error loading assets from bundle \"enemysoundfixes\". Did you install the plugin correctly?");
            }

            List<SpawnableEnemyWithRarity> allEnemies =
            [
                .. __instance.testAllEnemiesLevel.Enemies,
                .. __instance.testAllEnemiesLevel.OutsideEnemies,
                .. __instance.testAllEnemiesLevel.DaytimeEnemies,
            ];

            EnemyType mouthDog = null, giantKiwi = null;
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
                    case "Flowerman":
                        if (stunFlowerman != null)
                        {
                            enemy.enemyType.stunSFX = stunFlowerman;
                            Plugin.Logger.LogDebug("Fix bracken stun sound");
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
                    case "GiantKiwi":
                        giantKiwi = enemy.enemyType;
                        AudioSource giantKiwiFeatherPoofContainer = enemy.enemyType.enemyPrefab.GetComponent<GiantKiwiAI>()?.feathersPrefab?.GetComponent<AudioSource>();
                        if (giantKiwiFeatherPoofContainer != null)
                        {
                            giantKiwiFeatherPoofContainer.spatialBlend = 1f;
                            Plugin.Logger.LogDebug("Fix sapsucker death poof");
                        }
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
                if (giantKiwi != null)
                {
                    giantKiwi.hitBodySFX = References.hitEnemyBody;
                    Plugin.Logger.LogDebug("Overwritten missing giant sapsucker hit sound");
                }
            }
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.OnCollideWithEnemy))]
        [HarmonyPatch(typeof(BushWolfEnemy), nameof(BushWolfEnemy.OnCollideWithEnemy))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EnemyAI_Trans_OnCollideWithEnemy(IEnumerable<CodeInstruction> instructions)
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

        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.PlayThunderEffects))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> StormyWeather_Trans_PlayThunderEffects(IEnumerable<CodeInstruction> instructions)
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

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        static void RoundManager_Post_FinishGeneratingNewLevelClientRpc()
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

                    foreach (AnimatedObjectTrigger animatedObjectTrigger in Object.FindObjectsByType<AnimatedObjectTrigger>(FindObjectsSortMode.None))
                    {
                        if (animatedObjectTrigger.thisAudioSource != null)
                        {
                            Renderer rend = animatedObjectTrigger.transform.parent?.GetComponent<Renderer>();
                            if (animatedObjectTrigger.name == "PowerBoxDoor" || animatedObjectTrigger.thisAudioSource.name == "storage door")
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

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.Awake))]
        [HarmonyPostfix]
        static void RoundManager_Post_Awake(RoundManager __instance)
        {
            PatchManorDoors(__instance);
            MineshaftPatches(__instance);
        }

        static void PatchManorDoors(RoundManager roundManager)
        {
            AudioMixerGroup sfxDiagetic = null;

            IndoorMapType manor = roundManager.dungeonFlowTypes.FirstOrDefault(dungeonFlowType => dungeonFlowType.dungeonFlow?.name == "Level2Flow");
            if (manor != null)
            {
                bool cache = References.woodenDoorOpen == null || References.woodenDoorOpen.Length < 1 || References.woodenDoorClose == null || References.woodenDoorClose.Length < 1;

                foreach (GraphNode node in manor.dungeonFlow.Nodes)
                {
                    foreach (TileSet tileSet in node.TileSets)
                    {
                        if (tileSet.name == "Level2CapTiles")
                        {
                            GameObject garageTile = tileSet.TileWeights.Weights.FirstOrDefault(weight => weight.Value?.name == "GarageTile")?.Value;
                            if (garageTile != null)
                            {
                                GameObject garageInteractables = garageTile.transform.Find("SpawnInteractables")?.GetComponent<SpawnSyncedObject>()?.spawnPrefab;
                                if (garageInteractables != null)
                                {
                                    AnimatedObjectTrigger garbageBin = garageInteractables.transform.Find("GarbageBinContainer/GarbageBin")?.GetComponent<AnimatedObjectTrigger>();
                                    if (garbageBin != null && !garbageBin.GetComponent<AudioSource>())
                                    {
                                        AudioSource thisAudioSource = garbageBin.thisAudioSource;
                                        garbageBin.thisAudioSource = garbageBin.gameObject.AddComponent<AudioSource>();
                                        sfxDiagetic = thisAudioSource.outputAudioMixerGroup;
                                        garbageBin.thisAudioSource.outputAudioMixerGroup = sfxDiagetic;
                                        garbageBin.thisAudioSource.pitch = thisAudioSource.pitch;
                                        garbageBin.thisAudioSource.spatialBlend = thisAudioSource.spatialBlend;
                                        garbageBin.thisAudioSource.dopplerLevel = thisAudioSource.dopplerLevel;
                                        garbageBin.thisAudioSource.spread = thisAudioSource.spread;
                                        garbageBin.thisAudioSource.rolloffMode = thisAudioSource.rolloffMode;
                                        garbageBin.thisAudioSource.minDistance = thisAudioSource.minDistance;
                                        garbageBin.thisAudioSource.maxDistance = thisAudioSource.maxDistance;
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (GraphLine line in manor.dungeonFlow.Lines)
                {
                    foreach (DungeonArchetype archetype in line.DungeonArchetypes)
                    {
                        foreach (TileSet tileSet in archetype.TileSets)
                        {
                            if (tileSet.name == "Level2HallwayTilesB")
                            {
                                if (cache)
                                {
                                    GameObject manorStartRoom = tileSet.TileWeights.Weights.FirstOrDefault(weight => weight.Value?.name == "ManorStartRoomSmall")?.Value;
                                    if (manorStartRoom != null)
                                    {
                                        // fun!
                                        AnimatedObjectTrigger manorDoor = manorStartRoom.transform.Find("Doorways")?.GetComponentInChildren<Doorway>()?.ConnectorPrefabWeights?.FirstOrDefault(prefab => prefab.GameObject.name == "FancyDoorMapSpawn")?.GameObject.GetComponent<SpawnSyncedObject>()?.spawnPrefab?.GetComponentInChildren<AnimatedObjectTrigger>();

                                        if (manorDoor != null)
                                        {
                                            References.woodenDoorClose = manorDoor.boolFalseAudios;
                                            References.woodenDoorOpen = manorDoor.boolTrueAudios;
                                            Plugin.Logger.LogDebug("Cached wooden door sounds");
                                            cache = false;
                                        }
                                    }
                                }
                            }
                            else if (tileSet.name == "Level2RoomTiles")
                            {
                                GameObject greenhouseTile = tileSet.TileWeights.Weights.FirstOrDefault(weight => weight.Value?.name == "GreenhouseTile")?.Value;
                                if (greenhouseTile != null)
                                {
                                    GameObject greenhouseInteractables = greenhouseTile.transform.Find("GreenhouseSinkContainer/SpawnInteractables")?.GetComponent<SpawnSyncedObject>()?.spawnPrefab;
                                    if (greenhouseInteractables != null)
                                    {
                                        if (greenhouseInteractables.transform.Find("SwingOpenCabinetAudio") == null)
                                        {
                                            GameObject swingOpenCabinetAudio = new("SwingOpenCabinetAudio");
                                            swingOpenCabinetAudio.transform.SetParent(greenhouseInteractables.transform);
                                            swingOpenCabinetAudio.transform.SetLocalPositionAndRotation(new(-10.5155334f, -5.33208466f, 5.79676247f), Quaternion.Euler(0f, 90f, 0f));
                                            swingOpenCabinetAudio.transform.localScale = Vector3.one;

                                            AudioSource thisAudioSource = swingOpenCabinetAudio.AddComponent<AudioSource>();
                                            thisAudioSource.outputAudioMixerGroup = sfxDiagetic;
                                            thisAudioSource.volume = 0.717f;
                                            thisAudioSource.pitch = 0.91f;
                                            thisAudioSource.spatialBlend = 1f;
                                            thisAudioSource.spread = 41f;
                                            thisAudioSource.rolloffMode = AudioRolloffMode.Linear;
                                            thisAudioSource.minDistance = 1f;
                                            thisAudioSource.maxDistance = 12f;

                                            foreach (AnimatedObjectTrigger animatedObjectTrigger in greenhouseInteractables.GetComponentsInChildren<AnimatedObjectTrigger>())
                                            {
                                                if (animatedObjectTrigger.triggerAnimator != null && animatedObjectTrigger.triggerAnimator.name.StartsWith("BigCupboard"))
                                                {
                                                    animatedObjectTrigger.thisAudioSource = thisAudioSource;
                                                    Plugin.Logger.LogDebug($"Fixed greenhouse door \"{animatedObjectTrigger.name}\"");
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
        }

        static void MineshaftPatches(RoundManager roundManager)
        {
            IndoorMapType mineshaft = roundManager.dungeonFlowTypes.FirstOrDefault(dungeonFlowType => dungeonFlowType.dungeonFlow?.name == "Level3Flow");
            if (mineshaft != null)
            {
                PatchMineshaftDoors(mineshaft);
                GetButtonAudio(mineshaft);
            }
        }

        static void PatchMineshaftDoors(IndoorMapType mineshaft)
        {
            // oh boy... here comes part 2
            foreach (GraphLine line in mineshaft.dungeonFlow.Lines)
            {
                foreach (DungeonArchetype archetype in line.DungeonArchetypes)
                {
                    foreach (TileSet tileSet in archetype.TileSets)
                    {
                        if (tileSet.name == "Level3TunnelTiles")
                        {
                            GameObject tunnelSplit = tileSet.TileWeights.Weights.FirstOrDefault(weight => weight.Value?.name == "TunnelSplit")?.Value;
                            if (tunnelSplit != null)
                            {
                                // fun! 2!
                                Transform yellowMineDoor = tunnelSplit.transform.Find("DoorwayPointW")?.GetComponentInChildren<Doorway>()?.ConnectorPrefabWeights?.FirstOrDefault(prefab => prefab.GameObject.name == "MineDoorSpawn")?.GameObject.GetComponentInChildren<SpawnSyncedObject>()?.spawnPrefab?.transform;

                                if (yellowMineDoor != null)
                                {
                                    foreach (Collider collider in yellowMineDoor.GetComponentsInChildren<Collider>())
                                    {
                                        if (collider.gameObject.layer == 8 && collider.name == "LOSBlocker" && collider.transform.parent.name == "MineDoorMesh")
                                        {
                                            collider.gameObject.layer = 11;
                                            Plugin.Logger.LogDebug("Fixed mineshaft door occlusion");
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

        static void GetButtonAudio(IndoorMapType mineshaft)
        {
            if (References.cruiserDashboardButton == null || References.sfx == null)
            {
                foreach (GraphNode node in mineshaft.dungeonFlow.Nodes)
                {
                    foreach (TileSet tileSet in node.TileSets)
                    {
                        if (tileSet.name == "MineshaftStartRooms")
                        {
                            GameObject mineshaftStartTile = tileSet.TileWeights.Weights.FirstOrDefault(weight => weight.Value?.name == "MineshaftStartTile")?.Value;
                            if (mineshaftStartTile != null)
                            {
                                // fun!
                                AnimatedObjectTrigger redButton = mineshaftStartTile.transform.Find("ElevatorSpawn")?.GetComponentInChildren<SpawnSyncedObject>()?.spawnPrefab?.transform.Find("AnimContainer/controlBox/redButton")?.GetComponent<AnimatedObjectTrigger>();

                                if (redButton != null && redButton.boolFalseAudios != null && redButton.boolFalseAudios.Length > 0)
                                {
                                    References.cruiserDashboardButton = redButton.boolFalseAudios[0];
                                    References.sfx = redButton.GetComponent<AudioSource>()?.outputAudioMixerGroup;
                                    Plugin.Logger.LogDebug("Cached dashboard button sound");
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(EnemyVent), nameof(EnemyVent.OpenVentClientRpc))]
        [HarmonyPostfix]
        static void EnemyVent_Post_OpenVentClientRpc(EnemyVent __instance)
        {
            __instance.isPlayingAudio = false;
            __instance.ventAudio.Stop();
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.PlayDropSFX))]
        [HarmonyPrefix]
        static bool GrabbableObject_Pre_PlayDropSFX(GrabbableObject __instance)
        {
            return __instance is not LockPicker lockPicker || !lockPicker.isOnDoor;
        }

        [HarmonyPatch(typeof(Landmine), nameof(Landmine.Detonate))]
        [HarmonyPostfix]
        static void Landmine_Post_Detonate(Landmine __instance)
        {
            if (__instance.mineFarAudio != null && __instance.mineDetonateFar != null)
                __instance.mineFarAudio.PlayOneShot(__instance.mineDetonateFar);
        }

        [HarmonyPatch(typeof(ExtensionLadderItem), nameof(ExtensionLadderItem.StartLadderAnimation))]
        [HarmonyPostfix]
        static void ExtensionLadderItem_Post_StartLadderAnimation(ExtensionLadderItem __instance)
        {
            if (__instance.ladderBlinkWarning)
            {
                __instance.ladderBlinkWarning = false;
                Plugin.Logger.LogDebug("Fixed broken extension ladder warning");
            }
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlayRandomOutsideMusic))]
        [HarmonyPrefix]
        static bool SoundManager_Pre_PlayRandomOutsideMusic(SoundManager __instance)
        {
            return !Plugin.configEclipsesBlockMusic.Value || StartOfRound.Instance.currentLevel.currentWeather != LevelWeatherType.Eclipsed;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        static void StartOfRound_Post_Awake(StartOfRound __instance)
        {
            __instance.speakerAudioSource.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
            __instance.shipDoorAudioSource.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
            Plugin.Logger.LogDebug("Doppler level: Ship speakers");

            __instance.VehiclesList.FirstOrDefault(vehicle => vehicle.name == "CompanyCruiser").GetComponent<VehicleController>().radioAudio.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
            Plugin.Logger.LogDebug("Doppler level: Cruiser");

            AudioSource stickyNote = __instance.elevatorTransform.Find("StickyNoteItem")?.GetComponent<AudioSource>();
            if (stickyNote != null)
            {
                stickyNote.rolloffMode = AudioRolloffMode.Linear;
                Plugin.Logger.LogDebug("Audio rolloff: Sticky note");
            }
            AudioSource clipboard = __instance.elevatorTransform.Find("ClipboardManual")?.GetComponent<AudioSource>();
            if (clipboard != null)
            {
                clipboard.rolloffMode = AudioRolloffMode.Linear;
                Plugin.Logger.LogDebug("Audio rolloff: Clipboard");
            }

            foreach (UnlockableItem unlockableItem in StartOfRound.Instance.unlockablesList.unlockables)
            {
                switch (unlockableItem.unlockableName)
                {
                    /*case "Television":
                        unlockableItem.prefabObject.GetComponentInChildren<TVScript>().tvSFX.dopplerLevel = 0f * Plugin.configMusicDopplerLevel.Value;
                        Plugin.Logger.LogDebug("Doppler level: Television");
                        break;*/
                    case "Record player":
                        unlockableItem.prefabObject.GetComponentInChildren<AnimatedObjectTrigger>().thisAudioSource.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
                        Plugin.Logger.LogDebug("Doppler level: Record player");
                        break;
                    case "Disco Ball":
                        unlockableItem.prefabObject.GetComponentInChildren<CozyLights>().turnOnAudio.dopplerLevel = 0.92f * Plugin.configMusicDopplerLevel.Value;
                        Plugin.Logger.LogDebug("Doppler level: Disco ball");
                        break;
                    case "Microwave":
                        unlockableItem.prefabObject.transform.Find("MicrowaveBody").GetComponent<AudioSource>().playOnAwake = true;
                        Plugin.Logger.LogDebug("Audio: Microwave");
                        break;
                }
            }

            AudioClip shovelPickUp = null, pickUpPlasticBin = null, dropPlastic1 = null, grabCardboardBox = null;
            List<Item> metalSFXItems = [], plasticSFXItems = [], cardboardSFXItems = [];
            foreach (Item item in StartOfRound.Instance.allItemsList.itemsList)
            {
                bool linearRolloff = false;

                switch (item.name)
                {
                    case "Boombox":
                        item.spawnPrefab.GetComponent<BoomboxItem>().boomboxAudio.dopplerLevel = 0.3f * Plugin.configMusicDopplerLevel.Value;
                        Plugin.Logger.LogDebug("Doppler level: Boombox");
                        break;
                    case "BottleBin":
                        pickUpPlasticBin = item.grabSFX;
                        break;
                    case "Brush":
                    case "Candy":
                    case "Dentures":
                    //case "Phone":
                    case "PillBottle":
                    case "PlasticCup":
                    case "Remote":
                    case "SoccerBall":
                    case "SteeringWheel":
                    case "Toothpaste":
                    case "ToyCube":
                        plasticSFXItems.Add(item);
                        break;
                    case "Cog1":
                    case "MapDevice":
                    case "ZapGun":
                        linearRolloff = true;
                        break;
                    case "FancyCup":
                        metalSFXItems.Add(item);
                        break;
                    case "FancyPainting":
                        cardboardSFXItems.Add(item);
                        break;
                    case "FishTestProp":
                        linearRolloff = true;
                        plasticSFXItems.Add(item);
                        break;
                    case "GarbageLid":
                    case "MetalSheet":
                        metalSFXItems.Add(item);
                        break;
                    case "Mug":
                        dropPlastic1 = item.dropSFX;
                        break;
                    case "RedLocustHive":
                        linearRolloff = true;
                        break;
                    case "TeaKettle":
                        shovelPickUp = item.grabSFX;
                        break;
                    case "TragedyMask":
                        grabCardboardBox = item.grabSFX;
                        break;
                }

                if (linearRolloff)
                {
                    item.spawnPrefab.GetComponent<AudioSource>().rolloffMode = AudioRolloffMode.Linear;
                    Plugin.Logger.LogDebug($"Audio rolloff: {item.itemName}");
                }
            }

            if (shovelPickUp != null)
            {
                foreach (Item metalSFXItem in metalSFXItems)
                {
                    metalSFXItem.grabSFX = shovelPickUp;
                    Plugin.Logger.LogDebug($"Audio: {metalSFXItem.itemName}");
                }
            }
            if (pickUpPlasticBin != null)
            {
                foreach (Item plasticSFXItem in plasticSFXItems)
                {
                    plasticSFXItem.grabSFX = pickUpPlasticBin;
                    Plugin.Logger.LogDebug($"Audio: {plasticSFXItem.itemName}");
                    if (plasticSFXItem.name == "PillBottle" && dropPlastic1 != null)
                        plasticSFXItem.dropSFX = dropPlastic1;
                }
            }
            if (grabCardboardBox != null)
            {
                foreach (Item cardboardSFXItem in cardboardSFXItems)
                {
                    cardboardSFXItem.grabSFX = grabCardboardBox;
                    Plugin.Logger.LogDebug($"Audio: {cardboardSFXItem.itemName}");
                }
            }
        }

        [HarmonyPatch(typeof(ItemDropship), nameof(ItemDropship.Start))]
        [HarmonyPostfix]
        static void ItemDropship_Post_Start(ItemDropship __instance)
        {
            // fix doppler level for dropship (both music sources)
            Transform music = __instance.transform.Find("Music");
            if (music != null)
            {
                music.GetComponent<AudioSource>().dopplerLevel = 0.6f * Plugin.configMusicDopplerLevel.Value;
                AudioSource musicFar = music.Find("Music (1)")?.GetComponent<AudioSource>();
                if (musicFar != null)
                    musicFar.dopplerLevel = 0.6f * Plugin.configMusicDopplerLevel.Value;
                Plugin.Logger.LogDebug("Doppler level: Dropship");
            }
        }

        [HarmonyPatch(typeof(MineshaftElevatorController), nameof(MineshaftElevatorController.OnEnable))]
        [HarmonyPostfix]
        static void MineshaftElevatorController_Post_OnEnable(MineshaftElevatorController __instance)
        {
            __instance.elevatorJingleMusic.dopplerLevel = 0.58f * Plugin.configMusicDopplerLevel.Value;
            Plugin.Logger.LogDebug("Doppler level: Mineshaft elevator");
        }
    }
}
