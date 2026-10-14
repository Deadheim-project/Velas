using System;
using System.Linq;
using System.Reflection;
using Velas.Game;

namespace Velas.Integration
{
    /// <summary>
    /// Soft integration with Detalhes.VipList (VipList.dll), resolved entirely through
    /// reflection so this mod has no compile- or load-time dependency on it -- mirrors
    /// Velas.Clans.GuildsClanProvider (same pattern, same rationale: clan features keep
    /// working on installs that run Guilds without the other mod; VIP-gated sails degrade to
    /// "nobody is VIP" cleanly when VipList is missing, rather than failing to load).
    ///
    /// Mirrors NpcValheim/UI/VipShopShortcut.SenderIsVip: a remote sender is checked by
    /// platform user id (VipListApi.IsVip(string)); this process' own player/host has no
    /// peer/socket to read a platform id from, so it falls back to
    /// VipListApi.IsLocalPlayerVip() instead, exactly like the NpcValheim check does.
    /// </summary>
    internal static class VipProvider
    {
        private const string AssemblyName = "VipList";
        private const string ApiTypeName = "VipList.VipListApi";

        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private static bool _resolved;
        private static MethodInfo _isVipByPlatformId;
        private static MethodInfo _isLocalPlayerVip;

        /// <summary>Whether the VipList API was found and bound. False means every
        /// VipOnlySail check below returns "not VIP" -- see SailPermissionService, which
        /// still lets server admins through regardless.</summary>
        public static bool IsAvailable
        {
            get { Resolve(); return _isVipByPlatformId != null; }
        }

        /// <summary>Whether the player behind <paramref name="senderId"/> (an RPC sender id,
        /// or -- for the client-side convenience check -- the local player's own id) is on
        /// the VIP list.</summary>
        public static bool IsVip(long senderId)
        {
            Resolve();
            if (_isVipByPlatformId == null) return false;

            try
            {
                if (ValheimApi.IsLocalSender(senderId))
                    return InvokeIsLocalPlayerVip();

                var platformId = ValheimApi.GetPlatformUserId(senderId);
                if (!string.IsNullOrEmpty(platformId) &&
                    (bool)_isVipByPlatformId.Invoke(null, new object[] { platformId }))
                    return true;

                // Hosted server's own local player: the RPC id resolves fine via IsLocalSender
                // above in the common case, but a peer-based lookup can still miss it on some
                // listen-server topologies -- fall back the same way NpcValheim's
                // VipShopShortcut.SenderIsVip does.
                return ZNet.instance != null && ZNet.instance.IsServer() &&
                       senderId == ValheimApi.LocalRpcSenderId() && InvokeIsLocalPlayerVip();
            }
            catch (Exception e)
            {
                SailLog.Warn($"VipList.VipListApi.IsVip failed for sender {senderId}: {e.Message}");
                return false;
            }
        }

        private static bool InvokeIsLocalPlayerVip()
        {
            if (_isLocalPlayerVip == null) return false;
            try { return (bool)_isLocalPlayerVip.Invoke(null, Array.Empty<object>()); }
            catch (Exception e)
            {
                SailLog.Warn($"VipList.VipListApi.IsLocalPlayerVip failed: {e.Message}");
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
                    SailLog.Info("VipList mod not installed -- VipOnlySails will deny everyone except admins.");
                    return;
                }

                var apiType = assembly.GetType(ApiTypeName);
                if (apiType == null)
                {
                    SailLog.Warn("VipList assembly found but VipList.VipListApi type is missing; VIP integration disabled.");
                    return;
                }

                _isVipByPlatformId = apiType.GetMethods(AnyStatic)
                    .FirstOrDefault(m => m.Name == "IsVip" && m.GetParameters().Length == 1
                                         && m.GetParameters()[0].ParameterType == typeof(string));
                _isLocalPlayerVip = apiType.GetMethod("IsLocalPlayerVip", AnyStatic, null, Type.EmptyTypes, null);

                SailLog.Info($"VipList integration ready (IsVip={_isVipByPlatformId != null}, IsLocalPlayerVip={_isLocalPlayerVip != null})");
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not bind the VipList API: {e.Message}");
            }
        }
    }
}
