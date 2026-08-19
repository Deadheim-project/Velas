using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Velas.Cache;
using Velas.Model;

namespace Velas.Repository
{
    /// <summary>
    /// Talks to the GitHub repository configured in SailConfig.SailsRepositoryUrl:
    /// manifest.json -> per-sail image files. Every network call runs off the calling
    /// thread (Task.Run) so a slow/unreachable GitHub can never freeze the game -- callers
    /// await the Task from a background context and hand results back to SailManager, which
    /// is the only place allowed to touch Unity APIs with them.
    ///
    /// Repository layout this expects (documented for repo maintainers):
    ///   manifest.json          -- see SailManifestDocument / SailManifestEntry
    ///   &lt;file&gt; entries       -- relative paths (e.g. "sails/clan_deadheim_01.png")
    ///                              resolved against the repo root, one branch: "main"
    ///                              (falls back to "master" if "main" 404s).
    /// </summary>
    internal static class SailRepository
    {
        private static readonly HttpClient Http = CreateClient();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        // Only relative, forward-slash paths without ".." segments are accepted from the
        // manifest's "file" field -- this is what stops a malicious manifest from pointing
        // DownloadImageAsync at an arbitrary URL or escaping the repo.
        private static readonly Regex SafeRelativePath = new Regex(@"^(?!\/)(?!.*\.\.)[A-Za-z0-9_\-./]+$", RegexOptions.Compiled);

