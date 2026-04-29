using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace EnemySoundFixes
{
    internal enum CruiserMute
    {
        Nothing = -1,
        NotRadio,
        All
    }

    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency(GUID_SOUND_API, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(GUID_LOBBY_COMPATIBILITY, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(GUID_UPTURNED_VARIETY, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal const string PLUGIN_GUID = "butterystancakes.lethalcompany.enemysoundfixes", PLUGIN_NAME = "Enemy Sound Fixes", PLUGIN_VERSION = "1.9.7";
        internal static new ManualLogSource Logger;

        internal static ConfigEntry<bool> configThumperNoThunder, configBetterMimicSteps, configFixDoorSounds, configShootTheDog, configEclipsesBlockMusic, configWalkieHearsTalkies;
        internal static ConfigEntry<CruiserMute> configSpaceMutesCruiser;
        internal static ConfigEntry<float> configMusicDopplerLevel;

        const string GUID_LOBBY_COMPATIBILITY = "BMX.LobbyCompatibility";
        const string GUID_SOUND_API = "me.loaforc.soundapi";
        const string GUID_UPTURNED_VARIETY = "butterystancakes.lethalcompany.upturnedvariety";
        internal static bool INSTALLED_SOUND_API, INSTALLED_UPTURNED_VARIETY;

        void Awake()
        {
            Logger = base.Logger;

            if (Chainloader.PluginInfos.ContainsKey(GUID_LOBBY_COMPATIBILITY))
            {
                Logger.LogInfo("CROSS-COMPATIBILITY - Lobby Compatibility detected");
                LobbyCompatibility.Init();
            }

            if (Chainloader.PluginInfos.ContainsKey(GUID_SOUND_API))
            {
                INSTALLED_SOUND_API = true;
                Logger.LogInfo("CROSS-COMPATIBILITY - loaforcsSoundAPI detected");
            }

            if (Chainloader.PluginInfos.ContainsKey(GUID_UPTURNED_VARIETY))
            {
                INSTALLED_UPTURNED_VARIETY = true;
                Logger.LogInfo("CROSS-COMPATIBILITY - Upturned Variety detected");
            }

            configBetterMimicSteps = Config.Bind(
                "Misc",
                "BetterMimicSteps",
                true,
                "Mimic footstep volume and distance are altered to sound more accurate to actual players.");

            configThumperNoThunder = Config.Bind(
                "Misc",
                "ThumperNoThunder",
                true,
                "Thumpers no longer play thunder sound effects from their voice when they stop chasing after players.");

            configShootTheDog = Config.Bind(
                "Misc",
                "ShootTheDog",
                true,
                "Makes eyeless dogs play their stun sound effect on death, rather than falling silently.");

            configSpaceMutesCruiser = Config.Bind(
                "Misc",
                "SpaceMutesCruiser",
                CruiserMute.NotRadio,
                "What audio sources should be muted on the Cruiser when in orbit. (Engine sounds, the horn, the radio, etc.)");

            configFixDoorSounds = Config.Bind(
                "Misc",
                "FixDoorSounds",
                true,
                "Fixes backwards open/close sounds on breaker boxes and storage locker doors. Fixes Rend and Adamance cabin doors using steel door sounds.");

            configEclipsesBlockMusic = Config.Bind(
                "Misc",
                "EclipsesBlockMusic",
                true,
                "Prevents the morning/afternoon ambience music from playing during Eclipsed weather, which has its own ambient track.");

            configMusicDopplerLevel = Config.Bind(
                "Misc",
                "MusicDopplerLevel",
                0.333f,
                "Controls how much Unity's simulated \"Doppler effect\" applies to music sources like the dropship, boombox, etc. (This is what causes pitch distortion when moving towards/away from the source of the music)\n" +
                "1 is the same as vanilla. 0 will disable it completely (so music always plays at the correct pitch)");

            configWalkieHearsTalkies = Config.Bind(
                "Misc",
                "WalkieHearsTalkies",
                true,
                "Restores a cut sound effect of idle chatter when walkie-talkies are in use. Only audible when standing near a walkie-talkie that is turned on, someone is transmitting their voice, and you do not have a walkie-talkie turned on in your inventory. (This does *not* actually repeat what is being spoken over the line - it's just SFX)");

            // migrate from previous version if necessary
            Config.Bind("Misc", "DontFixMasks", false, "Legacy setting, doesn't work");
            Config.Remove(Config["Misc", "DontFixMasks"].Definition);
            Config.Bind("Misc", "FixMasks", true, "Legacy setting, doesn't work");
            Config.Remove(Config["Misc", "FixMasks"].Definition);
            Config.Save();

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }
}