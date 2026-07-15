using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure
{
    public sealed class PatternBoardSlot : MonoBehaviour
    {
        public int boardIndex;
        public int blankPosition = -1;
        public RectTransform visualRoot;
    }

    public sealed class PatternSpaceGameView : MathMiniGameViewBase
    {
        private readonly List<PatternChoiceState> choices = new List<PatternChoiceState>();
        private readonly List<Button> choiceButtons = new List<Button>();
        private readonly List<PatternBoardSlot> blankSlots = new List<PatternBoardSlot>();
        private PatternSpaceProblem problem;
        private PatternTile[] placedTiles;
        private int[] placedChoiceIndices;
        private int[] choiceToBlank;
        private Image symmetryAxis;
        private int interactionVersion;

        public PatternSpaceGameView(MathJourneyBootstrap journey, RectTransform safeRoot, RectTransform dragLayer,
            int difficulty, ScaffoldLevel supportLevel, bool assessment, int roundNumber, int roundCount)
            : base(journey, safeRoot, dragLayer, difficulty, supportLevel, assessment, roundNumber, roundCount)
        {
        }

        public override MathActivityKind ActivityKind
        {
            get { return MathActivityKind.PatternSpace; }
        }

        protected override string ProblemId
        {
            get { return problem == null ? "pattern-space-missing" : problem.id; }
        }

        protected override MathConcept Concept
        {
            get { return problem == null ? MathConcept.SpatialReasoning : problem.concept; }
        }

        protected override string AutomaticSupportMessage()
        {
            return BuildSupportMessage(SupportLevel, problem);
        }

        protected override string MissionSpeech()
        {
            return problem == null ? string.Empty : GardenMissionLine(problem);
        }

        protected override void ApplySupportVisuals()
        {
            if ((int)SupportLevel < (int)ScaffoldLevel.VisualCue)
            {
                return;
            }

            if (symmetryAxis != null)
            {
                symmetryAxis.color = MathPalette.Yellow;
            }

            RefreshSupportBlankHighlight();
        }

        public static string BuildSupportMessage(ScaffoldLevel supportLevel, PatternSpaceProblem source)
        {
            ScaffoldLevel level = DifficultyRules.ClampScaffold((int)supportLevel);
            if (level == ScaffoldLevel.None)
            {
                return string.Empty;
            }

            PatternPuzzleKind kind = source == null ? PatternPuzzleKind.ShapePattern : source.kind;
            if (level == ScaffoldLevel.VisualCue)
            {
                switch (kind)
                {
                    case PatternPuzzleKind.NumericSequence:
                        return "보이는 수 사이의 차이와 떨어진 칸 수를 함께 표시해 보세요.";
                    case PatternPuzzleKind.Rotation:
                        return "앞 화살표와 빈칸을 번갈아 보며 90도 회전을 따라가 보세요.";
                    case PatternPuzzleKind.Symmetry:
                        return "가운데 선에서 같은 거리인 두 칸을 한 쌍으로 보세요.";
                    case PatternPuzzleKind.SpatialFill:
                        return "빈칸에 닿는 선의 방향을 먼저 맞추고 끊긴 곳을 찾아보세요.";
                    default:
                        return "모양·색·방향 중 반복되는 한 가지를 먼저 찾아보세요.";
                }
            }

            int step = 0;
            bool hasNumericStep = kind == PatternPuzzleKind.NumericSequence &&
                                  TryComputeVisibleNumericStep(source, out step);
            if (level == ScaffoldLevel.WorkedExample)
            {
                if (hasNumericStep)
                {
                    return "따라 해 볼 예: 수의 차이 ÷ 떨어진 칸 수 = 한 칸 변화 " + step;
                }

                switch (kind)
                {
                    case PatternPuzzleKind.Rotation:
                        return "따라 해 볼 예: 90도씩 돌리며 앞뒤 블록의 회전 규칙과 맞을 때 멈춰요.";
                    case PatternPuzzleKind.Symmetry:
                        return "따라 해 볼 예: 가운데 선에서 같은 거리인 칸을 찾아 거울 방향으로 맞춰요.";
                    case PatternPuzzleKind.SpatialFill:
                        return "따라 해 볼 예: 빈칸에 닿는 선부터 맞추고 끊긴 선은 90도씩 돌려요.";
                    default:
                        return "따라 해 볼 예: 빈칸 양옆의 모양·색·방향을 하나씩 같은 주기와 비교해요.";
                }
            }

            if (hasNumericStep)
            {
                return "1단계 보이는 두 수를 빼기 · 2단계 떨어진 칸 수로 나누기 · 3단계 한 칸마다 " +
                       step + "씩 이어 가기";
            }

            switch (kind)
            {
                case PatternPuzzleKind.Rotation:
                    return "1단계 앞뒤 방향 확인 · 2단계 90도씩 돌리기 · 3단계 회전 규칙과 맞을 때 멈추기";
                case PatternPuzzleKind.Symmetry:
                    return "1단계 가운데 선 찾기 · 2단계 같은 거리 칸 짝짓기 · 3단계 거울 방향 확인";
                case PatternPuzzleKind.SpatialFill:
                    return "1단계 닿는 선 찾기 · 2단계 선 방향 맞추기 · 3단계 끊기면 90도씩 돌리기";
                default:
                    return "1단계 모양 보기 · 2단계 색 보기 · 3단계 방향까지 같은 반복 주기 찾기";
            }
        }

        protected override void BuildGame()
        {
            PatternPuzzleKind kind = Journey.ChoosePatternKind(IsAssessment, RoundNumber);
            problem = PatternSpaceGenerator.Generate(Journey.NextProblemSeed(), Difficulty, kind);
            placedTiles = new PatternTile[problem.blankIndices.Length];
            placedChoiceIndices = new int[problem.blankIndices.Length];
            for (int index = 0; index < placedChoiceIndices.Length; index++)
            {
                placedChoiceIndices[index] = -1;
            }

            choiceToBlank = new int[problem.choiceTiles.Length];
            for (int index = 0; index < choiceToBlank.Length; index++)
            {
                choiceToBlank[index] = -1;
            }

            RectTransform column = MathUiKit.CreateVertical(
                Body, "PatternColumn", MathGameLayoutBudget.PatternSpacing,
                new RectOffset(24, 24, 6, 6), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);
            RectTransform mission = MathUiKit.CreatePanel(column, "MissionSign",
                new Color(1f, 0.95f, 0.78f, 0.96f), -1f, 78f);
            MathUiKit.SetLayout(mission, -1f, 78f, 1f, 0f);
            TMP_Text prompt = MathUiKit.CreateText(mission, "Prompt", GardenMissionLine(problem), 29f,
                MathPalette.Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            MathUiKit.Pin(prompt.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.76f, 0.92f),
                Vector2.zero, Vector2.zero);
            Button help = MathUiKit.CreateButton(mission, "BandiHelp", "힌트", MathPalette.Lavender,
                MathPalette.White, ShowHint, 76f, 64f, 20f);
            MathUiKit.ExpandHitTarget(help);
            MathUiKit.Pin(help.GetComponent<RectTransform>(), new Vector2(0.80f, 0.12f),
                new Vector2(0.875f, 0.88f), Vector2.zero, Vector2.zero);
            Button undo = MathUiKit.CreateButton(mission, "Undo", "한칸", MathPalette.Coral,
                MathPalette.White, RemoveLastPlacement, 76f, 64f, 22f);
            MathUiKit.ExpandHitTarget(undo);
            MathUiKit.Pin(undo.GetComponent<RectTransform>(), new Vector2(0.90f, 0.12f),
                new Vector2(0.975f, 0.88f), Vector2.zero, Vector2.zero);

            BuildBoard(column);
            BuildChoices(column);
        }

        private void BuildBoard(Transform parent)
        {
            Vector2 cellSize = BoardCellSize();
            float boardHeight = problem.rows == 1 ? 185f : MathGameLayoutBudget.PatternMaximumBoardHeight;
            RectTransform boardPanel = MathUiKit.CreatePanel(parent, "BoardPanel",
                new Color(1f, 0.9f, 0.72f, 0.88f), -1f,
                boardHeight);
            MathUiKit.SetLayout(boardPanel, -1f, boardHeight, 1f, 1f);

            RectTransform grid = MathUiKit.CreateGrid(boardPanel, "BoardGrid", cellSize, new Vector2(10f, 10f),
                problem.columns, new RectOffset(12, 12, 12, 12));
            float width = problem.columns * cellSize.x + (problem.columns - 1) * 10f + 24f;
            float height = problem.rows * cellSize.y + (problem.rows - 1) * 10f + 24f;
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.5f);
            grid.pivot = new Vector2(0.5f, 0.5f);
            grid.sizeDelta = new Vector2(width, height);
            grid.anchoredPosition = Vector2.zero;

            for (int boardIndex = 0; boardIndex < problem.boardTiles.Length; boardIndex++)
            {
                int blankPosition = BlankPosition(boardIndex);
                if (blankPosition >= 0)
                {
                    CreateBlankSlot(grid, boardIndex, blankPosition);
                }
                else
                {
                    RectTransform cell = MathUiKit.CreatePanel(grid, "Cell" + boardIndex, MathPalette.Surface,
                        cellSize.x, cellSize.y);
                    CreateTileVisual(cell, problem.boardTiles[boardIndex], false);
                }
            }

            if (problem.kind == PatternPuzzleKind.Symmetry)
            {
                RectTransform axis = MathUiKit.CreatePanel(boardPanel, "SymmetryAxis", MathPalette.Coral, 8f,
                    height - 18f);
                axis.anchorMin = axis.anchorMax = new Vector2(0.5f, 0.5f);
                axis.pivot = new Vector2(0.5f, 0.5f);
                axis.sizeDelta = new Vector2(8f, height - 18f);
                axis.anchoredPosition = Vector2.zero;
                symmetryAxis = axis.GetComponent<Image>();
                axis.SetAsLastSibling();
            }
        }

        private void CreateBlankSlot(Transform parent, int boardIndex, int blankPosition)
        {
            Vector2 size = BoardCellSize();
            RectTransform cell = MathUiKit.CreatePanel(parent, "Blank" + boardIndex, MathPalette.Yellow, size.x, size.y);
            Button button = cell.gameObject.AddComponent<Button>();
            button.targetGraphic = cell.GetComponent<Image>();
            int captured = blankPosition;
            button.onClick.AddListener(delegate { OnBlankTapped(captured); });
            PatternBoardSlot slot = cell.gameObject.AddComponent<PatternBoardSlot>();
            slot.boardIndex = boardIndex;
            slot.blankPosition = blankPosition;
            RectTransform visual = MathUiKit.CreatePanel(cell, "VisualRoot", new Color(1f, 1f, 1f, 0f));
            MathUiKit.Stretch(visual, 7f, 7f);
            slot.visualRoot = visual;
            blankSlots.Add(slot);
            MathUiKit.CreateText(visual, "Empty", "?", 48f, MathPalette.DeepBlue,
                TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private void BuildChoices(Transform parent)
        {
            RectTransform choicePanel = MathUiKit.CreatePanel(parent, "ChoicePanel",
                new Color(0.83f, 0.76f, 1f, 0.9f), -1f,
                MathGameLayoutBudget.PatternChoicesHeight);
            MathUiKit.SetLayout(choicePanel, -1f, MathGameLayoutBudget.PatternChoicesHeight, 1f, 0f);
            RectTransform column = MathUiKit.CreateVertical(
                choicePanel, "ChoiceColumn", 2f, new RectOffset(12, 12, 5, 5), TextAnchor.MiddleCenter);
            MathUiKit.Stretch(column);
            TMP_Text label = MathUiKit.CreateText(column, "ChoiceLabel",
                "블록을 탭하면 다음 빈칸에 놓여요 · 끌어서 원하는 빈칸에 놓아도 돼요", 23f,
                MathPalette.DeepBlue, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(label.rectTransform, -1f, 28f, 1f, 0f);
            RectTransform content;
            ScrollRect scroll = MathUiKit.CreateScrollView(column, "ChoiceScroll", out content, true, false);
            MathUiKit.SetLayout(scroll.GetComponent<RectTransform>(), -1f, 100f, 1f, 0f);
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int index = 0; index < problem.choiceTiles.Length; index++)
            {
                PatternTile tile = problem.choiceTiles[index].Clone();
                if (problem.kind == PatternPuzzleKind.Rotation)
                {
                    int expectedRotation = Normalize(problem.expectedTiles[0].rotationQuarterTurns);
                    tile.rotationQuarterTurns = (expectedRotation + 1) % 4;
                }

                choices.Add(new PatternChoiceState { tile = tile, used = false });
                CreateChoiceButton(content, index);
            }
        }

        private void CreateChoiceButton(Transform parent, int choiceIndex)
        {
            RectTransform buttonRect = MathUiKit.CreatePanel(parent, "Choice" + choiceIndex, MathPalette.White, 116f, 88f);
            MathUiKit.SetLayout(buttonRect, 116f, 88f, 0f, 0f);
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonRect.GetComponent<Image>();
            choiceButtons.Add(button);
            CreateTileVisual(buttonRect, choices[choiceIndex].tile, true);
            TouchDragItem drag = buttonRect.gameObject.AddComponent<TouchDragItem>();
            drag.sourceIndex = choiceIndex;
            drag.Configure(DragLayer);
            drag.pressed = delegate { interactionVersion++; };
            drag.tapped = delegate { PlaceInNextBlank(choiceIndex); };
            drag.released = delegate(TouchDragItem item, GameObject target)
            {
                PatternBoardSlot slot = MathUiKit.FindInParents<PatternBoardSlot>(target);
                if (slot == null)
                {
                    ShowGentleMessage("노랗게 빛나는 빈칸에 블록을 놓아 보세요.");
                    return;
                }

                PlaceChoice(choiceIndex, slot.blankPosition);
            };
        }

        private void PlaceInNextBlank(int choiceIndex)
        {
            int blank = -1;
            for (int index = 0; index < placedTiles.Length; index++)
            {
                if (placedTiles[index] == null)
                {
                    blank = index;
                    break;
                }
            }

            if (blank < 0)
            {
                blank = placedTiles.Length - 1;
            }

            PlaceChoice(choiceIndex, blank);
        }

        private void PlaceChoice(int choiceIndex, int blankPosition)
        {
            if (choiceIndex < 0 || choiceIndex >= choices.Count || blankPosition < 0 ||
                blankPosition >= placedTiles.Length)
            {
                return;
            }

            int oldBlank = choiceToBlank[choiceIndex];
            if (oldBlank >= 0 && oldBlank != blankPosition)
            {
                placedTiles[oldBlank] = null;
                placedChoiceIndices[oldBlank] = -1;
                RefreshBlank(oldBlank);
            }

            int previousChoice = placedChoiceIndices[blankPosition];
            if (previousChoice >= 0 && previousChoice != choiceIndex)
            {
                choices[previousChoice].used = false;
                choiceToBlank[previousChoice] = -1;
            }

            choices[choiceIndex].used = true;
            choiceToBlank[choiceIndex] = blankPosition;
            placedChoiceIndices[blankPosition] = choiceIndex;
            placedTiles[blankPosition] = choices[choiceIndex].tile.Clone();
            RefreshBlank(blankPosition);
            RefreshChoices();
            RefreshSupportBlankHighlight();
            ScheduleAutoCheckIfFilled();
        }

        private void OnBlankTapped(int blankPosition)
        {
            if (blankPosition < 0 || blankPosition >= placedTiles.Length)
            {
                return;
            }

            if (placedTiles[blankPosition] == null)
            {
                ShowGentleMessage("아래 블록을 탭하거나 이 빈칸까지 끌어오세요.");
                return;
            }

            if (!CanRotatePlacedTile(problem.kind, placedTiles[blankPosition]))
            {
                ShowGentleMessage("이 퍼즐에서는 블록을 돌리지 않아요. 모양과 위치를 살펴보세요.");
                return;
            }

            placedTiles[blankPosition].rotationQuarterTurns =
                Normalize(placedTiles[blankPosition].rotationQuarterTurns + 1);
            int choice = placedChoiceIndices[blankPosition];
            if (choice >= 0)
            {
                choices[choice].tile.rotationQuarterTurns = placedTiles[blankPosition].rotationQuarterTurns;
            }

            RefreshBlank(blankPosition);
            RefreshChoices();
            ShowGentleMessage("블록을 90도 돌렸어요. 주변 방향과 이어지는지 살펴보세요.");
            ScheduleAutoCheckIfFilled();
        }

        public static bool CanRotatePlacedTile(PatternPuzzleKind kind, PatternTile tile)
        {
            if (tile == null || tile.shapeId == "number")
            {
                return false;
            }

            return kind == PatternPuzzleKind.Rotation ||
                   kind == PatternPuzzleKind.ShapePattern ||
                   kind == PatternPuzzleKind.SpatialFill;
        }

        private void RotateLastPlacement()
        {
            for (int index = placedTiles.Length - 1; index >= 0; index--)
            {
                if (placedTiles[index] != null)
                {
                    OnBlankTapped(index);
                    return;
                }
            }

            ShowGentleMessage("먼저 블록 하나를 빈칸에 놓아 보세요.");
        }

        private void RemoveLastPlacement()
        {
            interactionVersion++;
            for (int index = placedTiles.Length - 1; index >= 0; index--)
            {
                if (placedTiles[index] == null)
                {
                    continue;
                }

                int choice = placedChoiceIndices[index];
                if (choice >= 0)
                {
                    choices[choice].used = false;
                    choiceToBlank[choice] = -1;
                }

                placedTiles[index] = null;
                placedChoiceIndices[index] = -1;
                RefreshBlank(index);
                RefreshChoices();
                RefreshSupportBlankHighlight();
                ShowGentleMessage("블록을 다시 골라 볼 수 있어요.");
                return;
            }

            ShowGentleMessage("아직 놓은 블록이 없어요.");
        }

        private void RefreshBlank(int blankPosition)
        {
            PatternBoardSlot slot = SlotForBlank(blankPosition);
            if (slot == null)
            {
                return;
            }

            ClearChildren(slot.visualRoot);
            if (placedTiles[blankPosition] == null)
            {
                MathUiKit.CreateText(slot.visualRoot, "Empty", "?", 48f, MathPalette.DeepBlue,
                    TextAlignmentOptions.Center, FontStyles.Bold);
            }
            else
            {
                CreateTileVisual(slot.visualRoot, placedTiles[blankPosition], false);
            }
        }

        private void RefreshChoices()
        {
            for (int index = 0; index < choiceButtons.Count; index++)
            {
                Image image = choiceButtons[index].GetComponent<Image>();
                image.color = choices[index].used ? new Color(0.78f, 0.8f, 0.84f, 0.55f) : MathPalette.White;
                RectTransform rect = choiceButtons[index].transform as RectTransform;
                ClearChildren(rect);
                CreateTileVisual(rect, choices[index].tile, true);
            }
        }

        private void RefreshSupportBlankHighlight()
        {
            if (!UsesPersistentBlankCue(SupportLevel) || placedTiles == null)
            {
                return;
            }

            for (int index = 0; index < blankSlots.Count; index++)
            {
                Image image = blankSlots[index].GetComponent<Image>();
                if (image != null)
                {
                    image.color = MathPalette.Yellow;
                }
            }

            for (int index = 0; index < blankSlots.Count; index++)
            {
                int blankPosition = blankSlots[index].blankPosition;
                if (blankPosition < 0 || blankPosition >= placedTiles.Length || placedTiles[blankPosition] != null)
                {
                    continue;
                }

                Image image = blankSlots[index].GetComponent<Image>();
                if (image != null)
                {
                    image.color = MathPalette.Coral;
                }

                break;
            }
        }

        public static bool UsesPersistentBlankCue(ScaffoldLevel supportLevel)
        {
            return (int)DifficultyRules.ClampScaffold((int)supportLevel) >= (int)ScaffoldLevel.VisualCue;
        }

        private void CreateTileVisual(Transform parent, PatternTile tile, bool small)
        {
            if (tile == null)
            {
                return;
            }

            Color clayColor = tile.shapeId == "number" ? MathPalette.StarGold : MathPalette.Tile(tile.colorIndex);
            RectTransform clay = MathUiKit.CreatePanel(parent, "ClayTile", clayColor);
            MathUiKit.Stretch(clay, small ? 8f : 10f, small ? 7f : 9f);
            string glyph = TileGlyph(tile);
            TMP_Text text = MathUiKit.CreateText(clay, "TileVisual", glyph, small ? 39f : 48f,
                tile.shapeId == "number" ? MathPalette.NightBlue : MathPalette.White,
                TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.Stretch(text.rectTransform, 4f, 4f);
            if (tile.shapeId != "number")
            {
                clay.localEulerAngles = new Vector3(0f, 0f, -90f * Normalize(tile.rotationQuarterTurns));
                clay.localScale = new Vector3(tile.reflected ? -1f : 1f, 1f, 1f);
            }
        }

        private void ScheduleAutoCheckIfFilled()
        {
            interactionVersion++;
            for (int index = 0; index < placedTiles.Length; index++)
            {
                if (placedTiles[index] == null)
                {
                    return;
                }
            }

            int scheduledVersion = interactionVersion;
            Journey.RunAfter(0.5f, delegate
            {
                if (scheduledVersion == interactionVersion)
                {
                    CheckPuzzle();
                }
            });
        }

        private static string GardenMissionLine(PatternSpaceProblem source)
        {
            if (source == null)
            {
                return "빈자리에 맞는 조각을 놓아 줘!";
            }

            switch (source.kind)
            {
                case PatternPuzzleKind.NumericSequence:
                    return "숫자 길의 빈자리를 이어 줘!";
                case PatternPuzzleKind.Rotation:
                    return "조각을 놓고 탭해서 방향을 맞춰 줘!";
                case PatternPuzzleKind.Symmetry:
                    return "거울처럼 같은 자리에 조각을 놓아 줘!";
                default:
                    return "규칙에 맞는 조각으로 정원을 고쳐 줘!";
            }
        }

        private void CheckPuzzle()
        {
            int[] slots = new int[problem.blankIndices.Length];
            PatternTile[] placements = new PatternTile[problem.blankIndices.Length];
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index] = problem.blankIndices[index];
                placements[index] = placedTiles[index] == null ? null : placedTiles[index].Clone();
            }

            ValidationResult result = PatternSpaceValidator.Validate(problem, new PatternSpaceAttempt
            {
                slotIndices = slots,
                placedTiles = placements
            });
            if (result.IsSolved)
            {
                Complete();
                return;
            }

            ShowTryAgain(string.IsNullOrEmpty(result.messageKo)
                ? "주변 블록의 규칙을 보고 위치나 방향을 조금 바꾸어 볼까요?"
                : result.messageKo, HintForCode(result.hintKey));
        }

        private void ShowHint()
        {
            if (problem.kind == PatternPuzzleKind.NumericSequence)
            {
                int step;
                bool hasStep = TryComputeVisibleNumericStep(problem, out step);
                CountHint(HintsUsed == 0
                    ? "이웃한 수끼리 얼마나 커지는지 빼서 비교해 보세요."
                    : hasStep
                        ? "한 칸마다 " + step + "씩 커지는 규칙을 빈칸까지 이어 보세요."
                        : "보이는 수의 위치 차이도 함께 세어 같은 간격으로 이어 보세요.");
                return;
            }

            if (problem.kind == PatternPuzzleKind.Rotation)
            {
                CountHint(HintsUsed == 0
                    ? "화살표가 한 칸마다 90도씩 어느 방향으로 도는지 손가락으로 따라가 보세요."
                    : "놓은 화살표를 탭할 때마다 90도 돌아요. 앞 화살표 다음 방향까지 돌려 보세요.");
                return;
            }

            if (problem.kind == PatternPuzzleKind.Symmetry)
            {
                if (symmetryAxis != null)
                {
                    symmetryAxis.color = HintsUsed % 2 == 0 ? MathPalette.Yellow : MathPalette.Coral;
                }

                CountHint("가운데 선에서 같은 거리인 왼쪽 칸과 오른쪽 칸을 짝지어 보세요.");
                return;
            }

            CountHint(HintsUsed == 0
                ? "모양, 색, 방향 중 무엇이 차례로 바뀌는지 하나씩 따로 살펴보세요."
                : "빈칸 바로 앞과 한 주기 앞의 블록을 나란히 비교해 보세요.");
        }

        private int BlankPosition(int boardIndex)
        {
            for (int index = 0; index < problem.blankIndices.Length; index++)
            {
                if (problem.blankIndices[index] == boardIndex)
                {
                    return index;
                }
            }

            return -1;
        }

        private PatternBoardSlot SlotForBlank(int blankPosition)
        {
            for (int index = 0; index < blankSlots.Count; index++)
            {
                if (blankSlots[index].blankPosition == blankPosition)
                {
                    return blankSlots[index];
                }
            }

            return null;
        }

        public static bool TryComputeVisibleNumericStep(PatternSpaceProblem source, out int step)
        {
            step = 0;
            if (source == null || source.boardTiles == null || source.boardTiles.Length < 2 ||
                source.blankIndices == null)
            {
                return false;
            }

            int firstIndex = -1;
            PatternTile firstTile = null;
            for (int index = 0; index < source.boardTiles.Length; index++)
            {
                bool blank = false;
                for (int blankIndex = 0; blankIndex < source.blankIndices.Length; blankIndex++)
                {
                    if (source.blankIndices[blankIndex] == index)
                    {
                        blank = true;
                        break;
                    }
                }

                PatternTile tile = source.boardTiles[index];
                if (blank || tile == null || tile.shapeId != "number")
                {
                    continue;
                }

                if (firstTile == null)
                {
                    firstIndex = index;
                    firstTile = tile;
                    continue;
                }

                int indexDistance = index - firstIndex;
                int valueDistance = tile.value - firstTile.value;
                if (indexDistance > 0 && valueDistance % indexDistance == 0)
                {
                    step = valueDistance / indexDistance;
                    return step != 0;
                }
            }

            return false;
        }

        private Vector2 BoardCellSize()
        {
            if (problem.rows == 1)
            {
                return new Vector2(problem.columns >= 8 ? 138f : 164f, 132f);
            }

            return new Vector2(problem.columns >= 6 ? 132f : 166f, 96f);
        }

        private static string TileGlyph(PatternTile tile)
        {
            switch (tile.shapeId)
            {
                case "number": return tile.value.ToString();
                case "circle": return "●";
                case "triangle": return "▲";
                case "square": return "■";
                case "arrow": return "▲";
                case "leaf": return "◀";
                case "kite": return "◆";
                case "corner": return "└";
                case "bridge": return "━";
                default: return "◆";
            }
        }

        private static string HintForCode(string hintKey)
        {
            switch (hintKey)
            {
                case "pulse_next_blank": return "물음표가 남은 칸부터 채워 보세요.";
                case "rotate_tile": return "놓은 블록을 탭하면 90도 돌아가요.";
                case "show_symmetry_axis": return "가운데 선 양쪽에서 같은 거리의 칸을 짝지어 보세요.";
                default: return "한 칸 앞과 한 주기 앞의 블록을 비교해 보세요.";
            }
        }

        private static int Normalize(int value)
        {
            int normalized = value % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        private sealed class PatternChoiceState
        {
            public PatternTile tile;
            public bool used;
        }
    }
}