        private static HttpClient CreateClient()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            }
            catch { /* older runtime without Tls12 enum member; ignore */ }

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Velas-Mod");
            return client;
        }

        private static bool TryParseRepo(string repoUrl, out string owner, out string repo)
        {
            owner = repo = null;
            if (string.IsNullOrWhiteSpace(repoUrl)) return false;
            // Accepts "https://github.com/Owner/Repo" (with or without trailing slash/.git).
            var m = Regex.Match(repoUrl.Trim(), @"github\.com/([^/]+)/([^/]+?)(\.git)?/?$");
            if (!m.Success) return false;
            owner = m.Groups[1].Value;
            repo = m.Groups[2].Value;
            return !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo);
        }

        private static string RawUrl(string owner, string repo, string branch, string path) =>
            $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}";

        /// <summary>Fetches the manifest, preferring a fresh disk cache, falling back to a
        /// stale cache if the network fails, and returning null only when neither is
        /// available -- callers must treat null as "remote sails unavailable this session",
        /// never as an error to crash on.</summary>
        public static async Task<SailManifestDocument> FetchManifestAsync(bool forceRefresh = false)
        {
            if (!SailConfig.EnableRemoteSails.Value)
            {
                SailLog.Debug("remote sails disabled by config, skipping manifest fetch");
                return null;
            }

            if (!TryParseRepo(SailConfig.SailsRepositoryUrl.Value, out var owner, out var repo))
            {
                SailLog.Warn($"SailsRepositoryUrl is not a valid GitHub repo URL: '{SailConfig.SailsRepositoryUrl.Value}'");
                return TryLoadCachedManifest();
            }

            bool cacheFresh = SailConfig.EnableSailCache.Value &&
                               !forceRefresh &&
                               SailCache.ManifestIsFresh(SailConfig.CacheRefreshMinutes.Value);
            if (cacheFresh && SailCache.TryReadManifestText(out var cachedJson))
            {
                var cachedDoc = TryParseManifest(cachedJson);
                if (cachedDoc != null)
                {
                    SailLog.Debug("Loaded sail from cache: manifest (fresh)");
                    return cachedDoc;
                }
            }

            SailLog.Debug($"Fetching repository manifest from {owner}/{repo}");
            try
            {
                var json = await FetchTextWithBranchFallback(owner, repo, "manifest.json",
                    TimeSpan.FromSeconds(Math.Max(1, SailConfig.ManifestTimeoutSeconds.Value)));
                if (json == null) throw new Exception("manifest.json not found on main or master branch");

                var doc = TryParseManifest(json);
                if (doc == null) throw new Exception("manifest.json could not be parsed");

                if (SailConfig.EnableSailCache.Value) SailCache.WriteManifestText(json);
                SailLog.Debug($"Repository manifest loaded: {doc.sails.Count} sail(s)");
                return doc;
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not fetch repository manifest, falling back to cache: {e.Message}");
                return TryLoadCachedManifest();
            }
        }

        private static SailManifestDocument TryLoadCachedManifest()
        {
            if (!SailConfig.EnableSailCache.Value) return null;
            if (!SailCache.TryReadManifestText(out var json)) return null;
            var doc = TryParseManifest(json);
            if (doc != null) SailLog.Debug("Loaded sail from cache: manifest (stale/offline fallback)");
            return doc;
        }

        private static SailManifestDocument TryParseManifest(string json)
        {
            try
            {
                var doc = Json.Deserialize<SailManifestDocument>(json);
                if (doc?.sails == null) return null;
                // Drop entries with no id/file rather than failing the whole manifest --
                // one bad entry from a manually-edited repo should not take every sail down.
                doc.sails = doc.sails
                    .Where(e => e != null && !string.IsNullOrWhiteSpace(e.id) && !string.IsNullOrWhiteSpace(e.file))
                    .ToList();
                return doc;
            }
            catch (Exception e)
            {
                SailLog.Warn($"manifest.json is invalid: {e.Message}");
                return null;
            }
        }

        /// <summary>Downloads (or returns from cache) the raw bytes for one manifest entry.
        /// Validates the relative path before ever building a URL from it.</summary>
        public static async Task<byte[]> DownloadImageAsync(SailManifestEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.file)) return null;

            if (SailConfig.EnableSailCache.Value && SailCache.TryReadImage(entry.id, out var cached))
            {
                if (string.IsNullOrEmpty(entry.sha256) ||
                    string.Equals(SailCache.Sha256Hex(cached), entry.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    SailLog.Debug($"Loaded sail from cache: {entry.id}");
                    return cached;
                }
                SailLog.Debug($"Cached image for '{entry.id}' failed hash check, re-downloading");
            }

            if (!SafeRelativePath.IsMatch(entry.file))
            {
                SailLog.Warn($"sail '{entry.id}': manifest 'file' path rejected (unsafe path): '{entry.file}'");
                return null;
            }

            if (!TryParseRepo(SailConfig.SailsRepositoryUrl.Value, out var owner, out var repo))
                return null;

            SailLog.Debug($"Downloading sail: {entry.id}");
            try
            {
                var bytes = await FetchBytesWithBranchFallback(owner, repo, entry.file,
                    TimeSpan.FromSeconds(Math.Max(1, SailConfig.DownloadTimeoutSeconds.Value)),
                    Math.Max(1, SailConfig.MaxImageSizeKb.Value) * 1024L);
                if (bytes == null)
                {
                    SailLog.Warn($"sail '{entry.id}': image file not found in repository ('{entry.file}')");
                    return null;
                }

                if (!string.IsNullOrEmpty(entry.sha256) &&
                    !string.Equals(SailCache.Sha256Hex(bytes), entry.sha256, StringComparison.OrdinalIgnoreCase))
                {
                    SailLog.Warn($"sail '{entry.id}': downloaded image hash mismatch, discarding (possibly corrupted/incomplete download)");
                    return null;
                }

                if (SailConfig.EnableSailCache.Value) SailCache.TryWriteImage(entry.id, bytes);
                return bytes;
            }
            catch (Exception e)
            {
                SailLog.Warn($"sail '{entry.id}': download failed: {e.Message}");
                return null;
            }
        }

        private static async Task<string> FetchTextWithBranchFallback(string owner, string repo, string path, TimeSpan timeout)
        {
            foreach (var branch in new[] { "main", "master" })
            {
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                try
                {
                    var resp = await Http.GetAsync(RawUrl(owner, repo, branch, path), cts.Token).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                        return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    SailLog.Warn($"timed out fetching {path} from branch '{branch}'");
                }
                catch (HttpRequestException e)
                {
                    SailLog.Debug($"network error fetching {path} from branch '{branch}': {e.Message}");
                }
            }
            return null;
        }

        private static async Task<byte[]> FetchBytesWithBranchFallback(string owner, string repo, string path, TimeSpan timeout, long maxBytes)
        {
            foreach (var branch in new[] { "main", "master" })
            {
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                try
                {
                    var resp = await Http.GetAsync(RawUrl(owner, repo, branch, path), HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    if (resp.Content.Headers.ContentLength is long len && len > maxBytes)
                    {
                        SailLog.Warn($"remote file '{path}' reports {len / 1024}KB, exceeds MaxImageSizeKb, aborting download");
                        return null;
                    }

                    var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (bytes.LongLength > maxBytes)
                    {
                        SailLog.Warn($"remote file '{path}' exceeded MaxImageSizeKb after download, discarding");
                        return null;
                    }
                    return bytes;
                }
                catch (TaskCanceledException)
                {
                    SailLog.Warn($"timed out downloading {path} from branch '{branch}'");
                }
                catch (HttpRequestException e)
                {
                    SailLog.Debug($"network error downloading {path} from branch '{branch}': {e.Message}");
                }
            }
            return null;
        }
    }
}
