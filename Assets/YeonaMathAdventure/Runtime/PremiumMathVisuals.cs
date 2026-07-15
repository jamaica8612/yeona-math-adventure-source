using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YeonaMathAdventure
{
    /// <summary>
    /// Lightweight 2.5D presentation helpers used by the dedicated Math Journey.
    /// Everything is created at runtime because the Antura build setup deliberately
    /// regenerates the single bootstrap scene before tests and Android builds.
    /// </summary>
    public static class PremiumMathVisuals
    {
        public const string StarBridgeBackplate = "YeonaMathAdventure/Art/StarBridge-Backplate";
        public const string ForestFeastBackplate = "YeonaMathAdventure/Art/ForestFeast-Backplate";
        public const string MirrorGardenBackplate = "YeonaMathAdventure/Art/MirrorGarden-Backplate";
        public const string StarCompanion = "YeonaMathAdventure/Art/StarCompanion";

        private static Sprite roundedSprite;
        private static Sprite circleSprite;
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> loadedSprites =
            new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// Resources에 전용 아트가 있으면 이미지에 입히고 true를 돌려준다. 아트가 아직
        /// 없으면 false — 호출부는 기존 코드 생성 도형으로 폴백한다.
        /// </summary>
        public static bool TryApplyItemSprite(Image image, string resourcePath)
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

        public static void AddBackdrop(Transform parent, string resourcePath, Color veil)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning("[Yeona Math] Premium backdrop was not found at Resources/" + resourcePath);
                return;
            }

            GameObject backdropObject = MathUiKit.CreateRect("ScenicBackdrop", parent);
            RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
            MathUiKit.Stretch(backdropRect);
            RawImage backdrop = backdropObject.AddComponent<RawImage>();
            backdrop.texture = texture;
            backdrop.raycastTarget = false;
            backdropObject.AddComponent<RawImageAspectFill>();
            backdropRect.SetAsFirstSibling();

            if (veil.a > 0.001f)
            {
                GameObject veilObject = MathUiKit.CreateRect("AtmosphereVeil", parent);
                MathUiKit.Stretch(veilObject.GetComponent<RectTransform>());
                Image veilImage = veilObject.AddComponent<Image>();
                veilImage.color = veil;
                veilImage.raycastTarget = false;
                veilObject.transform.SetSiblingIndex(1);
            }
        }

        public static void StylePanel(Image image, Color color, bool shadow = true)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            if (shadow && image.GetComponent<Shadow>() == null)
            {
                Shadow dropShadow = image.gameObject.AddComponent<Shadow>();
                dropShadow.effectColor = new Color(0.08f, 0.12f, 0.26f, 0.22f);
                dropShadow.effectDistance = new Vector2(0f, -8f);
                dropShadow.useGraphicAlpha = true;
            }
        }

        public static void StyleButton(Button button, Image image, Color color)
        {
            StylePanel(image, color, true);
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            colors.pressedColor = new Color(0.86f, 0.9f, 0.98f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.7f, 0.72f, 0.78f, 0.6f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (button.GetComponent<ClayPressMotion>() == null)
            {
                button.gameObject.AddComponent<ClayPressMotion>();
            }
        }

        public static void StyleOval(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = CircleSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            if (image.GetComponent<Shadow>() == null)
            {
                Shadow shadow = image.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0.07f, 0.11f, 0.22f, 0.24f);
                shadow.effectDistance = new Vector2(0f, -7f);
                shadow.useGraphicAlpha = true;
            }
        }

        public static Image AddCircle(Transform parent, string name, Color color)
        {
            GameObject circle = MathUiKit.CreateRect(name, parent);
            Image image = circle.AddComponent<Image>();
            image.sprite = CircleSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image AddCharacter(Transform parent, string name, string resourcePath,
            Vector2 anchorMin, Vector2 anchorMax, float floatAmplitude = 8f)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning("[Yeona Math] Character art was not found at Resources/" + resourcePath);
                return null;
            }

            GameObject characterObject = MathUiKit.CreateRect(name, parent);
            RectTransform rect = characterObject.GetComponent<RectTransform>();
            MathUiKit.Pin(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Image image = characterObject.AddComponent<Image>();
            image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            image.preserveAspect = true;
            image.raycastTarget = false;
            AmbientFloat motion = characterObject.AddComponent<AmbientFloat>();
            motion.amplitude = floatAmplitude;
            motion.speed = 0.65f;
            motion.phase = 1.2f;
            return image;
        }

        public static void AddAmbientSparkles(Transform parent, int count, int seed)
        {
            var random = new System.Random(seed);
            for (int index = 0; index < count; index++)
            {
                Image sparkle = AddCircle(parent, "Sparkle" + index,
                    new Color(1f, 0.88f, 0.32f, 0.35f + (float)random.NextDouble() * 0.4f));
                RectTransform rect = sparkle.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(
                    0.08f + (float)random.NextDouble() * 0.84f,
                    0.1f + (float)random.NextDouble() * 0.8f);
                rect.sizeDelta = Vector2.one * (8f + (float)random.NextDouble() * 18f);
                rect.anchoredPosition = Vector2.zero;
                AmbientFloat floatMotion = sparkle.gameObject.AddComponent<AmbientFloat>();
                floatMotion.amplitude = 6f + (float)random.NextDouble() * 12f;
                floatMotion.speed = 0.45f + (float)random.NextDouble() * 0.8f;
                floatMotion.phase = (float)random.NextDouble() * 6.28f;
            }
        }

        private static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null)
                {
                    roundedSprite = CreateRoundedSprite(64, 18f, false);
                }

                return roundedSprite;
            }
        }

        private static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null)
                {
                    circleSprite = CreateRoundedSprite(64, 32f, true);
                }

                return circleSprite;
            }
        }

        private static Sprite CreateRoundedSprite(int size, float radius, bool circle)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = circle ? "YeonaClayCircle" : "YeonaClayRoundedRect",
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
                        float distance = Mathf.Sqrt(px * px + py * py);
                        alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    }
                    else
                    {
                        float dx = Mathf.Max(px - (half - radius), 0f);
                        float dy = Mathf.Max(py - (half - radius), 0f);
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Vector4 border = circle ? Vector4.zero : new Vector4(22f, 22f, 22f, 22f);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, border);
            sprite.name = texture.name + "Sprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }

    public sealed class RawImageAspectFill : MonoBehaviour
    {
        private RawImage image;
        private RectTransform rect;

        private void Awake()
        {
            image = GetComponent<RawImage>();
            rect = transform as RectTransform;
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void Apply()
        {
            if (image == null || image.texture == null || rect == null || rect.rect.height <= 0f)
            {
                return;
            }

            image.uvRect = CalculateUvRect(image.texture.width, image.texture.height,
                rect.rect.width, rect.rect.height);
        }

        public static Rect CalculateUvRect(int textureWidth, int textureHeight, float rectWidth, float rectHeight)
        {
            float textureAspect = Mathf.Max(1, textureWidth) / (float)Mathf.Max(1, textureHeight);
            float rectAspect = Mathf.Max(1f, rectWidth) / Mathf.Max(1f, rectHeight);
            if (rectAspect > textureAspect)
            {
                float visibleHeight = textureAspect / rectAspect;
                return new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
            }

            float visibleWidth = rectAspect / textureAspect;
            return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
        }
    }

    public sealed class ClayPressMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Vector3 targetScale = Vector3.one;

        private void OnEnable()
        {
            transform.localScale = Vector3.one;
            targetScale = Vector3.one;
        }

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * 18f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = new Vector3(0.94f, 0.94f, 1f);
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

    public sealed class AmbientFloat : MonoBehaviour
    {
        public float amplitude = 10f;
        public float speed = 0.7f;
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
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = origin + Vector2.up * Mathf.Sin(Time.unscaledTime * speed + phase) * amplitude;
        }
    }
}
