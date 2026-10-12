using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Velas.Rendering;

namespace Velas.Ships
{
    /// <summary>
    /// Ship -> Sail Renderers -> Sail Texture. The one place that knows how to find and paint
    /// the sail cloth on an arbitrary Ship instance, so ShipSailComponent and the automatic
    /// clan-sail logic never duplicate that lookup.
    ///
    /// Since Valheim 1.0 the sail is simulated by MagicaCloth2 (Ship.m_sailCloth) and can be
    /// made of more than one renderer/material slot. Painting only the first
    /// SkinnedMeshRenderer under m_sailObject left the rest of the cloth with the vanilla
    /// texture, so this class paints every slot that uses the sail material, and fits the
    /// image to the UV rectangle each slot samples (see <see cref="SailUvFitter"/>).
    /// </summary>
    internal static class ShipSailController
    {
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const string InstanceSuffix = " (Instance)";

        private static FieldInfo _sailObjectField;
        private static FieldInfo _mastObjectField;
        private static FieldInfo _sailClothField;

        /// <summary>One material slot of the sail cloth.</summary>
        internal readonly struct SailSlot
        {
            public readonly Renderer Renderer;
            public readonly int MaterialIndex;

            public SailSlot(Renderer renderer, int materialIndex)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
            }
        }

        public static List<SailSlot> FindSailSlots(Ship ship)
        {
            var slots = new List<SailSlot>();
            if (ship == null) return slots;

            // Reference renderers: the ones MagicaCloth simulates, or (older/modded boats
            // without it) the first cloth-looking renderer under m_sailObject.
            var references = GetClothRenderers(ship);
            if (references.Count == 0)
            {
                var fallback = FindFirstSailRenderer(GetSailObject(ship));
                if (fallback != null) references.Add(fallback);
            }
            if (references.Count == 0) return slots;

            var sailMaterialNames = new HashSet<string>();
            foreach (var r in references)
                foreach (var m in r.sharedMaterials)
                    if (m != null) sailMaterialNames.Add(BaseName(m.name));

            // Candidates: everything that could be drawing part of the sail. Only slots whose
            // material is the sail material are painted, so wood/rope under the same
            // hierarchy is left alone.
            var candidates = new HashSet<Renderer>(references);
            AddRenderers(candidates, GetSailObject(ship));
            AddRenderers(candidates, GetClothObject(ship));

            foreach (var renderer in candidates)
            {
                if (renderer == null) continue;
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                    if (shared[i] != null && sailMaterialNames.Contains(BaseName(shared[i].name)))
                        slots.Add(new SailSlot(renderer, i));
            }
            return slots;
        }

        /// <summary>Kept for callers that just need "the" sail renderer (debug/visual test).</summary>
        public static Renderer FindSailRenderer(Ship ship)
        {
            var slots = FindSailSlots(ship);
            return slots.Count > 0 ? slots[0].Renderer : null;
        }

        /// <summary>Applies a texture to every sail slot of this ship. Uses
        /// Renderer.materials (not sharedMaterials), which Unity instantiates per-renderer on
        /// first access -- so this never mutates the material asset shared by every ship of
        /// the same type, and the instances die with the ship.</summary>
        public static bool ApplyTexture(Ship ship, Texture2D texture)
        {
            if (texture == null) return false;
            var slots = FindSailSlots(ship);
            if (slots.Count == 0)
            {
                SailLog.Warn($"ship '{ship?.name}' has no recognizable sail renderer -- cannot apply texture");
                return false;
            }

            var byRenderer = new Dictionary<Renderer, List<int>>();
            foreach (var slot in slots)
            {
                if (!byRenderer.TryGetValue(slot.Renderer, out var list))
                    byRenderer[slot.Renderer] = list = new List<int>();
                list.Add(slot.MaterialIndex);
            }

            foreach (var pair in byRenderer)
            {
                var renderer = pair.Key;
                var mesh = GetMesh(renderer);
                var materials = renderer.materials;
                foreach (int index in pair.Value)
                {
                    if (index >= materials.Length || materials[index] == null) continue;
                    int submesh = mesh != null ? Mathf.Min(index, mesh.subMeshCount - 1) : index;
                    Texture applied = SailUvFitter.TryGetUvRect(mesh, submesh, out var uvRect)
                        ? SailUvFitter.Fit(texture, uvRect)
                        : texture;
                    materials[index].mainTexture = applied;
                    // Some sail materials tile/mirror their own texture (the Ashlands drakkar's
                    // is scale (-1, 1.27), offset (0, -0.13)); with a clamped custom image a
                    // negative scale samples only the edge column and the sail turns to
                    // stripes. The fitted image is laid out on the raw mesh UVs, so reset it.
                    materials[index].mainTextureScale = Vector2.one;
                    materials[index].mainTextureOffset = Vector2.zero;
                }
            }

            SailLog.Debug($"Painted '{texture.name}' on {slots.Count} sail slot(s) across {byRenderer.Count} renderer(s) of '{ship?.name}'");
            return true;
        }

