using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;
using Velas.Debug;
using Velas.Manager;
using Velas.UI;
using Velas.Utility;

namespace Velas
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.deadheim.velas";
        public const string Name = "Velas";
        public const string Version = "0.2.1";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private GameObject _root;
        private bool _sailManagerInitialized;
        private bool _repositoryRefreshQueued;

        private static ConfigSync _configSync;

        private void Awake()
        {
            Log = Logger;

            _configSync = new ConfigSync(Guid)
            {
                DisplayName = Name,
                CurrentVersion = Version,
                MinimumRequiredVersion = Version,
                ModRequired = true,
            };

            SailConfig.Bind(Config, _configSync);
            SailConfig.Enabled.SettingChanged += OnRepositorySettingChanged;
            SailConfig.SailsRepositoryUrl.SettingChanged += OnRepositorySettingChanged;
            SailConfig.EnableRemoteSails.SettingChanged += OnRepositorySettingChanged;

            var pluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            EnsureSailManagerInitialized(pluginDir);

            _root = new GameObject("VelasRoot");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.AddComponent<SailInputController>();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            SailDebugTools.RegisterAll();

            Log.LogInfo($"[Sails] {Name} {Version} loaded.");
        }

        private void EnsureSailManagerInitialized(string pluginDir = null)
        {
            if (_sailManagerInitialized || !SailConfig.Enabled.Value) return;
            SailManager.Initialize(pluginDir ?? (Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath));
            _sailManagerInitialized = true;
        }

        private void OnRepositorySettingChanged(object sender, System.EventArgs e)
        {
            if (_repositoryRefreshQueued) return;
            _repositoryRefreshQueued = true;
            MainThreadDispatcher.Enqueue(() =>
            {
                _repositoryRefreshQueued = false;
                if (!SailConfig.Enabled.Value) return;

                if (!_sailManagerInitialized)
                    EnsureSailManagerInitialized();
                else
                    SailManager.RefreshRemoteSails(forceRefresh: true);
            });
        }

        private void Update()
        {
            MainThreadDispatcher.Pump();
        }

        private void OnDestroy()
        {
            if (SailConfig.Enabled != null)
                SailConfig.Enabled.SettingChanged -= OnRepositorySettingChanged;
            if (SailConfig.SailsRepositoryUrl != null)
                SailConfig.SailsRepositoryUrl.SettingChanged -= OnRepositorySettingChanged;
            if (SailConfig.EnableRemoteSails != null)
                SailConfig.EnableRemoteSails.SettingChanged -= OnRepositorySettingChanged;
            _harmony?.UnpatchSelf();
        }
    }
}
