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
                    /*case "BaboonHawk":
                        if (References.baboonTakeDamage == null)
                        {
                            References.baboonTakeDamage = enemy.enemyType.hitBodySFX;
                            Plugin.Logger.LogDebug("Cached baboon hawk damage sound");
                        }
                        enemy.enemyType.hitBodySFX = null;
                        Plugin.Logger.LogDebug("Overwritten baboon hawk damage sound");
                        enemy.enemyType.enemyPrefab.GetComponent<BaboonBirdAI>().dieSFX = enemy.enemyType.deathSFX;
                        Plugin.Logger.LogDebug("Overwritten missing baboon hawk death sound");
                        break;*/
                    case "CadaverGrowths":
                        enemy.enemyType.timeToPlayAudio = 1f;
                        enemy.enemyType.loudnessMultiplier = 0.1f;
                        Plugin.Logger.LogDebug("Adjust cadaver vent sounds");
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
                            EnemyBehaviourState searching = enemy.enemyType.enemyPrefab.GetComponent<CrawlerAI>()?.enemyBehaviourStates?.FirstOrDefault(enemyBehaviourState => enemyBehaviourState.name == "searching");
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

        [HarmonyPatch(typeof(StormyWeather), nameof(StormyWeather.PlayThunderEffects))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> StormyWeather_Trans_PlayThunderEffects(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            FieldInfo shipCreakSFX = AccessTools.Field(typeof(StartOfRound), nameof(StartOfRound.shipCreakSFX));
            for (int i = 5; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && codes[i].operand as MethodInfo == References.PLAY_RANDOM_CLIP && codes[i - 1].opcode == OpCodes.Ldc_I4 && (int)codes[i - 1].operand == 1000 && codes[i - 5].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 5].operand == shipCreakSFX)
                {
                    codes[i - 1].opcode = OpCodes.Ldc_I4_6;
                    Plugin.Logger.LogDebug("Transpiler (Stormy weather): No \"Hey\" when ship is struck");
                    return codes;
                }
            }

            Plugin.Logger.LogError("Stormy weather transpiler failed");
            return instructions;
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
                                        Plugin.Logger.LogDebug($"Fixed garbage bin \"{garbageBin.name}\"");
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
            if (__instance is LockPicker lockPicker && lockPicker.isOnDoor)
                return false;

            if (__instance is GiftBoxItem giftBoxItem && (giftBoxItem.hasUsedGift || giftBoxItem.PoofParticle.isPlaying || giftBoxItem.presentAudio.isPlaying))
            {
                giftBoxItem.hasHitGround = true;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(Landmine), nameof(Landmine.Detonate))]
        [HarmonyPostfix]
        static void Landmine_Post_Detonate(Landmine __instance)
        {
            if (__instance.mineFarAudio != null && __instance.mineDetonateFar != null)
            {
                if (__instance.mineAudio != null)
                    __instance.mineFarAudio.pitch = __instance.mineAudio.pitch;
                __instance.mineFarAudio.PlayOneShot(__instance.mineDetonateFar);
            }
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
            if (__instance.speakerAudioSource != null)
            {
                __instance.speakerAudioSource.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
                if (__instance.shipDoorAudioSource != null)
                    __instance.shipDoorAudioSource.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
                Plugin.Logger.LogDebug("Doppler level: Ship speakers");
            }

            AudioSource lampAudio = __instance.elevatorTransform?.Find("LampSqueakAudio")?.GetComponent<AudioSource>();
            if (lampAudio != null)
            {
                lampAudio.dopplerLevel = 0f;
                Plugin.Logger.LogDebug("Doppler level: Ship lamp");
            }

            AudioSource radioAudio = __instance.VehiclesList?.FirstOrDefault(vehicle => vehicle.name == "CompanyCruiser")?.GetComponent<VehicleController>()?.radioAudio;
            if (radioAudio != null)
            {
                radioAudio.dopplerLevel = Plugin.configMusicDopplerLevel.Value;
                Plugin.Logger.LogDebug("Doppler level: Cruiser");
            }

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

            try
            {
                AssetBundle sfxBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "enemysoundfixes"));
                References.empty = sfxBundle.LoadAsset<AudioClip>("empty");
                sfxBundle.Unload(false);
            }
            catch
            {
                // it's ok if this fails to load, References.empty == null will replicate previous behavior from earlier versions
                Plugin.Logger.LogWarning("Encountered some error loading assets from bundle \"enemysoundfixes\". Did you install the plugin correctly?");
            }

            AudioClip shovelPickUp = null, pickUpPlasticBin = null, dropPlastic1 = null, dropPlastic2 = null, grabCardboardBox = null;
            List<Item> metalSFXItems = [], plasticSFXItems = [], cardboardSFXItems = [];
            Item pillBottle = null;
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
                    //case "Dentures":
                    case "Phone":
                    //case "PlasticCup":
                    case "Remote":
                    //case "SoccerBall":
                    case "SteeringWheel":
                    case "ToyCube":
                        plasticSFXItems.Add(item);
                        break;
                    case "Candy":
                    case "Toothpaste":
                        item.grabSFX = References.empty;
                        break;
                    case "Cog1":
                    case "MapDevice":
                    case "ZapGun":
                        linearRolloff = true;
                        break;
                    case "DustPan":
                        dropPlastic2 = item.dropSFX;
                        break;
                    case "FancyCup":
                        metalSFXItems.Add(item);
                        break;
                    case "FancyPainting":
                        cardboardSFXItems.Add(item);
                        break;
                    case "FishTestProp":
                        linearRolloff = true;
                        //plasticSFXItems.Add(item);
                        break;
                    case "GarbageLid":
                    case "MetalSheet":
                        metalSFXItems.Add(item);
                        break;
                    case "Mug":
                        dropPlastic1 = item.dropSFX;
                        break;
                    case "PillBottle":
                        pillBottle = item;
                        item.grabSFX = References.empty;
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
                    case "WalkieTalkie":
                        WalkieTalkie walkieTalkie = item.spawnPrefab.GetComponent<WalkieTalkie>();
                        walkieTalkie.gameObject.AddComponent<RadioChatter>().walkieTalkie = walkieTalkie;
                        Plugin.Logger.LogDebug("Walkie talkie: Let's make some noise!");
                        break;
                    case "WeedKillerBottle":
                        item.spawnPrefab.GetComponent<SprayPaintItem>().sprayAudio.loop = false;
                        Plugin.Logger.LogDebug("Loop: Weed killer");
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
                    if (plasticSFXItem.name == "Phone" && dropPlastic2 != null)
                        plasticSFXItem.dropSFX = dropPlastic2;
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
            if (pillBottle != null && dropPlastic1 != null)
            {
                pillBottle.dropSFX = dropPlastic1;
                Plugin.Logger.LogDebug($"Audio: {pillBottle.itemName}");
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

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
        [HarmonyPostfix]
        static void Terminal_Post_Awake(Terminal __instance)
        {
            BuyableVehicle cruiser = __instance.buyableVehicles.FirstOrDefault(buyableVehicle => buyableVehicle.vehicleDisplayName == "Cruiser");
            if (cruiser != null)
            {
                AudioSource clipboardCruiser = cruiser.secondaryPrefab?.transform.GetComponent<AudioSource>();
                if (clipboardCruiser != null)
                {
                    clipboardCruiser.rolloffMode = AudioRolloffMode.Linear;
                    Plugin.Logger.LogDebug("Audio rolloff: Clipboard (Cruiser)");
                }

                if (!Plugin.INSTALLED_VERSION55_COMPANY_CRUISER)
                {
                    VehicleController vehicleController = cruiser.vehiclePrefab?.GetComponent<VehicleController>();
                    if (vehicleController != null)
                    {
                        AudioClip companyCruiserEngineRun = null, cruiserEngineRun2 = null;
                        try
                        {
                            AssetBundle sfxBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "enemysoundfixes"));
                            companyCruiserEngineRun = sfxBundle.LoadAsset<AudioClip>("CompanyCruiser_EngineRun");
                            cruiserEngineRun2 = sfxBundle.LoadAsset<AudioClip>("Cruiser_EngineRun2");
                            sfxBundle.Unload(false);
                        }
                        catch
                        {
                            Plugin.Logger.LogError("Encountered some error loading assets from bundle \"enemysoundfixes\". Did you install the plugin correctly?");
                        }

                        if (companyCruiserEngineRun != null)
                        {
                            vehicleController.engineRun = companyCruiserEngineRun;
                            Plugin.Logger.LogDebug("Cruiser: Engine run");
                        }

                        if (cruiserEngineRun2 != null)
                        {
                            vehicleController.engineRun2 = cruiserEngineRun2;
                            Plugin.Logger.LogDebug("Cruiser: Engine run #2");
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(FlashlightItem), nameof(FlashlightItem.ItemActivate))]
        [HarmonyPrefix]
        static bool FlashlightItem_Pre_ItemActivate(FlashlightItem __instance, bool used)
        {
            if (__instance.itemProperties == null || (__instance.itemProperties.itemId != 6 && __instance.itemProperties.name != "FlashLaserPointer"))
                return true;

            if (__instance.flashlightInterferenceLevel < 2)
                __instance.SwitchFlashlight(used);

            __instance.flashlightAudio.PlayOneShot(__instance.flashlightClips[__instance.isBeingUsed ? 0 : 1]);
            RoundManager.Instance.PlayAudibleNoise(__instance.transform.position, 7f, 0.4f, 0, __instance.isInElevator && StartOfRound.Instance.hangarDoorsClosed);

            return false;
        }

        [HarmonyPatch(typeof(CozyLights), nameof(CozyLights.SetAudio))]
        [HarmonyPostfix]
        static void CozyLights_Post_SetAudio(CozyLights __instance)
        {
            if (!__instance.cozyLightsOn && __instance.turnOnAudio.isPlaying && __instance.turnOnAudio.volume > 0.3f)
                __instance.turnOnAudio.volume *= 0.3f;
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.HitEnemy))]
        [HarmonyPrefix]
        static void EnemyAI_Pre_HitEnemy(EnemyAI __instance, bool playHitSFX)
        {
            if (playHitSFX && !__instance.isEnemyDead)
            {
                if (__instance.creatureVoice != null && __instance.enemyType.hitEnemyVoiceSFX != null)
                {
                    if (__instance.enemyType.hitBodySFX == null)
                    {
                        __instance.creatureVoice.PlayOneShot(__instance.enemyType.hitEnemyVoiceSFX);
                        WalkieTalkie.TransmitOneShotAudio(__instance.creatureVoice, __instance.enemyType.hitEnemyVoiceSFX);
                        Plugin.Logger.LogDebug($"Played missing hit sound \"{__instance.enemyType.hitEnemyVoiceSFX.name}\" for \"{__instance.name}\"");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc))]
        [HarmonyPostfix]
        static void RoundManager_Post_GenerateNewLevelClientRpc(RoundManager __instance)
        {
            if (SoundManager.Instance != null && __instance.currentLevel != null)
            {
                if (__instance.currentLevel.levelAmbienceClips == null)
                {
                    if (SoundManager.Instance.currentLevelAmbience != null)
                    {
                        SoundManager.Instance.currentLevelAmbience = null;
                        Plugin.Logger.LogDebug("Cleared current level ambience library (current level has none defined)");
                    }
                }
                else if (SoundManager.Instance.currentLevelAmbience != __instance.currentLevel.levelAmbienceClips)
                {
                    SoundManager.Instance.currentLevelAmbience = __instance.currentLevel.levelAmbienceClips;
                    Plugin.Logger.LogDebug("Corrected assigned level ambience library (current level does not spawn enemies or scrap)");
                }
            }
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.ServerSoundTimer))]
        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.LocalPlayerSoundTimer))]
        [HarmonyPrefix]
        static bool SoundManager_Pre_SoundTimer()
        {
            return TimeOfDay.Instance == null || TimeOfDay.Instance.currentDayTimeStarted;
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlayNonDiageticSound))]
        [HarmonyPrefix]
        static bool SoundManager_Pre_PlayNonDiageticSound(SoundManager __instance)
        {
            if (StartOfRound.Instance.currentLevelID == 3)
            {
                __instance.ambienceAudioNonDiagetic.volume = Mathf.Lerp(__instance.ambienceAudioNonDiagetic.volume, 0f, Time.deltaTime);
                __instance.isInsanityMusicPlaying = false;
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.Start))]
        [HarmonyPostfix]
        static void SoundManager_Post_Start(SoundManager __instance)
        {
            __instance.musicSource.volume = 0f;
            __instance.ringingEarsAudio.volume = 0.5f;
        }

        [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.Update))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SoundManager_Trans_Update(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            FieldInfo timeSincePlayingLastMusic = AccessTools.Field(typeof(SoundManager), nameof(SoundManager.timeSincePlayingLastMusic));
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Stfld && (FieldInfo)codes[i].operand == timeSincePlayingLastMusic && codes[i - 1].opcode == OpCodes.Add && codes[i - 2].opcode == OpCodes.Ldc_R4 && (float)codes[i - 2].operand == 1f)
                {
                    codes[i - 2].opcode = OpCodes.Call;
                    codes[i - 2].operand = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.deltaTime));
                    Plugin.Logger.LogDebug("Transpiler (Music): Fix 200s cooldown");
                    return codes;
                }
            }

            Plugin.Logger.LogError("Music transpiler failed");
            return instructions;
        }
    }
}
