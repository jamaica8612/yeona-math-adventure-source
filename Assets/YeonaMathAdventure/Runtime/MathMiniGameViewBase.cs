using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure
{
    public abstract class MathMiniGameViewBase
    {
        protected readonly MathJourneyBootstrap Journey;
        protected readonly RectTransform SafeRoot;
        protected readonly RectTransform DragLayer;
        protected readonly int Difficulty;
        protected readonly ScaffoldLevel SupportLevel;
        protected readonly bool IsAssessment;
        protected readonly int RoundNumber;
        protected readonly int RoundCount;

        protected RectTransform Screen;
        protected RectTransform Body;
        protected TMP_Text FeedbackText;
        protected int RetryCount;
        protected int HintsUsed;

        private float startedAt;
        private bool completed;

        protected MathMiniGameViewBase(MathJourneyBootstrap journey, RectTransform safeRoot, RectTransform dragLayer,
            int difficulty, ScaffoldLevel supportLevel, bool isAssessment, int roundNumber, int roundCount)
        {
            Journey = journey;
            SafeRoot = safeRoot;
            DragLayer = dragLayer;
            Difficulty = DifficultyRules.Clamp(difficulty);
            SupportLevel = DifficultyRules.ClampScaffold((int)supportLevel);
            IsAssessment = isAssessment;
            RoundNumber = roundNumber;
            RoundCount = roundCount;
        }

        public abstract MathActivityKind ActivityKind { get; }

        public void Show()
        {
            startedAt = Time.realtimeSinceStartup;
            BuildShell();
            BuildGame();
            ApplyAutomaticSupport();
        }

        protected abstract void BuildGame();

        protected abstract string ProblemId { get; }

        protected abstract MathConcept Concept { get; }

        protected virtual string AutomaticSupportMessage()
        {
            return string.Empty;
        }

        protected virtual void ApplySupportVisuals()
        {
        }

        protected void ShowTryAgain(string message, string hint = "")
        {
            RetryCount++;
            string combined = string.IsNullOrEmpty(hint) ? message : message + "\n" + hint;
            SetFeedback(combined, MathPalette.DeepBlue);
            Journey.Pulse(FeedbackText.rectTransform);
        }

        protected void ShowGentleMessage(string message)
        {
            SetFeedback(message, MathPalette.DeepBlue);
            Journey.Pulse(FeedbackText.rectTransform);
        }

        protected void CountHint(string message)
        {
            HintsUsed++;
            SetFeedback(message, MathPalette.DeepBlue);
            Journey.Pulse(FeedbackText.rectTransform);
        }

        protected void Complete()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            int elapsed = Mathf.Max(1000, Mathf.RoundToInt((Time.realtimeSinceStartup - startedAt) * 1000f));
            Journey.CompleteRound(
                ActivityKind,
                Concept,
                ProblemId,
                Difficulty,
                SupportLevel,
                elapsed,
                RetryCount,
                HintsUsed);
        }

        protected Button ActionButton(Transform parent, string label, Color color, System.Action action,
            float width = 250f)
        {
            return MathUiKit.CreateButton(parent, label, label, color, MathPalette.White, action, width,
                MathGameLayoutBudget.ActionHeight, 32f);
        }

        private void BuildShell()
        {
            Screen = MathUiKit.CreateScreen(SafeRoot, ActivityKind + "Screen", MathPalette.NightBlue);
            string backdrop = ActivityKind == MathActivityKind.FairShare
                ? PremiumMathVisuals.ForestFeastBackplate
                : ActivityKind == MathActivityKind.PatternSpace
                    ? PremiumMathVisuals.MirrorGardenBackplate
                    : PremiumMathVisuals.StarBridgeBackplate;
            PremiumMathVisuals.AddBackdrop(Screen, backdrop, new Color(0.05f, 0.04f, 0.12f, 0.08f));
            PremiumMathVisuals.AddAmbientSparkles(Screen, 8, 300 + (int)ActivityKind * 71 + RoundNumber);

            PremiumMathVisuals.AddCharacter(Screen, "Bandi", PremiumMathVisuals.StarCompanion,
                new Vector2(0.015f, 0.74f), new Vector2(0.17f, 0.98f), 5f);

            RectTransform speech = MathUiKit.CreatePanel(Screen, "BandiSpeech",
                new Color(1f, 0.98f, 0.91f, 0.96f));
            MathUiKit.Pin(speech, new Vector2(0.155f, 0.77f), new Vector2(0.69f, 0.96f),
                Vector2.zero, Vector2.zero);
            FeedbackText = MathUiKit.CreateText(speech, "Feedback", OpeningLine(), 29f,
                MathPalette.Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Stretch(FeedbackText.rectTransform, 28f, 12f);

            RectTransform progressBadge = MathUiKit.CreatePanel(Screen, "RoundBadge",
                new Color(0.24f, 0.12f, 0.48f, 0.9f));
            MathUiKit.Pin(progressBadge, new Vector2(0.75f, 0.86f), new Vector2(0.9f, 0.96f),
                Vector2.zero, Vector2.zero);
            string progress = IsAssessment
                ? BuildStarTrail(Journey.AssessmentDisplayIndex, Journey.AssessmentTotalRounds)
                : BuildStarTrail(RoundNumber, RoundCount);
            TMP_Text progressText = MathUiKit.CreateText(progressBadge, "RoundText", progress, 30f,
                MathPalette.StarGold, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Stretch(progressText.rectTransform, 12f, 6f);

            if (!IsAssessment)
            {
                Button back = MathUiKit.CreateButton(Screen, "Back", "<", new Color(1f, 1f, 1f, 0.92f),
                    MathPalette.NightBlue, Journey.ShowJourneyHome, 90f, 76f, 44f);
                MathUiKit.Pin(back.GetComponent<RectTransform>(), new Vector2(0.92f, 0.86f),
                    new Vector2(0.975f, 0.96f), Vector2.zero, Vector2.zero);
            }

            Body = MathUiKit.CreatePanel(Screen, "GameBody", new Color(1f, 1f, 1f, 0.12f), -1f, -1f);
            MathUiKit.Pin(Body, new Vector2(0.035f, 0.055f), new Vector2(0.965f, 0.745f),
                Vector2.zero, Vector2.zero);
        }

        private string OpeningLine()
        {
            switch (ActivityKind)
            {
                case MathActivityKind.FairShare:
                    return "연아야, 친구들이 똑같이 먹게 도와줘!";
                case MathActivityKind.PatternSpace:
                    return "연아야, 맞는 조각을 놓아 길을 고쳐 줘!";
                default:
                    return "연아야, 별조각으로 다리를 깨워 줘!";
            }
        }

        private static string BuildStarTrail(int current, int total)
        {
            int safeTotal = Mathf.Clamp(total, 1, 6);
            int safeCurrent = Mathf.Clamp(current, 1, safeTotal);
            string trail = string.Empty;
            for (int index = 1; index <= safeTotal; index++)
            {
                trail += index <= safeCurrent ? "★" : "☆";
            }

            return trail;
        }

        private void SetFeedback(string message, Color color)
        {
            if (FeedbackText == null)
            {
                return;
            }

            FeedbackText.text = string.IsNullOrEmpty(message) ? "다른 방법으로 한 번 더 해 볼까요?" : message;
            FeedbackText.color = color;
        }

        private void ApplyAutomaticSupport()
        {
            string message = AutomaticSupportMessage();
            if (!string.IsNullOrWhiteSpace(message))
            {
                SetFeedback(message, MathPalette.DeepBlue);
            }

            ApplySupportVisuals();
        }
    }
}
