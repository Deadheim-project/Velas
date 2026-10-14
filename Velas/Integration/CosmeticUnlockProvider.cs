using System;
using System.Linq;
using System.Reflection;

namespace Velas.Integration
{
    /// <summary>
    /// Single connection point to the Deadheim per-character cosmetic registry described in
    /// the shared plan (side-session-notes/.../plano-implementacao.md, section "Base comum"):
    /// one registry of cosmetics unlocked per character, kept on the server -- titles today,
    /// sails among the things it grows into later. The pacote de Halloween (vela "Abóbora")
    /// and the passe de temporada are expected to unlock sails through it.
    ///
    /// As of 2026-10-14 the Deadheim session had not yet published that API's assembly/type/
    /// method names in the plan, so this cannot be wired for real without guessing names that
    /// may not match (the task asked not to invent that API). THIS is the one place to edit
    /// once it is named: fill in ApiTypeName below (and MethodName/AssemblyName if those also
    /// differ) following the same reflection pattern Velas already uses for Guilds
    /// (Clans/GuildsClanProvider.cs) and VipList (VipProvider.cs) -- no compile-time
    /// reference to Deadheim.dll.
    ///
    /// Fails closed: while unresolved (or if the registry answers with anything but a clean
    /// "yes"), every cosmetic-gated sail stays locked for everyone. A sail nobody can prove
    /// they unlocked must never silently become public just because the wiring is missing.
    /// </summary>
    internal static class CosmeticUnlockProvider
    {
        // TODO(sessão Velas, onda 1/2): atualizar quando o plano anotar o nome real.
        // Assinatura esperada (ajuste MethodName/parametros se a API publicada for diferente):
        //   static bool <MethodName>(long characterId, string cosmeticId)
        private const string AssemblyName = "Deadheim";
        private const string ApiTypeName = ""; // ainda não definido pela sessão do Deadheim (14/10)
        private const string MethodName = "IsUnlocked";

        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static bool _resolved;
        private static MethodInfo _isUnlocked;

        /// <summary>Whether the registry API was found and bound. False means every
        /// cosmetic-gated sail is locked for everyone (fail closed) -- see the class doc.</summary>
        public static bool IsAvailable
        {
            get { Resolve(); return _isUnlocked != null; }
        }

        /// <summary>Whether <paramref name="characterId"/> (the same character/player id
        /// SailPermissionService already uses for admin/VIP checks) has unlocked the cosmetic
        /// named <paramref name="cosmeticId"/> (e.g. "halloween_abobora_2026").</summary>
        public static bool IsUnlocked(long characterId, string cosmeticId)
        {
            Resolve();
            if (_isUnlocked == null || string.IsNullOrEmpty(cosmeticId)) return false;

            try
            {
                return _isUnlocked.Invoke(null, new object[] { characterId, cosmeticId }) is true;
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

            if (string.IsNullOrEmpty(ApiTypeName))
            {
                SailLog.Info("Cosmetic registry API ainda não anotada no plano -- velas que exigem cosmético ficam bloqueadas para todos até isso ser ligado (ver Integration/CosmeticUnlockProvider.cs).");
                return;
            }

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

                _isUnlocked = apiType.GetMethods(AnyStatic)
                    .FirstOrDefault(m => m.Name == MethodName && m.GetParameters().Length == 2);

                SailLog.Info($"Cosmetic registry integration ready (IsUnlocked={_isUnlocked != null})");
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not bind the cosmetic registry API: {e.Message}");
            }
        }
    }
}
