using HarmonyLib;

namespace EnemySoundFixes.Patches.Enemies
{
    [HarmonyPatch(typeof(PumaAI))]
    static class FeioparPatches
    {
        [HarmonyPatch(nameof(PumaAI.KillEnemy))]
        [HarmonyPrefix]
        static void PumaAI_Pre_KillEnemy(PumaAI __instance)
        {
            __instance.creatureVoice.Stop();
        }
    }
}
