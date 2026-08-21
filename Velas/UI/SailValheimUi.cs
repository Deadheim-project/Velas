using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace Velas.UI
{
    /// <summary>Native-Valheim UI helpers shared by the sail selector.</summary>
    internal static class SailValheimUi
    {
        public const int UiLayer = 5;
        public static readonly Color Orange = new Color(1f, 0.631f, 0.235f, 1f);
        public static readonly Color Beige = new Color(0.8529f, 0.725f, 0.5331f, 1f);
        public static readonly Color Yellow = new Color(1f, 0.889f, 0f, 1f);
        public static readonly Color Muted = new Color(0.62f, 0.58f, 0.50f, 1f);

        private static readonly ColorBlock ButtonColors = new ColorBlock
        {
            normalColor = new Color(0.824f, 0.824f, 0.824f, 1f),
            highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f),
            pressedColor = new Color(0.537f, 0.556f, 0.556f, 1f),
            selectedColor = new Color(0.824f, 0.824f, 0.824f, 1f),
            disabledColor = new Color(0.566f, 0.566f, 0.566f, 0.502f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        private static readonly ColorBlock ScrollColors = new ColorBlock
        {
            normalColor = new Color(0.926f, 0.645f, 0.34f, 1f),
            highlightedColor = new Color(1f, 0.786f, 0.088f, 1f),
            pressedColor = new Color(0.838f, 0.647f, 0.03f, 1f),
            selectedColor = new Color(1f, 0.786f, 0.088f, 1f),
            disabledColor = new Color(0.784f, 0.784f, 0.784f, 0.502f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };

        public static TMP_FontAsset BodyFont { get; private set; }
        public static TMP_FontAsset DisplayFont { get; private set; }
        public static Sprite PanelSprite { get; private set; }
        public static Sprite ButtonSprite { get; private set; }
        public static Sprite FieldSprite { get; private set; }
        private static Sprite _scrollHandleSprite;
        private static Sprite _scrollBackSprite;
        private static Material _panelMaterial;
        private static GameObject _buttonSfx;
        private static GameObject _selectSfx;
        private static bool _loaded;

        public static bool EnsureAssets()
        {
            if (_loaded) return true;
            var uiAtlas = Find<SpriteAtlas>("UIAtlas");
            var iconAtlas = Find<SpriteAtlas>("IconAtlas");
            Sprite FromAtlas(string name) => uiAtlas?.GetSprite(name) ?? iconAtlas?.GetSprite(name) ?? Find<Sprite>(name);

            PanelSprite = FromAtlas("woodpanel_trophys") ?? FromAtlas("woodpanel_settings");
            ButtonSprite = FromAtlas("button");
            FieldSprite = FromAtlas("text_field");
            _scrollHandleSprite = FromAtlas("UISprite");
            _scrollBackSprite = FromAtlas("Background");
            _panelMaterial = Find<Material>("litpanel");
            BodyFont = Find<TMP_FontAsset>("Valheim-AveriaSansLibre");
            DisplayFont = Find<TMP_FontAsset>("Valheim-Norse") ?? BodyFont;
            _buttonSfx = Find<GameObject>("sfx_gui_button");
            _selectSfx = Find<GameObject>("sfx_gui_select");
            if (PanelSprite == null || BodyFont == null) return false;
            _loaded = true;
            SailLog.Info($"native UI ready: panel='{PanelSprite.name}', font='{BodyFont.name}'");
            return true;
        }

        private static T Find<T>(string name) where T : UnityEngine.Object =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(x => x != null &&
                string.Equals(x.name, name, StringComparison.Ordinal));

        public static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var parent = FindGuiRoot();
            if (parent == null) return null;
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)) { layer = UiLayer };
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, 0f, 0f);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                                              AdditionalCanvasShaderChannels.Normal |
                                              AdditionalCanvasShaderChannels.Tangent;
            go.GetComponent<CanvasScaler>().referencePixelsPerUnit = 50f;
            go.transform.SetAsLastSibling();
            return go;
        }

        private static Transform FindGuiRoot()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "GuiRoot") return root.transform.Find("GUI");
                if (root.name == "_GameMain") return root.transform.Find("LoadingGUI");
            }
            return null;
        }

        public static RectTransform Rect(string name, Transform parent, bool active = true)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            if (!active) go.SetActive(false);
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Panel(Transform parent, float width, float height)
        {
            var rect = Rect("Panel", parent);
            rect.sizeDelta = new Vector2(width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = PanelSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            if (_panelMaterial != null) image.material = _panelMaterial;
            return rect;
        }

        public static RectTransform Inlay(Transform parent, string name)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);
            if (FieldSprite != null)
            {
                var frame = Rect("Frame", rect);
                Stretch(frame, 0f, 0f);
                var border = frame.gameObject.AddComponent<Image>();
                border.sprite = FieldSprite;
                border.type = Image.Type.Sliced;
                border.color = new Color(1f, 1f, 1f, 0.85f);
                border.raycastTarget = false;
            }
            return rect;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, int size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool display = false)
        {
            var rect = Rect("Label", parent, false);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = display ? DisplayFont : BodyFont;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.text = text ?? string.Empty;
            label.raycastTarget = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            rect.gameObject.SetActive(true);
            return label;
        }

        public static Button Button(Transform parent, string text, float width, float height, int size = 16)
        {
            var rect = Rect("Button", parent);
            rect.sizeDelta = new Vector2(width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = ButtonSprite;
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.image = image;
            button.colors = ButtonColors;
            AttachSfx(rect.gameObject);
            var label = Label(rect, text, size, Orange, TextAlignmentOptions.Center);
            Stretch((RectTransform)label.transform, 6f, 2f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = layout.minWidth = width;
            layout.preferredHeight = layout.minHeight = height;
            layout.flexibleHeight = 0f;
            return button;
        }

        private static void AttachSfx(GameObject go)
        {
            if (_buttonSfx == null) return;
            var sfx = go.AddComponent<ButtonSfx>();
            sfx.m_sfxPrefab = _buttonSfx;
            sfx.m_selectSfxPrefab = _selectSfx;
        }

        public static RectTransform ScrollGrid(Transform parent, Vector2 cellSize, Vector2 spacing, int columns)
        {
            const float barWidth = 13f;
            var viewport = Rect("Viewport", parent);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-(barWidth + 4f), 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;

            var bar = Rect("Scrollbar", parent);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(barWidth, 0f);
            var back = bar.gameObject.AddComponent<Image>();
            back.sprite = _scrollBackSprite;
            back.color = new Color(0f, 0f, 0f, 0.75f);
            var area = Rect("SlidingArea", bar);
            Stretch(area, 0f, 0f);
            var handle = Rect("Handle", area);
            Stretch(handle, 0f, 0f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = _scrollHandleSprite;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.colors = ScrollColors;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        public static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }

    internal sealed class SailDragWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Target;
        private Vector2 _offset;
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Target.parent,
                eventData.position, eventData.pressEventCamera, out var point);
            _offset = Target.anchoredPosition - point;
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (Target != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Target.parent, eventData.position, eventData.pressEventCamera, out var point))
                Target.anchoredPosition = point + _offset;
        }
    }
}
