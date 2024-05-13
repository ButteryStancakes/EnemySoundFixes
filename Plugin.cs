using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
//using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EnemySoundFixes
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.enemysoundfixes", PLUGIN_NAME = "Enemy Sound Fixes", PLUGIN_VERSION = "1.0.0";
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
        static AudioClip /*hitEnemyBody,*/ baboonTakeDamage/*, centipedeWalk*/;

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
            }
            /*if (centipedeWalk == null)
            {
                try
                {
                    AssetBundle assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "enemysoundfixes"));
                    centipedeWalk = assetBundle.LoadAsset<AudioClip>("CentipedeWalk");
                    assetBundle.Unload(false);
                }
                catch
                {
                    Plugin.Logger.LogError("Failed to load assets from asset bundle (\"enemysoundfixes\" file); did you install the plugin correctly?");
                }
            }
            if (centipedeWalk != null)
            {
                SpawnableEnemyWithRarity centipede = __instance.testAllEnemiesLevel.Enemies.FirstOrDefault(spawnableEnemyWithRarity => spawnableEnemyWithRarity.enemyType.name == "Centipede");
                if (centipede != null)
                {
                    EnemyBehaviourState attacking = centipede.enemyType.enemyPrefab.GetComponent<CentipedeAI>().enemyBehaviourStates.FirstOrDefault(behaviorState => behaviorState.name == "attacking");
                    if (attacking != null)
                    {
                        attacking.SFXClip = centipedeWalk;
                        Plugin.Logger.LogInfo("Overwritten snare flea chase sound effect (10s)");
                    }
                }
            }
            */
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

        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.KillEnemy))]
        [HarmonyPrefix]
        static void BaboonBirdAIPreKillEnemy(BaboonBirdAI __instance)
        {
            if (__instance.dieSFX == null)
                __instance.dieSFX = __instance.enemyType.deathSFX;
            Plugin.Logger.LogInfo($"Baboon hawk: Overwritten missing death sound");
        }

        [HarmonyPatch(typeof(CentipedeAI), "delayedShriek", MethodType.Enumerator)]
        [HarmonyTranspiler()]
        static IEnumerable<CodeInstruction> transDelayedShriek(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            Label label = generator.DefineLabel();

            int insertAt = -1;
            for (int i = 0; i < codes.Count - 1; i++)
            {
                //Plugin.Logger.LogInfo(codes[i]);
                if (codes[i].opcode == OpCodes.Ldloc_1 && codes[i + 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 1].operand == typeof(EnemyAI).GetField(nameof(EnemyAI.creatureVoice), BindingFlags.Instance | BindingFlags.Public))
                {
                    insertAt = i;
                    codes[i].labels.Add(label);
                    break;
                }
            }

            if (insertAt != -1)
            {
                codes.Insert(insertAt, new CodeInstruction(OpCodes.Ldloc_1, null));
                codes.Insert(insertAt + 1, new CodeInstruction(OpCodes.Ldfld, typeof(EnemyAI).GetField(nameof(EnemyAI.isEnemyDead), BindingFlags.Instance | BindingFlags.Public)));
                codes.Insert(insertAt + 2, new CodeInstruction(OpCodes.Brfalse, label));
                codes.Insert(insertAt + 3, new CodeInstruction(OpCodes.Ldc_I4_0, null));
                codes.Insert(insertAt + 4, new CodeInstruction(OpCodes.Ret, null));
                Plugin.Logger.LogInfo("Transpiler: Patched Centipede shriek (added isEnemyDead check)");
            }

            return codes;
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.Update))]
        [HarmonyPrefix]
        static void CentipedeAIPreUpdate(CentipedeAI __instance)
        {
            if (__instance.creatureSFX.isPlaying)
            {
                if (__instance.creatureSFX.clip.name == "monsterNoise2")
                {
                    if (__instance.creatureSFX.loop)
                    {
                        __instance.creatureSFX.loop = false;
                        Plugin.Logger.LogInfo($"Snare flea: Don't loop ceiling grapple noises");
                    }
                    if (__instance.creatureVoice.pitch > 1f)
                    {
                        __instance.creatureVoice.pitch = 1f;
                        Plugin.Logger.LogInfo($"Snare flea: Reset \"voice\" pitch for attacking again");
                    }
                }
                else
                {
                    if (__instance.creatureSFX.clip.name == "CentipedeWalk" && (__instance.isEnemyDead || __instance.currentBehaviourState.name != "attacking"))
                    {
                        __instance.creatureSFX.Stop();
                        Plugin.Logger.LogInfo($"Snare flea: Stop walking while dead, clinging to player, or sneaking away");
                    }
                    else if (!__instance.creatureSFX.loop && !__instance.isEnemyDead)
                    {
                        __instance.creatureSFX.loop = true;
                        Plugin.Logger.LogInfo($"Snare flea: Loop walking and clinging");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.KillEnemy))]
        [HarmonyPostfix]
        static void CentipedeAIPostKillEnemy(CentipedeAI __instance)
        {
            // randomize death pitch
            __instance.creatureVoice.pitch = Random.value > 0.5f ? 1f : 1.7f;
        }
    }
}