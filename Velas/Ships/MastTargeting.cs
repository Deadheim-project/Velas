using UnityEngine;

namespace Velas.Ships
{
    /// <summary>
    /// Answers "is the local player aiming at a ship's mast right now?". Vanilla hover can't
    /// tell: Player.FindHoverObject resolves any collider with an attached rigidbody to the
    /// rigidbody's GameObject, so every plank of a ship hovers as the ship root. Instead the
    /// camera ray is tested against the mast as a vertical segment
    /// (<see cref="ShipSailController.TryGetMastSegment"/>), which works whether or not the
    /// mast model has its own collider.
    ///
    /// The result is cached per frame: SailInputController (the key) and the HUD hint patch
    /// both ask every frame, and only one of them pays for the lookup.
    /// </summary>
    internal static class MastTargeting
    {
        private const float ShipSearchSlack = 20f;

        private static int _frame = -1;
        private static Ship _cached;

        public static Ship GetAimedMastShip(Player player)
        {
            if (_frame == Time.frameCount) return _cached;
            _frame = Time.frameCount;
            _cached = Find(player);
            return _cached;
        }

        private static Ship Find(Player player)
        {
            if (player == null || GameCamera.instance == null) return null;

            float maxDistance = SailConfig.MaxInteractionDistance.Value;
            var ship = ShipFinder.FindNearest(player.transform.position, maxDistance + ShipSearchSlack);
            if (ship == null) return null;
            if (!ShipSailController.TryGetMastSegment(ship, out var bottom, out var top)) return null;

            var cam = GameCamera.instance.transform;
            var ray = new Ray(cam.position, cam.forward);
            ClosestPoints(ray, bottom, top, out float rayT, out Vector3 onRay, out Vector3 onMast);

            if (rayT <= 0f) return null;
            if (Vector3.Distance(onRay, onMast) > SailConfig.MastInteractionRadius.Value) return null;
            if (Vector3.Distance(player.transform.position, onMast) > maxDistance) return null;
            if (IsBlocked(ray, rayT, ship, player)) return null;
            return ship;
        }

        /// <summary>Something that is neither this ship nor the player sits between the
        /// camera and the mast (a wall, another boat, terrain).</summary>
        private static bool IsBlocked(Ray ray, float distance, Ship ship, Player player)
        {
            var hits = Physics.RaycastAll(ray, Mathf.Max(0f, distance - 0.2f), Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                var collider = hit.collider;
                if (collider == null) continue;
                if (collider.GetComponentInParent<Ship>() == ship) continue;
                if (collider.GetComponentInParent<Player>() == player) continue;
                return true;
            }
            return false;
        }

        /// <summary>Closest points between a ray and a segment.</summary>
        private static void ClosestPoints(Ray ray, Vector3 a, Vector3 b,
            out float rayT, out Vector3 onRay, out Vector3 onSegment)
        {
            Vector3 d1 = ray.direction;
            Vector3 d2 = b - a;
            Vector3 r = ray.origin - a;
            float aa = Vector3.Dot(d1, d1);
            float ee = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float c = Vector3.Dot(d1, r);
            float bb = Vector3.Dot(d1, d2);
            float denom = aa * ee - bb * bb;

            float s = denom > 1e-6f ? (bb * f - c * ee) / denom : 0f;
            float t = ee > 1e-6f ? Mathf.Clamp01((bb * s + f) / ee) : 0f;
            // Re-project onto the ray after clamping onto the segment.
            s = Mathf.Max(0f, (bb * t - c) / aa);

            rayT = s;
            onRay = ray.origin + d1 * s;
            onSegment = a + d2 * t;
        }
    }
}
