using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class KidnapperFoxPatches
    {
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
            if (__instance.isEnemyDead && __instance.spitParticle.isEmitting)
            {
                __instance.spitParticle.Stop();
                Plugin.Logger.LogInfo("Kidnapper fox: Cancel drool");
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
            Label label = generator.DefineLabel();
            codes[^1].labels.Add(label);
            for (int i = codes.Count - 1; i >= 0; i--)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == cancelReelingPlayerIn)
                {
                    codes.InsertRange(i + 1, [
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, References.IS_ENEMY_DEAD),
                        new CodeInstruction(OpCodes.Brtrue, label)
                    ]);
                    Plugin.Logger.LogDebug("Transpiler (Kidnapper fox): Don't cry when dead");
                    break;
                }
            }

            return codes;
        }
    }
}
