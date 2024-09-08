using HarmonyLib;
using UnityEngine;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class ManeaterPatches
    {
        [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.HitEnemy))]
        [HarmonyPrefix]
        static void CaveDwellerAIPreHitEnemy(CaveDwellerAI __instance, int force, bool playHitSFX)
        {
            GeneralPatches.playHitSound = playHitSFX && !__instance.isEnemyDead && __instance.enemyHP <= 1;
        }

        [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.KillEnemy))]
        [HarmonyPostfix]
        static void CaveDwellerAIPostKillEnemy(CaveDwellerAI __instance, bool destroy)
        {
            if (destroy)
                return;

            // creatureSFX.Stop() added in v64
            if (GeneralPatches.playHitSound)
            {
                GeneralPatches.playHitSound = false;
                if (!destroy)
                {
                    __instance.creatureSFX.Stop();
                    __instance.creatureSFX.PlayOneShot(__instance.enemyType.hitBodySFX);
                    Plugin.Logger.LogDebug("Maneater: Play hit sound on death");
                }
            }

            foreach (AudioSource maneaterAudio in new AudioSource[]{
                __instance.clickingAudio1,
                __instance.clickingAudio2,
                __instance.walkingAudio,
                __instance.screamAudio,
                __instance.screamAudioNonDiagetic
            })
            {
                maneaterAudio.Stop();
                maneaterAudio.mute = true;
            }
            __instance.creatureVoice.Stop();
            __instance.creatureVoice.PlayOneShot(__instance.dieSFX);
            Plugin.Logger.LogDebug("Maneater: Played backup death sound");
        }
    }
}
