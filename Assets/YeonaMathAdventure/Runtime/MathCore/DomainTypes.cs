using System;

namespace YeonaMathAdventure.MathCore
{
    public enum MathConcept
    {
        TargetNumber = 0,
        DivisionAndRemainder = 1,
        NumericPattern = 2,
        ShapePattern = 3,
        Rotation = 4,
        Symmetry = 5,
        SpatialReasoning = 6
    }

    public enum ScaffoldLevel
    {
        None = 0,
        VisualCue = 1,
        GuidedSteps = 2,
        WorkedExample = 3
    }

    public enum ParentConceptStatus
    {
        Familiar = 0,
        Practicing = 1,
        NeedsHelp = 2
    }

    public enum AttemptState
    {
        Solved = 0,
        Continue = 1,
        Adjust = 2
    }

    public enum ArithmeticOperator
    {
        Add = 0,
        Subtract = 1,
        Multiply = 2,
        Divide = 3
    }

    [Serializable]
    public sealed class ValidationResult
    {
        public AttemptState state;
        public string feedbackCode;
        public string messageKo;
        public string hintKey;

        public bool IsSolved
        {
            get { return state == AttemptState.Solved; }
        }

        public static ValidationResult Solved(string feedbackCode, string messageKo)
        {
            return new ValidationResult
            {
                state = AttemptState.Solved,
                feedbackCode = feedbackCode,
                messageKo = messageKo,
                hintKey = string.Empty
            };
        }

        public static ValidationResult Continue(string feedbackCode, string messageKo, string hintKey)
        {
            return new ValidationResult
            {
                state = AttemptState.Continue,
                feedbackCode = feedbackCode,
                messageKo = messageKo,
                hintKey = hintKey
            };
        }

        public static ValidationResult Adjust(string feedbackCode, string messageKo, string hintKey)
        {
            return new ValidationResult
            {
                state = AttemptState.Adjust,
                feedbackCode = feedbackCode,
                messageKo = messageKo,
                hintKey = hintKey
            };
        }
    }

    public static class DifficultyRules
    {
        public const int Minimum = 1;
        public const int Maximum = 5;

        public static int Clamp(int value)
        {
            if (value < Minimum)
            {
                return Minimum;
            }

            if (value > Maximum)
            {
                return Maximum;
            }

            return value;
        }

        public static ScaffoldLevel ClampScaffold(int value)
        {
            if (value < (int)ScaffoldLevel.None)
            {
                return ScaffoldLevel.None;
            }

            if (value > (int)ScaffoldLevel.WorkedExample)
            {
                return ScaffoldLevel.WorkedExample;
            }

            return (ScaffoldLevel)value;
        }
    }
}
