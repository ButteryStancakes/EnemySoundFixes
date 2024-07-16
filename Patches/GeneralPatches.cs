using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class GeneralPatches
    {
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
                }

                if (References.hitEnemyBody == null && enemy.enemyType.hitBodySFX.name == "HitEnemyBody")
                {
                    References.hitEnemyBody = enemy.enemyType.hitBodySFX;
                    Plugin.Logger.LogInfo("Cached generic damage sound");
                }
            }

            if (References.hitEnemyBody != null)
            {
                if (mouthDog != null)
                {
                    mouthDog.hitBodySFX = References.hitEnemyBody;
                    Plugin.Logger.LogInfo("Overwritten missing eyeless dog hit sound");
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
                Plugin.Logger.LogWarning($"\"{__instance.name}.AnimatedObjectTrigger\" doesn't have particles attached");
            }
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
    }
}
