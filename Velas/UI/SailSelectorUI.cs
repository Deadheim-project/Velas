using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Velas.Manager;
using Velas.Model;
using Velas.Permissions;
using Velas.Ships;

namespace Velas.UI
{
    /// <summary>
    /// The sail picker: a simple IMGUI grid (consistent with the rest of the mod using plain
    /// OnGUI rather than pulling in a UI toolkit dependency). Shows every known sail with a
    /// preview, name, generic/custom + clan tags, and greys out ones the local player is not
    /// allowed to use (spec section 7 prefers "shown but locked" over hiding).
    /// </summary>
    internal class SailSelectorUI : MonoBehaviour
    {
        private const int CellSize = 100;
        private const int Padding = 10;
        private const int PreviewSize = 72;

        private bool _open;
        private Ship _targetShip;
        private ShipSailComponent _targetComponent;
        private Vector2 _scroll;
        private Rect _windowRect = new Rect(0, 0, 520, 420);
        private GUIStyle _lockedStyle;
        private GUIStyle _cellStyle;
        private GUIStyle _labelStyle;

        public bool IsOpen => _open;

        public void Toggle(Ship ship, ShipSailComponent component)
        {
            if (_open && _targetShip == ship) Close();
            else Open(ship, component);
        }

        public void Open(Ship ship, ShipSailComponent component)
        {
            if (ship == null || component == null) return;
            _targetShip = ship;
            _targetComponent = component;
            _open = true;
            SailUiInputBlocker.IsOpen = true;
            _windowRect.x = (Screen.width - _windowRect.width) / 2f;
            _windowRect.y = (Screen.height - _windowRect.height) / 2f;
            SailLog.Debug($"Opening sail selector for ship '{ship.name}'");
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            SailUiInputBlocker.IsOpen = false;
            _targetShip = null;
            _targetComponent = null;
        }

        private void Update()
        {
            if (!_open) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            // A ship that despawned/unloaded, or a player who walked far enough away, closes
            // the panel instead of leaving it pointed at a ship the player can no longer see.
            if (_targetShip == null)
            {
                Close();
                return;
            }

            var player = Player.m_localPlayer;
            if (player != null &&
                Vector3.Distance(player.transform.position, _targetShip.transform.position) > SailConfig.MaxInteractionDistance.Value * 2f)
            {
                Close();
            }
        }

        private void OnGUI()
        {
            if (!_open) return;
            EnsureStyles();
            _windowRect = GUILayout.Window(0x0DEAD511, _windowRect, DrawWindow, "Velas do navio");
        }

        private void EnsureStyles()
        {
            if (_cellStyle != null) return;
            _cellStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperCenter, fixedWidth = CellSize, fixedHeight = CellSize + 34 };
            _lockedStyle = new GUIStyle(_cellStyle);
            _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.UpperCenter, wordWrap = true };
        }

        private void DrawWindow(int id)
        {
            var sails = SailManager.AllSails
                .OrderBy(s => s.Source)
                .ThenBy(s => s.DisplayName)
                .ToList();

            GUILayout.Label(RemoteStatusLabel());
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Width(500), GUILayout.Height(320));

            int columns = 4;
            for (int i = 0; i < sails.Count; i += columns)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < columns; c++)
                {
                    int idx = i + c;
                    if (idx >= sails.Count) { GUILayout.Space(CellSize + 8); continue; }
                    DrawCell(sails[idx]);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("Fechar")) Close();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private string RemoteStatusLabel() => SailManager.RemoteState switch
        {
            Manager.RemoteSailsState.Loading => "Carregando velas do repositório...",
            Manager.RemoteSailsState.Unavailable => "Velas remotas indisponíveis (offline?) -- mostrando apenas as genéricas.",
            Manager.RemoteSailsState.Loaded => $"{SailManager.AllSails.Count} vela(s) disponíveis.",
            _ => "",
        };

        private void DrawCell(SailDefinition sail)
        {
            var permission = SailPermissionService.CanUseLocal(sail);
            var texture = SailManager.ResolveTexture(sail.Id);
            var current = _targetComponent != null && _targetComponent.CurrentSailId == sail.Id;

            GUILayout.BeginVertical(permission.Allowed ? _cellStyle : _lockedStyle, GUILayout.Width(CellSize));

            var prevColor = GUI.color;
            GUI.color = permission.Allowed ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.8f);
            var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(true));
            if (texture != null) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            GUI.color = prevColor;

            string tag = sail.Source == Model.SailSource.Generic ? "Genérica" : (sail.IsPublic ? "Custom" : $"Clã: {sail.ClanId}");
            if (current) tag = "✓ " + tag;
            if (!permission.Allowed) tag = "🔒 " + tag;

            GUILayout.Label(sail.DisplayName, _labelStyle);
            GUILayout.Label(tag, _labelStyle);

            bool clicked = GUILayout.Button(permission.Allowed ? "Usar" : "Bloqueada", GUILayout.Width(CellSize - 10));
            GUILayout.EndVertical();

            if (clicked)
            {
                if (permission.Allowed)
                {
                    SailLog.Debug($"Applying sail: player selected '{sail.Id}'");
                    _targetComponent.RequestSetSail(sail.Id);
                }
                else
                {
                    SailLog.Debug($"Blocked sail click: '{sail.Id}' -- {permission.Describe(sail)}");
                }
            }
        }
    }
}
