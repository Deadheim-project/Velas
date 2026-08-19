using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Velas.Ships
{
    /// <summary>Finds the nearest ship to a point, for the "open selector" keybind. Reads
    /// Ship.s_currentShips (the game's own static registry) via reflection instead of
    /// FindObjectsOfType, and -- crucially -- is only ever called on a keypress, never from
    /// Update every frame, per spec section 19.</summary>
    internal static class ShipFinder
    {
        private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static FieldInfo _currentShipsField;
        private static bool _resolveFailed;

        public static Ship FindNearest(Vector3 position, float maxDistance)
        {
            Ship best = null;
            float bestDist = maxDistance;

            foreach (var item in EnumerateShips())
            {
                if (!(item is Ship ship) || ship == null) continue;
                float d = Vector3.Distance(position, ship.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = ship;
                }
            }
            return best;
        }

        private static IEnumerable EnumerateShips()
        {
            if (!_resolveFailed)
            {
                try
                {
                    _currentShipsField ??= typeof(Ship).GetField("s_currentShips", AnyStatic);
                    if (_currentShipsField?.GetValue(null) is IEnumerable list)
                        return list;
                }
                catch (System.Exception e)
                {
                    SailLog.Warn($"could not read Ship.s_currentShips, falling back to a full scan: {e.Message}");
                }
                _resolveFailed = true;
            }

            return UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None);
        }
    }
}
