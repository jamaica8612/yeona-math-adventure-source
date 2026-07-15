using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure
{
    /// <summary>
    /// One-scene entry point for the independent Math Journey. The scene only needs
    /// this component; every responsive screen is built below one safe-area canvas.
    /// </summary>
    public sealed class MathJourneyBootstrap : MonoBehaviour
    {
        private const string LocalProfileId = "yeona_local";
        private const int NormalRoundsPerActivity = 2;
        // The story-shaped six-round schedule alternates difficulties 1 and 2, so a
        // perfect run averages 1.5. Thresholds keep slow or supported play at level 2.
        private const float PlacementLevelThreeThreshold = 1.4f;
        private const float PlacementLevelTwoThreshold = 0.9f;
        public const string ChildDisplayNameKo = "연아";
        public const string KoreanDisplayTitle = "연아의 별다리 모험";
        public const int AssessmentRoundsPerGame = 2;
        public const int AssessmentTotalRoundCount = AssessmentRoundsPerGame * 3;
        public const ScaffoldLevel AssessmentSupportLevel = ScaffoldLevel.VisualCue;

        private static readonly MathActivityKind[] AssessmentOrder =
        {
            MathActivityKind.FairShare,
            MathActivityKind.FairShare,
            MathActivityKind.TargetNumber,
            MathActivityKind.TargetNumber,
            MathActivityKind.PatternSpace,
            MathActivityKind.PatternSpace
        };

        private Canvas canvas;
        private RectTransform safeRoot;
        private RectTransform dragLayer;
        private ProgressRepository progressRepository;
        private MathRuntimeMetaStore metaStore;
        private MathProgressData progress;
        private MathRuntimeMetaData meta;
        private IMathRewardBridge rewardBridge;
        private readonly MathTeacherAI mathTeacher = new MathTeacherAI();

        private bool assessmentRunning;
        private int assessmentCursor;
        private MathActivityKind currentActivity;
        private int currentRound;
        private int currentRoundCount;
        private int screenGeneration;

        public int AssessmentDisplayIndex
        {
            get { return Mathf.Clamp(assessmentCursor + 1, 1, AssessmentTotalRounds); }
        }

        public int AssessmentTotalRounds
        {
            get { return AssessmentTotalRoundCount; }
        }

        public MathProgressData Progress
        {
            get { return progress; }
        }

        public MathRuntimeMetaData Meta
        {
            get { return meta; }
        }

        public static bool WasCorrectOnFirstCheck(int retryCount)
        {
            return retryCount <= 0;
        }

        public static int AssessmentDifficultyForRound(int assessmentIndex)
        {
            if (assessmentIndex < 0 || assessmentIndex >= AssessmentOrder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(assessmentIndex));
            }

            // Each location starts with a low-floor direct-manipulation round, then adds
            // one layer of complexity before the story moves to the next location.
            // This prevents a two-step multiplication problem from being the first view.
            return assessmentIndex % AssessmentRoundsPerGame == 0 ? 1 : 2;
        }

        public static MathActivityKind AssessmentActivityForRound(int assessmentIndex)
        {
            if (assessmentIndex < 0 || assessmentIndex >= AssessmentOrder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(assessmentIndex));
            }

            return AssessmentOrder[assessmentIndex];
        }

        public static ScaffoldLevel ResolveSupportLevel(bool assessment, MathTeacherDecision decision)
        {
            if (assessment || decision == null)
            {
                return AssessmentSupportLevel;
            }

            return DifficultyRules.ClampScaffold((int)decision.scaffoldLevel);
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Application.targetFrameRate = 60;
            EnsureNewInputEventSystem();
            canvas = MathUiKit.CreateCanvas(transform, out safeRoot, out dragLayer);

            MathJsonFileStore fileStore = new MathJsonFileStore();
            progressRepository = new ProgressRepository(fileStore, new UnityJsonProgressCodec());
            metaStore = new MathRuntimeMetaStore(fileStore.DirectoryPath);
            progress = progressRepository.LoadOrCreate(LocalProfileId);
            meta = metaStore.LoadOrCreate();
            MigratePremiumExperienceIfNeeded();
            rewardBridge = new DirectMathAnturaRewardBridge();
            EnsureVisibleConcepts();
        }

        private void MigratePremiumExperienceIfNeeded()
        {
            const int premiumExperienceVersion = 2;
            if (meta.experienceVersion >= premiumExperienceVersion)
            {
                return;
            }

            // v0.1.x was a technical UI prototype. Preserve learned concept history and
            // rewards, but let an existing install experience the new story-shaped opening.
            meta.experienceVersion = premiumExperienceVersion;
            meta.assessmentComplete = false;
            meta.assessmentInProgress = false;
            meta.assessmentNextRound = 0;
            meta.assessmentOutcomes.Clear();
            meta.selectedRewardId = string.Empty;
            metaStore.Save(meta);
        }

        private void Start()
        {
            ShowStartScreen();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveAll();
            }
        }

        private void OnApplicationQuit()
        {
            SaveAll();
        }

        public uint NextProblemSeed()
        {
            unchecked
            {
                uint timePart = (uint)DateTime.UtcNow.Ticks;
                uint seed = (uint)meta.seedCounter * 2654435761u ^ timePart;
                meta.seedCounter++;
                return seed == 0u ? 1u : seed;
            }
        }

        public PatternPuzzleKind ChoosePatternKind(bool assessment, int roundNumber)
        {
            if (assessment)
            {
                return roundNumber <= 1 ? PatternPuzzleKind.NumericSequence : PatternPuzzleKind.Rotation;
            }

            MathTeacherDecision decision = mathTeacher.SelectForActivity(
                progress,
                MathJourneyActivity.PatternSpace);
            return PatternKindForConcept(decision.concept);
        }

        public void CompleteRound(MathActivityKind activity, MathConcept concept, string problemId, int difficulty,
            ScaffoldLevel supportLevel, int elapsedMilliseconds, int retryCount, int hintsUsed)
        {
            ConceptProgressData conceptProgress = progress.GetOrCreateConcept(concept);
            AttemptRecord record = new AttemptRecord
            {
                problemId = problemId,
                concept = concept,
                successful = WasCorrectOnFirstCheck(retryCount),
                elapsedMilliseconds = elapsedMilliseconds,
                expectedDurationMilliseconds = 90000,
                retryCount = Mathf.Max(0, retryCount),
                hintsUsed = Mathf.Max(0, hintsUsed),
                numberDifficulty = DifficultyRules.Clamp(difficulty),
                stepDifficulty = conceptProgress.currentStepDifficulty,
                scaffoldLevel = DifficultyRules.ClampScaffold((int)supportLevel),
                completedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            mathTeacher.RecordAttemptAndAdapt(progress, record);

            progress.rewardTokens++;
            progress.journeyNodeIndex++;
            TryAwardOptionalReward(1);

            if (assessmentRunning)
            {
                meta.assessmentInProgress = true;
                meta.assessmentOutcomes.Add(new MathAssessmentOutcomeData
                {
                    activity = activity,
                    difficulty = difficulty,
                    elapsedMilliseconds = elapsedMilliseconds,
                    retries = retryCount,
                    hints = hintsUsed
                });
                meta.assessmentNextRound = Mathf.Clamp(
                    assessmentCursor + 1,
                    0,
                    AssessmentOrder.Length);
            }

            SaveAll();
            bool moreRounds = assessmentRunning
                ? assessmentCursor + 1 < AssessmentOrder.Length
                : currentRound + 1 < currentRoundCount;
            string nextLabel = moreRounds ? "다음 탐험" : assessmentRunning ? "내 단계 보기" : "추천 활동 보기";
            ShowSuccessCelebration(activity, nextLabel, delegate
            {
                if (assessmentRunning)
                {
                    assessmentCursor = meta.assessmentNextRound;
                    if (assessmentCursor < AssessmentOrder.Length)
                    {
                        LaunchAssessmentRound();
                    }
                    else
                    {
                        FinishAssessment();
                    }
                }
                else
                {
                    currentRound++;
                    if (currentRound < currentRoundCount)
                    {
                        LaunchCurrentRound();
                    }
                    else
                    {
                        meta.completedSessions++;
                        meta.recommendedActivity = ComputeRecommendation();
                        SaveAll();
                        ShowJourneyHome();
                    }
                }
            });
        }

        public void ShowStartScreen()
        {
            assessmentRunning = false;
            ClearSafeRoot();
            RectTransform screen = MathUiKit.CreateScreen(safeRoot, "StartScreen", MathPalette.NightBlue);
            PremiumMathVisuals.AddBackdrop(screen, PremiumMathVisuals.StarBridgeBackplate,
                new Color(0.08f, 0.02f, 0.2f, 0.13f));
            PremiumMathVisuals.AddAmbientSparkles(screen, 14, 20260715);
            PremiumMathVisuals.AddCharacter(screen, "Bandi", PremiumMathVisuals.StarCompanion,
                new Vector2(0.68f, 0.2f), new Vector2(0.96f, 0.79f), 10f);

            RectTransform logoCard = MathUiKit.CreatePanel(screen, "AdventureTitle",
                new Color(1f, 0.96f, 0.84f, 0.95f), 780f, 170f);
            MathUiKit.Pin(logoCard, new Vector2(0.04f, 0.73f), new Vector2(0.49f, 0.94f),
                Vector2.zero, Vector2.zero);
            TMP_Text title = MathUiKit.CreateText(logoCard, "Title", KoreanDisplayTitle, 57f,
                MathPalette.NightBlue, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Stretch(title.rectTransform, 34f, 18f);

            RectTransform storyCard = MathUiKit.CreatePanel(screen, "OpeningStory",
                new Color(1f, 0.98f, 0.91f, 0.96f), 850f, 340f);
            MathUiKit.Pin(storyCard, new Vector2(0.045f, 0.08f), new Vector2(0.52f, 0.54f),
                Vector2.zero, Vector2.zero);

            TMP_Text invitation = MathUiKit.CreateText(storyCard, "Invitation",
                meta.assessmentComplete
                    ? ChildDisplayNameKo + "야, 오늘은 어떤 별섬을 깨워 볼까?"
                    : ChildDisplayNameKo + "야, 별다리가 잠들었어!\n우리 손으로 다시 반짝이게 해 주자.",
                39f, MathPalette.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            MathUiKit.Pin(invitation.rectTransform, new Vector2(0.07f, 0.42f), new Vector2(0.93f, 0.91f),
                Vector2.zero, Vector2.zero);

            string startLabel = meta.assessmentComplete
                ? "별섬으로 돌아가기"
                : meta.assessmentInProgress
                    ? "모험 이어가기"
                    : "별섬으로 출발!";
            Action startAction = meta.assessmentComplete
                ? (Action)ShowJourneyHome
                : meta.assessmentInProgress ? (Action)ContinueAssessment : BeginAssessment;
            Button start = MathUiKit.CreateButton(storyCard, "StartButton", startLabel, MathPalette.StarGold,
                MathPalette.NightBlue, startAction, 610f, 108f, 36f);
            MathUiKit.Pin(start.GetComponent<RectTransform>(), new Vector2(0.12f, 0.08f),
                new Vector2(0.88f, 0.37f), Vector2.zero, Vector2.zero);

            if (meta.assessmentComplete)
            {
                Button parent = MathUiKit.CreateButton(screen, "ParentButton", "보호자",
                    new Color(1f, 1f, 1f, 0.9f), MathPalette.NightBlue, ShowParentSummary, 190f, 78f, 25f);
                MathUiKit.Pin(parent.GetComponent<RectTransform>(), new Vector2(0.84f, 0.86f),
                    new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero);
            }
        }

        public void ShowJourneyHome()
        {
            assessmentRunning = false;
            meta.recommendedActivity = ComputeRecommendation();
            SaveAll();
            ClearSafeRoot();
            RectTransform screen = MathUiKit.CreateScreen(safeRoot, "JourneyHome", MathPalette.NightBlue);
            PremiumMathVisuals.AddBackdrop(screen, PremiumMathVisuals.StarBridgeBackplate,
                new Color(0.1f, 0.03f, 0.22f, 0.2f));
            PremiumMathVisuals.AddAmbientSparkles(screen, 10, 7311);
            PremiumMathVisuals.AddCharacter(screen, "Bandi", PremiumMathVisuals.StarCompanion,
                new Vector2(0.68f, 0.24f), new Vector2(0.92f, 0.76f), 8f);

            RectTransform hud = MathUiKit.CreatePanel(screen, "WorldHud", new Color(1f, 0.98f, 0.9f, 0.94f));
            MathUiKit.Pin(hud, new Vector2(0.035f, 0.83f), new Vector2(0.965f, 0.96f), Vector2.zero, Vector2.zero);
            TMP_Text heading = MathUiKit.CreateText(hud, "Heading", "연아의 별섬", 44f, MathPalette.NightBlue,
                TextAlignmentOptions.Left, FontStyles.Bold);
            MathUiKit.Pin(heading.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.52f, 0.92f),
                Vector2.zero, Vector2.zero);
            TMP_Text stars = MathUiKit.CreateText(hud, "StarCount", "★  " + progress.rewardTokens, 34f,
                MathPalette.NightBlue, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Pin(stars.rectTransform, new Vector2(0.61f, 0.08f), new Vector2(0.78f, 0.92f),
                Vector2.zero, Vector2.zero);
            Button parent = MathUiKit.CreateButton(hud, "ParentButton", "보호자", MathPalette.Lavender,
                MathPalette.White, ShowParentSummary, 190f, 72f, 25f);
            MathUiKit.Pin(parent.GetComponent<RectTransform>(), new Vector2(0.80f, 0.12f),
                new Vector2(0.975f, 0.88f), Vector2.zero, Vector2.zero);

            RectTransform quest = MathUiKit.CreatePanel(screen, "RecommendedQuest",
                new Color(1f, 0.97f, 0.84f, 0.96f));
            MathUiKit.Pin(quest, new Vector2(0.05f, 0.35f), new Vector2(0.52f, 0.76f), Vector2.zero, Vector2.zero);
            TMP_Text questTitle = MathUiKit.CreateText(quest, "QuestTitle",
                "별 친구가 기다리고 있어!\n" + AdventureActivityName(meta.recommendedActivity), 39f,
                MathPalette.Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            MathUiKit.Pin(questTitle.rectTransform, new Vector2(0.07f, 0.38f), new Vector2(0.93f, 0.9f),
                Vector2.zero, Vector2.zero);
            Button recommended = MathUiKit.CreateButton(quest, "RecommendedPlay", "도와주러 가기",
                MathPalette.StarGold, MathPalette.NightBlue,
                delegate { StartActivity(meta.recommendedActivity); }, 520f, 96f, 31f);
            MathUiKit.Pin(recommended.GetComponent<RectTransform>(), new Vector2(0.11f, 0.08f),
                new Vector2(0.89f, 0.34f), Vector2.zero, Vector2.zero);

            RectTransform activityStrip = MathUiKit.CreateHorizontal(screen, "AdventureDoors", 22f,
                new RectOffset(24, 24, 18, 18), TextAnchor.MiddleCenter);
            MathUiKit.Pin(activityStrip, new Vector2(0.05f, 0.055f), new Vector2(0.95f, 0.29f),
                Vector2.zero, Vector2.zero);
            MathUiKit.CreateButton(activityStrip, "ForestDoor", "숲속 잔치", MathPalette.Mint,
                MathPalette.White, delegate { StartActivity(MathActivityKind.FairShare); }, 500f, 150f, 33f);
            MathUiKit.CreateButton(activityStrip, "BridgeDoor", "별다리", MathPalette.StarGold,
                MathPalette.NightBlue, delegate { StartActivity(MathActivityKind.TargetNumber); }, 500f, 150f, 33f);
            MathUiKit.CreateButton(activityStrip, "GardenDoor", "거울 정원", MathPalette.Lavender,
                MathPalette.White, delegate { StartActivity(MathActivityKind.PatternSpace); }, 500f, 150f, 33f);
        }

        public void ShowParentSummary()
        {
            ClearSafeRoot();
            RectTransform screen = MathUiKit.CreateScreen(safeRoot, "ParentSummary", MathPalette.Sky);
            RectTransform column = MathUiKit.CreateVertical(
                screen, "ParentColumn", 18f, new RectOffset(54, 54, 28, 34), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);

            RectTransform header = MathUiKit.CreateHorizontal(column, "ParentHeader", 18f, null, TextAnchor.MiddleCenter);
            MathUiKit.SetLayout(header, -1f, 98f, 1f, 0f);
            MathUiKit.CreateButton(header, "BackButton", "여정으로", MathPalette.White, MathPalette.DeepBlue,
                ShowJourneyHome, 210f, 84f, 28f);
            TMP_Text title = MathUiKit.CreateText(header, "Title", "보호자용 학습 요약", 48f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(title.rectTransform, 1000f, 88f, 1f, 0f);
            CreateStatusPill(header, "최근 10회 기준", MathPalette.LavenderLight, 260f);

            RectTransform summaryStrip = MathUiKit.CreatePanel(column, "SummaryStrip", MathPalette.Surface, -1f, 116f);
            MathUiKit.SetLayout(summaryStrip, -1f, 116f, 1f, 0f);
            MathUiKit.CreateText(summaryStrip, "SummaryText",
                "첫 확인 정답률뿐 아니라 풀이 시간, 다시 시도한 횟수, 힌트 사용량을 함께 봅니다. 어려울 때는 수 범위를 낮추기보다 시각 단계를 더합니다.",
                28f, MathPalette.DeepBlue, TextAlignmentOptions.Center);

            RectTransform list;
            ScrollRect scroll = MathUiKit.CreateScrollView(column, "ConceptScroll", out list, false, true);
            MathUiKit.SetLayout(scroll.GetComponent<RectTransform>(), -1f, 690f, 1f, 1f);
            VerticalLayoutGroup listLayout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 14f;
            listLayout.padding = new RectOffset(16, 16, 16, 16);
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ParentConceptSummary[] summaries = ParentProgressSummaryBuilder.Build(progress);
            for (int index = 0; index < summaries.Length; index++)
            {
                CreateConceptSummaryRow(list, summaries[index]);
            }

            TMP_Text privacy = MathUiKit.CreateText(column, "Privacy",
                "저장 위치: 이 앱의 로컬 전용 폴더 · 이름, 음성, 자유 입력은 외부로 보내지 않습니다.",
                24f, MathPalette.Quiet, TextAlignmentOptions.Center);
            MathUiKit.SetLayout(privacy.rectTransform, -1f, 52f, 1f, 0f);
        }

        public void Pulse(RectTransform target)
        {
            if (target != null && target.gameObject.activeInHierarchy)
            {
                StartCoroutine(PulseRoutine(target));
            }
        }

        public void RunAfter(float delaySeconds, Action action)
        {
            int scheduledGeneration = screenGeneration;
            StartCoroutine(RunAfterRoutine(Mathf.Max(0f, delaySeconds), action, scheduledGeneration));
        }

        private void BeginAssessment()
        {
            assessmentRunning = true;
            assessmentCursor = 0;
            meta.assessmentComplete = false;
            meta.assessmentInProgress = true;
            meta.assessmentNextRound = 0;
            meta.assessmentOutcomes.Clear();
            SaveAll();
            LaunchAssessmentRound();
        }

        private void ContinueAssessment()
        {
            assessmentRunning = true;
            meta.Normalize();
            assessmentCursor = meta.assessmentNextRound;
            if (assessmentCursor >= AssessmentOrder.Length)
            {
                FinishAssessment();
                return;
            }

            LaunchAssessmentRound();
        }

        private void LaunchAssessmentRound()
        {
            currentActivity = AssessmentOrder[assessmentCursor];
            currentRound = assessmentCursor % 2;
            currentRoundCount = 2;
            int difficulty = AssessmentDifficultyForRound(assessmentCursor);

            LaunchGameView(currentActivity, difficulty, AssessmentSupportLevel, true, currentRound + 1, 2);
        }

        private void StartActivity(MathActivityKind activity)
        {
            assessmentRunning = false;
            currentActivity = activity;
            currentRound = 0;
            currentRoundCount = NormalRoundsPerActivity;
            LaunchCurrentRound();
        }

        private void LaunchCurrentRound()
        {
            MathTeacherDecision decision = mathTeacher.SelectForActivity(
                progress,
                ToTeacherActivity(currentActivity));
            LaunchGameView(
                currentActivity,
                decision.numberDifficulty,
                ResolveSupportLevel(false, decision),
                false,
                currentRound + 1,
                currentRoundCount);
        }

        private void LaunchGameView(MathActivityKind activity, int difficulty, ScaffoldLevel supportLevel,
            bool assessment, int roundNumber, int roundCount)
        {
            ClearSafeRoot();
            MathMiniGameViewBase view;
            switch (activity)
            {
                case MathActivityKind.FairShare:
                    view = new FairShareGameView(
                        this, safeRoot, dragLayer, difficulty, supportLevel, assessment, roundNumber, roundCount);
                    break;
                case MathActivityKind.PatternSpace:
                    view = new PatternSpaceGameView(
                        this, safeRoot, dragLayer, difficulty, supportLevel, assessment, roundNumber, roundCount);
                    break;
                default:
                    view = new TargetNumberGameView(
                        this, safeRoot, dragLayer, difficulty, supportLevel, assessment, roundNumber, roundCount);
                    break;
            }

            view.Show();
        }

        private void FinishAssessment()
        {
            int placement = CalculatePlacementLevel(meta.assessmentOutcomes);
            MathActivityKind recommendation = WeakestAssessmentActivity();
            meta.assessmentComplete = true;
            meta.assessmentInProgress = false;
            meta.assessmentNextRound = 0;
            meta.placementLevel = placement;
            meta.recommendedActivity = recommendation;
            for (int index = 0; index < progress.concepts.Count; index++)
            {
                ConceptProgressData concept = progress.concepts[index];
                concept.currentNumberDifficulty = placement;
                concept.currentStepDifficulty = placement;
                concept.currentScaffoldLevel = placement == 1 ? ScaffoldLevel.GuidedSteps : ScaffoldLevel.VisualCue;
            }

            meta.assessmentOutcomes.Clear();
            SaveAll();
            assessmentRunning = false;
            ShowPlacementResult();
        }

        private void ShowPlacementResult()
        {
            ClearSafeRoot();
            RectTransform screen = MathUiKit.CreateScreen(safeRoot, "PlacementResult", MathPalette.NightBlue);
            PremiumMathVisuals.AddBackdrop(screen, PremiumMathVisuals.StarBridgeBackplate,
                new Color(1f, 0.72f, 0.2f, 0.08f));
            PremiumMathVisuals.AddAmbientSparkles(screen, 32, 10101);
            PremiumMathVisuals.AddCharacter(screen, "Bandi", PremiumMathVisuals.StarCompanion,
                new Vector2(0.06f, 0.16f), new Vector2(0.4f, 0.84f), 12f);

            RectTransform card = MathUiKit.CreatePanel(screen, "IslandAwakeCard",
                new Color(1f, 0.97f, 0.84f, 0.97f));
            MathUiKit.Pin(card, new Vector2(0.39f, 0.16f), new Vector2(0.94f, 0.86f),
                Vector2.zero, Vector2.zero);
            TMP_Text heading = MathUiKit.CreateText(card, "Heading",
                ChildDisplayNameKo + "야, 첫 별섬이 깨어났어!", 44f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Pin(heading.rectTransform, new Vector2(0.07f, 0.72f), new Vector2(0.93f, 0.93f),
                Vector2.zero, Vector2.zero);
            TMP_Text invitation = MathUiKit.CreateText(card, "RewardInvitation",
                "마음에 드는 선물을 하나 골라 봐.", 29f, MathPalette.DeepBlue,
                TextAlignmentOptions.Center, FontStyles.Normal);
            MathUiKit.Pin(invitation.rectTransform, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.72f),
                Vector2.zero, Vector2.zero);

            Button flower = MathUiKit.CreateButton(card, "FlowerReward", "꽃빛 정원",
                MathPalette.Mint, MathPalette.White, delegate { SelectFirstReward("flower_garden"); },
                620f, 108f, 31f);
            MathUiKit.Pin(flower.GetComponent<RectTransform>(), new Vector2(0.12f, 0.33f),
                new Vector2(0.88f, 0.53f), Vector2.zero, Vector2.zero);
            Button friend = MathUiKit.CreateButton(card, "StarFriendReward", "★  별토끼 친구",
                MathPalette.Lavender, MathPalette.White, delegate { SelectFirstReward("star_bunny"); },
                620f, 108f, 31f);
            MathUiKit.Pin(friend.GetComponent<RectTransform>(), new Vector2(0.12f, 0.09f),
                new Vector2(0.88f, 0.29f), Vector2.zero, Vector2.zero);
        }

        private void SelectFirstReward(string rewardId)
        {
            meta.selectedRewardId = string.IsNullOrWhiteSpace(rewardId) ? "flower_garden" : rewardId;
            SaveAll();
            ShowJourneyHome();
        }

        private void ShowSuccessCelebration(MathActivityKind activity, string nextLabel, Action next)
        {
            RectTransform modal = MathUiKit.CreateScreen(safeRoot, "SuccessCelebration", MathPalette.NightBlue);
            modal.SetAsLastSibling();
            Image modalBlocker = modal.GetComponent<Image>();
            if (modalBlocker != null)
            {
                modalBlocker.raycastTarget = true;
            }
            string backdrop = activity == MathActivityKind.FairShare
                ? PremiumMathVisuals.ForestFeastBackplate
                : activity == MathActivityKind.PatternSpace
                    ? PremiumMathVisuals.MirrorGardenBackplate
                    : PremiumMathVisuals.StarBridgeBackplate;
            PremiumMathVisuals.AddBackdrop(modal, backdrop, new Color(1f, 0.78f, 0.22f, 0.08f));
            PremiumMathVisuals.AddAmbientSparkles(modal, 28, progress.rewardTokens * 97 + 11);
            PremiumMathVisuals.AddCharacter(modal, "BandiCelebration", PremiumMathVisuals.StarCompanion,
                new Vector2(0.07f, 0.12f), new Vector2(0.43f, 0.86f), 14f);

            RectTransform card = MathUiKit.CreatePanel(modal, "SuccessSpeech",
                new Color(1f, 0.97f, 0.84f, 0.97f), 840f, 520f);
            MathUiKit.Pin(card, new Vector2(0.42f, 0.18f), new Vector2(0.92f, 0.82f),
                Vector2.zero, Vector2.zero);
            TMP_Text starText = MathUiKit.CreateText(card, "Stars", "★  +1", 68f, MathPalette.StarGold,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Pin(starText.rectTransform, new Vector2(0.08f, 0.7f), new Vector2(0.92f, 0.94f),
                Vector2.zero, Vector2.zero);
            TMP_Text heading = MathUiKit.CreateText(card, "Heading", SuccessHeading(activity), 43f,
                MathPalette.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Pin(heading.rectTransform, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.72f),
                Vector2.zero, Vector2.zero);
            TMP_Text reaction = MathUiKit.CreateText(card, "Reaction", SuccessReaction(activity), 28f,
                MathPalette.DeepBlue, TextAlignmentOptions.Center, FontStyles.Normal);
            MathUiKit.Pin(reaction.rectTransform, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.5f),
                Vector2.zero, Vector2.zero);
            Button nextButton = MathUiKit.CreateButton(card, "NextButton", nextLabel, MathPalette.StarGold,
                MathPalette.NightBlue,
                delegate
                {
                    modal.gameObject.SetActive(false);
                    Destroy(modal.gameObject);
                    if (next != null)
                    {
                        next();
                    }
                }, 520f, 96f, 32f);
            MathUiKit.Pin(nextButton.GetComponent<RectTransform>(), new Vector2(0.18f, 0.07f),
                new Vector2(0.82f, 0.27f), Vector2.zero, Vector2.zero);
            StartCoroutine(CelebrateRoutine(card, starText));
        }

        private void CreateFeaturePill(Transform parent, string label, Color color)
        {
            RectTransform pill = MathUiKit.CreatePanel(parent, label, color, 330f, 86f);
            MathUiKit.CreateText(pill, "Text", label, 28f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private void CreateStatusPill(Transform parent, string label, Color color, float width)
        {
            RectTransform pill = MathUiKit.CreatePanel(parent, label, color, width, 72f);
            MathUiKit.CreateText(pill, "Text", label, 27f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private void CreateActivityCard(Transform parent, MathActivityKind activity, string description, Color accent,
            string symbol)
        {
            RectTransform card = MathUiKit.CreatePanel(parent, activity + "Card", MathPalette.Surface, 560f, 440f);
            MathUiKit.SetLayout(card, 560f, 440f, 1f, 1f);
            RectTransform column = MathUiKit.CreateVertical(
                card, "CardColumn", 12f, new RectOffset(28, 28, 22, 24), TextAnchor.MiddleCenter);
            MathUiKit.Stretch(column);
            RectTransform symbolPanel = MathUiKit.CreatePanel(column, "Symbol", accent, -1f, 94f);
            MathUiKit.SetLayout(symbolPanel, -1f, 94f, 1f, 0f);
            MathUiKit.CreateText(symbolPanel, "SymbolText", symbol, 43f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
            TMP_Text title = MathUiKit.CreateText(column, "Title", MathUiKit.ActivityLabel(activity), 35f,
                MathPalette.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(title.rectTransform, -1f, 60f, 1f, 0f);
            TMP_Text body = MathUiKit.CreateText(column, "Description", description, 27f, MathPalette.Quiet,
                TextAlignmentOptions.Center);
            MathUiKit.SetLayout(body.rectTransform, -1f, 100f, 1f, 0f);
            MathUiKit.CreateButton(column, "Play", "시작", MathPalette.DeepBlue, MathPalette.White,
                delegate { StartActivity(activity); }, 300f, 94f, 31f);
        }

        private void CreateConceptSummaryRow(Transform parent, ParentConceptSummary summary)
        {
            Color stateColor = summary.status == ParentConceptStatus.Familiar
                ? MathPalette.MintLight
                : summary.status == ParentConceptStatus.NeedsHelp ? new Color(1f, 0.89f, 0.87f) : MathPalette.LavenderLight;
            RectTransform row = MathUiKit.CreatePanel(parent, summary.concept + "Row", MathPalette.Surface, -1f, 110f);
            MathUiKit.SetLayout(row, -1f, 110f, 1f, 0f);
            RectTransform horizontal = MathUiKit.CreateHorizontal(
                row, "Row", 24f, new RectOffset(28, 28, 14, 14), TextAnchor.MiddleCenter);
            MathUiKit.Stretch(horizontal);
            TMP_Text concept = MathUiKit.CreateText(horizontal, "Concept", MathUiKit.ConceptLabel(summary.concept), 31f,
                MathPalette.Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            MathUiKit.SetLayout(concept.rectTransform, 700f, 76f, 1f, 0f);
            TMP_Text evidence = MathUiKit.CreateText(horizontal, "Evidence",
                "최근 기록 " + summary.recentEvidenceCount + "회", 26f, MathPalette.Quiet,
                TextAlignmentOptions.Center);
            MathUiKit.SetLayout(evidence.rectTransform, 300f, 70f, 0f, 0f);
            RectTransform status = MathUiKit.CreatePanel(horizontal, "Status", stateColor, 330f, 72f);
            MathUiKit.CreateText(status, "StatusText", summary.statusLabelKo, 28f, MathPalette.DeepBlue,
                TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private void EnsureVisibleConcepts()
        {
            MathConcept[] concepts =
            {
                MathConcept.TargetNumber,
                MathConcept.DivisionAndRemainder,
                MathConcept.NumericPattern,
                MathConcept.ShapePattern,
                MathConcept.Rotation,
                MathConcept.Symmetry,
                MathConcept.SpatialReasoning
            };
            for (int index = 0; index < concepts.Length; index++)
            {
                progress.GetOrCreateConcept(concepts[index]);
            }
        }

        private MathActivityKind ComputeRecommendation()
        {
            return FromTeacherActivity(mathTeacher.SelectNext(progress).activity);
        }

        private static MathJourneyActivity ToTeacherActivity(MathActivityKind activity)
        {
            switch (activity)
            {
                case MathActivityKind.TargetNumber:
                    return MathJourneyActivity.TargetNumber;
                case MathActivityKind.FairShare:
                    return MathJourneyActivity.FairShare;
                case MathActivityKind.PatternSpace:
                    return MathJourneyActivity.PatternSpace;
                default:
                    throw new ArgumentOutOfRangeException("activity");
            }
        }

        private static MathActivityKind FromTeacherActivity(MathJourneyActivity activity)
        {
            switch (activity)
            {
                case MathJourneyActivity.TargetNumber:
                    return MathActivityKind.TargetNumber;
                case MathJourneyActivity.FairShare:
                    return MathActivityKind.FairShare;
                case MathJourneyActivity.PatternSpace:
                    return MathActivityKind.PatternSpace;
                default:
                    throw new ArgumentOutOfRangeException("activity");
            }
        }

        private static PatternPuzzleKind PatternKindForConcept(MathConcept concept)
        {
            switch (concept)
            {
                case MathConcept.NumericPattern:
                    return PatternPuzzleKind.NumericSequence;
                case MathConcept.ShapePattern:
                    return PatternPuzzleKind.ShapePattern;
                case MathConcept.Rotation:
                    return PatternPuzzleKind.Rotation;
                case MathConcept.Symmetry:
                    return PatternPuzzleKind.Symmetry;
                default:
                    return PatternPuzzleKind.SpatialFill;
            }
        }

        public static int CalculatePlacementLevel(IList<MathAssessmentOutcomeData> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0)
            {
                return 2;
            }

            float score = 0f;
            int validOutcomeCount = 0;
            for (int index = 0; index < outcomes.Count; index++)
            {
                MathAssessmentOutcomeData result = outcomes[index];
                if (result == null)
                {
                    continue;
                }

                float roundScore = DifficultyRules.Clamp(result.difficulty);
                roundScore -= Mathf.Min(1.25f, Mathf.Max(0, result.retries) * 0.38f);
                roundScore -= Mathf.Min(1f, Mathf.Max(0, result.hints) * 0.5f);
                if (Mathf.Max(0, result.elapsedMilliseconds) > 120000)
                {
                    roundScore -= 0.45f;
                }

                score += roundScore;
                validOutcomeCount++;
            }

            if (validOutcomeCount == 0)
            {
                return 2;
            }

            float average = score / validOutcomeCount;
            if (average >= PlacementLevelThreeThreshold)
            {
                return 3;
            }

            return average >= PlacementLevelTwoThreshold ? 2 : 1;
        }

        private MathActivityKind WeakestAssessmentActivity()
        {
            float[] totals = new float[3];
            int[] counts = new int[3];
            for (int index = 0; index < meta.assessmentOutcomes.Count; index++)
            {
                MathAssessmentOutcomeData outcome = meta.assessmentOutcomes[index];
                int activityIndex = (int)outcome.activity;
                totals[activityIndex] += outcome.retries * 2f + outcome.hints * 2.5f +
                                         outcome.elapsedMilliseconds / 90000f;
                counts[activityIndex]++;
            }

            int weakest = 0;
            float highest = float.MinValue;
            for (int index = 0; index < totals.Length; index++)
            {
                float average = counts[index] == 0 ? 0f : totals[index] / counts[index];
                if (average > highest)
                {
                    highest = average;
                    weakest = index;
                }
            }

            return (MathActivityKind)weakest;
        }

        private void SaveAll()
        {
            if (progressRepository == null || progress == null || metaStore == null || meta == null)
            {
                return;
            }

            try
            {
                progressRepository.Save(progress);
                metaStore.Save(meta);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Yeona Math save deferred: " + exception.Message);
            }
        }

        private void TryAwardOptionalReward(int amount)
        {
            if (rewardBridge == null)
            {
                return;
            }

            try
            {
                rewardBridge.TryAward(amount);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Yeona Math optional Antura reward skipped: " + exception.Message);
            }
        }

        private void ClearSafeRoot()
        {
            // Invalidate delayed auto-validation scheduled by the screen being removed.
            screenGeneration++;
            for (int index = safeRoot.childCount - 1; index >= 0; index--)
            {
                GameObject child = safeRoot.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            for (int index = dragLayer.childCount - 1; index >= 0; index--)
            {
                GameObject child = dragLayer.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static void EnsureNewInputEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventObject = new GameObject("YeonaMathEventSystem", typeof(EventSystem));
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            BaseInputModule[] modules = eventSystem.GetComponents<BaseInputModule>();
            for (int index = 0; index < modules.Length; index++)
            {
                modules[index].enabled = modules[index] == inputModule;
            }
        }

        private static string RecommendationReason(MathActivityKind activity)
        {
            switch (activity)
            {
                case MathActivityKind.FairShare:
                    return "몫과 남는 수를 물건으로 확인하며 한 단계씩 이어가요.";
                case MathActivityKind.PatternSpace:
                    return "규칙을 눈으로 찾고 블록을 돌려 공간 감각을 키워요.";
                default:
                    return "가능한 식이 여러 개인 목표 수를 자유롭게 만들어 봐요.";
            }
        }

        private static string AdventureActivityName(MathActivityKind activity)
        {
            switch (activity)
            {
                case MathActivityKind.FairShare:
                    return "숲속 친구들의 잔치를 준비하자.";
                case MathActivityKind.PatternSpace:
                    return "거울 정원의 길을 다시 맞추자.";
                default:
                    return "끊어진 별다리를 이어 보자.";
            }
        }

        private static string PlacementExplanation(int level)
        {
            if (level >= 3)
            {
                return "큰 수와 여러 단계 식부터 시작해요. 막히면 시각 힌트를 바로 더해 줄게요.";
            }

            if (level == 2)
            {
                return "두세 단계 규칙과 연산부터 시작해요. 익숙해지면 숫자 범위가 자연스럽게 커져요.";
            }

            return "수의 크기는 유지하면서 블록과 중간 단계를 더 많이 보여 주는 방식으로 시작해요.";
        }

        private static string SuccessReaction(MathActivityKind activity)
        {
            switch (activity)
            {
                case MathActivityKind.FairShare: return "모두가 같은 만큼 받아서 활짝 웃고 있어!";
                case MathActivityKind.PatternSpace: return "맞춘 조각을 따라 정원 불빛이 켜졌어!";
                default: return "별빛이 다리 끝까지 달려가고 있어!";
            }
        }

        private static string SuccessHeading(MathActivityKind activity)
        {
            switch (activity)
            {
                case MathActivityKind.FairShare: return "와! 숲속 잔치가 시작됐어!";
                case MathActivityKind.PatternSpace: return "거울 정원이 다시 반짝여!";
                default: return "별다리가 깨어났어!";
            }
        }

        private static IEnumerator PulseRoutine(RectTransform target)
        {
            Vector3 original = target.localScale;
            const float duration = 0.24f;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = Mathf.Sin(elapsed / duration * Mathf.PI);
                target.localScale = original * (1f + wave * 0.055f);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = original;
            }
        }

        private IEnumerator RunAfterRoutine(float delaySeconds, Action action, int scheduledGeneration)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(delaySeconds);
            }

            if (action != null && scheduledGeneration == screenGeneration)
            {
                action();
            }
        }

        private static IEnumerator CelebrateRoutine(RectTransform card, TMP_Text stars)
        {
            float elapsed = 0f;
            const float duration = 0.5f;
            while (elapsed < duration && card != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                card.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, eased);
                if (stars != null)
                {
                    stars.color = Color.Lerp(MathPalette.Yellow, MathPalette.Coral, Mathf.PingPong(t * 2f, 1f));
                }

                yield return null;
            }

            if (card != null)
            {
                card.localScale = Vector3.one;
            }
        }

    }
}
