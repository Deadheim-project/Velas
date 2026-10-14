using System;
using System.Linq;
using System.Reflection;
using Velas.Game;

namespace Velas.Integration
{
    /// <summary>
    /// Connection point to the Deadheim per-character cosmetic registry (shared plan, section
    /// "Base comum" -- confirmed 14/10/2026 by the Deadheim session: Deadheim.CosmeticosApi,
    /// Version 1, same reflection scheme as Pvp/PvpApi and TreinadorApi). Resolved entirely
    /// through reflection, no compile-time reference to Deadheim.dll -- same pattern as
    /// Clans/GuildsClanProvider.cs and VipProvider.cs.
    ///
    /// Server/API shape: Grant(long playerId, string key) and Has(long playerId, string key)
    /// are server-authoritative, backed by a per-world file only the server (or whoever hosts
    /// it) ever loads; LocalHas(string key)/LocalKeys() answer for the local player's own
    /// synced copy and work correctly on any client. SailPermissionService.CanUse passes the
    /// same character/player id already used for admin/VIP checks, so the only case that
    /// behaves differently here is: a ship whose ZDO owner is a non-host client checking a
    /// *remote* player's cosmetic via Has() can get a false "not unlocked" (the owning
    /// client's own file has no entry for someone else) -- same class of limitation as
    /// ValheimApi.IsAdmin's non-server branch. There is no broadcast of granted keys (unlike
    /// VipList's ServerSync'd list), so this cannot be fixed purely on the Velas side; fine for
    /// now since fail-closed on a false negative only means the sail stays locked a bit longer,
    /// never that it leaks open.
    ///
    /// Still fails closed if the assembly/type/methods ever go missing or change shape again:
    /// every cosmetic-gated sail stays locked for everyone rather than becoming public.
    /// </summary>
    internal static class CosmeticUnlockProvider
    {
        private const string AssemblyName = "Deadheim";
        private const string ApiTypeName = "Deadheim.CosmeticosApi";

        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static bool _resolved;
        private static MethodInfo _has;      // static bool Has(long playerId, string key)
        private static MethodInfo _localHas; // static bool LocalHas(string key)

        /// <summary>Whether the registry API was found and bound. False means every
        /// cosmetic-gated sail is locked for everyone (fail closed) -- see the class doc.</summary>
        public static bool IsAvailable
        {
            get { Resolve(); return _has != null; }
        }

        /// <summary>Whether <paramref name="characterId"/> (the same character/player id
        /// SailPermissionService already uses for admin/VIP checks) has unlocked the cosmetic
        /// named <paramref name="cosmeticId"/> (e.g. "titulo:fundador", "sail:halloween_abobora" --
        /// format is the free "categoria:nome" text the Deadheim session documented).
        ///
        /// For the local player, prefers LocalHas (always accurate, synced copy) over Has
        /// (server-authoritative, but only reliable when this process is the host) -- see the
        /// class doc for why.</summary>
        public static bool IsUnlocked(long characterId, string cosmeticId)
        {
            Resolve();
            if (string.IsNullOrEmpty(cosmeticId)) return false;

            try
            {
                if (ValheimApi.IsLocalSender(characterId) && _localHas != null)
                    return _localHas.Invoke(null, new object[] { cosmeticId }) is true;

                if (_has == null) return false;
                return _has.Invoke(null, new object[] { characterId, cosmeticId }) is true;
            }
            catch (Exception e)
            {
                SailLog.Warn($"cosmetic registry lookup failed for '{cosmeticId}' (character {characterId}): {e.Message}");
                return false;
            }
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == AssemblyName);
                if (assembly == null)
                {
                    SailLog.Warn($"assembly '{AssemblyName}' not loaded -- cosmetic registry unavailable this session.");
                    return;
                }

                var apiType = assembly.GetType(ApiTypeName);
                if (apiType == null)
                {
                    SailLog.Warn($"type '{ApiTypeName}' not found in '{AssemblyName}'; cosmetic registry integration disabled.");
                    return;
                }

                _has = apiType.GetMethods(AnyStatic)
                    .FirstOrDefault(m => m.Name == "Has" && m.GetParameters().Length == 2
                                          && m.GetParameters()[0].ParameterType == typeof(long)
                                          && m.GetParameters()[1].ParameterType == typeof(string));
                _localHas = apiType.GetMethods(AnyStatic)
                    .FirstOrDefault(m => m.Name == "LocalHas" && m.GetParameters().Length == 1
                                          && m.GetParameters()[0].ParameterType == typeof(string));

                SailLog.Info($"Cosmetic registry integration ready (Has={_has != null}, LocalHas={_localHas != null})");
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not bind the cosmetic registry API: {e.Message}");
            }
        }
    }
}
