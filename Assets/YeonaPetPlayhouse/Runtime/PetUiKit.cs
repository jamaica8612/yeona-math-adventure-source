using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YeonaPetPlayhouse
{
    /// <summary>따뜻한 파스텔 팔레트. 새 색은 여기에만 추가한다.</summary>
    public static class PetPalette
    {
        public static readonly Color Cream = Hex("#FFF6E9");
        public static readonly Color WallPink = Hex("#FFE3EC");
        public static readonly Color FloorWood = Hex("#F4D8AE");
        public static readonly Color Ink = Hex("#4A3B5C");
        public static readonly Color White = Color.white;
        public static readonly Color KittyBody = Hex("#FFF1DC");
        public static readonly Color KittyEar = Hex("#FFC9A3");
        public static readonly Color Blush = Hex("#FFB3C7");
        public static readonly Color Carrot = Hex("#FF9F4A");
        public static readonly Color CarrotLeaf = Hex("#6FC46D");
        public static readonly Color BubbleBlue = Hex("#BDE7FF");
        public static readonly Color SoapMint = Hex("#8FDCC4");
        public static readonly Color MoonGold = Hex("#FFD97A");
        public static readonly Color NightVeil = new Color(0.10f, 0.07f, 0.25f, 0.82f);
        public static readonly Color HeartRed = Hex("#FF7A96");
        public static readonly Color StarGold = Hex("#FFC94D");

        private static Color Hex(string value)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(value, out parsed) ? parsed : Color.white;
        }
    }

    /// <summary>
    /// 코드로 UI를 조립하는 작은 키트. 검증된 수학 앱의 킷을 펫 게임용으로 다듬은 버전.
    /// 모든 터치 대상은 지름 120px 이상(만 4세 손가락 기준).
    /// </summary>
    public static class PetUiKit
    {
        private static Sprite roundedSprite;
        private static Sprite circleSprite;
        private static readonly Dictionary<string, Sprite> loadedSprites = new Dictionary<string, Sprite>();

        public static Canvas CreateCanvas(Transform parent, out RectTransform safeRoot, out RectTransform overlayLayer)
        {
            GameObject canvasObject = new GameObject(
                "PlayhouseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            GameObject background = CreateRect("Background", canvasObject.transform);
            Stretch(background.GetComponent<RectTransform>());
            background.AddComponent<Image>().color = PetPalette.Cream;

            GameObject safe = CreateRect("SafeArea", canvasObject.transform);
            safeRoot = safe.GetComponent<RectTransform>();
            Stretch(safeRoot);
            safe.AddComponent<SafeAreaFitter>();

            GameObject overlay = CreateRect("OverlayLayer", canvasObject.transform);
            overlayLayer = overlay.GetComponent<RectTransform>();
            Stretch(overlayLayer);
            overlayLayer.SetAsLastSibling();
            return canvas;
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

        public static void Pin(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        public static Image CreateRoundedPanel(Transform parent, string name, Color color)
        {
            GameObject panel = CreateRect(name, parent);
            Image image = panel.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        public static Image CreateCircle(Transform parent, string name, Color color, bool raycast = false)
        {
            GameObject circle = CreateRect(name, parent);
            Image image = circle.AddComponent<Image>();
            image.sprite = CircleSprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static TMP_Text CreateText(Transform parent, string name, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject textObject = CreateRect(name, parent);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = KidFontProvider.Resolve();
            if (font != null)
            {
                text.font = font;
            }

            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            Stretch(text.rectTransform);
            return text;
        }

        /// <summary>큰 원형 아이콘 버튼. iconBuilder가 버튼 안을 채운다(그림 or 코드 도형).</summary>
        public static Button CreateRoundButton(Transform parent, string name, Color background,
            Action onClick, Action<RectTransform> iconBuilder)
        {
            GameObject buttonObject = CreateRect(name, parent);
            Image image = buttonObject.AddComponent<Image>();
            image.sprite = CircleSprite;
            image.color = background;
            Shadow shadow = buttonObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.3f, 0.2f, 0.3f, 0.25f);
            shadow.effectDistance = new Vector2(0f, -6f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(delegate { onClick(); });
            }

            buttonObject.AddComponent<SquishOnPress>();
            if (iconBuilder != null)
            {
                GameObject icon = CreateRect("Icon", buttonObject.transform);
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                Stretch(iconRect, 18f, 18f);
                iconBuilder(iconRect);
            }

            return button;
        }

        /// <summary>Resources에 전용 아트가 있으면 입히고 true. 없으면 false(코드 도형 폴백).</summary>
        public static bool TryApplySprite(Image image, string resourcePath)
        {
            if (image == null || string.IsNullOrEmpty(resourcePath))
            {
                return false;
            }

            Sprite sprite;
            if (!loadedSprites.TryGetValue(resourcePath, out sprite))
            {
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                sprite = texture == null
                    ? null
                    : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                loadedSprites[resourcePath] = sprite;
            }

            if (sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            return true;
        }

        public static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null)
                {
                    roundedSprite = CreateGeneratedSprite(64, 18f, false);
                }

                return roundedSprite;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null)
                {
                    circleSprite = CreateGeneratedSprite(64, 32f, true);
                }

                return circleSprite;
            }
        }

        private static Sprite CreateGeneratedSprite(int size, float radius, bool circle)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half);
                    float py = Mathf.Abs(y + 0.5f - half);
                    float alpha;
                    if (circle)
                    {
                        alpha = Mathf.Clamp01(radius + 0.75f - Mathf.Sqrt(px * px + py * py));
                    }
                    else
                    {
                        float dx = Mathf.Max(px - (half - radius), 0f);
                        float dy = Mathf.Max(py - (half - radius), 0f);
                        alpha = Mathf.Clamp01(radius + 0.75f - Mathf.Sqrt(dx * dx + dy * dy));
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Vector4 border = circle ? Vector4.zero : new Vector4(22f, 22f, 22f, 22f);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }

    /// <summary>주아체(Jua) 로더. 빌드 스크립트가 구워 둔 정적 TMP 에셋만 사용한다.</summary>
    public static class KidFontProvider
    {
        private const string PrebuiltFontResourcePath = "YeonaPetPlayhouse/Fonts/Jua-Static";

        private static TMP_FontAsset cached;
        private static bool resolutionAttempted;

        public static TMP_FontAsset Resolve()
        {
            if (resolutionAttempted)
            {
                return cached;
            }

            resolutionAttempted = true;
            cached = Resources.Load<TMP_FontAsset>(PrebuiltFontResourcePath);
            if (cached == null)
            {
                Debug.LogWarning("[Yeona Pet] 구워진 Jua 폰트가 없습니다. 'Yeona Pet > Setup' 실행 필요. TMP 기본 폰트로 표시됩니다(한글 미지원 가능).");
            }

            return cached;
        }
    }

    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect appliedArea;

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (UnityEngine.Screen.safeArea != appliedArea)
            {
                Apply();
            }
        }

        private void Apply()
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            Rect safeArea = UnityEngine.Screen.safeArea;
            appliedArea = safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Mathf.Max(1f, UnityEngine.Screen.width);
            anchorMin.y /= Mathf.Max(1f, UnityEngine.Screen.height);
            anchorMax.x /= Mathf.Max(1f, UnityEngine.Screen.width);
            anchorMax.y /= Mathf.Max(1f, UnityEngine.Screen.height);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>누르면 말랑하게 눌리는 촉감.</summary>
    public sealed class SquishOnPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 targetScale = Vector3.one;

        private void OnEnable()
        {
            transform.localScale = Vector3.one;
            targetScale = Vector3.one;
        }

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 16f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = new Vector3(0.9f, 0.9f, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            targetScale = Vector3.one;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = Vector3.one;
        }
    }

    /// <summary>둥실둥실 떠 있는 숨쉬기 모션.</summary>
    public sealed class GentleBob : MonoBehaviour
    {
        public float amplitude = 10f;
        public float speed = 0.8f;
        public float phase;

        private RectTransform rect;
        private Vector2 origin;

        private void Awake()
        {
            rect = transform as RectTransform;
            origin = rect == null ? Vector2.zero : rect.anchoredPosition;
        }

        private void Update()
        {
            if (rect != null)
            {
                rect.anchoredPosition = origin + Vector2.up * (Mathf.Sin(Time.time * speed + phase) * amplitude);
            }
        }
    }

    /// <summary>코루틴 기반 연출 모음 — 외부 트윈 라이브러리 없이 상용 감각의 '주스'를 만든다.</summary>
    public static class PetJuice
    {
        public static IEnumerator ScalePunch(Transform target, float punch = 1.18f, float duration = 0.34f)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 baseScale = Vector3.one;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                // 통통 튀는 감쇠 사인 곡선.
                float wobble = Mathf.Sin(progress * Mathf.PI * 3f) * (1f - progress);
                target.localScale = baseScale * (1f + (punch - 1f) * wobble);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = baseScale;
            }
        }

        public static IEnumerator FloatAndFade(RectTransform target, Vector2 velocity, float duration = 1.1f)
        {
            if (target == null)
            {
                yield break;
            }

            CanvasGroup group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.gameObject.AddComponent<CanvasGroup>();
            }

            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                target.anchoredPosition += velocity * Time.deltaTime;
                group.alpha = 1f - progress * progress;
                target.localScale = Vector3.one * (1f + 0.25f * progress);
                yield return null;
            }

            if (target != null)
            {
                UnityEngine.Object.Destroy(target.gameObject);
            }
        }

        public static IEnumerator FadeImage(Image image, float from, float to, float duration)
        {
            if (image == null)
            {
                yield break;
            }

            float elapsed = 0f;
            Color color = image.color;
            while (elapsed < duration && image != null)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                image.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            if (image != null)
            {
                image.color = new Color(color.r, color.g, color.b, to);
            }
        }
    }
}
