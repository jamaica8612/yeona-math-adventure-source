using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YeonaPetPlayhouse.PetCore;

namespace YeonaPetPlayhouse
{
    /// <summary>
    /// 연아의 별냥이 놀이집 — 원 씬 장난감 상자. 실패·점수·시간제한 없음.
    /// 모든 UI는 코드로 조립하고, 전용 아트가 Resources에 들어오면 자동 교체된다.
    /// </summary>
    public sealed class PlayhouseBootstrap : MonoBehaviour
    {
        public const string PetNameKo = "나비";

        private const string SaveKeyHunger = "yeona_pet_hunger";
        private const string SaveKeyDirt = "yeona_pet_dirt";
        private const string SaveKeySleepiness = "yeona_pet_sleepiness";
        private const string SaveKeyLastSeen = "yeona_pet_last_seen_unix";
        // 오래 방치해도 욕구를 이 값 이상으로 복원하지 않는다 — 다시 켰을 때
        // "혼나는 기분"이 들지 않게 하는 무실패 규칙.
        private const float RestoreCap = 80f;
        private const float OfflineMaxMinutes = 480f;

        private PetSimulation pet;
        private RectTransform safeRoot;
        private RectTransform overlayLayer;

        private RectTransform petRoot;
        private RectTransform petMouthAnchor;
        private GameObject eyesOpen;
        private GameObject eyesClosed;
        private Image petArtImage;
        private GameObject codeArtGroup;

        private RectTransform moodBubble;
        private RectTransform moodIconRoot;
        private PetMood lastMood = PetMood.Happy;
        private PetMood lastAnnouncedMood = PetMood.Happy;

        private Image nightOverlayImage;
        private Button nightOverlayButton;
        private bool bathing;
        private BathScrubCatcher bathCatcher;
        private float scrubCooldown;
        private float saveCooldown;

        private void Awake()
        {
            UnityEngine.Screen.orientation = ScreenOrientation.AutoRotation;
            UnityEngine.Screen.autorotateToLandscapeLeft = true;
            UnityEngine.Screen.autorotateToLandscapeRight = true;
            UnityEngine.Screen.autorotateToPortrait = false;
            UnityEngine.Screen.autorotateToPortraitUpsideDown = false;
            Application.targetFrameRate = 60;

            EnsureEventSystem();
            PetVoice.Initialize(this);
            pet = LoadPet();

            PetUiKit.CreateCanvas(transform, out safeRoot, out overlayLayer);
            BuildRoom();
            BuildPet();
            BuildMoodBubble();
            BuildActionButtons();
            BuildNightOverlay();

            RefreshMoodVisuals(true);
            PetVoice.Play("greeting");
        }

        private void Update()
        {
            if (pet == null)
            {
                return;
            }

            bool wasSleeping = pet.IsSleeping;
            pet.Tick(Time.deltaTime);
            if (wasSleeping && !pet.IsSleeping)
            {
                OnMorning();
            }

            if (pet.CurrentMood != lastMood)
            {
                RefreshMoodVisuals(false);
            }

            scrubCooldown -= Time.deltaTime;
            saveCooldown -= Time.deltaTime;
            if (saveCooldown <= 0f)
            {
                saveCooldown = 20f;
                SavePet();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SavePet();
            }
        }

        private void OnApplicationQuit()
        {
            SavePet();
        }

        // ---------- 방 꾸미기 ----------

