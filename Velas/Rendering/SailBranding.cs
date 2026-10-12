using System.Collections.Generic;
using UnityEngine;

namespace Velas.Rendering
{
    /// <summary>
    /// Stamps the server icon onto the vanilla sail texture, for ships that never picked a
    /// custom sail. The vanilla texture is not CPU-readable, so it is copied through a
    /// RenderTexture first; the icon is then alpha-blended on the CPU, centred on the UV
    /// rectangle the cloth samples (the vanilla texture is an atlas, see SailUvFitter).
    ///
    /// One composite per (vanilla texture, UV rect) is shared by every ship of that type, and
    /// it is capped at <see cref="MaxSize"/> so branding never costs more than one small
    /// texture per boat model.
    /// </summary>
    internal static class SailBranding
    {
        private const int MaxSize = 1024;

        private static readonly Dictionary<string, Texture2D> Composites = new Dictionary<string, Texture2D>();
        private static readonly HashSet<Texture> Produced = new HashSet<Texture>();

        /// <summary>True for textures this class created -- used so a ship is never branded
        /// on top of an already-branded material.</summary>
        public static bool IsBranded(Texture texture) => texture != null && Produced.Contains(texture);

        public static Texture2D Compose(Texture vanilla, Rect uvRect, Texture2D icon, float iconHeightFraction,
            bool flipX = false, bool flipY = false)
        {
            if (vanilla == null || icon == null) return null;

            string key = $"{vanilla.GetInstanceID()}|{icon.GetInstanceID()}|{iconHeightFraction:F2}|" +
                         $"{uvRect.xMin:F3}|{uvRect.yMin:F3}|{uvRect.xMax:F3}|{uvRect.yMax:F3}|{flipX}|{flipY}";
            if (Composites.TryGetValue(key, out var cached) && cached != null) return cached;

            float shrink = Mathf.Min(1f, (float)MaxSize / Mathf.Max(vanilla.width, vanilla.height));
            int width = Mathf.Max(4, Mathf.RoundToInt(vanilla.width * shrink));
            int height = Mathf.Max(4, Mathf.RoundToInt(vanilla.height * shrink));

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D result;
            try
            {
                Graphics.Blit(vanilla, rt);
                RenderTexture.active = rt;
                result = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = vanilla.name + "_server_icon",
                    wrapMode = vanilla.wrapMode,
                };
                result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            }
            catch (System.Exception e)
            {
                SailLog.Warn($"could not copy vanilla sail texture '{vanilla.name}': {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }

            Stamp(result, uvRect, icon, iconHeightFraction, flipX, flipY);
            result.Apply(true, true); // drop the CPU copy: nothing reads it again

            Composites[key] = result;
            Produced.Add(result);
            SailLog.Debug($"Branded vanilla sail '{vanilla.name}' ({width}x{height}) with the server icon");
            return result;
        }

        private static void Stamp(Texture2D target, Rect uvRect, Texture2D icon, float heightFraction, bool flipX, bool flipY)
        {
            int w = target.width, h = target.height;
            float rectW = uvRect.width * w, rectH = uvRect.height * h;

            float iconH = Mathf.Clamp01(heightFraction) * rectH;
            float iconW = iconH * icon.width / icon.height;
            if (iconW > rectW * 0.9f)
            {
                iconW = rectW * 0.9f;
                iconH = iconW * icon.height / icon.width;
            }

            float cx = uvRect.center.x * w, cy = uvRect.center.y * h;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(cx - iconW / 2f), 0, w);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(cx + iconW / 2f), 0, w);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(cy - iconH / 2f), 0, h);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(cy + iconH / 2f), 0, h);
            if (x1 <= x0 || y1 <= y0) return;

            int bw = x1 - x0, bh = y1 - y0;
            var pixels = target.GetPixels(x0, y0, bw, bh);
            float left = cx - iconW / 2f, bottom = cy - iconH / 2f;
            for (int y = 0; y < bh; y++)
            {
                float v = (y0 + y + 0.5f - bottom) / iconH;
                if (v < 0f || v > 1f) continue;
                for (int x = 0; x < bw; x++)
                {
                    float u = (x0 + x + 0.5f - left) / iconW;
                    if (u < 0f || u > 1f) continue;
                    var src = icon.GetPixelBilinear(flipX ? 1f - u : u, flipY ? 1f - v : v);
                    if (src.a <= 0.003f) continue;
                    int i = y * bw + x;
                    var dst = pixels[i];
                    pixels[i] = new Color(
                        Mathf.Lerp(dst.r, src.r, src.a),
                        Mathf.Lerp(dst.g, src.g, src.a),
                        Mathf.Lerp(dst.b, src.b, src.a),
                        dst.a);
                }
            }
            target.SetPixels(x0, y0, bw, bh, pixels);
        }
    }
}
