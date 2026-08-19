namespace Velas.Clans
{
    /// <summary>
    /// Player -> Clan abstraction. SailPermissionService and the automatic-clan-sail logic
    /// only ever talk to this interface, never to a specific clan mod's API directly -- that
    /// is what lets the underlying clan system be swapped later without touching permission
    /// or ship logic. See GuildsClanProvider for the concrete implementation used today.
    /// </summary>
    internal interface IClanProvider
    {
        /// <summary>Whether a real clan system is available right now. False (e.g. the
        /// Guilds mod isn't installed) makes every clan-restricted sail behave as if
        /// SailConfig.EnableClanSails were off for that lookup.</summary>
        bool IsAvailable { get; }

        /// <summary>Clan/guild name of the player behind this RPC sender id, or null if they
        /// belong to no clan. Never key this off a display name -- see GuildsClanProvider.</summary>
        string GetPlayerClan(long senderId);
    }
}
