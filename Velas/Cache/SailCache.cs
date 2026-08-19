using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;

namespace Velas.Cache
{
    /// <summary>
    /// Disk cache for the manifest and remote sail images, under
    /// BepInEx/config/Velas/cache/. Nothing here trusts a path that comes from the
    /// manifest: every file name is either a sail id we already validated (alnum/underscore)
    /// or is derived from a content hash, so a malicious manifest cannot make the mod write
    /// or read outside this folder (see SailRepository for the manifest-side validation).
    /// </summary>
    internal static class SailCache
    {
        private static string _root;

        public static string Root
        {
            get
            {
                if (_root == null)
                {
                    _root = Path.Combine(Paths.ConfigPath, "Velas", "cache");
                    Directory.CreateDirectory(_root);
                    Directory.CreateDirectory(ImagesDir);
                }
                return _root;
            }
        }

        public static string ImagesDir => Path.Combine(Paths.ConfigPath, "Velas", "cache", "images");
        public static string ManifestPath => Path.Combine(Root, "manifest.json");
        public static string ManifestMetaPath => Path.Combine(Root, "manifest.meta");

        /// <summary>Only [a-zA-Z0-9_-] survive, capped at 128 chars. This is the sole gate
        /// standing between "identifier from an external manifest" and a filesystem path, so
        /// it must reject anything that could contain '..', '/', '\\' or drive letters.</summary>
        public static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var chars = new char[Math.Min(id.Length, 128)];
            int n = 0;
            for (int i = 0; i < id.Length && n < chars.Length; i++)
            {
                char c = id[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') chars[n++] = c;
            }
            return n == 0 ? null : new string(chars, 0, n);
        }

        public static string ImagePathFor(string sailId)
        {
            var safe = SanitizeId(sailId);
            return safe == null ? null : Path.Combine(ImagesDir, safe + ".img");
        }

        public static bool TryReadImage(string sailId, out byte[] bytes)
        {
            bytes = null;
            var path = ImagePathFor(sailId);
            if (path == null || !File.Exists(path)) return false;
            try
            {
                bytes = File.ReadAllBytes(path);
                return bytes.Length > 0;
            }
            catch (Exception e)
            {
                SailLog.Warn($"failed reading cached image for '{sailId}': {e.Message}");
                bytes = null;
                return false;
            }
        }

        /// <summary>Writes to a temp file then renames over the destination, so a crash or a
        /// truncated download mid-write can never leave a half-written file that looks valid
        /// on the next read.</summary>
        public static bool TryWriteImage(string sailId, byte[] bytes)
        {
            var path = ImagePathFor(sailId);
            if (path == null || bytes == null || bytes.Length == 0) return false;
            var tmp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(ImagesDir);
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                SailLog.Warn($"failed caching image for '{sailId}': {e.Message}");
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort */ }
                return false;
            }
        }

        public static void RemoveImage(string sailId)
        {
            var path = ImagePathFor(sailId);
            if (path == null) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static bool TryReadManifestText(out string json)
        {
            json = null;
            try
            {
                if (!File.Exists(ManifestPath)) return false;
                json = File.ReadAllText(ManifestPath);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (Exception e)
            {
                SailLog.Warn($"failed reading cached manifest: {e.Message}");
                return false;
            }
        }

        public static void WriteManifestText(string json)
        {
            try
            {
                Directory.CreateDirectory(Root);
                File.WriteAllText(ManifestPath, json);
                File.WriteAllText(ManifestMetaPath, DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e)
            {
                SailLog.Warn($"failed writing manifest cache: {e.Message}");
            }
        }

        public static bool ManifestIsFresh(int maxAgeMinutes)
        {
            try
            {
                if (!File.Exists(ManifestMetaPath)) return false;
                var text = File.ReadAllText(ManifestMetaPath);
                if (!DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var when))
                    return false;
                return (DateTime.UtcNow - when).TotalMinutes < maxAgeMinutes;
            }
            catch
            {
                return false;
            }
        }
    }
}
