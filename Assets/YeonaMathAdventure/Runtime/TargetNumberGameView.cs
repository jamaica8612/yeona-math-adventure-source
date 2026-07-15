using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure
{
    public sealed class ExpressionDropZone : MonoBehaviour
    {
    }

    public sealed class TargetNumberGameView : MathMiniGameViewBase
    {
        private readonly List<ExpressionToken> expression = new List<ExpressionToken>();
        private readonly List<Button> numberButtons = new List<Button>();
        private readonly List<Button> operatorButtons = new List<Button>();
        private TargetNumberProblem problem;
        private RectTransform expressionRow;
        private RectTransform expressionDropPanel;
        private TMP_Text liveValueText;
        private bool[] usedNumbers;
        private bool[] usedOperators;
        private int interactionVersion;

        public TargetNumberGameView(MathJourneyBootstrap journey, RectTransform safeRoot, RectTransform dragLayer,
            int difficulty, ScaffoldLevel supportLevel, bool assessment, int roundNumber, int roundCount)
            : base(journey, safeRoot, dragLayer, difficulty, supportLevel, assessment, roundNumber, roundCount)
        {
        }

        public override MathActivityKind ActivityKind
        {
            get { return MathActivityKind.TargetNumber; }
        }

        protected override string ProblemId
        {
            get { return problem == null ? "target-number-missing" : problem.id; }
        }

        protected override MathConcept Concept
        {
            get { return MathConcept.TargetNumber; }
        }

        protected override string AutomaticSupportMessage()
        {
            return BuildSupportMessage(SupportLevel, problem);
        }

        protected override void ApplySupportVisuals()
        {
            if ((int)SupportLevel < (int)ScaffoldLevel.VisualCue || liveValueText == null)
            {
                return;
            }

            liveValueText.text = (int)SupportLevel >= (int)ScaffoldLevel.GuidedSteps
                ? "1단계: 숫자 하나를 놓고 계산을 시작해요."
                : "숫자 → 연산 → 숫자 순서로 이어 보세요.";
            if ((int)SupportLevel >= (int)ScaffoldLevel.GuidedSteps && expressionDropPanel != null)
            {
                expressionDropPanel.GetComponent<Image>().color = MathPalette.MintLight;
            }
        }

        public static string BuildSupportMessage(ScaffoldLevel supportLevel, TargetNumberProblem source)
        {
            ScaffoldLevel level = DifficultyRules.ClampScaffold((int)supportLevel);
            if (level == ScaffoldLevel.None)
            {
                return string.Empty;
            }

            int target = source == null ? 0 : source.target;
            if (level == ScaffoldLevel.VisualCue)
            {
                return "목표와 현재 값을 번갈아 보며 숫자 → 연산 → 숫자 순서로 놓아 보세요.";
            }

            if (level == ScaffoldLevel.WorkedExample && source != null &&
                !string.IsNullOrWhiteSpace(source.knownSolutionExpression))
            {
                return "따라 해 볼 예: " + source.knownSolutionExpression + " · 계산 순서를 하나씩 확인해요.";
            }

            if (source == null)
            {
                return "1단계 숫자 하나 고르기 · 2단계 ×·÷로 큰 변화 · 3단계 +·-로 목표와의 차이 줄이기";
            }

            return "1단계 숫자 하나 고르기 · 2단계 ×·÷로 큰 변화 · 3단계 +·-로 목표 " + target +
                   "과의 차이 줄이기";
        }

        protected override void BuildGame()
        {
            problem = TargetNumberGenerator.Generate(Journey.NextProblemSeed(), Difficulty);
            usedNumbers = new bool[problem.numberBlocks.Length];
            usedOperators = new bool[problem.operatorBlocks.Length];

            RectTransform column = MathUiKit.CreateVertical(
                Body, "TargetNumberColumn", MathGameLayoutBudget.TargetSpacing,
                new RectOffset(28, 28, 6, 6), TextAnchor.UpperCenter);
            MathUiKit.Stretch(column);

            RectTransform mission = MathUiKit.CreatePanel(column, "MissionSign",
                new Color(1f, 0.95f, 0.78f, 0.96f), -1f, 78f);
            MathUiKit.SetLayout(mission, -1f, 78f, 1f, 0f);
            TMP_Text prompt = MathUiKit.CreateText(mission, "Prompt",
                "별다리의 힘을 " + problem.target + "으로 맞춰 줘!", 31f,
                MathPalette.Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            MathUiKit.Pin(prompt.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.76f, 0.92f),
                Vector2.zero, Vector2.zero);
            Button help = MathUiKit.CreateButton(mission, "BandiHelp", "?", MathPalette.Lavender,
                MathPalette.White, ShowHint, 76f, 64f, 30f);
            MathUiKit.ExpandHitTarget(help);
            MathUiKit.Pin(help.GetComponent<RectTransform>(), new Vector2(0.80f, 0.12f),
                new Vector2(0.875f, 0.88f), Vector2.zero, Vector2.zero);
            Button reset = MathUiKit.CreateButton(mission, "Reset", "다시", MathPalette.Coral,
                MathPalette.White, ClearExpression, 76f, 64f, 22f);
            MathUiKit.ExpandHitTarget(reset);
            MathUiKit.Pin(reset.GetComponent<RectTransform>(), new Vector2(0.90f, 0.12f),
                new Vector2(0.975f, 0.88f), Vector2.zero, Vector2.zero);

            RectTransform target = MathUiKit.CreatePanel(column, "Target", MathPalette.StarGold, 520f,
                MathGameLayoutBudget.TargetCardHeight);
            MathUiKit.SetLayout(target, 520f, MathGameLayoutBudget.TargetCardHeight, 0f, 0f);
            MathUiKit.CreateText(target, "TargetText", "★  " + problem.target, 52f, MathPalette.NightBlue,
                TextAlignmentOptions.Center, FontStyles.Bold);

            expressionDropPanel = MathUiKit.CreatePanel(column, "ExpressionDrop",
                new Color(0.33f, 0.32f, 0.74f, 0.9f), -1f,
                MathGameLayoutBudget.TargetExpressionHeight);
            expressionDropPanel.gameObject.AddComponent<ExpressionDropZone>();
            MathUiKit.SetLayout(expressionDropPanel, -1f, MathGameLayoutBudget.TargetExpressionHeight, 1f, 0f);
            RectTransform expressionColumn = MathUiKit.CreateVertical(
                expressionDropPanel, "ExpressionColumn", 2f, new RectOffset(18, 18, 4, 4), TextAnchor.MiddleCenter);
            MathUiKit.Stretch(expressionColumn);
            RectTransform expressionContent;
            ScrollRect expressionScroll = MathUiKit.CreateScrollView(
                expressionColumn, "ExpressionScroll", out expressionContent, true, false);
            MathUiKit.SetLayout(expressionScroll.GetComponent<RectTransform>(), -1f, 88f, 1f, 0f);
            expressionScroll.movementType = ScrollRect.MovementType.Clamped;
            expressionRow = expressionContent;
            expressionRow.anchorMin = new Vector2(0f, 0f);
            expressionRow.anchorMax = new Vector2(0f, 1f);
            expressionRow.pivot = new Vector2(0f, 0.5f);
            expressionRow.anchoredPosition = Vector2.zero;
            HorizontalLayoutGroup expressionLayout = expressionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            expressionLayout.spacing = 10f;
            expressionLayout.padding = new RectOffset(8, 8, 0, 0);
            expressionLayout.childAlignment = TextAnchor.MiddleLeft;
            expressionLayout.childControlWidth = true;
            expressionLayout.childControlHeight = true;
            expressionLayout.childForceExpandWidth = false;
            expressionLayout.childForceExpandHeight = false;
            ContentSizeFitter expressionFitter = expressionRow.gameObject.AddComponent<ContentSizeFitter>();
            expressionFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            liveValueText = MathUiKit.CreateText(expressionColumn, "LiveValue", "조각을 다리 홈으로 옮겨 줘.",
                24f, MathPalette.White, TextAlignmentOptions.Center);
            MathUiKit.SetLayout(liveValueText.rectTransform, -1f, 24f, 1f, 0f);
            RebuildExpression();

            RectTransform blockArea = MathUiKit.CreateHorizontal(
                column, "BlockArea", 18f, null, TextAnchor.MiddleCenter);
            MathUiKit.SetLayout(blockArea, -1f, MathGameLayoutBudget.TargetBlocksHeight, 1f, 0f);

            RectTransform numberPanel = MathUiKit.CreatePanel(blockArea, "Numbers",
                new Color(0.76f, 0.94f, 1f, 0.88f), 650f, 264f);
            MathUiKit.SetLayout(numberPanel, 650f, 264f, 1f, 0f);
            RectTransform numberColumn = MathUiKit.CreateVertical(
                numberPanel, "NumberColumn", 5f, new RectOffset(16, 16, 8, 8), TextAnchor.MiddleCenter);
            MathUiKit.Stretch(numberColumn);
            TMP_Text numberLabel = MathUiKit.CreateText(numberColumn, "NumberLabel", "별조각", 24f,
                MathPalette.DeepBlue, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(numberLabel.rectTransform, -1f, 32f, 1f, 0f);
            RectTransform numberRow = MathUiKit.CreateGrid(numberColumn, "NumberRow", new Vector2(140f, 104f),
                new Vector2(10f, 8f), 3);
            MathUiKit.SetLayout(numberRow, -1f, 218f, 1f, 0f);
            for (int index = 0; index < problem.numberBlocks.Length; index++)
            {
                CreateNumberBlock(numberRow, index);
            }

            RectTransform operatorPanel = MathUiKit.CreatePanel(blockArea, "Operators",
                new Color(0.9f, 0.84f, 1f, 0.9f),
                540f, 264f);
            MathUiKit.SetLayout(operatorPanel, 540f, 264f, 0f, 0f);
            RectTransform operatorColumn = MathUiKit.CreateVertical(
                operatorPanel, "OperatorColumn", 5f, new RectOffset(14, 14, 8, 8), TextAnchor.MiddleCenter);
            MathUiKit.Stretch(operatorColumn);
            TMP_Text operatorLabel = MathUiKit.CreateText(operatorColumn, "OperatorLabel", "연결 마법", 24f,
                MathPalette.DeepBlue, TextAlignmentOptions.Center, FontStyles.Bold);
            MathUiKit.SetLayout(operatorLabel.rectTransform, -1f, 32f, 1f, 0f);
            RectTransform operatorRow = MathUiKit.CreateGrid(operatorColumn, "OperatorRow", new Vector2(118f, 104f),
                new Vector2(9f, 8f), 3);
            MathUiKit.SetLayout(operatorRow, -1f, 218f, 1f, 0f);
            for (int index = 0; index < problem.operatorBlocks.Length; index++)
            {
                CreateOperatorBlock(operatorRow, index);
            }

            if (Difficulty >= 3)
            {
                CreateParenthesisBlock(operatorRow, "(");
                CreateParenthesisBlock(operatorRow, ")");
            }

        }

        private void CreateNumberBlock(Transform parent, int index)
        {
            Button button = MathUiKit.CreateButton(parent, "Number" + index, problem.numberBlocks[index].ToString(),
                NumberBlockColor(index), MathPalette.NightBlue, null, 132f, 116f, 39f);
            numberButtons.Add(button);
            TouchDragItem drag = button.gameObject.AddComponent<TouchDragItem>();
            drag.sourceIndex = index;
            drag.Configure(DragLayer);
            drag.pressed = delegate { interactionVersion++; };
            drag.tapped = delegate { AddNumber(index); };
            drag.released = delegate(TouchDragItem item, GameObject target)
            {
                if (MathUiKit.FindInParents<ExpressionDropZone>(target) != null)
                {
                    AddNumber(index);
                }
            };
        }

        private void CreateOperatorBlock(Transform parent, int index)
        {
            Button button = MathUiKit.CreateButton(parent, "Operator" + index, OperatorSymbol(problem.operatorBlocks[index]),
                MathPalette.Lavender, MathPalette.White, null, 108f, 116f, 43f);
            operatorButtons.Add(button);
            TouchDragItem drag = button.gameObject.AddComponent<TouchDragItem>();
            drag.sourceIndex = index;
            drag.Configure(DragLayer);
            drag.pressed = delegate { interactionVersion++; };
            drag.tapped = delegate { AddOperator(index); };
            drag.released = delegate(TouchDragItem item, GameObject target)
            {
                if (MathUiKit.FindInParents<ExpressionDropZone>(target) != null)
                {
                    AddOperator(index);
                }
            };
        }

        private void CreateParenthesisBlock(Transform parent, string symbol)
        {
            MathUiKit.CreateButton(parent, "Parenthesis" + symbol, symbol, MathPalette.White, MathPalette.DeepBlue,
                delegate { AddParenthesis(symbol); }, 82f, 116f, 40f);
        }

        private void AddNumber(int index)
        {
            if (index < 0 || index >= usedNumbers.Length || usedNumbers[index])
            {
                ShowGentleMessage("사용한 숫자는 식에서 빼면 다시 쓸 수 있어요.");
                return;
            }

            if (expression.Count > 0 && (expression[expression.Count - 1].kind == TokenKind.Number ||
                                         expression[expression.Count - 1].text == ")"))
            {
                ShowGentleMessage("숫자 사이에 연산 블록을 하나 놓아 볼까요?");
                return;
            }

            usedNumbers[index] = true;
            expression.Add(new ExpressionToken
            {
                text = problem.numberBlocks[index].ToString(),
                kind = TokenKind.Number,
                sourceIndex = index
            });
            RefreshSources();
            RebuildExpression();
        }

        private void AddOperator(int index)
        {
            if (index < 0 || index >= usedOperators.Length || usedOperators[index])
            {
                ShowGentleMessage("사용한 연산 블록은 식에서 빼면 다시 쓸 수 있어요.");
                return;
            }

            if (expression.Count == 0 || expression[expression.Count - 1].kind == TokenKind.Operator ||
                expression[expression.Count - 1].text == "(")
            {
                ShowGentleMessage("연산 블록 앞에 숫자를 먼저 놓아 보세요.");
                return;
            }

            usedOperators[index] = true;
            expression.Add(new ExpressionToken
            {
                text = OperatorSymbol(problem.operatorBlocks[index]),
                kind = TokenKind.Operator,
                sourceIndex = index
            });
            RefreshSources();
            RebuildExpression();
        }

        private void AddParenthesis(string symbol)
        {
            if (symbol == "(")
            {
                if (expression.Count > 0 && (expression[expression.Count - 1].kind == TokenKind.Number ||
                                             expression[expression.Count - 1].text == ")"))
                {
                    ShowGentleMessage("여는 괄호 앞에는 연산 블록이 필요해요.");
                    return;
                }
            }
            else
            {
                int opens = 0;
                for (int index = 0; index < expression.Count; index++)
                {
                    opens += expression[index].text == "(" ? 1 : expression[index].text == ")" ? -1 : 0;
                }

                string previous = expression.Count == 0 ? string.Empty : expression[expression.Count - 1].text;
                bool previousIsOperator = expression.Count > 0 &&
                                          expression[expression.Count - 1].kind == TokenKind.Operator;
                if (!CanAppendClosingParenthesis(opens, previous, previousIsOperator))
                {
                    ShowGentleMessage("괄호 안의 식을 먼저 완성해 보세요.");
                    return;
                }
            }

            expression.Add(new ExpressionToken { text = symbol, kind = TokenKind.Parenthesis, sourceIndex = -1 });
            RebuildExpression();
        }

        public static bool CanAppendClosingParenthesis(
            int unmatchedOpenCount,
            string previousToken,
            bool previousIsOperator)
        {
            return unmatchedOpenCount > 0 &&
                   !string.IsNullOrEmpty(previousToken) &&
                   previousToken != "(" &&
                   !previousIsOperator;
        }

        public static bool ShouldScheduleAutomaticValidation(
            TargetNumberProblem source,
            ExpressionEvaluation evaluation)
        {
            return source != null && evaluation != null && evaluation.usedNumbers != null &&
                   evaluation.usedNumbers.Length >= source.minimumNumbersUsed;
        }

        private void RemoveToken(int tokenIndex)
        {
            if (tokenIndex < 0 || tokenIndex >= expression.Count)
            {
                return;
            }

            ExpressionToken token = expression[tokenIndex];
            if (token.kind == TokenKind.Number && token.sourceIndex >= 0)
            {
                usedNumbers[token.sourceIndex] = false;
            }
            else if (token.kind == TokenKind.Operator && token.sourceIndex >= 0)
            {
                usedOperators[token.sourceIndex] = false;
            }

            expression.RemoveAt(tokenIndex);
            RefreshSources();
            RebuildExpression();
        }

        private void ClearExpression()
        {
            expression.Clear();
            for (int index = 0; index < usedNumbers.Length; index++)
            {
                usedNumbers[index] = false;
            }

            for (int index = 0; index < usedOperators.Length; index++)
            {
                usedOperators[index] = false;
            }

            RefreshSources();
            RebuildExpression();
            ShowGentleMessage("새로운 방법으로 다시 이어 보세요.");
        }

        private void RebuildExpression()
        {
            // Every edit invalidates the delayed validation scheduled for the previous expression.
            interactionVersion++;
            int scheduledVersion = interactionVersion;
            for (int index = expressionRow.childCount - 1; index >= 0; index--)
            {
                expressionRow.GetChild(index).gameObject.SetActive(false);
                Object.Destroy(expressionRow.GetChild(index).gameObject);
            }

            if (expression.Count == 0)
            {
                TMP_Text empty = MathUiKit.CreateText(expressionRow, "Empty", "여기에 식을 만들어요", 31f,
                    new Color(1f, 1f, 1f, 0.82f), TextAlignmentOptions.Center, FontStyles.Bold);
                MathUiKit.SetLayout(empty.rectTransform, 640f, 70f, 1f, 0f);
                liveValueText.text = "조각을 다리 홈으로 옮겨 줘.";
                return;
            }

            for (int index = 0; index < expression.Count; index++)
            {
                int captured = index;
                Color color = expression[index].kind == TokenKind.Number ? MathPalette.White : MathPalette.LavenderLight;
                MathUiKit.CreateButton(expressionRow, "Token" + index, expression[index].text, color, MathPalette.Ink,
                    delegate { RemoveToken(captured); }, 106f, 70f, 33f);
            }

            ExpressionEvaluation evaluation;
            string error;
            if (ArithmeticExpressionEvaluator.TryEvaluate(ExpressionText(), out evaluation, out error))
            {
                liveValueText.text = "지금 만든 값  =  " + evaluation.value;
                ValidationResult preview = TargetNumberValidator.Validate(problem, ExpressionText());
                bool matchesTarget = evaluation.value.Equals(RationalNumber.FromInteger(problem.target));
                if (matchesTarget && !preview.IsSolved && !string.IsNullOrEmpty(preview.messageKo))
                {
                    liveValueText.text = evaluation.value + "!  " + preview.messageKo;
                }
                liveValueText.color = preview.IsSolved ? MathPalette.StarGold : MathPalette.White;
                if (ShouldScheduleAutomaticValidation(problem, evaluation))
                {
                    Journey.RunAfter(0.65f, delegate
                    {
                        if (scheduledVersion == interactionVersion)
                        {
                            CheckExpression();
                        }
                    });
                }
            }
            else
            {
                liveValueText.text = "식을 이어서 완성해 보세요.";
                liveValueText.color = MathPalette.Quiet;
            }
        }

        private void RefreshSources()
        {
            for (int index = 0; index < numberButtons.Count; index++)
            {
                numberButtons[index].interactable = !usedNumbers[index];
            }

            for (int index = 0; index < operatorButtons.Count; index++)
            {
                operatorButtons[index].interactable = !usedOperators[index];
            }
        }

        private void CheckExpression()
        {
            ValidationResult result = TargetNumberValidator.Validate(problem, ExpressionText());
            if (result.IsSolved)
            {
                Complete();
                return;
            }

            ShowTryAgain(string.IsNullOrEmpty(result.messageKo)
                ? "아주 가까워요. 블록 순서를 바꾸어 다시 만들어 볼까요?"
                : result.messageKo, HintForCode(result.hintKey));
        }

        private void ShowHint()
        {
            if (HintsUsed == 0)
            {
                CountHint("곱셈과 나눗셈을 먼저 계산한 뒤, 목표와 얼마나 차이 나는지 살펴보세요.");
            }
            else if (HintsUsed == 1)
            {
                CountHint("아직 쓰지 않은 숫자 하나를 골라 목표와의 차이를 크게 줄이는 연산부터 시험해 보세요.");
            }
            else
            {
                CountHint("한 가지 가능한 길은 “" + problem.knownSolutionExpression + "”이에요. 다른 식도 좋아요!");
            }
        }

        private string ExpressionText()
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < expression.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(expression[index].text);
            }

            return builder.ToString();
        }

        private static string HintForCode(string hintKey)
        {
            switch (hintKey)
            {
                case "add_number_block": return "사용하지 않은 숫자 블록을 하나 더 이어 보세요.";
                case "replace_divisor": return "0이 아닌 숫자로 나누어야 해요.";
                case "compare_with_target": return "지금 값과 목표의 차이를 먼저 비교해 보세요.";
                default: return "식 끝이 숫자로 끝나는지, 블록이 번갈아 놓였는지 살펴보세요.";
            }
        }

        private static string OperatorSymbol(ArithmeticOperator operation)
        {
            switch (operation)
            {
                case ArithmeticOperator.Subtract: return "-";
                case ArithmeticOperator.Multiply: return "×";
                case ArithmeticOperator.Divide: return "÷";
                default: return "+";
            }
        }

        private static Color NumberBlockColor(int index)
        {
            switch ((index % 4 + 4) % 4)
            {
                case 0: return MathPalette.StarGold;
                case 1: return new Color(0.31f, 0.67f, 0.96f, 1f);
                case 2: return MathPalette.Mint;
                default: return new Color(1f, 0.54f, 0.42f, 1f);
            }
        }

        private enum TokenKind
        {
            Number,
            Operator,
            Parenthesis
        }

        private sealed class ExpressionToken
        {
            public string text;
            public TokenKind kind;
            public int sourceIndex;
        }
    }
}
