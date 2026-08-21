using System.Collections;
using System.Reflection;
using UnityEngine;
using Velas.Manager;
using Velas.Ships;
using Velas.UI;

namespace Velas.Debug
{
    /// <summary>
    /// Opt-in visual integration test. It only exists when Valheim is launched with
    /// -velas-visual-test, so normal players and DebugMode users never get a spawned ship.
    /// The texture changes go through ShipSailComponent.RequestSetSail, exactly like the UI.
    /// </summary>
    internal sealed class SailVisualTestRunner : MonoBehaviour
    {
        private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private bool _cleanupOnly;

        internal static void EnsureCreated(bool cleanupOnly = false)
        {
            var go = new GameObject("Velas_VisualTest");
            DontDestroyOnLoad(go);
            go.AddComponent<SailVisualTestRunner>()._cleanupOnly = cleanupOnly;
        }

        private IEnumerator Start()
        {
            SailLog.Info("VISUALTEST: waiting for the local player and network scene");
            float deadline = Time.realtimeSinceStartup + 180f;
            while ((Player.m_localPlayer == null || ZNetScene.instance == null || ObjectDB.instance == null) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            var player = Player.m_localPlayer;
            if (player == null || ZNetScene.instance == null)
            {
                SailLog.Error("VISUALTEST FAIL: world did not become ready");
                yield break;
            }

            yield return new WaitForSeconds(2f);

            float repositoryDeadline = Time.realtimeSinceStartup + 30f;
            while (SailManager.RemoteState == RemoteSailsState.Loading &&
                   Time.realtimeSinceStartup < repositoryDeadline)
                yield return null;

            int remoteCount = 0;
            foreach (var sail in SailManager.AllSails)
                if (sail.Source == Model.SailSource.Remote) remoteCount++;
            if (SailManager.RemoteState == RemoteSailsState.Loaded && remoteCount > 0)
                SailLog.Info($"VISUALTEST PASS: remote catalog loaded with {remoteCount} sail(s)");
            else
                SailLog.Warn($"VISUALTEST: remote catalog state={SailManager.RemoteState} count={remoteCount}; testing bundled sails");

            if (_cleanupOnly)
            {
                CleanupPreviousDisplayShip(player.transform.position);
                SailLog.Info("VISUALTEST: cleanup-only run complete");
                yield break;
            }

            CleanupPreviousDisplayShip(player.transform.position);

            var prefab = ZNetScene.instance.GetPrefab("Raft") ??
                         ZNetScene.instance.GetPrefab("Karve") ??
                         ZNetScene.instance.GetPrefab("VikingShip");
            if (prefab == null)
            {
                SailLog.Error("VISUALTEST FAIL: no stock ship prefab found");
                yield break;
            }

            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 point = player.transform.position + forward * 7f + Vector3.up * 1f;
            var instance = Instantiate(prefab, point, Quaternion.LookRotation(player.transform.right, Vector3.up));
            var ship = instance.GetComponent<Ship>();
            if (ship == null)
            {
                SailLog.Error($"VISUALTEST FAIL: prefab '{prefab.name}' has no Ship component");
                Destroy(instance);
                yield break;
            }

            var body = instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }

            // LineAttach expects a normally simulated boat. A frozen display boat can leave
            // one endpoint uninitialized and make vanilla throw every LateUpdate; ropes are
            // irrelevant to the sail test, so keep them out of this opt-in display fixture.
            foreach (var line in instance.GetComponentsInChildren<LineAttach>(true))
                line.enabled = false;

            var nview = instance.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid()) nview.GetZDO().Set("DHS_VelasVisualTest", 1);

            ForceFullSail(ship);
            yield return new WaitForSeconds(2f);

            var component = instance.GetComponent<ShipSailComponent>();
            if (component == null)
            {
                SailLog.Error("VISUALTEST FAIL: ShipSailComponent was not attached");
                yield break;
            }

            SailLog.Info($"VISUALTEST PASS: spawned '{prefab.name}' with ShipSailComponent");
            string ravenId = SailManager.Get("remote_raven_banner") != null
                ? "remote_raven_banner" : "generic_raven_banner";
            string nordicId = SailManager.Get("remote_nordic_pattern") != null
                ? "remote_nordic_pattern" : "generic_nordic_pattern";

            yield return ApplyAndCheck(ship, component, ravenId);
            SailLog.Info($"VISUALTEST STAGE: {ravenId} ready for screenshot");
            yield return new WaitForSeconds(30f);

            yield return ApplyAndCheck(ship, component, nordicId);
            SailLog.Info($"VISUALTEST STAGE: {nordicId} ready for screenshot");
            yield return new WaitForSeconds(30f);

            var ui = FindAnyObjectByType<SailSelectorUI>();
            if (ui == null)
            {
                SailLog.Error("VISUALTEST FAIL: SailSelectorUI was not created");
                yield break;
            }

            ui.Open(ship, component);
            SailLog.Info("VISUALTEST PASS: selector opened; visual test complete");
            yield return new WaitForSeconds(30f);
            ui.Close();
            var testView = ship.GetComponent<ZNetView>();
            if (testView != null && testView.IsValid())
            {
                testView.ClaimOwnership();
                testView.Destroy();
                SailLog.Info("VISUALTEST: display ship cleaned up");
            }
        }

        private static void CleanupPreviousDisplayShip(Vector3 playerPosition)
        {
            foreach (var ship in FindObjectsByType<Ship>(FindObjectsSortMode.None))
            {
                if (ship == null || Vector3.Distance(ship.transform.position, playerPosition) > 100f) continue;
                var nview = ship.GetComponent<ZNetView>();
                bool marked = nview != null && nview.IsValid() &&
                              nview.GetZDO().GetInt("DHS_VelasVisualTest", 0) == 1;
                if (!marked) continue;

                nview.ClaimOwnership();
                nview.Destroy();
                SailLog.Info($"VISUALTEST: removed previous display ship '{ship.name}'");
            }
        }

        private static IEnumerator ApplyAndCheck(Ship ship, ShipSailComponent component, string sailId)
        {
            component.RequestSetSail(sailId);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (component.CurrentSailId != sailId && Time.realtimeSinceStartup < deadline)
                yield return null;

            var expected = SailManager.ResolveTexture(sailId);
            var renderer = ShipSailController.FindSailRenderer(ship);
            bool applied = component.CurrentSailId == sailId && renderer != null &&
                           renderer.material.mainTexture == expected;
            if (applied)
                SailLog.Info($"VISUALTEST PASS: '{sailId}' persisted and rendered through RPC");
            else
                SailLog.Error($"VISUALTEST FAIL: '{sailId}' current='{component.CurrentSailId}' " +
                              $"renderer={renderer != null} texture={(renderer != null && renderer.material.mainTexture == expected)}");
        }

        private static void ForceFullSail(Ship ship)
        {
            try
            {
                var speed = typeof(Ship).GetField("m_speed", AnyInstance);
                if (speed != null && speed.FieldType.IsEnum)
                    speed.SetValue(ship, System.Enum.Parse(speed.FieldType, "Full"));

                var sailObject = typeof(Ship).GetField("m_sailObject", AnyInstance)?.GetValue(ship) as GameObject;
                if (sailObject != null) sailObject.SetActive(true);
            }
            catch (System.Exception e)
            {
                SailLog.Warn($"VISUALTEST: could not force full sail: {e.Message}");
            }
        }
    }
}
