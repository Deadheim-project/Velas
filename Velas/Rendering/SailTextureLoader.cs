using System;
using UnityEngine;
using Velas.Game;

namespace Velas.Rendering
{
    /// <summary>
    /// Turns validated image bytes into a Texture2D. Every caller MUST go through
    /// <see cref="TryLoad"/> rather than constructing a Texture2D directly -- this is where
    /// section 18's "don't turn untrusted GitHub content straight into game state" rules
    /// (size/dimension caps, corrupt-file handling) are enforced, in one place.
    /// </summary>
    internal static class SailTextureLoader
    {
        private const int MinDimension = 4;

        public static bool TryLoad(byte[] bytes, string debugLabel, out Texture2D texture)
        {
            texture = null;
            if (bytes == null || bytes.Length == 0)
            {
                SailLog.Warn($"'{debugLabel}': empty image data");
                return false;
            }

            int maxBytes = Math.Max(1, SailConfig.MaxImageSizeKb.Value) * 1024;
            if (bytes.Length > maxBytes)
            {
                SailLog.Warn($"'{debugLabel}': image too large ({bytes.Length / 1024}KB > {SailConfig.MaxImageSizeKb.Value}KB limit), rejected");
                return false;
            }

            Texture2D tex = null;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ValheimApi.TryLoadImage(tex, bytes))
                {
                    SailLog.Warn($"'{debugLabel}': not a valid/decodable image, rejected");
                    UnityEngine.Object.Destroy(tex);
                    return false;
                }

                int maxDim = Math.Max(MinDimension, SailConfig.MaxImageDimension.Value);
                if (tex.width < MinDimension || tex.height < MinDimension || tex.width > maxDim || tex.height > maxDim)
                {
                    SailLog.Warn($"'{debugLabel}': image dimensions {tex.width}x{tex.height} out of allowed range [{MinDimension}, {maxDim}], rejected");
                    UnityEngine.Object.Destroy(tex);
                    return false;
                }

                tex.wrapMode = TextureWrapMode.Clamp;
                tex.name = "Sail_" + debugLabel;
                texture = tex;
                return true;
            }
            catch (Exception e)
            {
                SailLog.Warn($"'{debugLabel}': exception decoding image: {e.Message}");
                if (tex != null) UnityEngine.Object.Destroy(tex);
                texture = null;
                return false;
            }
        }
    }
}
