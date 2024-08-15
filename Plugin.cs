using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using BepInEx.Configuration;

namespace EnemySoundFixes
{
    internal enum CruiserMute
    {
        Nothing = -1,
        NotRadio,
        All
    }

    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "butterystancakes.lethalcompany.enemysoundfixes", PLUGIN_NAME = "Enemy Sound Fixes", PLUGIN_VERSION = "1.5.4";
        internal static new ManualLogSource Logger;
        internal static ConfigEntry<bool> configFixMasks, configThumperNoThunder, configBetterMimicSteps;
        internal static ConfigEntry<CruiserMute> configSpaceMutesCruiser;

        void Awake()
        {
            Logger = base.Logger;

            configFixMasks = Config.Bind(
                "Misc",
                "FixMasks",
                true,
                "(Host only, requires game restart) Fixes masks' broken audio intervals.\nDisabling this is useful if you use a voice mimicking mod. (Skinwalkers, Mirage, etc.)");

            configBetterMimicSteps = Config.Bind(
                "Misc",
                "BetterMimicSteps",
                false,
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

            // migrate from previous version if necessary
            if (configFixMasks.Value)
            {
                bool dontFixMasks = Config.Bind("Misc", "DontFixMasks", false, "Legacy setting, use \"FixMasks\" instead").Value;
                if (dontFixMasks)
                    configFixMasks.Value = false;
                Config.Remove(Config["Misc", "DontFixMasks"].Definition);
                Config.Save();
            }

            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }
    }
}