namespace Velas.Model
{
    /// <summary>
    /// One sail the player can pick, resolved from either the bundled generic set or the
    /// remote manifest. This is the shape everything downstream (permissions, UI, the
    /// controller that paints a ship) works with -- neither knows or cares whether the
    /// texture came from disk or from GitHub.
    /// </summary>
    public class SailDefinition
    {
        /// <summary>Stable id persisted on ships (e.g. "clan_deadheim_01"). Never the display
        /// name -- renames in the manifest must not orphan ships that already picked it.</summary>
        public string Id;

        public string DisplayName;
        public SailSource Source;

        /// <summary>Null/empty = public, usable by anyone. Otherwise the clan/guild name that
        /// owns this sail; see <see cref="Permissions.SailPermissionService"/>.</summary>
        public string ClanId;

        /// <summary>When true, ships built by a member of <see cref="ClanId"/> get this sail
        /// automatically. At most one automatic sail should be marked per clan; the manager
        /// picks the first match if more than one is (see SailManager.GetAutomaticClanSail).</summary>
        public bool IsClanDefaultSail;

        /// <summary>For remote sails: relative file path inside the repository (from the
        /// manifest). For generic sails: the file name under Assets/GenericSails.</summary>
        public string ImageFile;

        /// <summary>SHA-256 of the image file, lowercase hex, optional. When the manifest
        /// supplies it we can validate the cached/downloaded bytes and detect corruption
        /// without re-hitting GitHub.</summary>
        public string Sha256;

        public bool IsPublic => string.IsNullOrEmpty(ClanId);

        public override string ToString() => $"{Id} ({Source}{(IsPublic ? "" : $", clan={ClanId}")})";
    }
}
