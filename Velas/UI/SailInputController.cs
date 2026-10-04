using UnityEngine;
using Velas.Ships;

namespace Velas.UI
{
    /// <summary>Watches for SailConfig.OpenSailSelectorKey and opens the selector for the ship
    /// the player is looking at or steering -- the key does nothing anywhere else, and the
    /// lookup only runs when the key is actually pressed, never every frame.</summary>
    internal class SailInputController : MonoBehaviour
    {
        private SailSelectorUI _ui;

        private void Awake()
        {
            _ui = gameObject.AddComponent<SailSelectorUI>();
        }

        private void Update()
        {
            if (!SailConfig.Enabled.Value) return;
            if (Player.m_localPlayer == null) return;
            if (!Input.GetKeyDown(SailConfig.OpenSailSelectorKey.Value)) return;

            // Don't let the bind also fire while some other vanilla/mod menu already owns
            // input focus (inventory, chat, another mod's panel).
            if (Menu.IsVisible() && !_ui.IsOpen) return;

            if (_ui.IsOpen)
            {
                _ui.Close();
                return;
            }

            // Only reacts while the player is actually interacting with a ship: looking at it
            // (crosshair hover) or holding its rudder. Anywhere else the key stays silent, so
            // it doesn't clash with whatever else the player has bound to it.
            var player = Player.m_localPlayer;
            var ship = GetInteractedShip(player);
            if (ship == null) return;

            var component = ship.GetComponent<ShipSailComponent>();
            if (component == null)
            {
                SailLog.Warn($"ship '{ship.name}' had no ShipSailComponent when the selector was requested");
                return;
            }

            _ui.Open(ship, component);
        }

        private static Ship GetInteractedShip(Player player)
        {
            var controlled = player.GetControlledShip();
            if (controlled != null) return controlled;

            var hover = player.GetHoverObject();
            if (hover == null) return null;

            var ship = hover.GetComponentInParent<Ship>();
            if (ship == null) return null;

            float distance = Vector3.Distance(player.transform.position, ship.transform.position);
            return distance <= SailConfig.MaxInteractionDistance.Value ? ship : null;
        }
    }
}
