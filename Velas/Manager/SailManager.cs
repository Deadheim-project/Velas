using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Velas.Cache;
using Velas.Model;
using Velas.Repository;
using Velas.Rendering;
using Velas.Utility;

namespace Velas.Manager
{
    internal enum RemoteSailsState
    {
        NotLoaded,
        Loading,
        Loaded,
        Unavailable,
    }

    /// <summary>
    /// Registry of every sail the mod currently knows about (generic + remote) and their
    /// resolved textures. This is the one place that owns the "SailId -> Texture2D" mapping;
    /// everything else (UI, ShipSailController, permissions) asks it questions instead of
    /// touching the cache/repository/loader directly.
    /// </summary>
    internal static class SailManager
    {
        private static readonly Dictionary<string, SailDefinition> Sails = new Dictionary<string, SailDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> InFlight = new HashSet<string>();
        private static readonly HashSet<string> WarnedMissing = new HashSet<string>();

        public static RemoteSailsState RemoteState { get; private set; } = RemoteSailsState.NotLoaded;
        public static event Action SailsChanged;

        public static IReadOnlyList<SailDefinition> AllSails => Sails.Values.ToList();

        public static SailDefinition Get(string sailId) =>
            !string.IsNullOrEmpty(sailId) && Sails.TryGetValue(sailId, out var def) ? def : null;

        /// <summary>Call once from Plugin.Awake. Loads the bundled generic sails synchronously
        /// (must always succeed / be instant -- offline play depends on it) and kicks off the
        /// remote manifest fetch in the background.</summary>
        public static void Initialize(string pluginDirectory)
        {
            SailLog.Debug("Loading local sails");
            LoadGenericSails(pluginDirectory);

            if (SailConfig.EnableRemoteSails.Value)
                RefreshRemoteSails();
            else
                RemoteState = RemoteSailsState.Unavailable;
        }

        public static void RefreshRemoteSails(bool forceRefresh = false)
        {
            if (!SailConfig.EnableRemoteSails.Value)
            {
                RemoteState = RemoteSailsState.Unavailable;
                return;
            }

            RemoteState = RemoteSailsState.Loading;
            _ = RefreshRemoteSailsAsync(forceRefresh);
        }

        private static void LoadGenericSails(string pluginDirectory)
        {
            try
            {
                var dir = Path.Combine(pluginDirectory, "Assets", "GenericSails");
                if (!Directory.Exists(dir))
                {
                    SailLog.Warn($"generic sails folder missing: {dir}");
                    return;
                }

                foreach (var file in Directory.GetFiles(dir, "*.png").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var id = "generic_" + Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    var bytes = File.ReadAllBytes(file);
                    if (!SailTextureLoader.TryLoad(bytes, id, out var tex))
                    {
                        SailLog.Warn($"bundled generic sail '{file}' failed to load -- shipping asset problem");
                        continue;
                    }

                    var def = new SailDefinition
                    {
                        Id = id,
                        DisplayName = Prettify(Path.GetFileNameWithoutExtension(file)),
                        Source = Model.SailSource.Generic,
                        ImageFile = file,
                    };
                    Sails[id] = def;
                    Textures[id] = tex;
                }

                SailLog.Info($"Loaded {Sails.Count} generic sail(s)");
                SailsChanged?.Invoke();
            }
            catch (Exception e)
            {
                SailLog.Error($"failed loading generic sails: {e.Message}");
            }
        }

        private static string Prettify(string fileName)
        {
            var parts = fileName.Replace('_', ' ').Split(' ');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }

        private static async Task RefreshRemoteSailsAsync(bool forceRefresh)
        {
            var manifest = await SailRepository.FetchManifestAsync(forceRefresh).ConfigureAwait(false);
            if (manifest == null)
            {
                MainThreadDispatcher.Enqueue(() =>
                {
                    RemoteState = RemoteSailsState.Unavailable;
                    SailLog.Info("Remote sails unavailable this session (offline or repository unreachable) -- generic sails still work.");
                    SailsChanged?.Invoke();
                });
                return;
            }

            var remoteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tasks = new List<Task>();
            foreach (var entry in manifest.sails)
            {
                var id = SailCache.SanitizeId(entry.id);
                if (id == null || !string.Equals(id, entry.id, StringComparison.Ordinal))
                {
                    SailLog.Warn($"manifest entry id '{entry.id}' rejected (must be alnum/underscore/hyphen)");
                    continue;
                }
                remoteIds.Add(id);
                tasks.Add(LoadRemoteSailAsync(entry));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            MainThreadDispatcher.Enqueue(() =>
            {
                // A sail that vanished from the manifest is de-listed from the selector, but
                // we deliberately leave any already-loaded texture in place: a ship that still
                // references it keeps rendering instead of popping to a fallback the moment
                // someone edits the repository.
                var removed = Sails.Values
                    .Where(s => s.Source == Model.SailSource.Remote && !remoteIds.Contains(s.Id))
                    .Select(s => s.Id).ToList();
                foreach (var id in removed)
                {
                    SailLog.Info($"sail '{id}' is no longer in the repository manifest -- removed from selector");
                    Sails.Remove(id);
                }

                RemoteState = RemoteSailsState.Loaded;
                SailLog.Info($"Remote sails ready ({remoteIds.Count} from repository)");
                SailsChanged?.Invoke();
            });
        }

        private static async Task LoadRemoteSailAsync(SailManifestEntry entry)
        {
            var bytes = await SailRepository.DownloadImageAsync(entry).ConfigureAwait(false);
            if (bytes == null) return; // already logged by the repository

            MainThreadDispatcher.Enqueue(() =>
            {
                if (!SailTextureLoader.TryLoad(bytes, entry.id, out var tex)) return;

                if (Textures.TryGetValue(entry.id, out var old) && old != null)
                    UnityEngine.Object.Destroy(old);

                Sails[entry.id] = new SailDefinition
                {
                    Id = entry.id,
                    DisplayName = string.IsNullOrWhiteSpace(entry.name) ? entry.id : entry.name,
                    Source = Model.SailSource.Remote,
                    ClanId = entry.clan,
                    IsClanDefaultSail = entry.clanDefault,
                    ImageFile = entry.file,
                    Sha256 = entry.sha256,
                };
                Textures[entry.id] = tex;
                SailsChanged?.Invoke();
            });
        }

        /// <summary>Resolves the texture for a SailId that may belong to a ship built before
        /// this session started (or before a sail was ever fetched). Tries the in-memory
        /// cache, then falls back to whatever is on disk, then to the "plain sail" generic as
        /// a last resort so a missing/removed/corrupt sail never leaves the mesh blank.</summary>
        public static Texture2D ResolveTexture(string sailId)
        {
            if (string.IsNullOrEmpty(sailId)) return null;
            if (Textures.TryGetValue(sailId, out var tex) && tex != null) return tex;

            if (SailCache.TryReadImage(sailId, out var cached) && SailTextureLoader.TryLoad(cached, sailId, out var loaded))
            {
                Textures[sailId] = loaded;
                return loaded;
            }

            if (WarnedMissing.Add(sailId))
                SailLog.Warn($"sail '{sailId}' has no available texture (removed/never downloaded) -- falling back to plain sail");

            return Textures.TryGetValue("generic_plain_sail", out var fallback) ? fallback : null;
        }
    }
}
