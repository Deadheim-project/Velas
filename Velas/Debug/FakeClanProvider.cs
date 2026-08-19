using Velas.Clans;

namespace Velas.Debug
{
    /// <summary>Test-only IClanProvider that reports a fixed clan for every player,
    /// regardless of whether the real Guilds mod is installed. Lets clan behavior (automatic
    /// sail, permission checks) be exercised without a second real player or the Guilds mod
    /// -- swapped in/out only through SailDebugTools, never touched by normal mod code.</summary>
    internal sealed class FakeClanProvider : IClanProvider
    {
        private readonly string _clan;

        public FakeClanProvider(string clan) => _clan = clan;

        public bool IsAvailable => true;
        public string GetPlayerClan(long senderId) => _clan;
    }
}
