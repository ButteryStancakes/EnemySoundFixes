using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using System.Linq;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class SnareFleaPatches
    {
        [HarmonyPatch(typeof(CentipedeAI), "delayedShriek", MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TransDelayedShriek(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();
            Label label = generator.DefineLabel();

            for (int i = 0; i < codes.Count - 1; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_1 && codes[i + 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i + 1].operand == References.CREATURE_VOICE)
                {
                    codes[i].labels.Add(label);
                    codes.InsertRange(i,
                    [
                        new CodeInstruction(OpCodes.Ldloc_1),
                        new CodeInstruction(OpCodes.Ldfld, References.IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Brfalse, label),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ret)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler (Snare flea): Don't shriek when dead");
                    break;
                }
            }

            return codes;
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.Update))]
        [HarmonyPrefix]
        static void CentipedeAIPreUpdate(CentipedeAI __instance)
        {
            if (__instance.creatureSFX.isPlaying && __instance.creatureSFX.clip == __instance.enemyBehaviourStates[2].SFXClip && (__instance.isEnemyDead || __instance.currentBehaviourStateIndex != 2))
            {
                __instance.creatureSFX.Stop();
                __instance.creatureSFX.clip = null; // don't block hit sound when attacking its dead body if it was killed in attacking state
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
        static void PostPlayAudioOfCurrentState(EnemyAI __instance)
        {
            if (__instance is CentipedeAI && __instance.currentBehaviourStateIndex == 1 && __instance.creatureVoice.pitch > 1f)
            {
                __instance.creatureVoice.pitch = 1f;
                Plugin.Logger.LogInfo("Snare flea: Reset \"voice\" pitch for attacking again");
            }
        }

        [HarmonyPatch(typeof(CentipedeAI), nameof(CentipedeAI.HitEnemy))]
        [HarmonyPrefix]
        static void CentipedeAIPreHitEnemy(CentipedeAI __instance)
        {
            // stop chasing sound early so hit sound doesn't get blocked by other code
            if (__instance.creatureSFX.isPlaying && __instance.creatureSFX.clip == __instance.enemyBehaviourStates[2].SFXClip)
            {
                __instance.creatureSFX.Stop();
                __instance.creatureSFX.clip = null;
            }
        }
    }
}
