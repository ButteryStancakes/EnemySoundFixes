using HarmonyLib;

namespace EnemySoundFixes.Patches
{
    [HarmonyPatch]
    class BaboonHawkPatches
    {
        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.HitEnemy))]
        [HarmonyPostfix]
        static void BaboonBirdAIPostHitEnemy(BaboonBirdAI __instance, bool playHitSFX)
        {
            if (playHitSFX)
            {
                if (!__instance.isEnemyDead)
                {
                    if (References.baboonTakeDamage != null)
                    {
                        __instance.creatureVoice.PlayOneShot(References.baboonTakeDamage);
                        Plugin.Logger.LogInfo("Baboon hawk: Ouch");
                    }
                }
            }
        }
    }
}
