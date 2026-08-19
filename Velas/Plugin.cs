using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
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
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private GameObject _root;

        private void Awake()
        {
            Log = Logger;

            SailConfig.Bind(Config);

            if (!SailConfig.Enabled.Value)
            {
                Log.LogInfo("[Sails] Enabled=false, mod is inert.");
                return;
            }

            var pluginDir = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            SailManager.Initialize(pluginDir);

            _root = new GameObject("VelasRoot");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _root.AddComponent<SailInputController>();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll();

            SailDebugTools.RegisterAll();

            Log.LogInfo($"[Sails] {Name} {Version} loaded.");
        }

        private void Update()
        {
            MainThreadDispatcher.Pump();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}
