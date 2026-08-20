using System.Collections.Generic;

namespace Velas.Model
{
    /// <summary>Plain-data mirror of manifest.json, deserialized by Unity JsonUtility.
    /// Kept separate from <see cref="SailDefinition"/> so the wire format can evolve (new
    /// optional fields) without touching the type the rest of the mod works with.</summary>
    public class SailManifestEntry
    {
        public string id;
        public string name;
        public string file;
        public string clan;
        public bool clanDefault;
        public string sha256;
    }

    public class SailManifestDocument
    {
        public int formatVersion = 1;
        public List<SailManifestEntry> sails = new List<SailManifestEntry>();
    }
}
