using System;
using UnityEngine;

namespace Velas.Debug
{
    /// <summary>
    /// Dev-only helper behind the "dhs spawnTestShip" console command. Finds a safe patch of
    /// open water near the player before spawning anything, specifically to avoid the failure
    /// mode called out in the spec: a boat instantiated on/inside terrain falls, clips, or
    /// gets destroyed by WearNTear the instant it exists.
    /// </summary>
    internal static class TestShipSpawner
    {
        private const float SeaLevelFallback = 30f;
        private const float SearchStep = 4f;
        private const int SearchRings = 12;
        private const int PointsPerRing = 10;

        public static bool TrySpawn(Vector3 origin, string prefabName, out string message)
        {
            message = null;

            if (ZNetScene.instance == null)
            {
                message = "ZNetScene não está pronto (mundo ainda carregando?).";
                return false;
            }

            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                message = $"Prefab de navio '{prefabName}' não encontrado.";
                return false;
            }

            if (!TryFindSafeWaterPoint(origin, out var point))
            {
                message = "Não há água adequada por perto para spawnar um navio de teste.";
                return false;
            }

            var toPlayer = origin - point;
            toPlayer.y = 0f;
            var forward = toPlayer.sqrMagnitude > 0.01f ? -toPlayer.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);

            var instance = UnityEngine.Object.Instantiate(prefab, point, rotation);
            var ship = instance.GetComponent<Ship>();
            if (ship == null)
            {
                message = $"Prefab '{prefabName}' não tem componente Ship.";
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            message = $"Navio de teste '{prefabName}' criado em {point:F1}.";
            SailLog.Info(message);
            return true;
        }

        /// <summary>Rings of candidate points around origin, nearest first. A point is
        /// accepted only when: (1) there is no solid ground/seabed poking above the
        /// waterline there, and (2) the terrain height is comfortably below sea level -- both
        /// checked so the ship never lands on rocks, a beach, or a shallow reef.</summary>
        private static bool TryFindSafeWaterPoint(Vector3 origin, out Vector3 result)
        {
            result = origin;
            float seaLevel = GetSeaLevel();

            if (IsSafeWaterPoint(origin, seaLevel, out var y0))
            {
                result = new Vector3(origin.x, y0, origin.z);
                return true;
            }

            for (int ring = 1; ring <= SearchRings; ring++)
            {
                float radius = ring * SearchStep;
                for (int p = 0; p < PointsPerRing; p++)
                {
                    float angle = p * (360f / PointsPerRing) * Mathf.Deg2Rad;
                    var candidate = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (IsSafeWaterPoint(candidate, seaLevel, out var y))
                    {
                        result = new Vector3(candidate.x, y, candidate.z);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsSafeWaterPoint(Vector3 point, float seaLevel, out float spawnY)
        {
            spawnY = seaLevel;
            try
            {
                if (ZoneSystem.instance == null) return false;

                float groundHeight;
                bool hasGround = ZoneSystem.instance.GetSolidHeight(point, out groundHeight);
                // Solid ground/rock breaking the surface here (or close under it) means this
                // spot is a beach/reef/seabed shelf, not open water -- reject it.
                if (hasGround && groundHeight > seaLevel - 1.5f) return false;

                float terrainHeight;
                if (ZoneSystem.instance.GetGroundHeight(point, out terrainHeight))
                {
                    if (terrainHeight > seaLevel - 1.5f) return false;
                }

                spawnY = seaLevel + 0.35f; // small clearance so the hull starts above the surface, not clipped into it
                return true;
            }
            catch (Exception e)
            {
                SailLog.Warn($"water check failed at {point}: {e.Message}");
                return false;
            }
        }

        private static float GetSeaLevel()
        {
            try
            {
                if (ZoneSystem.instance != null) return ZoneSystem.instance.m_waterLevel;
            }
            catch (Exception e)
            {
                SailLog.Warn($"could not read ZoneSystem.m_waterLevel, using fallback {SeaLevelFallback}: {e.Message}");
            }
            return SeaLevelFallback;
        }
    }
}
