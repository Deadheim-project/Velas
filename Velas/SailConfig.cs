using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace Velas
{
    /// <summary>All player/server-facing configuration in one place. Server-controlled
    /// values are registered with Blaxxun's ServerSync; local input/debug preferences stay
    /// on each client. Individual ship choices continue to synchronize through their ZDO.</summary>
    internal static class SailConfig
    {
        public static ConfigEntry<bool> LockConfiguration;
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<KeyCode> OpenSailSelectorKey;
        public static ConfigEntry<string> SailsRepositoryUrl;
        public static ConfigEntry<bool> EnableRemoteSails;
        public static ConfigEntry<bool> EnableSailCache;
        public static ConfigEntry<bool> EnableClanSails;
        public static ConfigEntry<bool> EnableAutomaticClanSail;
        public static ConfigEntry<bool> DebugMode;

        public static ConfigEntry<float> MaxInteractionDistance;
        public static ConfigEntry<int> ManifestTimeoutSeconds;
        public static ConfigEntry<int> DownloadTimeoutSeconds;
        public static ConfigEntry<int> MaxImageSizeKb;
        public static ConfigEntry<int> MaxImageDimension;
        public static ConfigEntry<int> CacheRefreshMinutes;

        public static void Bind(ConfigFile config, ConfigSync configSync)
        {
            LockConfiguration = config.Bind("ServerSync", "LockConfiguration", true,
                "When enabled on the server, synchronized settings cannot be changed by clients.");
            configSync.AddLockingConfigEntry(LockConfiguration);

            Enabled = BindSynced(config, configSync, "General", "Enabled", true,
                "Master switch. When false the mod does not patch ships, open the UI, or contact the repository.");

            OpenSailSelectorKey = BindLocal(config, "General", "OpenSailSelectorKey", KeyCode.G,
                "Key that opens the sail selector while near/interacting with a ship.");

            MaxInteractionDistance = BindSynced(config, configSync, "General", "MaxInteractionDistance", 10f,
                "Max distance (meters) from the player to a ship for the selector to open, so a distant ship can never be changed by accident.");

            SailsRepositoryUrl = BindSynced(config, configSync, "Repository", "SailsRepositoryUrl",
                "https://github.com/Deadheim-project/repositorio-das-velas",
                "GitHub repository that hosts manifest.json and the custom/clan sail images. Never hardcode this elsewhere -- always read it from here.");

            EnableRemoteSails = BindSynced(config, configSync, "Repository", "EnableRemoteSails", true,
                "Fetch and offer custom sails from SailsRepositoryUrl. When false, only the bundled generic sails are available.");

            EnableSailCache = BindSynced(config, configSync, "Repository", "EnableSailCache", true,
                "Cache downloaded manifest/images on disk so the selector does not re-download every time it opens.");

            CacheRefreshMinutes = BindSynced(config, configSync, "Repository", "CacheRefreshMinutes", 30,
                "Minutes a cached manifest is trusted before refetching from GitHub. Individual images are cached by content hash and are not affected by this.");

            ManifestTimeoutSeconds = BindSynced(config, configSync, "Repository", "ManifestTimeoutSeconds", 8,
                "HTTP timeout for the manifest request.");

            DownloadTimeoutSeconds = BindSynced(config, configSync, "Repository", "DownloadTimeoutSeconds", 15,
                "HTTP timeout per image download.");

            MaxImageSizeKb = BindSynced(config, configSync, "Repository", "MaxImageSizeKb", 2048,
                "Reject any remote image larger than this (KB). Protects against a misbehaving or malicious repository filling the cache disk.");

            MaxImageDimension = BindSynced(config, configSync, "Repository", "MaxImageDimension", 2048,
                "Reject any remote image wider or taller than this many pixels.");

            EnableClanSails = BindSynced(config, configSync, "Clans", "EnableClanSails", true,
                "Enforce clan ownership on sails that declare one. When false, clan-restricted sails are treated as public (useful for singleplayer/testing without a clan mod installed).");

            EnableAutomaticClanSail = BindSynced(config, configSync, "Clans", "EnableAutomaticClanSail", true,
                "When a clan member builds a new ship, automatically apply that clan's default sail (if one is configured in the manifest).");

            DebugMode = BindLocal(config, "Debug", "DebugMode", false,
                "Verbose [Sails] logging for repository fetches, cache hits, permission checks and sync. Also unlocks the dev commands in SailDebugTools.");
        }

        private static ConfigEntry<T> BindSynced<T>(ConfigFile config, ConfigSync configSync,
            string section, string key, T defaultValue, string description)
        {
            var entry = config.Bind(section, key, defaultValue, description);
            configSync.AddConfigEntry(entry).SynchronizedConfig = true;
            return entry;
        }

        private static ConfigEntry<T> BindLocal<T>(ConfigFile config, string section,
            string key, T defaultValue, string description) =>
            config.Bind(section, key, defaultValue, description);
    }
}
