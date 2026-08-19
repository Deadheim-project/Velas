using System;
using System.Linq;
using System.Reflection;
using Velas.Game;

namespace Velas.Clans
{
    /// <summary>
    /// Soft integration with blaxxun-boop's "Guilds" mod (org.bepinex.plugins.guilds),
    /// resolved entirely through reflection so this mod has no compile- or load-time
    /// dependency on it -- mirrors NpcValheim/Integration/EpicMmoApi.cs.
    ///
    /// We talk to Guilds.API directly rather than to RaidSystem's GuildsIntegration wrapper:
    /// RaidSystem itself only *consumes* Guilds (a hard BepInDependency on it), it does not
    /// re-expose an API of its own for other mods to call. Depending on Guilds directly means
    /// this mod's clan features keep working even on installs that run Guilds without
    /// RaidSystem, and degrade to "no clans" cleanly when neither is present.
    /// </summary>
    internal sealed class GuildsClanProvider : IClanProvider
    {
        private const string AssemblyName = "Guilds";
        private const string ApiTypeName = "Guilds.API";

        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private bool _resolved;
        private MethodInfo _isLoaded;
        private MethodInfo _getPlayerGuild;
        private PropertyInfo _guildName;

        public bool IsAvailable
        {
            get
            {
                Resolve();
                if (_isLoaded == null) return false;
                try { return (bool)_isLoaded.Invoke(null, Array.Empty<object>()); }
                catch (Exception e)
                {
                    SailLog.Warn($"Guilds.API.IsLoaded() threw: {e.Message}");
                    return false;
                }
            }
        }

        public string GetPlayerClan(long senderId)
        {
            Resolve();
            if (_getPlayerGuild == null || _guildName == null) return null;
            if (!IsAvailable) return null;

            var player = ValheimApi.FindOnlinePlayer(senderId);
            if (player == null) return null;

            try
            {
                var guild = _getPlayerGuild.Invoke(null, new object[] { player });
                if (guild == null) return null;
                return _guildName.GetValue(guild) as string;
            }
            catch (Exception e)
            {
                SailLog.Warn($"Guilds.API.GetPlayerGuild failed for sender {senderId}: {e.Message}");
                return null;
            }
        }

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == AssemblyName);
                if (assembly == null)
                {
                    SailLog.Info("Guilds mod not installed -- clan sails will be treated as public for everyone.");
                    return;
                }

                var apiType = assembly.GetType(ApiTypeName);
                if (apiType == null)
                {
                    SailLog.Warn("Guilds assembly found but Guilds.API type is missing; clan integration disabled.");
                    return;
                }

                _isLoaded = apiType.GetMethod("IsLoaded", AnyStatic, null, Type.EmptyTypes, null);
                _getPlayerGuild = apiType.GetMethods(AnyStatic)
                    .FirstOrDefault(m => m.Name == "GetPlayerGuild" && m.GetParameters().Length == 1
                                          && m.GetParameters()[0].ParameterType == typeof(Player));

                var guildType = _getPlayerGuild?.ReturnType;
                _guildName = guildType?.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                             ?? guildType?.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);

                SailLog.Info($"Guilds integration ready (IsLoaded={_isLoaded != null}, GetPlayerGuild={_getPlayerGuild != null}, Name={_guildName != null})");
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not bind the Guilds API: {e.Message}");
            }
        }
    }
}
