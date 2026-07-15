using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure
{
    public enum FairShareSupportDestination
    {
        None = 0,
        Recipients = 1,
        Remainder = 2
    }

    public sealed class FairShareGameView : MathMiniGameViewBase
    {
        private readonly Dictionary<TouchDragItem, SharePieceState> pieces =
            new Dictionary<TouchDragItem, SharePieceState>();
        private readonly List<MathDropZone> recipientZones = new List<MathDropZone>();
        private readonly List<TMP_Text> recipientCountTexts = new List<TMP_Text>();
        private readonly List<Image> recipientPanels = new List<Image>();

        private FairShareProblem problem;
        private MathDropZone sourceZone;
        private MathDropZone remainderZone;
        private TMP_Text sourceCountText;
        private TMP_Text remainderCountText;
        private Image remainderPanelImage;
        private int interactionVersion;

        public FairShareGameView(MathJourneyBootstrap journey, RectTransform safeRoot, RectTransform dragLayer,
            int difficulty, ScaffoldLevel supportLevel, bool assessment, int roundNumber, int roundCount)
            : base(journey, safeRoot, dragLayer, difficulty, supportLevel, assessment, roundNumber, roundCount)
        {
        }

        public override MathActivityKind ActivityKind
        {
            get { return MathActivityKind.FairShare; }
        }

        protected override string ProblemId
        {
            get { return problem == null ? "fair-share-missing" : problem.id; }
        }

        protected override MathConcept Concept
        {
            get { return MathConcept.DivisionAndRemainder; }
        }

        protected override string AutomaticSupportMessage()
        {
            return BuildSupportMessage(SupportLevel, problem);
        }

        protected override string MissionSpeech()
        {
            if (problem == null)
            {
                return string.Empty;
            }

            return problem.itemNameKo + " " + problem.totalItems + "개를 " + problem.recipientCount +
                   "곳에 똑같이 나눠 줘!";
        }

        protected override void ApplySupportVisuals()
        {
            if ((int)SupportLevel >= (int)ScaffoldLevel.VisualCue)
            {
                HighlightNextSupportDestination();
            }
        }

        // 나머지가 없고 총량을 한 화면에서 셀 수 있으면 만 4세 트랙으로 본다.
        // 이 구간에서는 수식(예: 17 = 4 × 4 + 1) 대신 세기 언어만 쓴다.
        public static bool IsCountingLevel(FairShareProblem source)
        {
            return source != null && !source.useRemainderTray && source.totalItems <= 10;
        }

        public static string BuildSupportMessage(ScaffoldLevel supportLevel, FairShareProblem source)
        {
            ScaffoldLevel level = DifficultyRules.ClampScaffold((int)supportLevel);
            if (level == ScaffoldLevel.None)
            {
                return string.Empty;
            }

            if (IsCountingLevel(source))
            {
                if (level == ScaffoldLevel.VisualCue)
                {
                    return "노랗게 빛나는 " + source.recipientNameKo + "부터 하나씩 놓아 보자!";
                }

                if (level == ScaffoldLevel.WorkedExample)
                {
                    return "하나씩 번갈아 놓아 보자. 모두 " + source.expectedEach + "개씩 되면 성공이야!";
                }

                return "한 곳에 하나씩, 번갈아 가며 나눠 주자!";
            }

            if (level == ScaffoldLevel.VisualCue)
            {
                return "상자별 개수를 비교하고 가장 적은 상자를 노란색 표시로 찾아보세요.";
            }

            if (source == null)
            {
                return "1단계 한 곳에 하나씩 · 2단계 가장 적은 곳부터 · 3단계 남은 물건 따로 두기";
            }

            if (level == ScaffoldLevel.WorkedExample)
            {
                return "따라 해 볼 식: " + source.totalItems + " = " + source.recipientCount + " × " +
                       source.expectedEach + " + " + source.expectedRemainder;
            }

            return "1단계 " + source.recipientCount + "곳에 1개씩 · 2단계 가장 적은 곳부터 · 3단계 남은 " +
                   source.expectedRemainder + "개는 남는 칸";
        }

        protected override void BuildGame()
        {
            problem = FairShareGenerator.Generate(Journey.NextProblemSeed(), Difficulty);
            RectTransform column = MathUiKit.CreateVertical(
                Body, "FairShareColumn", 12f, new RectOffset(24, 24, 16, 16), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);

            RectTransform mission = MathUiKit.CreatePanel(column, "MissionSign",
                new Color(1f, 0.95f, 0.78f, 0.96f), -1f, 78f);
            MathUiKit.SetLayout(mission, -1f, 78f, 1f, 0f);
            TMP_Text prompt = MathUiKit.CreateText(mission, "Prompt",
                problem.itemNameKo + " " + problem.totalItems + "개를 " + problem.recipientCount +
                "곳에 똑같이 나눠 줘!", 30f, MathPalette.Ink,
                TextAlignmentOptions.Left, FontStyles.Bold);
            MathUiKit.Pin(prompt.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.76f, 0.92f),
                Vector2.zero, Vector2.zero);
            Button help = MathUiKit.CreateButton(mission, "BandiHelp", "힌트", MathPalette.Lavender,
                MathPalette.White, ShowHint, 76f, 64f, 20f);
            MathUiKit.ExpandHitTarget(help);
            MathUiKit.Pin(help.GetComponent<RectTransform>(), new Vector2(0.80f, 0.12f),
                new Vector2(0.875f, 0.88f), Vector2.zero, Vector2.zero);
            Button reset = MathUiKit.CreateButton(mission, "Reset", "다시", MathPalette.Coral,
                MathPalette.White, ReturnAllToSource, 76f, 64f, 22f);
            MathUiKit.ExpandHitTarget(reset);
            MathUiKit.Pin(reset.GetComponent<RectTransform>(), new Vector2(0.90f, 0.12f),
                new Vector2(0.975f, 0.88f), Vector2.zero, Vector2.zero);

            RectTransform main = MathUiKit.CreateHorizontal(column, "ShareWorkspace", 18f, null,
                TextAnchor.MiddleCenter);
            MathUiKit.SetLayout(main, -1f, 514f, 1f, 1f);
            BuildSourcePool(main);
            BuildRecipientWorkspace(main);

            CreatePieces();
            RefreshCounts();
        }

        private void BuildSourcePool(Transform parent)
        {
            RectTransform panel = MathUiKit.CreatePanel(parent, "SourcePool",
                new Color(0.92f, 0.78f, 0.48f, 0.9f), 510f, 500f);
            MathUiKit.SetLayout(panel, 510f, 500f, 0f, 1f);
            RectTransform column = MathUiKit.CreateVertical(
                panel, "SourceColumn", 8f, new RectOffset(15, 15, 12, 12), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);
            sourceCountText = MathUiKit.CreateText(column, "SourceCount", "나눌 물건", 28f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(sourceCountText.rectTransform, -1f, 42f, 1f, 0f);
            RectTransform content;
            ScrollRect scroll = MathUiKit.CreateScrollView(column, "SourceScroll", out content, false, true);
            MathUiKit.SetLayout(scroll.GetComponent<RectTransform>(), -1f,
                MathGameLayoutBudget.FairSourceScrollHeight, 1f, 1f);
            GridLayoutGroup grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(104f, 82f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // Put the drop zone on the full viewport, not only on the shrinking grid content.
            // This keeps returning an item possible even when the source is empty.
            sourceZone = scroll.gameObject.AddComponent<MathDropZone>();
            sourceZone.zoneIndex = -1;
            sourceZone.contentRoot = content;
        }

        private void BuildRecipientWorkspace(Transform parent)
        {
            RectTransform panel = MathUiKit.CreatePanel(parent, "RecipientWorkspace",
                new Color(0.78f, 0.94f, 1f, 0.82f), -1f, 500f);
            MathUiKit.SetLayout(panel, -1f, 500f, 1f, 1f);
            RectTransform column = MathUiKit.CreateVertical(
                panel, "RecipientColumn", 7f, new RectOffset(15, 15, 10, 10), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);
            TMP_Text instruction = MathUiKit.CreateText(column, "Instruction",
                "탭하면 빈 곳부터 차례로 · 끌어서 원하는 곳으로", 25f, MathPalette.DeepBlue,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(instruction.rectTransform, -1f, 38f, 1f, 0f);

            RectTransform recipientContent;
            ScrollRect recipientScroll = MathUiKit.CreateScrollView(
                column, "RecipientScroll", out recipientContent, true, false);
            MathUiKit.SetLayout(recipientScroll.GetComponent<RectTransform>(), -1f,
                problem.useRemainderTray
                    ? MathGameLayoutBudget.FairRecipientScrollWithRemainderHeight
                    : MathGameLayoutBudget.FairRecipientScrollWithoutRemainderHeight,
                1f, 1f);
            recipientContent.anchorMin = new Vector2(0f, 0f);
            recipientContent.anchorMax = new Vector2(0f, 1f);
            recipientContent.pivot = new Vector2(0f, 0.5f);
            GridLayoutGroup grid = recipientContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(278f, 156f);
            grid.spacing = new Vector2(12f, 10f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount = 1;
            grid.startAxis = GridLayoutGroup.Axis.Vertical;
            grid.childAlignment = TextAnchor.MiddleLeft;
            ContentSizeFitter recipientFitter = recipientContent.gameObject.AddComponent<ContentSizeFitter>();
            recipientFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int index = 0; index < problem.recipientCount; index++)
            {
                CreateRecipientZone(recipientContent, index);
            }

            if (problem.useRemainderTray)
            {
                RectTransform remainderPanel = MathUiKit.CreatePanel(column, "Remainder", MathPalette.Yellow, -1f, 80f);
                remainderPanelImage = remainderPanel.GetComponent<Image>();
                MathUiKit.SetLayout(remainderPanel, -1f, 80f, 1f, 0f);
                RectTransform row = MathUiKit.CreateHorizontal(
                    remainderPanel, "RemainderRow", 10f, new RectOffset(18, 18, 8, 8), TextAnchor.MiddleCenter);
                MathUiKit.Stretch(row);
                remainderCountText = MathUiKit.CreateText(row, "RemainderLabel", "남는 칸 0개", 27f,
                    MathPalette.Ink, TextAlignmentOptions.Left, FontStyles.Bold);
                MathUiKit.SetLayout(remainderCountText.rectTransform, 270f, 60f, 0f, 0f);
                RectTransform remainderContent;
                ScrollRect remainderScroll = MathUiKit.CreateScrollView(
                    row, "RemainderScroll", out remainderContent, true, false);
                MathUiKit.SetLayout(remainderScroll.GetComponent<RectTransform>(), -1f, 62f, 1f, 0f);
                remainderScroll.movementType = ScrollRect.MovementType.Clamped;
                remainderContent.anchorMin = new Vector2(0f, 0f);
                remainderContent.anchorMax = new Vector2(0f, 1f);
                remainderContent.pivot = new Vector2(0f, 0.5f);
                remainderContent.anchoredPosition = Vector2.zero;
                HorizontalLayoutGroup remainderLayout =
                    remainderContent.gameObject.AddComponent<HorizontalLayoutGroup>();
                remainderLayout.spacing = 5f;
                remainderLayout.padding = new RectOffset(6, 6, 0, 0);
                remainderLayout.childAlignment = TextAnchor.MiddleLeft;
                remainderLayout.childControlWidth = true;
                remainderLayout.childControlHeight = true;
                remainderLayout.childForceExpandWidth = false;
                remainderLayout.childForceExpandHeight = false;
                ContentSizeFitter remainderFitter = remainderContent.gameObject.AddComponent<ContentSizeFitter>();
                remainderFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                remainderZone = remainderScroll.gameObject.AddComponent<MathDropZone>();
                remainderZone.zoneIndex = -2;
                remainderZone.contentRoot = remainderContent;
            }
        }

        private void CreateRecipientZone(Transform parent, int zoneIndex)
        {
            RectTransform panel = MathUiKit.CreatePanel(parent, "Recipient" + zoneIndex, MathPalette.Surface, 278f, 156f);
            Image panelImage = panel.GetComponent<Image>();
            PremiumMathVisuals.StyleOval(panelImage,
                zoneIndex % 2 == 0 ? new Color(1f, 0.96f, 0.88f, 0.98f) : new Color(0.93f, 0.9f, 1f, 0.98f));
            recipientPanels.Add(panelImage);
            RectTransform column = MathUiKit.CreateVertical(
                panel, "RecipientColumn", 4f, new RectOffset(8, 8, 6, 6), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);
            TMP_Text label = MathUiKit.CreateText(column, "Label",
                problem.recipientNameKo + " " + (zoneIndex + 1) + "  ·  0개", 23f, MathPalette.Ink,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(label.rectTransform, -1f, 34f, 1f, 0f);
            recipientCountTexts.Add(label);
            RectTransform content = MathUiKit.CreateGrid(column, "Items", new Vector2(58f, 48f),
                new Vector2(4f, 3f), 4, new RectOffset(4, 4, 2, 2));
            MathUiKit.SetLayout(content, -1f, 102f, 1f, 1f);
            MathDropZone zone = content.gameObject.AddComponent<MathDropZone>();
            zone.zoneIndex = zoneIndex;
            zone.contentRoot = content;
            recipientZones.Add(zone);
        }

        private void CreatePieces()
        {
            List<int> amounts = BuildPieceAmounts();
            for (int index = 0; index < amounts.Count; index++)
            {
                int amount = amounts[index];
                string label = amount == 1 ? string.Empty : "×" + amount;
                Button button = MathUiKit.CreateButton(sourceZone.contentRoot, "Piece" + index, label,
                    ItemColor(problem.itemNameKo), MathPalette.White, null, 104f, 82f, amount == 1 ? 30f : 24f);
                // 전용 스프라이트가 Resources에 있으면 그것을 쓰고, 없으면 기존 코드 생성 원으로 폴백.
                if (!PremiumMathVisuals.TryApplyItemSprite(button.GetComponent<Image>(),
                        ItemSpriteResource(problem.itemNameKo)))
                {
                    PremiumMathVisuals.StyleOval(button.GetComponent<Image>(), ItemColor(problem.itemNameKo));
                    AddPieceHighlight(button.transform, index);
                }
                TouchDragItem drag = button.gameObject.AddComponent<TouchDragItem>();
                drag.sourceIndex = index;
                drag.amount = amount;
                drag.Configure(DragLayer);
                drag.pressed = delegate { interactionVersion++; };
                SharePieceState state = new SharePieceState { amount = amount, zone = -1 };
                pieces.Add(drag, state);
                drag.tapped = OnPieceTapped;
                drag.released = OnPieceReleased;
            }
        }

        private List<int> BuildPieceAmounts()
        {
            List<int> amounts = new List<int>();
            if (problem.totalItems <= 36)
            {
                for (int index = 0; index < problem.totalItems; index++)
                {
                    amounts.Add(1);
                }

                return amounts;
            }

            const int bundle = 5;
            int bundlesPerRecipient = problem.expectedEach / bundle;
            int singlesPerRecipient = problem.expectedEach % bundle;
            for (int bundleIndex = 0; bundleIndex < bundlesPerRecipient; bundleIndex++)
            {
                for (int recipient = 0; recipient < problem.recipientCount; recipient++)
                {
                    amounts.Add(bundle);
                }
            }

            for (int singleIndex = 0; singleIndex < singlesPerRecipient; singleIndex++)
            {
                for (int recipient = 0; recipient < problem.recipientCount; recipient++)
                {
                    amounts.Add(1);
                }
            }

            for (int index = 0; index < problem.expectedRemainder; index++)
            {
                amounts.Add(1);
            }

            return amounts;
        }

        private void OnPieceTapped(TouchDragItem item)
        {
            SharePieceState state = pieces[item];
            if (state.zone == -1)
            {
                int best = FindRecipientForAmount(state.amount);
                if (best >= 0)
                {
                    MovePiece(item, best, false);
                }
                else if (remainderZone != null)
                {
                    MovePiece(item, -2, false);
                }
                else
                {
                    ShowGentleMessage("상자 속 물건을 옮겨 같은 수로 맞춰 보세요.");
                }

                return;
            }

            if (state.zone >= 0 && state.zone + 1 < recipientZones.Count)
            {
                MovePiece(item, state.zone + 1, false);
            }
            else if (state.zone >= 0 && remainderZone != null)
            {
                MovePiece(item, -2, false);
            }
            else
            {
                MovePiece(item, -1, false);
            }
        }

        private void OnPieceReleased(TouchDragItem item, GameObject target)
        {
            MathDropZone zone = MathUiKit.FindInParents<MathDropZone>(target);
            if (zone == null)
            {
                ShowGentleMessage("빛나는 상자나 남는 칸에 놓아 보세요.");
                return;
            }

            MovePiece(item, zone.zoneIndex, true);
        }

        private void MovePiece(TouchDragItem item, int zoneIndex, bool fromDrag)
        {
            MathDropZone destination = ZoneForIndex(zoneIndex);
            if (destination == null)
            {
                return;
            }

            pieces[item].zone = zoneIndex;
            if (fromDrag)
            {
                item.CommitTo(destination.contentRoot);
            }
            else
            {
                item.transform.SetParent(destination.contentRoot, false);
                RectTransform rect = item.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.zero;
                }
            }

            RefreshCounts();
            interactionVersion++;
            int scheduledVersion = interactionVersion;
            if (SourceCount() == 0)
            {
                Journey.RunAfter(0.55f, delegate
                {
                    if (scheduledVersion == interactionVersion && SourceCount() == 0)
                    {
                        CheckSharing();
                    }
                });
            }
        }

        private MathDropZone ZoneForIndex(int zoneIndex)
        {
            if (zoneIndex == -1)
            {
                return sourceZone;
            }

            if (zoneIndex == -2)
            {
                return remainderZone;
            }

            return zoneIndex >= 0 && zoneIndex < recipientZones.Count ? recipientZones[zoneIndex] : null;
        }

        private int FindRecipientForAmount(int amount)
        {
            int[] counts = RecipientCounts();
            int best = -1;
            int lowest = int.MaxValue;
            for (int index = 0; index < counts.Length; index++)
            {
                if (counts[index] + amount <= problem.expectedEach && counts[index] < lowest)
                {
                    lowest = counts[index];
                    best = index;
                }
            }

            return best;
        }

        private void RefreshCounts()
        {
            int source = 0;
            int remainder = 0;
            int[] recipients = new int[problem.recipientCount];
            foreach (KeyValuePair<TouchDragItem, SharePieceState> pair in pieces)
            {
                SharePieceState state = pair.Value;
                if (state.zone == -1)
                {
                    source += state.amount;
                }
                else if (state.zone == -2)
                {
                    remainder += state.amount;
                }
                else if (state.zone >= 0 && state.zone < recipients.Length)
                {
                    recipients[state.zone] += state.amount;
                }
            }

            sourceCountText.text = "나눌 " + problem.itemNameKo + "  ·  " + source + "개 남음";
            for (int index = 0; index < recipientCountTexts.Count; index++)
            {
                recipientCountTexts[index].text = problem.recipientNameKo + " " + (index + 1) + "  ·  " +
                                                  recipients[index] + "개";
                recipientPanels[index].color = index % 2 == 0
                    ? new Color(1f, 0.96f, 0.88f, 0.98f)
                    : new Color(0.93f, 0.9f, 1f, 0.98f);
            }

            if (remainderCountText != null)
            {
                remainderCountText.text = "남는 칸  " + remainder + "개";
            }

            if ((int)SupportLevel >= (int)ScaffoldLevel.VisualCue)
            {
                HighlightNextSupportDestination();
            }
        }

        private int SourceCount()
        {
            int count = 0;
            foreach (SharePieceState state in pieces.Values)
            {
                if (state.zone == -1)
                {
                    count += state.amount;
                }
            }

            return count;
        }

        private int[] RecipientCounts()
        {
            int[] recipients = new int[problem.recipientCount];
            foreach (SharePieceState state in pieces.Values)
            {
                if (state.zone >= 0 && state.zone < recipients.Length)
                {
                    recipients[state.zone] += state.amount;
                }
            }

            return recipients;
        }

        private int RemainderCount()
        {
            int count = 0;
            foreach (SharePieceState state in pieces.Values)
            {
                if (state.zone == -2)
                {
                    count += state.amount;
                }
            }

            return count;
        }

        private void CheckSharing()
        {
            ValidationResult result = FairShareValidator.Validate(problem, new FairShareAttempt
            {
                recipientItemCounts = RecipientCounts(),
                remainderCount = RemainderCount()
            });
            if (result.IsSolved)
            {
                Complete();
                return;
            }

            ShowTryAgain(string.IsNullOrEmpty(result.messageKo)
                ? "상자마다 같은 수가 되도록 하나씩 옮겨 볼까요?"
                : result.messageKo, HintForCode(result.hintKey));
        }

        private void ShowHint()
        {
            if (HintsUsed == 0)
            {
                CountHint("각 상자에 하나씩 차례대로 나누면 공평한지 바로 볼 수 있어요.");
                HighlightNextSupportDestination();
            }
            else if (HintsUsed == 1)
            {
                CountHint("지금 가장 적은 상자가 노랗게 빛나요. 그곳부터 채워 보세요.");
                HighlightNextSupportDestination();
            }
            else if (IsCountingLevel(problem))
            {
                CountHint("모든 " + problem.recipientNameKo + "가 " + problem.expectedEach +
                          "개씩 받으면 성공이에요.");
            }
            else
            {
                CountHint(problem.totalItems + " = " + problem.recipientCount + " × " + problem.expectedEach +
                          " + " + problem.expectedRemainder + " 로 나눌 수 있어요.");
            }
        }

        private void HighlightLowestRecipients()
        {
            int[] counts = RecipientCounts();
            int minimum = int.MaxValue;
            for (int index = 0; index < counts.Length; index++)
            {
                minimum = Mathf.Min(minimum, counts[index]);
            }

            for (int index = 0; index < recipientPanels.Count; index++)
            {
                recipientPanels[index].color = counts[index] == minimum ? MathPalette.Yellow : MathPalette.Surface;
            }
        }

        private void HighlightNextSupportDestination()
        {
            int[] counts = RecipientCounts();
            int sourceCount = SourceCount();
            FairShareSupportDestination destination = DetermineSupportDestination(
                counts,
                problem.expectedEach,
                sourceCount,
                remainderPanelImage != null);

            if (remainderPanelImage != null)
            {
                remainderPanelImage.color = MathPalette.Yellow;
            }

            if (destination != FairShareSupportDestination.Recipients)
            {
                for (int index = 0; index < recipientPanels.Count; index++)
                {
                    recipientPanels[index].color = MathPalette.Surface;
                }

                if (destination == FairShareSupportDestination.Remainder && remainderPanelImage != null)
                {
                    remainderPanelImage.color = MathPalette.Coral;
                    ShowGentleMessage("모두 " + problem.expectedEach + "개씩 받았어요. 남은 " + sourceCount +
                                      "개는 주황색 남는 칸으로 옮겨요.");
                }

                return;
            }

            HighlightLowestRecipients();
        }

        public static FairShareSupportDestination DetermineSupportDestination(
            int[] recipientCounts,
            int expectedEach,
            int sourceCount,
            bool hasRemainderTray)
        {
            if (recipientCounts == null || recipientCounts.Length == 0 || expectedEach < 0)
            {
                return FairShareSupportDestination.None;
            }

            for (int index = 0; index < recipientCounts.Length; index++)
            {
                if (recipientCounts[index] < expectedEach)
                {
                    return FairShareSupportDestination.Recipients;
                }
            }

            return sourceCount > 0 && hasRemainderTray
                ? FairShareSupportDestination.Remainder
                : FairShareSupportDestination.None;
        }

        private void ReturnAllToSource()
        {
            interactionVersion++;
            foreach (KeyValuePair<TouchDragItem, SharePieceState> pair in pieces)
            {
                pair.Value.zone = -1;
                pair.Key.transform.SetParent(sourceZone.contentRoot, false);
            }

            RefreshCounts();
            ShowGentleMessage("물건을 모두 모았어요. 다른 순서로 다시 나누어 보세요.");
        }

        private static string HintForCode(string hintKey)
        {
            switch (hintKey)
            {
                case "highlight_unplaced_items": return "왼쪽에 남은 물건을 모두 옮겨야 해요.";
                case "compare_recipient_counts": return "가장 많은 상자와 가장 적은 상자를 비교해 보세요.";
                case "highlight_remainder_tray": return "모두에게 나눈 뒤 남는 것만 노란 칸에 놓아요.";
                default: return "한 상자씩 번갈아 가며 하나씩 놓아 보세요.";
            }
        }

        private static string ItemGlyph(string itemName)
        {
            if (itemName.Contains("귤")) return "●";
            if (itemName.Contains("별")) return "★";
            if (itemName.Contains("색연필")) return "▰";
            return "◆";
        }

        public static string ItemSpriteResource(string itemName)
        {
            if (itemName.Contains("귤")) return "YeonaMathAdventure/Art/Items/Item-Tangerine";
            if (itemName.Contains("별")) return "YeonaMathAdventure/Art/Items/Item-Star";
            if (itemName.Contains("색연필")) return "YeonaMathAdventure/Art/Items/Item-Pencil";
            return "YeonaMathAdventure/Art/Items/Item-Marble";
        }

        private static Color ItemColor(string itemName)
        {
            if (itemName.Contains("귤")) return new Color(1f, 0.47f, 0.16f, 1f);
            if (itemName.Contains("별")) return MathPalette.StarGold;
            if (itemName.Contains("색연필")) return new Color(0.24f, 0.58f, 0.93f, 1f);
            return MathPalette.Berry;
        }

        private static void AddPieceHighlight(Transform parent, int index)
        {
            Image shine = PremiumMathVisuals.AddCircle(parent, "ClayShine" + index,
                new Color(1f, 1f, 1f, 0.42f));
            MathUiKit.Pin(shine.rectTransform, new Vector2(0.18f, 0.57f), new Vector2(0.43f, 0.85f),
                Vector2.zero, Vector2.zero);
            Image leaf = PremiumMathVisuals.AddCircle(parent, "Leaf" + index,
                new Color(0.26f, 0.63f, 0.24f, 1f));
            MathUiKit.Pin(leaf.rectTransform, new Vector2(0.38f, 0.78f), new Vector2(0.62f, 1.02f),
                Vector2.zero, Vector2.zero);
        }

        private sealed class SharePieceState
        {
            public int amount;
            public int zone;
        }
    }
}
