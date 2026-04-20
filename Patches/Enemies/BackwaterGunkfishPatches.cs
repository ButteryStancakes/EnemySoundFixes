using HarmonyLib;
using UnityEngine;

namespace EnemySoundFixes.Patches.Enemies
{
    [HarmonyPatch(typeof(StingrayAI))]
    static class BackwaterGunkfishPatches
    {
        [HarmonyPatch(nameof(StingrayAI.KillEnemy))]
        [HarmonyPostfix]
        static void StingrayAI_Post_KillEnemy(StingrayAI __instance, bool destroy)
        {
            if (destroy)
                return;

            foreach (AudioSource gunkfishAudio in new AudioSource[]{
                __instance.floppingAudio,
                __instance.slidingAudio,
                __instance.whiningAudio
            })
            {
                gunkfishAudio.Stop();
                gunkfishAudio.mute = true;
            }
        }
    }
}
