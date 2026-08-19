using System.Reflection;
using UnityEngine;

namespace Velas.Ships
{
    /// <summary>
    /// Ship -> Sail Renderer -> Sail Texture. The one place that knows how to find and paint
    /// the sail mesh on an arbitrary Ship instance, so ShipSailComponent and the automatic
    /// clan-sail logic never duplicate that lookup. Works against Ship.m_sailObject, which is
    /// the same field for every stock boat (Raft/Karve/Longship) -- adding a boat with a
    /// differently-named sail object only needs a new case in FindSailRenderer, everything
    /// above this class stays the same.
    /// </summary>
    internal static class ShipSailController
    {
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static FieldInfo _sailObjectField;

        public static Renderer FindSailRenderer(Ship ship)
        {
            if (ship == null) return null;

            var sailObject = GetSailObject(ship);
            if (sailObject == null) return null;

            // Sail cloth is usually a SkinnedMeshRenderer; fall back to a plain MeshRenderer
            // for boats/mods that use a static mesh instead.
            var renderer = sailObject.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null) return renderer;
            return sailObject.GetComponentInChildren<MeshRenderer>();
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

        /// <summary>Applies a texture to this ship's sail. Uses Renderer.material (not
        /// sharedMaterial), which Unity auto-instantiates per-renderer on first access -- so
        /// this never mutates the material asset shared by every ship of the same type, and
        /// the instance is cleaned up automatically when the renderer/ship is destroyed.</summary>
        public static bool ApplyTexture(Ship ship, Texture2D texture)
        {
            if (texture == null) return false;
            var renderer = FindSailRenderer(ship);
            if (renderer == null)
            {
                SailLog.Warn($"ship '{ship?.name}' has no recognizable sail renderer -- cannot apply texture");
                return false;
            }

            renderer.material.mainTexture = texture;
            return true;
        }
    }
}
