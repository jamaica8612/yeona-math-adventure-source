using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YeonaMathAdventure
{
    public static class MathPalette
    {
        public static readonly Color Sky = Hex("#EAF7FF");
        public static readonly Color DeepBlue = Hex("#2457A6");
        public static readonly Color Ink = Hex("#183153");
        public static readonly Color White = Color.white;
        public static readonly Color Mint = Hex("#52C7A5");
        public static readonly Color MintLight = Hex("#DDF8EF");
        public static readonly Color Yellow = Hex("#FFD669");
        public static readonly Color Coral = Hex("#FF887B");
        public static readonly Color Lavender = Hex("#9D8CFF");
        public static readonly Color LavenderLight = Hex("#ECE8FF");
        public static readonly Color SoftBlue = Hex("#D9EEFF");
        public static readonly Color Surface = Hex("#FFFDF8");
        public static readonly Color Quiet = Hex("#677B96");
        public static readonly Color NightBlue = Hex("#33206E");
        public static readonly Color StarGold = Hex("#FFBE2E");
        public static readonly Color Berry = Hex("#F04F55");
        public static readonly Color Leaf = Hex("#58A94A");
        public static readonly Color ClayCream = Hex("#FFF4D8");

        public static Color Tile(int index)
        {
            switch ((index % 5 + 5) % 5)
            {
                case 0: return Mint;
                case 1: return Lavender;
                case 2: return Coral;
                case 3: return Yellow;
                default: return DeepBlue;
            }
        }

        private static Color Hex(string value)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(value, out parsed) ? parsed : Color.white;
        }
    }

    public static class MathUiKit
    {
        public const float LargeTouchHeight = 104f;

        public static float ComputeReferenceScale(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return 1f;
            }

            // The game is landscape-only. Matching height keeps the same usable vertical budget
            // on 16:9, 20:9 and 4:3 instead of shrinking it on wider phones.
            return screenHeight / 1080f;
        }

        public static Canvas CreateCanvas(Transform parent, out RectTransform safeRoot, out RectTransform dragLayer)
        {
            GameObject canvasObject = new GameObject(
                "YeonaMathCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject background = CreateRect("Background", canvasObject.transform);
            Stretch(background.GetComponent<RectTransform>());
            background.AddComponent<Image>().color = MathPalette.Sky;

            GameObject safe = CreateRect("SafeArea", canvasObject.transform);
            safeRoot = safe.GetComponent<RectTransform>();
            Stretch(safeRoot);
            safe.AddComponent<SafeAreaFitter>();

            GameObject drag = CreateRect("DragLayer", canvasObject.transform);
            dragLayer = drag.GetComponent<RectTransform>();
            Stretch(dragLayer);
            dragLayer.SetAsLastSibling();
            return canvas;
        }

        public static RectTransform CreateScreen(Transform parent, string name, Color background)
        {
            GameObject screen = CreateRect(name, parent);
            RectTransform rect = screen.GetComponent<RectTransform>();
            Stretch(rect);
            Image image = screen.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = false;
            return rect;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color, float preferredWidth = -1f,
            float preferredHeight = -1f)
        {
            GameObject panel = CreateRect(name, parent);
            Image image = panel.AddComponent<Image>();
            PremiumMathVisuals.StylePanel(image, color, color.a > 0.05f);
            LayoutElement element = panel.AddComponent<LayoutElement>();
            if (preferredWidth >= 0f)
            {
                element.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                element.preferredHeight = preferredHeight;
            }

            return panel.GetComponent<RectTransform>();
        }

        public static RectTransform CreateVertical(Transform parent, string name, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.UpperCenter)
        {
            GameObject container = CreateRect(name, parent);
            VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return container.GetComponent<RectTransform>();
        }

        public static RectTransform CreateHorizontal(Transform parent, string name, float spacing, RectOffset padding = null,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GameObject container = CreateRect(name, parent);
            HorizontalLayoutGroup layout = container.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset();
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return container.GetComponent<RectTransform>();
        }

        public static RectTransform CreateGrid(Transform parent, string name, Vector2 cellSize, Vector2 spacing,
            int columns, RectOffset padding = null)
        {
            GameObject container = CreateRect(name, parent);
            GridLayoutGroup grid = container.AddComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = padding ?? new RectOffset();
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            return container.GetComponent<RectTransform>();
        }

        public static TMP_Text CreateText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            GameObject textObject = CreateRect(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = KoreanFontProvider.Resolve();
            if (font != null)
            {
                text.font = font;
            }

            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, Color background, Color foreground,
            Action onClick, float width = -1f, float height = LargeTouchHeight, float fontSize = 36f)
        {
            GameObject buttonObject = CreateRect(name, parent);
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            PremiumMathVisuals.StyleButton(button, image, background);
            if (onClick != null)
            {
                button.onClick.AddListener(delegate { onClick(); });
            }

            LayoutElement element = buttonObject.AddComponent<LayoutElement>();
            element.minHeight = Mathf.Max(88f, height);
            element.preferredHeight = height;
            if (width > 0f)
            {
                element.minWidth = Mathf.Min(width, 120f);
                element.preferredWidth = width;
            }

            TMP_Text text = CreateText(buttonObject.transform, "Label", label, fontSize, foreground,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform, 18f, 12f);
            return button;
        }

        public static void ExpandHitTarget(Button button, float padding = 16f)
        {
            if (button == null || padding <= 0f)
            {
                return;
            }

            GameObject target = CreateRect("ExpandedHitTarget", button.transform);
            RectTransform rect = target.GetComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(-padding, -padding);
            rect.offsetMax = new Vector2(padding, padding);
            Image hitImage = target.AddComponent<Image>();
            hitImage.color = Color.clear;
            hitImage.raycastTarget = true;
            target.transform.SetAsFirstSibling();
        }

        public static RectTransform CreateSpacer(Transform parent, float height, float width = 1f)
        {
            GameObject spacer = CreateRect("Spacer", parent);
            LayoutElement layout = spacer.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.preferredWidth = width;
            layout.flexibleWidth = 1f;
            return spacer.GetComponent<RectTransform>();
        }

        public static void SetLayout(RectTransform rect, float preferredWidth, float preferredHeight,
            float flexibleWidth = 0f, float flexibleHeight = 0f)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = rect.gameObject.AddComponent<LayoutElement>();
            }

            if (preferredWidth >= 0f)
            {
                element.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                element.preferredHeight = preferredHeight;
            }

            element.flexibleWidth = flexibleWidth;
            element.flexibleHeight = flexibleHeight;
        }

        public static ScrollRect CreateScrollView(Transform parent, string name, out RectTransform content,
            bool horizontal, bool vertical)
        {
            GameObject viewport = CreateRect(name, parent);
            Image image = viewport.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.22f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 55f;

            GameObject contentObject = CreateRect("Content", viewport.transform);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            return scroll;
        }

        public static string ActivityLabel(MathActivityKind kind)
        {
            switch (kind)
            {
                case MathActivityKind.FairShare: return "공평하게 나누기";
                case MathActivityKind.PatternSpace: return "규칙과 공간 퍼즐";
                default: return "목표 숫자 만들기";
            }
        }

        public static string ConceptLabel(MathCore.MathConcept concept)
        {
            switch (concept)
            {
                case MathCore.MathConcept.TargetNumber: return "수와 연산";
                case MathCore.MathConcept.DivisionAndRemainder: return "나눗셈과 나머지";
                case MathCore.MathConcept.NumericPattern: return "수의 규칙";
                case MathCore.MathConcept.ShapePattern: return "도형 규칙";
                case MathCore.MathConcept.Rotation: return "도형 회전";
                case MathCore.MathConcept.Symmetry: return "대칭";
                default: return "공간 감각";
            }
        }

        public static GameObject CreateRect(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.transform.SetParent(parent, false);
            return created;
        }

        public static void Stretch(RectTransform rect, float horizontalInset = 0f, float verticalInset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        public static void Pin(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        public static T FindInParents<T>(GameObject gameObject) where T : Component
        {
            Transform current = gameObject == null ? null : gameObject.transform;
            while (current != null)
            {
                T component = current.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }

                current = current.parent;
            }

            return null;
        }
    }

    /// <summary>
    /// Fixed vertical budgets shared by the game views and EditMode clipping tests.
    /// The 4:3 case has the same 1080 reference height but the least horizontal room,
    /// so every game must fit inside the body's 700 preferred reference pixels before
    /// any flexible-height surplus is considered.
    /// </summary>
    public static class MathGameLayoutBudget
    {
        public const float BodyPreferredHeight = 700f;
        public const float ActionHeight = 104f;
        public const float ShellFixedHeight = 282f;

        public const float TargetVerticalPadding = 8f;
        public const float TargetSpacing = 6f;
        public const float TargetPromptHeight = 52f;
        public const float TargetCardHeight = 68f;
        public const float TargetExpressionHeight = 124f;
        public const float TargetBlocksHeight = 272f;

        public const float FairVerticalPadding = 32f;
        public const float FairSpacing = 12f;
        public const float FairPromptHeight = 64f;
        public const float FairWorkspaceHeight = 420f;
        public const float FairWorkspacePanelHeight = 410f;
        public const float FairSourceScrollHeight = 336f;
        public const float FairRecipientScrollWithRemainderHeight = 248f;
        public const float FairRecipientScrollWithoutRemainderHeight = 335f;

        public const float PatternVerticalPadding = 8f;
        public const float PatternSpacing = 5f;
        public const float PatternPromptHeight = 48f;
        public const float PatternMaximumBoardHeight = 332f;
        public const float PatternChoicesHeight = 140f;

        public static float AvailableBodyHeight(int screenWidth, int screenHeight)
        {
            return AvailableBodyHeightForSafeArea(screenWidth, screenHeight, screenHeight);
        }

        public static float AvailableBodyHeightForSafeArea(int screenWidth, int screenHeight, int safeAreaHeight)
        {
            float scale = MathUiKit.ComputeReferenceScale(screenWidth, screenHeight);
            return Mathf.Max(0, safeAreaHeight) / Mathf.Max(0.0001f, scale) - ShellFixedHeight;
        }

        public static float AvailableGameContentWidth(int screenWidth, int screenHeight, float innerHorizontalPadding)
        {
            float scale = MathUiKit.ComputeReferenceScale(screenWidth, screenHeight);
            return screenWidth / Mathf.Max(0.0001f, scale) - 84f - Mathf.Max(0f, innerHorizontalPadding);
        }

        public static float TargetPreferredContentHeight
        {
            get
            {
                return TargetVerticalPadding + TargetSpacing * 4f + TargetPromptHeight + TargetCardHeight +
                       TargetExpressionHeight + TargetBlocksHeight + ActionHeight;
            }
        }

        public static float FairPreferredContentHeight
        {
            get
            {
                return FairVerticalPadding + FairSpacing * 2f + FairPromptHeight + FairWorkspaceHeight + ActionHeight;
            }
        }

        public static float PatternPreferredContentHeight
        {
            get
            {
                return PatternVerticalPadding + PatternSpacing * 3f + PatternPromptHeight +
                       PatternMaximumBoardHeight + PatternChoicesHeight + ActionHeight;
            }
        }
    }

    /// <summary>
    /// Shared pointer component for large blocks. Every block can be tapped or
    /// dragged; game views decide whether the release target accepts it.
    /// </summary>
    public sealed class TouchDragItem : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler
    {
        public int sourceIndex;
        public int amount = 1;
        public Action<TouchDragItem> pressed;
        public Action<TouchDragItem> tapped;
        public Action<TouchDragItem, GameObject> released;

        private RectTransform rect;
        private CanvasGroup canvasGroup;
        private RectTransform dragLayer;
        private Transform originalParent;
        private int originalSiblingIndex;
        private Transform committedParent;
        private bool dragging;

        public void Configure(RectTransform layer)
        {
            dragLayer = layer;
            rect = transform as RectTransform;
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        public void CommitTo(Transform destination)
        {
            committedParent = destination;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pressed != null)
            {
                pressed(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (dragLayer == null)
            {
                return;
            }

            dragging = true;
            committedParent = null;
            originalParent = transform.parent;
            originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetParent(dragLayer, true);
            transform.SetAsLastSibling();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.88f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging && rect != null)
            {
                rect.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
            if (released != null)
            {
                released(this, eventData.pointerCurrentRaycast.gameObject);
            }

            Transform destination = committedParent == null ? originalParent : committedParent;
            transform.SetParent(destination, false);
            if (committedParent == null && originalParent != null)
            {
                transform.SetSiblingIndex(Mathf.Clamp(originalSiblingIndex, 0, originalParent.childCount - 1));
            }

            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            dragging = false;
            committedParent = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!dragging && tapped != null)
            {
                tapped(this);
            }
        }
    }

    public sealed class MathDropZone : MonoBehaviour
    {
        public int zoneIndex;
        public RectTransform contentRoot;
    }
}
