using GameNetcodeStuff;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.Audio;

namespace EnemySoundFixes
{
    internal class References
    {
        internal static readonly FieldInfo  CREATURE_VOICE = AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.creatureVoice)),
                                            IS_ENEMY_DEAD = AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.isEnemyDead)),
                                            ENGINE_AUDIO_1 = AccessTools.Field(typeof(VehicleController), nameof(VehicleController.engineAudio1));

        internal static readonly MethodInfo REALTIME_SINCE_STARTUP = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.realtimeSinceStartup)),
                                            PLAY_ONE_SHOT = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.PlayOneShot), [typeof(AudioClip)]),
                                            STOP = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Stop), []),
                                            DAMAGE_PLAYER = AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer)),
                                            HIT_ENEMY = AccessTools.Method(typeof(EnemyAI), nameof(EnemyAI.HitEnemy)),
                                            PLAY_RANDOM_CLIP = AccessTools.Method(typeof(RoundManager), nameof(RoundManager.PlayRandomClip));

        internal static AudioClip /*baboonTakeDamage,*/ hitEnemyBody, cruiserDashboardButton;
        internal static AudioClip[] woodenDoorOpen, woodenDoorClose;
        internal static AudioMixerGroup sfx;
    }
}