        /// <summary>Stamps the server icon on the vanilla sail of this ship (no custom sail
        /// chosen). Slots already showing a custom or branded texture are left alone.</summary>
        public static bool ApplyServerIcon(Ship ship, Texture2D icon, float heightFraction)
        {
            if (icon == null) return false;
            var slots = FindSailSlots(ship);
            int painted = 0;
            foreach (var slot in slots)
            {
                var shared = slot.Renderer.sharedMaterials;
                if (slot.MaterialIndex >= shared.Length || shared[slot.MaterialIndex] == null) continue;
                var vanilla = shared[slot.MaterialIndex].mainTexture;
                if (vanilla == null || SailBranding.IsBranded(vanilla) || SailUvFitter.SourceImage(vanilla) != vanilla) continue;

                var mesh = GetMesh(slot.Renderer);
                int submesh = mesh != null ? Mathf.Min(slot.MaterialIndex, mesh.subMeshCount - 1) : slot.MaterialIndex;
                if (!SailUvFitter.TryGetUvRect(mesh, submesh, out var uvRect))
                    uvRect = new Rect(0f, 0f, 1f, 1f);

                // The vanilla texture keeps the material's own tiling, so stamp the icon where
                // that tiling actually samples (mirrored back when the scale is negative).
                var material = shared[slot.MaterialIndex];
                uvRect = ApplyTiling(uvRect, material.mainTextureScale, material.mainTextureOffset,
                    out bool flipX, out bool flipY);

                var branded = SailBranding.Compose(vanilla, uvRect, icon, heightFraction, flipX, flipY);
                if (branded == null) continue;
                slot.Renderer.materials[slot.MaterialIndex].mainTexture = branded;
                painted++;
            }
            return painted > 0;
        }

        /// <summary>True when every sail slot currently shows <paramref name="image"/>
        /// (directly or as its UV-fitted copy).</summary>
        public static bool IsShowing(Ship ship, Texture2D image)
        {
            var slots = FindSailSlots(ship);
            if (slots.Count == 0 || image == null) return false;
            foreach (var slot in slots)
            {
                var materials = slot.Renderer.sharedMaterials;
                if (slot.MaterialIndex >= materials.Length || materials[slot.MaterialIndex] == null) return false;
                if (SailUvFitter.SourceImage(materials[slot.MaterialIndex].mainTexture) != image) return false;
            }
            return true;
        }

        /// <summary>The mast as a vertical segment in world space: through the mast pivot
        /// (Ship.m_mastObject, falling back to the sail object), spanning the height of the
        /// sail cloth plus the deck below it.</summary>
        public static bool TryGetMastSegment(Ship ship, out Vector3 bottom, out Vector3 top)
        {
            bottom = top = Vector3.zero;
            if (ship == null) return false;

            var mast = GetMastObject(ship) ?? GetSailObject(ship);
            if (mast == null) return false;

            Vector3 up = ship.transform.up;
            Vector3 pivot = mast.transform.position;

            float low = Vector3.Dot(ship.transform.position - pivot, up) - 0.5f;
            float high = 0f;
            foreach (var slot in FindSailSlots(ship))
            {
                var b = slot.Renderer.bounds;
                high = Mathf.Max(high, Vector3.Dot(b.max - pivot, up), Vector3.Dot(b.min - pivot, up));
                low = Mathf.Min(low, Vector3.Dot(b.min - pivot, up));
            }
            if (high < 1f) high = 6f; // sail furled/unknown: assume a typical mast height

            bottom = pivot + up * low;
            top = pivot + up * (high + 0.5f);
            return true;
        }

        /// <summary>Human-readable dump of what the controller sees, for dhs_sail_inspect.</summary>
        public static IEnumerable<string> Describe(Ship ship)
        {
            yield return $"Navio '{ship.name}': sailObject={GetSailObject(ship)?.name ?? "null"} mast={GetMastObject(ship)?.name ?? "null"} clothRenderers={GetClothRenderers(ship).Count}";
            foreach (var slot in FindSailSlots(ship))
            {
                var mesh = GetMesh(slot.Renderer);
                var materials = slot.Renderer.sharedMaterials;
                var material = slot.MaterialIndex < materials.Length ? materials[slot.MaterialIndex] : null;
                var tex = material != null ? material.mainTexture : null;
                int submesh = mesh != null ? Mathf.Min(slot.MaterialIndex, mesh.subMeshCount - 1) : slot.MaterialIndex;
                string uv = SailUvFitter.TryGetUvRect(mesh, submesh, out var rect) ? rect.ToString() : "n/a";
                yield return $"  {slot.Renderer.GetType().Name} '{slot.Renderer.name}' slot {slot.MaterialIndex}: " +
                             $"material='{material?.name}' shader='{material?.shader?.name}' " +
                             $"texture='{tex?.name}' {(tex != null ? $"{tex.width}x{tex.height}" : "")} " +
                             $"mesh='{mesh?.name}' readable={mesh?.isReadable} submeshes={mesh?.subMeshCount} uvRect={uv} " +
                             $"centerPixel={SamplePixel(tex)}";
                if (material == null) continue;
                foreach (var prop in material.GetTexturePropertyNames())
                {
                    var t = material.GetTexture(prop);
                    yield return $"    tex {prop} = '{t?.name}' {(t != null ? $"{t.width}x{t.height}" : "")}";
                }
                yield return $"    keywords: {string.Join(" ", material.shaderKeywords)}";
                foreach (var prop in new[] { "_Cutoff", "_AlphaCutoff", "_Color", "_Glossiness", "_Metallic" })
                    if (material.HasProperty(prop))
                        yield return prop == "_Color"
                            ? $"    {prop} = {material.GetColor(prop)}"
                            : $"    {prop} = {material.GetFloat(prop)}";
            }
        }

