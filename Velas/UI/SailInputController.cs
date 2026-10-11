using UnityEngine;
using Velas.Ships;

namespace Velas.UI
{
    /// <summary>Watches for SailConfig.OpenSailSelectorKey and opens the selector for the ship
    /// whose mast the player is aiming at -- the key does nothing anywhere else.</summary>
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
            if (SailUiInputBlocker.IsTyping()) return;
            if (!Input.GetKeyDown(SailConfig.OpenSailSelectorKey.Value)) return;

            // Don't let the bind also fire while some other vanilla/mod menu already owns
            // input focus (inventory, chat, another mod's panel).
            if (Menu.IsVisible() && !_ui.IsOpen) return;

            if (_ui.IsOpen)
            {
                _ui.Close();
                return;
            }

            // Only reacts while the player aims at a ship's mast (the HUD shows the
            // "[G] Trocar vela" prompt there). Anywhere else -- the deck, the rudder, open
            // world -- the key stays silent, so it doesn't clash with other binds on it.
            var player = Player.m_localPlayer;
            var ship = MastTargeting.GetAimedMastShip(player);
            if (ship == null) return;

            var component = ship.GetComponent<ShipSailComponent>();
            if (component == null)
            {
                SailLog.Warn($"ship '{ship.name}' had no ShipSailComponent when the selector was requested");
                return;
            }

            _ui.Open(ship, component);
        }
    }
}
