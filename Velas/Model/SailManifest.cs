using System;
using System.Runtime.Serialization;

namespace Velas.Model
{
    /// <summary>Plain-data mirror of manifest.json, deserialized by the .NET data-contract JSON serializer.
    /// Kept separate from <see cref="SailDefinition"/> so the wire format can evolve (new
    /// optional fields) without touching the type the rest of the mod works with.</summary>
    [Serializable]
    [DataContract]
    public class SailManifestEntry
    {
        [DataMember(Name = "id")]
        public string id;
        [DataMember(Name = "name")]
        public string name;
        [DataMember(Name = "file")]
        public string file;
        [DataMember(Name = "clan")]
        public string clan;
        [DataMember(Name = "clanDefault")]
        public bool clanDefault;
        [DataMember(Name = "sha256")]
        public string sha256;
    }

    [Serializable]
    [DataContract]
    public class SailManifestDocument
    {
        [DataMember(Name = "formatVersion")]
        public int formatVersion = 1;
        [DataMember(Name = "sails")]
        public SailManifestEntry[] sails = Array.Empty<SailManifestEntry>();
    }
}
