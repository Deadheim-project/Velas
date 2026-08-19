namespace Velas.Clans
{
    /// <summary>Process-wide access point for the active IClanProvider. A single static
    /// instance is enough: the mod only ever needs "what clan is this player in", never
    /// per-caller configuration of the provider itself.</summary>
    internal static class ClanProvider
    {
        public static IClanProvider Current { get; private set; } = new GuildsClanProvider();

        /// <summary>Test hook -- lets SailDebugTools swap in a fake provider to simulate
        /// clan membership without needing the Guilds mod or a second real player.</summary>
        public static void SetProvider(IClanProvider provider) => Current = provider ?? new GuildsClanProvider();

        public static void ResetToDefault() => Current = new GuildsClanProvider();
    }
}
