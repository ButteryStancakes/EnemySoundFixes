using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;
using BepInEx.Bootstrap;

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
    public class Plugin : BaseUnityPlugin
    {
        internal const string PLUGIN_GUID = "butterystancakes.lethalcompany.enemysoundfixes", PLUGIN_NAME = "Enemy Sound Fixes", PLUGIN_VERSION = "1.8.0";
        internal static new ManualLogSource Logger;

        internal static ConfigEntry<bool> configThumperNoThunder, configBetterMimicSteps, configFixDoorSounds;
        internal static ConfigEntry<CruiserMute> configSpaceMutesCruiser;

        const string GUID_LOBBY_COMPATIBILITY = "BMX.LobbyCompatibility";
        const string GUID_SOUND_API = "me.loaforc.soundapi";
        internal static bool INSTALLED_SOUND_API;

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

            configSpaceMutesCruiser = Config.Bind(
                "Misc",
                "SpaceMutesCruiser",
                CruiserMute.NotRadio,
                "What audio sources should be muted on the Cruiser when in orbit. (Engine sounds, the horn, the radio, etc.)");

            configFixDoorSounds = Config.Bind(
                "Misc",
                "FixDoorSounds",
                true,
                "Fixes backwards open/close sounds on factory doors, breaker boxes, and storage locker doors. Fixes Rend and Adamance cabin doors using steel door sounds.");

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