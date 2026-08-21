using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Velas.Manager;
using Velas.Model;
using Velas.Permissions;
using Velas.Ships;

namespace Velas.UI
{
    /// <summary>Native-Valheim sail picker, visually aligned with NpcValheim windows.</summary>
    internal sealed class SailSelectorUI : MonoBehaviour
    {
        private const float Width = 940f;
        private const float Height = 640f;
        private bool _open;
        private Ship _targetShip;
        private ShipSailComponent _targetComponent;
        private GameObject _canvas;
        private RectTransform _grid;
        private TextMeshProUGUI _status;
        private string _renderSignature;
        private readonly List<Sprite> _previewSprites = new List<Sprite>();

        public bool IsOpen => _open;

        public void Toggle(Ship ship, ShipSailComponent component)
        {
            if (_open && _targetShip == ship) Close();
            else Open(ship, component);
        }

        public void Open(Ship ship, ShipSailComponent component)
        {
            if (ship == null || component == null) return;
            Close();
            _targetShip = ship;
            _targetComponent = component;
            _open = true;
            SailUiInputBlocker.IsOpen = true;
            TryBuildWindow();
            SailLog.Debug($"Opening native sail selector for ship '{ship.name}'");
        }

        public void Close()
        {
            if (!_open && _canvas == null) return;
            _open = false;
            SailUiInputBlocker.IsOpen = false;
            _targetShip = null;
            _targetComponent = null;
            _renderSignature = null;
            DestroyPreviews();
            if (_canvas != null) Destroy(_canvas);
            _canvas = null;
            _grid = null;
            _status = null;
        }

        private void OnDestroy() => Close();

        private void Update()
        {
            if (!_open) return;
            if (_canvas == null) TryBuildWindow();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (_targetShip == null)
            {
                Close();
                return;
            }
            var player = Player.m_localPlayer;
            if (player != null && Vector3.Distance(player.transform.position, _targetShip.transform.position) >
                SailConfig.MaxInteractionDistance.Value * 2f)
            {
                Close();
                return;
            }
            if (_grid == null) return;
            var signature = BuildSignature();
            if (signature != _renderSignature) RebuildCards(signature);
        }

        private void TryBuildWindow()
        {
            if (!_open || _canvas != null || !SailValheimUi.EnsureAssets()) return;
            _canvas = SailValheimUi.CreateCanvas("Velas_Selector", 5000);
            if (_canvas == null) return;
            var panel = SailValheimUi.Panel(_canvas.transform, Width, Height);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            var titleBar = SailValheimUi.Rect("TitleBar", panel);
            SailValheimUi.Anchor(titleBar, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -58f), Vector2.zero);
            titleBar.gameObject.AddComponent<Image>().color = Color.clear;
            titleBar.gameObject.AddComponent<SailDragWindow>().Target = panel;
            var title = SailValheimUi.Label(titleBar, "Velas do navio", 30, SailValheimUi.Orange,
                TextAlignmentOptions.Center, true);
            SailValheimUi.Stretch((RectTransform)title.transform, 60f, 10f);

            var closeX = SailValheimUi.Button(panel, "X", 36f, 36f, 18);
            SailValheimUi.Anchor((RectTransform)closeX.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-52f, -52f), new Vector2(-16f, -16f));
            closeX.onClick.AddListener(Close);

            _status = SailValheimUi.Label(panel, string.Empty, 16, SailValheimUi.Beige,
                TextAlignmentOptions.Left);
            SailValheimUi.Anchor((RectTransform)_status.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(30f, -94f), new Vector2(-30f, -62f));

            var inlay = SailValheimUi.Inlay(panel, "SailGrid");
            SailValheimUi.Anchor(inlay, Vector2.zero, Vector2.one,
                new Vector2(24f, 70f), new Vector2(-24f, -102f));
            _grid = SailValheimUi.ScrollGrid(inlay, new Vector2(204f, 200f), new Vector2(10f, 10f), 4);

