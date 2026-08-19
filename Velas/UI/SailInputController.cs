using UnityEngine;
using Velas.Ships;

namespace Velas.UI
{
    /// <summary>Watches for SailConfig.OpenSailSelectorKey and opens the selector for the
    /// nearest valid ship -- only does the (cheap, on-keypress-only) ship search when the key
    /// is actually pressed, never every frame.</summary>
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

            var player = Player.m_localPlayer;
            var ship = ShipFinder.FindNearest(player.transform.position, SailConfig.MaxInteractionDistance.Value);
            if (ship == null)
            {
                player.Message(MessageHud.MessageType.Center, "Nenhum navio próximo o suficiente.");
                return;
            }

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
