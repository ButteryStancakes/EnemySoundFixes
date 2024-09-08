using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class PlayerPatches
    {
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPrefix]
        static void PlayerControllerBPreDamagePlayer(CauseOfDeath causeOfDeath, ref bool fallDamage)
        {
            if (causeOfDeath == CauseOfDeath.Gravity && !fallDamage)
            {
                fallDamage = true;
                Plugin.Logger.LogDebug("Player: Treat Gravity damage as fall damage");
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayerFromOtherClientClientRpc))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> PlayerControllerBTransDamagePlayerFromOtherClientClientRpc(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 3; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && (MethodInfo)codes[i].operand == References.DAMAGE_PLAYER)
                {
                    for (int j = i - 1; j > 0; j--)
                    {
                        if (codes[j].opcode == OpCodes.Ldarg_1 && codes[j + 1].opcode == OpCodes.Ldc_I4_1)
                        {
                            codes[j + 1].opcode = OpCodes.Ldc_I4_0; // hasDamageSFX: false
                            Plugin.Logger.LogDebug("Transpiler (Players): Melee weapons don't stack hit sounds");
                            break;
                        }
                    }
                }
            }

            return codes;
        }
    }
}
