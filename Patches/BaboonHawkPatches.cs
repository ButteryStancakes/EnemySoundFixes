using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class BaboonHawkPatches
    {
        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.HitEnemy))]
        [HarmonyPostfix]
        static void BaboonBirdAIPostHitEnemy(BaboonBirdAI __instance, bool playHitSFX)
        {
            if (playHitSFX && !__instance.isEnemyDead)
            {
                if (!__instance.isEnemyDead && References.baboonTakeDamage != null)
                {
                    __instance.creatureVoice.PlayOneShot(References.baboonTakeDamage);
                    Plugin.Logger.LogDebug("Baboon hawk: Ouch");
                }
                else if (References.hitEnemyBody != null)
                    __instance.creatureSFX.PlayOneShot(References.hitEnemyBody);
            }
        }

        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.OnCollideWithEnemy))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EnemyTransOnCollideWithEnemy(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<CodeInstruction> codes = instructions.ToList();

            MethodInfo resetTrigger = AccessTools.Method(typeof(Animator), nameof(Animator.ResetTrigger), [typeof(string)]), roundManagerInstance = AccessTools.DeclaredPropertyGetter(typeof(RoundManager), nameof(RoundManager.Instance));
            for (int i = 3; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == resetTrigger)
                {
                    for (int j = i + 1; j < codes.Count; j++)
                    {
                        if (codes[j].opcode == OpCodes.Call && (MethodInfo)codes[j].operand == roundManagerInstance)
                        {
                            Label label = generator.DefineLabel();
                            codes[j].labels.Add(label);
                            codes.InsertRange(i - 3, [
                                new CodeInstruction(OpCodes.Ldarg_2),
                                new CodeInstruction(OpCodes.Ldfld, References.IS_ENEMY_DEAD),
                                new CodeInstruction(OpCodes.Brtrue, label)
                            ]);
                            Plugin.Logger.LogDebug("Transpiler (Baboon hawk): Don't play hit sound when attacking dead enemy (A)");
                            i += 3;
                            break;
                        }
                    }
                }
                else if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == References.HIT_ENEMY)
                {
                    codes.RemoveAt(i - 2);
                    codes.InsertRange(i - 2,
                    [
                        new CodeInstruction(OpCodes.Ldarg_2),
                        new CodeInstruction(OpCodes.Ldfld, References.IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ceq)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler (Baboon hawk): Don't play hit sound when attacking dead enemy (B)");
                    i += 4;
                }
            }

            return codes;
        }
    }
}
