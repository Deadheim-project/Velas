namespace Velas.Ships
{
    /// <summary>ZDO custom-data keys used on a ship's own ZDO. Only the sail id is persisted
    /// (never the image/texture) -- the client resolves the id to a texture through
    /// SailManager, exactly per spec section 8.</summary>
    internal static class SailZdoKeys
    {
        public const string SailId = "DHS_SailId";
        public const string Initialized = "DHS_Init";
    }
}
