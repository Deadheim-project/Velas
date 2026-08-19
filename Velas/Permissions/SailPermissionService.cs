using Velas.Clans;
using Velas.Model;

namespace Velas.Permissions
{
    internal enum SailDenyReason
    {
        None,
        SailUnknown,
        WrongClan,
        NoClan,
        ClanSystemUnavailable,
    }

    internal readonly struct SailPermissionResult
    {
        public readonly bool Allowed;
        public readonly SailDenyReason Reason;

        public SailPermissionResult(bool allowed, SailDenyReason reason)
        {
            Allowed = allowed;
            Reason = reason;
        }

        public static readonly SailPermissionResult Ok = new SailPermissionResult(true, SailDenyReason.None);

        public string Describe(SailDefinition sail) => Reason switch
        {
            SailDenyReason.None => "allowed",
            SailDenyReason.SailUnknown => "sail is not known to this client/server",
            SailDenyReason.WrongClan => $"sail belongs to clan '{sail?.ClanId}', player is not a member",
            SailDenyReason.NoClan => $"sail belongs to clan '{sail?.ClanId}', player has no clan",
            SailDenyReason.ClanSystemUnavailable => "clan system unavailable, but EnableClanSails is on",
            _ => "unknown",
        };
    }

    /// <summary>
    /// The single place that decides "can this player use this sail". Both the UI (to grey
    /// out options) and the ship's RPC handler (the actual authority) call this -- the RPC
    /// handler's answer is the one that matters, since a client could otherwise be tricked or
    /// modified into showing a locked sail as available.
    /// </summary>
    internal static class SailPermissionService
    {
        /// <summary>Convenience for client-side UI checks (greying out locked sails) --
        /// the authoritative check still happens server/owner-side in
        /// ShipSailComponent.RPC_RequestSetSail using the real RPC sender id.</summary>
        public static SailPermissionResult CanUseLocal(SailDefinition sail)
        {
            long localId = Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;
            return CanUse(sail, localId);
        }

        public static SailPermissionResult CanUse(SailDefinition sail, long senderId)
        {
            if (sail == null) return new SailPermissionResult(false, SailDenyReason.SailUnknown);
            if (sail.IsPublic) return SailPermissionResult.Ok;

            if (!SailConfig.EnableClanSails.Value) return SailPermissionResult.Ok;

            var provider = ClanProvider.Current;
            if (provider == null || !provider.IsAvailable)
                return new SailPermissionResult(false, SailDenyReason.ClanSystemUnavailable);

            var playerClan = provider.GetPlayerClan(senderId);
            if (string.IsNullOrEmpty(playerClan))
                return new SailPermissionResult(false, SailDenyReason.NoClan);

            if (!string.Equals(playerClan, sail.ClanId, System.StringComparison.OrdinalIgnoreCase))
                return new SailPermissionResult(false, SailDenyReason.WrongClan);

            return SailPermissionResult.Ok;
        }
    }
}
