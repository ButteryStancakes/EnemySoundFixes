using GameNetcodeStuff;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace EnemySoundFixes
{
    internal class References
    {
        internal static readonly FieldInfo CREATURE_VOICE = AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.creatureVoice));
        internal static readonly FieldInfo IS_ENEMY_DEAD = AccessTools.Field(typeof(EnemyAI), nameof(EnemyAI.isEnemyDead));
        internal static readonly FieldInfo ENGINE_AUDIO_1 = AccessTools.Field(typeof(VehicleController), nameof(VehicleController.engineAudio1));

        internal static readonly MethodInfo REALTIME_SINCE_STARTUP = AccessTools.DeclaredPropertyGetter(typeof(Time), nameof(Time.realtimeSinceStartup));
        internal static readonly MethodInfo PLAY_ONE_SHOT = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.PlayOneShot), [typeof(AudioClip)]);
        internal static readonly MethodInfo STOP = AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Stop), []);
        internal static readonly MethodInfo DAMAGE_PLAYER = AccessTools.Method(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer));
        internal static readonly MethodInfo HIT_ENEMY = AccessTools.Method(typeof(EnemyAI), nameof(EnemyAI.HitEnemy));
        internal static readonly MethodInfo PLAY_RANDOM_CLIP = AccessTools.Method(typeof(RoundManager), nameof(RoundManager.PlayRandomClip));

        internal static AudioClip baboonTakeDamage, hitEnemyBody;
    }
}