            var hint = SailValheimUi.Label(panel, "Selecione uma vela para aplicá-la ao navio próximo.", 15,
                SailValheimUi.Muted, TextAlignmentOptions.Left);
            SailValheimUi.Anchor((RectTransform)hint.transform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(26f, 20f), new Vector2(-190f, 54f));
            var close = SailValheimUi.Button(panel, "Fechar", 150f, 40f, 16);
            SailValheimUi.Anchor((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-174f, 14f), new Vector2(-24f, 54f));
            close.onClick.AddListener(Close);
            RebuildCards(BuildSignature());
        }

        private string BuildSignature() =>
            $"{SailManager.RemoteState}|{SailManager.AllSails.Count}|{_targetComponent?.CurrentSailId}";

        private void RebuildCards(string signature)
        {
            _renderSignature = signature;
            DestroyPreviews();
            for (var i = _grid.childCount - 1; i >= 0; i--) Destroy(_grid.GetChild(i).gameObject);
            var sails = SailManager.AllSails.OrderBy(s => s.Source).ThenBy(s => s.DisplayName).ToList();
            _status.text = StatusText(sails.Count);
            _status.color = SailManager.RemoteState == RemoteSailsState.Unavailable
                ? SailValheimUi.Yellow : SailValheimUi.Beige;
            foreach (var sail in sails) CreateCard(sail);
        }

        private string StatusText(int count)
        {
            switch (SailManager.RemoteState)
            {
                case RemoteSailsState.Loading:
                    return $"{count} vela(s) disponíveis  •  carregando catálogo remoto...";
                case RemoteSailsState.Unavailable:
                    return $"{count} vela(s) disponíveis  •  catálogo remoto indisponível";
                case RemoteSailsState.Loaded:
                    return $"{count} vela(s) disponíveis  •  catálogo remoto conectado";
                default:
                    return $"{count} vela(s) disponíveis";
            }
        }

        private void CreateCard(SailDefinition sail)
        {
            var permission = SailPermissionService.CanUseLocal(sail);
            var current = _targetComponent != null && _targetComponent.CurrentSailId == sail.Id;
            var card = SailValheimUi.Inlay(_grid, $"Sail_{sail.Id}");
            if (current) card.GetComponent<Image>().color = new Color(0.34f, 0.24f, 0.04f, 0.9f);

            var previewFrame = SailValheimUi.Rect("Preview", card);
            SailValheimUi.Anchor(previewFrame, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(14f, -112f), new Vector2(-14f, -10f));
            previewFrame.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);
            var texture = SailManager.ResolveTexture(sail.Id);
            if (texture != null)
            {
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                _previewSprites.Add(sprite);
                var imageRect = SailValheimUi.Rect("Image", previewFrame);
                SailValheimUi.Stretch(imageRect, 5f, 5f);
                var image = imageRect.gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = permission.Allowed ? Color.white : new Color(0.42f, 0.42f, 0.42f, 0.78f);
                image.raycastTarget = false;
            }

            var name = SailValheimUi.Label(card, sail.DisplayName, 16,
                current ? SailValheimUi.Yellow : SailValheimUi.Orange, TextAlignmentOptions.Center);
            SailValheimUi.Anchor((RectTransform)name.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(10f, -139f), new Vector2(-10f, -114f));
            var type = current ? "Em uso" : SailType(sail, permission.Allowed);
            var typeLabel = SailValheimUi.Label(card, type, 13,
                current ? SailValheimUi.Yellow : SailValheimUi.Muted, TextAlignmentOptions.Center);
            SailValheimUi.Anchor((RectTransform)typeLabel.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(8f, -161f), new Vector2(-8f, -139f));

            var button = SailValheimUi.Button(card,
                current ? "Selecionada" : permission.Allowed ? "Usar" : "Bloqueada", 174f, 30f, 14);
            SailValheimUi.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-87f, 7f), new Vector2(87f, 37f));
            button.interactable = permission.Allowed && !current;
            if (permission.Allowed && !current)
            {
                button.onClick.AddListener(() =>
                {
                    if (_targetComponent == null) return;
                    SailLog.Debug($"Applying sail from native selector: '{sail.Id}'");
                    _targetComponent.RequestSetSail(sail.Id);
                    RebuildCards(BuildSignature());
                });
            }
        }

        private static string SailType(SailDefinition sail, bool allowed)
        {
            if (!allowed) return "Bloqueada por clã";
            if (sail.Source == SailSource.Generic) return "Genérica";
            return sail.IsPublic ? "Personalizada" : $"Clã: {sail.ClanId}";
        }

        private void DestroyPreviews()
        {
            foreach (var sprite in _previewSprites)
                if (sprite != null) Destroy(sprite);
            _previewSprites.Clear();
        }
    }
}