        private void BuildRoom()
        {
            RectTransform wall = PetUiKit.CreateRect("Wall", safeRoot).GetComponent<RectTransform>();
            PetUiKit.Pin(wall, new Vector2(0f, 0.32f), new Vector2(1f, 1f));
            Image wallImage = wall.gameObject.AddComponent<Image>();
            wallImage.color = PetPalette.WallPink;
            wallImage.raycastTarget = false;
            if (PetUiKit.TryApplySprite(wallImage, "YeonaPetPlayhouse/Art/Room-Wall"))
            {
                wallImage.preserveAspect = false;
            }

            RectTransform floor = PetUiKit.CreateRect("Floor", safeRoot).GetComponent<RectTransform>();
            PetUiKit.Pin(floor, new Vector2(0f, 0f), new Vector2(1f, 0.33f));
            Image floorImage = floor.gameObject.AddComponent<Image>();
            floorImage.color = PetPalette.FloorWood;
            floorImage.raycastTarget = false;

            // 창문 + 해: 단순한 도형 데코 (아트 교체 대상).
            Image window = PetUiKit.CreateCircle(safeRoot, "Window", PetPalette.BubbleBlue);
            PetUiKit.Pin(window.rectTransform, new Vector2(0.08f, 0.62f), new Vector2(0.24f, 0.9f));
            Image sun = PetUiKit.CreateCircle(window.transform, "Sun", PetPalette.MoonGold);
            PetUiKit.Pin(sun.rectTransform, new Vector2(0.55f, 0.55f), new Vector2(0.9f, 0.9f));

            Image rug = PetUiKit.CreateCircle(safeRoot, "Rug", new Color(1f, 1f, 1f, 0.55f));
            PetUiKit.Pin(rug.rectTransform, new Vector2(0.28f, 0.04f), new Vector2(0.72f, 0.3f));

            TMPro.TMP_Text title = PetUiKit.CreateText(safeRoot, "Title", "별냥이 " + PetNameKo, 52f, PetPalette.Ink,
                TMPro.TextAlignmentOptions.Center);
            PetUiKit.Pin(title.rectTransform, new Vector2(0.68f, 0.88f), new Vector2(0.98f, 0.99f));
        }

        // ---------- 별토끼 ----------

        private void BuildPet()
        {
            petRoot = PetUiKit.CreateRect("Pet", safeRoot).GetComponent<RectTransform>();
            PetUiKit.Pin(petRoot, new Vector2(0.36f, 0.16f), new Vector2(0.64f, 0.72f));
            GentleBob bob = petRoot.gameObject.AddComponent<GentleBob>();
            bob.amplitude = 9f;
            bob.speed = 1.1f;

            codeArtGroup = PetUiKit.CreateRect("CodeArt", petRoot);
            PetUiKit.Stretch(codeArtGroup.GetComponent<RectTransform>());

            // 고양이 삼각 귀: 마름모(45도 회전)를 몸통 뒤에 두면 윗부분만 삼각형으로 보인다.
            Image leftEar = PetUiKit.CreateRoundedPanel(codeArtGroup.transform, "EarL", PetPalette.KittyEar);
            PetUiKit.Pin(leftEar.rectTransform, new Vector2(0.13f, 0.56f), new Vector2(0.4f, 0.95f));
            leftEar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            leftEar.raycastTarget = false;
            Image leftInner = PetUiKit.CreateRoundedPanel(leftEar.transform, "EarInnerL", PetPalette.Blush);
            PetUiKit.Stretch(leftInner.rectTransform, 26f, 26f);
            leftInner.raycastTarget = false;
            Image rightEar = PetUiKit.CreateRoundedPanel(codeArtGroup.transform, "EarR", PetPalette.KittyEar);
            PetUiKit.Pin(rightEar.rectTransform, new Vector2(0.6f, 0.56f), new Vector2(0.87f, 0.95f));
            rightEar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            rightEar.raycastTarget = false;
            Image rightInner = PetUiKit.CreateRoundedPanel(rightEar.transform, "EarInnerR", PetPalette.Blush);
            PetUiKit.Stretch(rightInner.rectTransform, 26f, 26f);
            rightInner.raycastTarget = false;

            Image body = PetUiKit.CreateCircle(codeArtGroup.transform, "Body", PetPalette.KittyBody);
            PetUiKit.Pin(body.rectTransform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0.78f));
            Shadow bodyShadow = body.gameObject.AddComponent<Shadow>();
            bodyShadow.effectColor = new Color(0.4f, 0.25f, 0.35f, 0.2f);
            bodyShadow.effectDistance = new Vector2(0f, -8f);

            // 뜬 눈.
            eyesOpen = PetUiKit.CreateRect("EyesOpen", codeArtGroup.transform);
            PetUiKit.Stretch(eyesOpen.GetComponent<RectTransform>());
            Image eyeL = PetUiKit.CreateCircle(eyesOpen.transform, "EyeL", PetPalette.Ink);
            PetUiKit.Pin(eyeL.rectTransform, new Vector2(0.3f, 0.44f), new Vector2(0.38f, 0.56f));
            Image eyeR = PetUiKit.CreateCircle(eyesOpen.transform, "EyeR", PetPalette.Ink);
            PetUiKit.Pin(eyeR.rectTransform, new Vector2(0.62f, 0.44f), new Vector2(0.7f, 0.56f));

