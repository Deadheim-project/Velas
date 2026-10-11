using System.Collections.Generic;
using UnityEngine;

namespace Velas.Rendering
{
    /// <summary>
    /// Makes a full sail image cover the sail mesh even when the mesh's UVs only use part of
    /// the texture (the vanilla sail material is an atlas: the cloth samples a sub-rectangle
    /// of it, so a plain image dropped in shows only that slice -- "cut in the middle").
    ///
    /// The fix is done in the texture, not through mainTextureScale/Offset, because Valheim's
    /// custom shaders do not all honour _MainTex_ST: the image is blitted into a new texture
    /// laid out like the atlas, with the picture exactly over the UV rectangle the mesh uses.
    /// </summary>
    internal static class SailUvFitter
    {
        private const float FullCoverage = 0.98f;
        private const int MaxFittedSize = 4096;

        private static readonly Dictionary<string, Texture2D> Fitted = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<Texture, Texture2D> SourceOf = new Dictionary<Texture, Texture2D>();

        /// <summary>UV bounds of the triangles of one submesh. False when the mesh cannot be
        /// read (not readable / no UVs) -- the caller then applies the image as-is.</summary>
        public static bool TryGetUvRect(Mesh mesh, int submesh, out Rect rect)
        {
            rect = default;
            if (mesh == null || !mesh.isReadable) return false;

            var uvs = mesh.uv;
            if (uvs == null || uvs.Length == 0) return false;

            int[] indices = submesh >= 0 && submesh < mesh.subMeshCount ? mesh.GetIndices(submesh) : null;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            if (indices != null && indices.Length > 0)
            {
                foreach (int i in indices)
                {
                    if (i < 0 || i >= uvs.Length) continue;
                    Grow(uvs[i], ref minX, ref minY, ref maxX, ref maxY);
                }
            }
            else
            {
                foreach (var uv in uvs) Grow(uv, ref minX, ref minY, ref maxX, ref maxY);
            }

            if (maxX - minX < 0.01f || maxY - minY < 0.01f) return false;
            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        private static void Grow(Vector2 uv, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            if (uv.x < minX) minX = uv.x;
            if (uv.y < minY) minY = uv.y;
            if (uv.x > maxX) maxX = uv.x;
            if (uv.y > maxY) maxY = uv.y;
        }

        /// <summary>Returns the texture to put on a material whose mesh samples
        /// <paramref name="uvRect"/>. Already-covering rects return the image untouched.</summary>
        public static Texture Fit(Texture2D image, Rect uvRect)
        {
            if (image == null) return null;

            bool insideUnitSquare = uvRect.xMin >= -0.001f && uvRect.yMin >= -0.001f &&
                                    uvRect.xMax <= 1.001f && uvRect.yMax <= 1.001f;
            bool coversAll = uvRect.width >= FullCoverage && uvRect.height >= FullCoverage;
            if (!insideUnitSquare || coversAll) return image;

            string key = $"{image.GetInstanceID()}|{uvRect.xMin:F3}|{uvRect.yMin:F3}|{uvRect.xMax:F3}|{uvRect.yMax:F3}";
            if (Fitted.TryGetValue(key, out var cached) && cached != null) return cached;

            int width = Mathf.Clamp(Mathf.RoundToInt(image.width / uvRect.width), 4, MaxFittedSize);
            int height = Mathf.Clamp(Mathf.RoundToInt(image.height / uvRect.height), 4, MaxFittedSize);

            var scale = new Vector2(1f / uvRect.width, 1f / uvRect.height);
            var offset = new Vector2(-uvRect.xMin / uvRect.width, -uvRect.yMin / uvRect.height);

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                // Outside the sail's rectangle the Clamp wrap mode smears the image's edge
                // pixels, which is harmless: the mesh never samples there.
                Graphics.Blit(image, rt, scale, offset);
                RenderTexture.active = rt;
                var fitted = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = image.name + "_fitted",
                    wrapMode = TextureWrapMode.Clamp,
                };
                fitted.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                fitted.Apply(true, false);

                Fitted[key] = fitted;
                SourceOf[fitted] = image;
                SailLog.Debug($"Fitted '{image.name}' to sail UV rect {uvRect} ({width}x{height})");
                return fitted;
            }
            catch (System.Exception e)
            {
                SailLog.Warn($"could not fit '{image.name}' to the sail UVs, applying it unscaled: {e.Message}");
                return image;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Drops (and destroys) the fitted copies of an image that is being replaced,
        /// so a re-downloaded sail does not leave its old atlas-sized copies behind.</summary>
        public static void Forget(Texture2D image)
        {
            if (image == null) return;
            var stale = new List<string>();
            foreach (var pair in Fitted)
                if (pair.Value == null || (SourceOf.TryGetValue(pair.Value, out var src) && src == image))
                    stale.Add(pair.Key);
            foreach (var key in stale)
            {
                var fitted = Fitted[key];
                Fitted.Remove(key);
                if (fitted == null) continue;
                SourceOf.Remove(fitted);
                Object.Destroy(fitted);
            }
        }

        /// <summary>The original sail image behind a texture this class produced (or the
        /// texture itself when it was applied unfitted).</summary>
        public static Texture SourceImage(Texture applied) =>
            applied != null && SourceOf.TryGetValue(applied, out var source) ? source : applied;
    }
}
