using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class EyelessDogPatches
    {
        const float TIME_DROP_CARRIED_BODY = 5.01f;

        static Dictionary<MouthDogAI, (float Pitch, float Time)> dogPitches = [];

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.KillEnemy))]
        [HarmonyPostfix]
        static void MouthDogAIPostKillEnemy(MouthDogAI __instance, bool destroy)
        {
            // happens after creatureSFX.Stop()
            if (GeneralPatches.playHitSound)
            {
                GeneralPatches.playHitSound = false;
                if (!destroy && References.hitEnemyBody != null)
                {
                    __instance.creatureSFX.PlayOneShot(__instance.enemyType.hitBodySFX);
                    Plugin.Logger.LogInfo("Mouth dog: Play hit sound on death");
                }
            }

            if (!destroy)
            {
                __instance.creatureVoice.mute = true;
                Plugin.Logger.LogInfo("Eyeless dog: Don't start breathing after death");
            }
        }

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.Start))]
        [HarmonyPostfix]
        static void MouthDogAIPostStart(MouthDogAI __instance)
        {
            System.Random random = new(StartOfRound.Instance.randomMapSeed + (int)__instance.NetworkObjectId);
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

        [HarmonyPatch(typeof(MouthDogAI), "enterChaseMode", MethodType.Enumerator)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MouthDogAITransEnterChaseMode(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = instructions.ToList();

            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == References.PLAY_ONE_SHOT && codes[i - 1].opcode == OpCodes.Ldfld && (FieldInfo)codes[i - 1].operand == AccessTools.Field(typeof(MouthDogAI), nameof(MouthDogAI.breathingSFX)))
                {
                    for (int j = i - 4; j <= i; j++)
                        codes[j].opcode = OpCodes.Nop;
                    Plugin.Logger.LogDebug("Transpiler (Eyeless dog): Fix overlapping breathing");
                    break;
                }
            }

            return codes;
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

        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.HitEnemy))]
        [HarmonyPrefix]
        static void MouthDogAIPreHitEnemy(MouthDogAI __instance, int force, bool playHitSFX)
        {
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead && __instance.enemyHP <= force;
        }
    }
}