            // 감은 눈 (가는 둥근 선).
            eyesClosed = PetUiKit.CreateRect("EyesClosed", codeArtGroup.transform);
            PetUiKit.Stretch(eyesClosed.GetComponent<RectTransform>());
            Image closedL = PetUiKit.CreateRoundedPanel(eyesClosed.transform, "ClosedL", PetPalette.Ink);
            PetUiKit.Pin(closedL.rectTransform, new Vector2(0.28f, 0.485f), new Vector2(0.4f, 0.515f));
            Image closedR = PetUiKit.CreateRoundedPanel(eyesClosed.transform, "ClosedR", PetPalette.Ink);
            PetUiKit.Pin(closedR.rectTransform, new Vector2(0.6f, 0.485f), new Vector2(0.72f, 0.515f));
            eyesClosed.SetActive(false);

            Image blushL = PetUiKit.CreateCircle(codeArtGroup.transform, "BlushL", PetPalette.Blush);
            PetUiKit.Pin(blushL.rectTransform, new Vector2(0.2f, 0.34f), new Vector2(0.32f, 0.42f));
            Image blushR = PetUiKit.CreateCircle(codeArtGroup.transform, "BlushR", PetPalette.Blush);
            PetUiKit.Pin(blushR.rectTransform, new Vector2(0.68f, 0.34f), new Vector2(0.8f, 0.42f));

            Image nose = PetUiKit.CreateCircle(codeArtGroup.transform, "Nose", PetPalette.Blush);
            PetUiKit.Pin(nose.rectTransform, new Vector2(0.47f, 0.4f), new Vector2(0.53f, 0.45f));

            Image smile = PetUiKit.CreateRoundedPanel(codeArtGroup.transform, "Smile", PetPalette.Ink);
            PetUiKit.Pin(smile.rectTransform, new Vector2(0.46f, 0.345f), new Vector2(0.54f, 0.36f));
            smile.raycastTarget = false;

            // 고양이 수염 (양쪽 세 가닥).
            for (int side = 0; side < 2; side++)
            {
                for (int line = 0; line < 3; line++)
                {
                    Image whisker = PetUiKit.CreateRoundedPanel(codeArtGroup.transform,
                        "Whisker" + side + line, new Color(0.29f, 0.23f, 0.36f, 0.55f));
                    float y = 0.4f + line * 0.055f;
                    if (side == 0)
                    {
                        PetUiKit.Pin(whisker.rectTransform, new Vector2(0.02f, y), new Vector2(0.2f, y + 0.014f));
                        whisker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (line - 1) * -7f);
                    }
                    else
                    {
                        PetUiKit.Pin(whisker.rectTransform, new Vector2(0.8f, y), new Vector2(0.98f, y + 0.014f));
                        whisker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (line - 1) * 7f);
                    }