        private static string SamplePixel(Texture texture)
        {
            if (!(texture is Texture2D tex2d) || !tex2d.isReadable) return "n/a";
            try
            {
                Color32 c = tex2d.GetPixelBilinear(0.5f, 0.5f);
                return $"rgba({c.r},{c.g},{c.b},{c.a})";
            }
            catch (System.Exception)
            {
                return "n/a";
            }
        }

        /// <summary>Maps a mesh-UV rectangle through a material's _MainTex_ST into texture space,
        /// folded back into the unit square when it fits inside one tile (Repeat wrap).</summary>
        private static Rect ApplyTiling(Rect rect, Vector2 scale, Vector2 offset, out bool flipX, out bool flipY)
        {
            flipX = scale.x < 0f;
            flipY = scale.y < 0f;
            float x0 = rect.xMin * scale.x + offset.x, x1 = rect.xMax * scale.x + offset.x;
            float y0 = rect.yMin * scale.y + offset.y, y1 = rect.yMax * scale.y + offset.y;
            var mapped = Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Max(x0, x1), Mathf.Max(y0, y1));

            float shiftX = Mathf.Floor(mapped.xMin), shiftY = Mathf.Floor(mapped.yMin);
            if (mapped.xMax - shiftX <= 1.001f) mapped.x -= shiftX;
            if (mapped.yMax - shiftY <= 1.001f) mapped.y -= shiftY;
            return mapped;
        }

        private static string BaseName(string materialName)
        {
            if (materialName == null) return "";
            while (materialName.EndsWith(InstanceSuffix))
                materialName = materialName.Substring(0, materialName.Length - InstanceSuffix.Length);
            return materialName;
        }

        private static void AddRenderers(HashSet<Renderer> set, GameObject root)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) set.Add(r);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) set.Add(r);
        }

        private static Renderer FindFirstSailRenderer(GameObject sailObject)
        {
            if (sailObject == null) return null;
            // Sail cloth is usually a SkinnedMeshRenderer; fall back to a plain MeshRenderer
            // for boats/mods that use a static mesh instead.
            var renderer = sailObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer != null) return renderer;
            return sailObject.GetComponentInChildren<MeshRenderer>(true);
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>MagicaCloth's ClothSerializeData.sourceRenderers, read by reflection so
        /// the mod does not need a compile-time reference to MagicaClothV2.</summary>
        private static List<Renderer> GetClothRenderers(Ship ship)
        {
            var result = new List<Renderer>();
            try
            {
                var cloth = GetCloth(ship);
                if (cloth == null) return result;

                var serializeData = cloth.GetType().GetProperty("SerializeData", AnyInstance)?.GetValue(cloth)
                                    ?? cloth.GetType().GetField("serializeData", AnyInstance)?.GetValue(cloth);
                var sources = serializeData?.GetType().GetField("sourceRenderers", AnyInstance)?.GetValue(serializeData) as IEnumerable;
                if (sources == null) return result;
                foreach (var item in sources)
                    if (item is Renderer r && r != null) result.Add(r);
            }
            catch (System.Exception e)
            {
                SailLog.Warn($"could not read Ship.m_sailCloth renderers: {e.Message}");
            }
            return result;
        }

        private static Component GetCloth(Ship ship)
        {
            _sailClothField ??= typeof(Ship).GetField("m_sailCloth", AnyInstance);
            return _sailClothField?.GetValue(ship) as Component;
        }

        private static GameObject GetClothObject(Ship ship)
        {
            try
            {
                var cloth = GetCloth(ship);
                return cloth != null ? cloth.gameObject : null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static GameObject GetSailObject(Ship ship)
        {
            try
            {
                _sailObjectField ??= typeof(Ship).GetField("m_sailObject", AnyInstance);
                return _sailObjectField?.GetValue(ship) as GameObject;
            }
            catch (System.Exception e)
            {
                SailLog.Warn($"could not read Ship.m_sailObject: {e.Message}");
                return null;
            }
        }

        private static GameObject GetMastObject(Ship ship)
        {
            try
            {
                _mastObjectField ??= typeof(Ship).GetField("m_mastObject", AnyInstance);
                return _mastObjectField?.GetValue(ship) as GameObject;
            }
            catch (System.Exception e)
            {
                SailLog.Warn($"could not read Ship.m_mastObject: {e.Message}");
                return null;
            }
        }
    }
}
