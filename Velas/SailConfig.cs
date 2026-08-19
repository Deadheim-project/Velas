using BepInEx.Configuration;
using UnityEngine;

namespace Velas
{
    /// <summary>All player/server-facing configuration in one place, bound the same way
    /// NpcValheim does it (direct BepInEx.Configuration.Config.Bind, no wrapper). Nothing
    /// here goes through ConfigSync -- ship-sail choices are per-object (in the ship's own
    /// ZDO), not global settings that need to match between server and client.</summary>
    internal static class SailConfig
    {
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

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true,
                "Master switch. When false the mod does not patch ships, open the UI, or contact the repository.");

            OpenSailSelectorKey = config.Bind("General", "OpenSailSelectorKey", KeyCode.G,
                "Key that opens the sail selector while near/interacting with a ship.");

            MaxInteractionDistance = config.Bind("General", "MaxInteractionDistance", 10f,
                "Max distance (meters) from the player to a ship for the selector to open, so a distant ship can never be changed by accident.");

            SailsRepositoryUrl = config.Bind("Repository", "SailsRepositoryUrl",
                "https://github.com/Deadheim-project/repositorio-das-velas",
                "GitHub repository that hosts manifest.json and the custom/clan sail images. Never hardcode this elsewhere -- always read it from here.");

            EnableRemoteSails = config.Bind("Repository", "EnableRemoteSails", true,
                "Fetch and offer custom sails from SailsRepositoryUrl. When false, only the bundled generic sails are available.");

            EnableSailCache = config.Bind("Repository", "EnableSailCache", true,
                "Cache downloaded manifest/images on disk so the selector does not re-download every time it opens.");

            CacheRefreshMinutes = config.Bind("Repository", "CacheRefreshMinutes", 30,
                "Minutes a cached manifest is trusted before refetching from GitHub. Individual images are cached by content hash and are not affected by this.");

            ManifestTimeoutSeconds = config.Bind("Repository", "ManifestTimeoutSeconds", 8,
                "HTTP timeout for the manifest request.");

            DownloadTimeoutSeconds = config.Bind("Repository", "DownloadTimeoutSeconds", 15,
                "HTTP timeout per image download.");

            MaxImageSizeKb = config.Bind("Repository", "MaxImageSizeKb", 2048,
                "Reject any remote image larger than this (KB). Protects against a misbehaving or malicious repository filling the cache disk.");

            MaxImageDimension = config.Bind("Repository", "MaxImageDimension", 2048,
                "Reject any remote image wider or taller than this many pixels.");

            EnableClanSails = config.Bind("Clans", "EnableClanSails", true,
                "Enforce clan ownership on sails that declare one. When false, clan-restricted sails are treated as public (useful for singleplayer/testing without a clan mod installed).");

            EnableAutomaticClanSail = config.Bind("Clans", "EnableAutomaticClanSail", true,
                "When a clan member builds a new ship, automatically apply that clan's default sail (if one is configured in the manifest).");

            DebugMode = config.Bind("Debug", "DebugMode", false,
                "Verbose [Sails] logging for repository fetches, cache hits, permission checks and sync. Also unlocks the dev commands in SailDebugTools.");
        }
    }
}