                    whisker.raycastTarget = false;
                }
            }

            // 입 위치 = 당근이 날아가 닿는 지점.
            petMouthAnchor = PetUiKit.CreateRect("Mouth", codeArtGroup.transform).GetComponent<RectTransform>();
            PetUiKit.Pin(petMouthAnchor, new Vector2(0.48f, 0.32f), new Vector2(0.52f, 0.36f));

            // 전용 아트가 있으면 코드 도형 전체를 그림 한 장으로 교체.
            GameObject artObject = PetUiKit.CreateRect("PetArt", petRoot);
            PetUiKit.Stretch(artObject.GetComponent<RectTransform>());
            petArtImage = artObject.AddComponent<Image>();
            petArtImage.raycastTarget = false;
            if (PetUiKit.TryApplySprite(petArtImage, "YeonaPetPlayhouse/Art/Kitty-Happy"))
            {
                codeArtGroup.SetActive(false);
            }
            else
            {
                artObject.SetActive(false);
            }

            // 펫 전체가 하나의 큰 터치 대상 (쓰다듬기).
            GameObject touch = PetUiKit.CreateRect("PetTouch", petRoot);
            PetUiKit.Stretch(touch.GetComponent<RectTransform>());
            Image touchImage = touch.AddComponent<Image>();
            touchImage.color = Color.clear;
            touchImage.raycastTarget = true;
            Button petButton = touch.AddComponent<Button>();
            petButton.targetGraphic = touchImage;
            petButton.transition = Selectable.Transition.None;
            petButton.onClick.AddListener(OnPetTapped);
        }

        private void BuildMoodBubble()
        {
            moodBubble = PetUiKit.CreateRect("MoodBubble", safeRoot).GetComponent<RectTransform>();
            PetUiKit.Pin(moodBubble, new Vector2(0.6f, 0.66f), new Vector2(0.73f, 0.88f));
            Image bubble = moodBubble.gameObject.AddComponent<Image>();
            bubble.sprite = PetUiKit.CircleSprite;
            bubble.color = new Color(1f, 1f, 1f, 0.95f);
            bubble.raycastTarget = false;
            GentleBob bob = moodBubble.gameObject.AddComponent<GentleBob>();
            bob.amplitude = 6f;
            bob.speed = 1.6f;

            moodIconRoot = PetUiKit.CreateRect("Icon", moodBubble).GetComponent<RectTransform>();
            PetUiKit.Stretch(moodIconRoot, 26f, 26f);
        }

        private void BuildActionButtons()
        {
            RectTransform row = PetUiKit.CreateRect("Actions", safeRoot).GetComponent<RectTransform>();
            PetUiKit.Pin(row, new Vector2(0.24f, 0.015f), new Vector2(0.76f, 0.16f));
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 54f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button feed = PetUiKit.CreateRoundButton(row, "FeedButton", PetPalette.White, OnFeedPressed, BuildCarrotIcon);
            SetButtonSize(feed, 150f);
            Button bath = PetUiKit.CreateRoundButton(row, "BathButton", PetPalette.White, OnBathPressed, BuildBubbleIcon);
            SetButtonSize(bath, 150f);
            Button sleep = PetUiKit.CreateRoundButton(row, "SleepButton", PetPalette.White, OnSleepPressed, BuildMoonIcon);
            SetButtonSize(sleep, 150f);
        }

        private static void SetButtonSize(Button button, float size)
        {
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = size;
            element.preferredHeight = size;
            element.minWidth = size;
            element.minHeight = size;
        }

        private void BuildNightOverlay()
        {
            GameObject overlay = PetUiKit.CreateRect("NightOverlay", overlayLayer);
            PetUiKit.Stretch(overlay.GetComponent<RectTransform>());
            nightOverlayImage = overlay.AddComponent<Image>();
            nightOverlayImage.color = new Color(
                PetPalette.NightVeil.r, PetPalette.NightVeil.g, PetPalette.NightVeil.b, 0f);
            nightOverlayImage.raycastTarget = false;
            nightOverlayButton = overlay.AddComponent<Button>();
            nightOverlayButton.transition = Selectable.Transition.None;
            nightOverlayButton.onClick.AddListener(OnNightOverlayTapped);
            nightOverlayButton.enabled = false;
        }

        // ---------- 아이콘 (아트 교체 전 코드 도형) ----------

        private void BuildCarrotIcon(RectTransform root)
        {
            Image image = root.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (PetUiKit.TryApplySprite(image, "YeonaPetPlayhouse/Art/Icon-Carrot"))
            {
                return;
            }

            image.enabled = false;
            Image carrotBody = PetUiKit.CreateCircle(root, "CarrotBody", PetPalette.Carrot);
            PetUiKit.Pin(carrotBody.rectTransform, new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.7f));
            Image leaf = PetUiKit.CreateCircle(root, "Leaf", PetPalette.CarrotLeaf);
            PetUiKit.Pin(leaf.rectTransform, new Vector2(0.38f, 0.62f), new Vector2(0.62f, 0.92f));
        }

        private void BuildBubbleIcon(RectTransform root)
        {
            Image image = root.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (PetUiKit.TryApplySprite(image, "YeonaPetPlayhouse/Art/Icon-Bath"))
            {
                return;
            }

            image.enabled = false;
            Image soap = PetUiKit.CreateRoundedPanel(root, "Soap", PetPalette.SoapMint);
            PetUiKit.Pin(soap.rectTransform, new Vector2(0.14f, 0.12f), new Vector2(0.86f, 0.52f));
            Image bubbleA = PetUiKit.CreateCircle(root, "BubbleA", PetPalette.BubbleBlue);
            PetUiKit.Pin(bubbleA.rectTransform, new Vector2(0.2f, 0.56f), new Vector2(0.44f, 0.8f));
            Image bubbleB = PetUiKit.CreateCircle(root, "BubbleB", PetPalette.BubbleBlue);
            PetUiKit.Pin(bubbleB.rectTransform, new Vector2(0.52f, 0.62f), new Vector2(0.82f, 0.92f));
        }

        private void BuildMoonIcon(RectTransform root)
        {
            Image image = root.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (PetUiKit.TryApplySprite(image, "YeonaPetPlayhouse/Art/Icon-Moon"))
            {
                return;
            }

            image.enabled = false;
            Image moon = PetUiKit.CreateCircle(root, "Moon", PetPalette.MoonGold);
            PetUiKit.Pin(moon.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
            // 초승달 느낌: 배경색 원을 겹쳐서 파낸다.
            Image bite = PetUiKit.CreateCircle(root, "MoonBite", PetPalette.White);
            PetUiKit.Pin(bite.rectTransform, new Vector2(0.3f, 0.28f), new Vector2(1.02f, 1f));
        }

        // ---------- 상호작용 ----------

        private void OnFeedPressed()
        {
            if (pet.IsSleeping)
            {
                WakeGently();
            }

            RectTransform carrot = SpawnCarrot();
            StartCoroutine(FlyCarrotToMouth(carrot));
        }

        private RectTransform SpawnCarrot()
        {
            RectTransform carrot = PetUiKit.CreateRect("FlyingCarrot", overlayLayer).GetComponent<RectTransform>();
            carrot.sizeDelta = new Vector2(110f, 110f);
            Image image = carrot.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            if (!PetUiKit.TryApplySprite(image, "YeonaPetPlayhouse/Art/Icon-Carrot"))
            {
                image.enabled = false;
                Image body = PetUiKit.CreateCircle(carrot, "Body", PetPalette.Carrot);
                PetUiKit.Pin(body.rectTransform, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.65f));
                Image leaf = PetUiKit.CreateCircle(carrot, "Leaf", PetPalette.CarrotLeaf);
                PetUiKit.Pin(leaf.rectTransform, new Vector2(0.35f, 0.6f), new Vector2(0.65f, 0.95f));
            }

            carrot.position = new Vector3(
                UnityEngine.Screen.width * 0.35f, UnityEngine.Screen.height * 0.12f, 0f);
            return carrot;
        }

        private System.Collections.IEnumerator FlyCarrotToMouth(RectTransform carrot)
        {
            Vector3 start = carrot.position;
            const float duration = 0.45f;
            float elapsed = 0f;
            while (elapsed < duration && carrot != null && petMouthAnchor != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - progress) * (1f - progress);
                Vector3 target = petMouthAnchor.position;
                Vector3 position = Vector3.Lerp(start, target, eased);
                // 포물선 느낌으로 살짝 띄운다.
                position.y += Mathf.Sin(progress * Mathf.PI) * UnityEngine.Screen.height * 0.12f;
                carrot.position = position;
                carrot.localScale = Vector3.one * (1f - 0.4f * progress);
                yield return null;
            }

            if (carrot != null)
            {
                Destroy(carrot.gameObject);
            }

            pet.Feed();
            PetVoice.PlayOneOf(true, "feed_yum_1", "feed_yum_2");
            StartCoroutine(PetJuice.ScalePunch(petRoot, 1.16f));
            SpawnHearts(2);
            RefreshMoodVisuals(false);
        }

        private void OnBathPressed()
        {
            if (pet.IsSleeping)
            {
                WakeGently();
            }

            if (bathing)
            {
                EndBath(false);
                return;
            }

            bathing = true;
            PetVoice.Play("bath_start");
            GameObject catcher = PetUiKit.CreateRect("BathCatcher", petRoot);
            PetUiKit.Stretch(catcher.GetComponent<RectTransform>(), -40f, -40f);
            Image image = catcher.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            bathCatcher = catcher.AddComponent<BathScrubCatcher>();
            bathCatcher.scrubbed = OnScrubbed;
        }

        private void OnScrubbed(Vector2 screenPosition)
        {
            if (!bathing || scrubCooldown > 0f)
            {
                return;
            }

            scrubCooldown = 0.12f;
            pet.Scrub();

            RectTransform bubble = PetUiKit.CreateRect("Bubble", overlayLayer).GetComponent<RectTransform>();
            float size = UnityEngine.Random.Range(46f, 96f);
            bubble.sizeDelta = new Vector2(size, size);
            Image image = bubble.gameObject.AddComponent<Image>();
            image.sprite = PetUiKit.CircleSprite;
            image.color = new Color(
                PetPalette.BubbleBlue.r, PetPalette.BubbleBlue.g, PetPalette.BubbleBlue.b, 0.85f);
            image.raycastTarget = false;
            bubble.position = new Vector3(
                screenPosition.x + UnityEngine.Random.Range(-30f, 30f),
                screenPosition.y + UnityEngine.Random.Range(-20f, 40f), 0f);
            StartCoroutine(PetJuice.FloatAndFade(bubble, new Vector2(UnityEngine.Random.Range(-25f, 25f), 130f)));

            if (pet.IsClean)
            {
                EndBath(true);
            }
        }

        private void EndBath(bool celebrate)
        {
            bathing = false;
            if (bathCatcher != null)
            {
                Destroy(bathCatcher.gameObject);
                bathCatcher = null;
            }

            if (celebrate)
            {
                PetVoice.Play("bath_done");
                StartCoroutine(PetJuice.ScalePunch(petRoot, 1.2f));
                SpawnSparkles(8);
            }

            RefreshMoodVisuals(false);
        }

        private void OnSleepPressed()
        {
            if (pet.IsSleeping)
            {
                WakeGently();
                return;
            }

            if (bathing)
            {
                EndBath(false);
            }

            pet.StartSleep();
            PetVoice.Play("sleep_start");
            nightOverlayImage.raycastTarget = true;
            nightOverlayButton.enabled = true;
            StartCoroutine(PetJuice.FadeImage(nightOverlayImage, 0f, PetPalette.NightVeil.a, 0.6f));
            RefreshMoodVisuals(false);
        }

        private void OnNightOverlayTapped()
        {
            WakeGently();
        }

        private void WakeGently()
        {
            pet.WakeUp();
            OnMorning();
        }

        private void OnMorning()
        {
            nightOverlayImage.raycastTarget = false;
            nightOverlayButton.enabled = false;
            StartCoroutine(PetJuice.FadeImage(nightOverlayImage, nightOverlayImage.color.a, 0f, 0.5f));
            PetVoice.Play("wake_up");
            StartCoroutine(PetJuice.ScalePunch(petRoot, 1.12f));
            RefreshMoodVisuals(false);
        }

        private void OnPetTapped()
        {
            if (pet.IsSleeping || bathing)
            {
                return;
            }

            PetVoice.PlayOneOf(false, "giggle_1", "giggle_2", "giggle_3");
            StartCoroutine(PetJuice.ScalePunch(petRoot, 1.1f, 0.28f));
            SpawnHearts(1);
        }

        // ---------- 표정·말풍선 ----------

        private void RefreshMoodVisuals(bool firstTime)
        {
            PetMood mood = pet.CurrentMood;
            lastMood = mood;

            bool sleeping = mood == PetMood.Sleeping;
            if (eyesOpen != null && codeArtGroup.activeSelf)
            {
                eyesOpen.SetActive(!sleeping);
                eyesClosed.SetActive(sleeping);
            }

            if (petArtImage != null && petArtImage.gameObject.activeSelf)
            {
                PetUiKit.TryApplySprite(petArtImage, ArtForMood(mood));
            }

            bool wants = mood == PetMood.WantsFood || mood == PetMood.WantsBath || mood == PetMood.Sleepy;
            moodBubble.gameObject.SetActive(wants);
            if (wants)
            {
                for (int index = moodIconRoot.childCount - 1; index >= 0; index--)
                {
                    Destroy(moodIconRoot.GetChild(index).gameObject);
                }

                if (mood == PetMood.WantsFood)
                {
                    BuildCarrotIcon(moodIconRoot);
                }
                else if (mood == PetMood.WantsBath)
                {
                    BuildBubbleIcon(moodIconRoot);
                }
                else
                {
                    BuildMoonIcon(moodIconRoot);
                }

                if (!firstTime && mood != lastAnnouncedMood)
                {
                    PetVoice.Play(
                        mood == PetMood.WantsFood ? "want_food" : mood == PetMood.WantsBath ? "want_bath" : "want_sleep",
                        false);
                }

                lastAnnouncedMood = mood;
            }
            else
            {
                lastAnnouncedMood = PetMood.Happy;
            }
        }

        private static string ArtForMood(PetMood mood)
        {
            switch (mood)
            {
                case PetMood.Sleeping: return "YeonaPetPlayhouse/Art/Kitty-Sleeping";
                case PetMood.WantsFood: return "YeonaPetPlayhouse/Art/Kitty-Hungry";
                case PetMood.WantsBath: return "YeonaPetPlayhouse/Art/Kitty-Dirty";
                case PetMood.Sleepy: return "YeonaPetPlayhouse/Art/Kitty-Sleepy";
                default: return "YeonaPetPlayhouse/Art/Kitty-Happy";
            }
        }

        private void SpawnHearts(int count)
        {
            for (int index = 0; index < count; index++)
            {
                RectTransform heart = PetUiKit.CreateRect("Heart", overlayLayer).GetComponent<RectTransform>();
                float size = UnityEngine.Random.Range(56f, 84f);
                heart.sizeDelta = new Vector2(size, size);
                Image image = heart.gameObject.AddComponent<Image>();
                image.sprite = PetUiKit.CircleSprite;
                image.color = PetPalette.HeartRed;
                image.raycastTarget = false;
                Vector3 basePosition = petRoot.position;
                heart.position = basePosition + new Vector3(
                    UnityEngine.Random.Range(-90f, 90f), UnityEngine.Random.Range(60f, 140f), 0f);
                StartCoroutine(PetJuice.FloatAndFade(heart, new Vector2(UnityEngine.Random.Range(-35f, 35f), 170f), 1.2f));
            }
        }

        private void SpawnSparkles(int count)
        {
            for (int index = 0; index < count; index++)
            {
                RectTransform sparkle = PetUiKit.CreateRect("Sparkle", overlayLayer).GetComponent<RectTransform>();
                float size = UnityEngine.Random.Range(24f, 56f);
                sparkle.sizeDelta = new Vector2(size, size);
                Image image = sparkle.gameObject.AddComponent<Image>();
                image.sprite = PetUiKit.CircleSprite;
                image.color = PetPalette.StarGold;
                image.raycastTarget = false;
                sparkle.position = petRoot.position + new Vector3(
                    UnityEngine.Random.Range(-160f, 160f), UnityEngine.Random.Range(-80f, 160f), 0f);
                StartCoroutine(PetJuice.FloatAndFade(sparkle,
                    new Vector2(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(80f, 200f)), 0.9f));
            }
        }

        // ---------- 저장/복원 ----------

        private static PetSimulation LoadPet()
        {
            if (!PlayerPrefs.HasKey(SaveKeyLastSeen))
            {
                return PetSimulation.CreateFresh();
            }

            float hunger = PlayerPrefs.GetFloat(SaveKeyHunger, 30f);
            float dirt = PlayerPrefs.GetFloat(SaveKeyDirt, 20f);
            float sleepiness = PlayerPrefs.GetFloat(SaveKeySleepiness, 10f);

            long lastSeen;
            long.TryParse(PlayerPrefs.GetString(SaveKeyLastSeen, "0"), out lastSeen);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            float offlineMinutes = Mathf.Clamp((now - lastSeen) / 60f, 0f, OfflineMaxMinutes);

            hunger = Mathf.Min(hunger + PetSimulation.HungerPerMinute * offlineMinutes, RestoreCap);
            dirt = Mathf.Min(dirt + PetSimulation.DirtPerMinute * offlineMinutes, RestoreCap);
            sleepiness = Mathf.Min(sleepiness + PetSimulation.SleepinessPerMinute * offlineMinutes, RestoreCap);
            return PetSimulation.Restore(hunger, dirt, sleepiness);
        }

        private void SavePet()
        {
            if (pet == null)
            {
                return;
            }

            PlayerPrefs.SetFloat(SaveKeyHunger, pet.Hunger);
            PlayerPrefs.SetFloat(SaveKeyDirt, pet.Dirt);
            PlayerPrefs.SetFloat(SaveKeySleepiness, pet.Sleepiness);
            PlayerPrefs.SetString(SaveKeyLastSeen,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            Type inputSystemModule = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                eventSystem.AddComponent(inputSystemModule);
            }
            else
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }
    }

    /// <summary>목욕 모드에서 문지르기(드래그)를 받아 넘겨 주는 투명 판.</summary>
    public sealed class BathScrubCatcher : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        public Action<Vector2> scrubbed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (scrubbed != null)
            {
                scrubbed(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (scrubbed != null)
            {
                scrubbed(eventData.position);
            }
        }
    }
}
