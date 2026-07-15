using System;
using System.Collections.Generic;
using System.Globalization;

namespace YeonaMathAdventure.MathCore
{
    [Serializable]
    public sealed class TargetNumberProblem
    {
        public string id;
        public MathConcept concept = MathConcept.TargetNumber;
        public int difficulty;
        public int target;
        public int[] numberBlocks = new int[0];
        public ArithmeticOperator[] operatorBlocks = new ArithmeticOperator[0];
        public bool consumeOperatorBlocks = true;
        public int minimumNumbersUsed = 2;
        public int maximumNumbersUsed = 4;
        public string promptKo;
        public string knownSolutionExpression;
    }

    public static class TargetNumberValidator
    {
        public static ValidationResult Validate(TargetNumberProblem problem, string expression)
        {
            if (problem == null)
            {
                return ValidationResult.Adjust("problem_missing", "문제를 다시 준비하고 있어요.", "reload_problem");
            }

            ExpressionEvaluation evaluation;
            string errorCode;
            if (!ArithmeticExpressionEvaluator.TryEvaluate(expression, out evaluation, out errorCode))
            {
                if (errorCode == "division_by_zero")
                {
                    return ValidationResult.Adjust("division_by_zero", "0으로는 나눌 수 없어요. 다른 블록을 놓아 볼까요?", "replace_divisor");
                }

                return ValidationResult.Continue("expression_incomplete", "블록 사이를 이어서 식을 완성해 보세요.", "complete_expression");
            }

            if (evaluation.usedNumbers.Length < problem.minimumNumbersUsed)
            {
                return ValidationResult.Continue("more_numbers_needed", "숫자 블록을 하나 더 이어 보세요.", "add_number_block");
            }

            if (evaluation.usedNumbers.Length > problem.maximumNumbersUsed)
            {
                return ValidationResult.Adjust("too_many_numbers", "이번에는 더 짧은 식으로 만들어 볼까요?", "remove_number_block");
            }

            if (!UsesAvailableNumbers(problem.numberBlocks, evaluation.usedNumbers))
            {
                return ValidationResult.Adjust("number_block_unavailable", "판에 있는 숫자 블록만 사용할 수 있어요.", "check_number_blocks");
            }

            if (!UsesAvailableOperators(problem, evaluation.usedOperators))
            {
                return ValidationResult.Adjust("operator_block_unavailable", "판에 있는 연산 블록으로 다시 이어 보세요.", "check_operator_blocks");
            }

            RationalNumber target = RationalNumber.FromInteger(problem.target);
            if (!evaluation.value.Equals(target))
            {
                return ValidationResult.Adjust(
                    "target_not_reached",
                    "가까워지고 있어요. 먼저 곱하거나 나눌 부분을 살펴보세요.",
                    "compare_with_target");
            }

            return ValidationResult.Solved("target_reached", "멋져요! 목표 숫자를 만들었어요.");
        }

        private static bool UsesAvailableNumbers(int[] available, int[] used)
        {
            if (available == null || used == null)
            {
                return false;
            }

            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int index = 0; index < available.Length; index++)
            {
                Increment(counts, available[index]);
            }

            for (int index = 0; index < used.Length; index++)
            {
                int remaining;
                if (!counts.TryGetValue(used[index], out remaining) || remaining <= 0)
                {
                    return false;
                }

                counts[used[index]] = remaining - 1;
            }

            return true;
        }

        private static bool UsesAvailableOperators(TargetNumberProblem problem, ArithmeticOperator[] used)
        {
            if (problem.operatorBlocks == null || used == null)
            {
                return false;
            }

            Dictionary<ArithmeticOperator, int> counts = new Dictionary<ArithmeticOperator, int>();
            for (int index = 0; index < problem.operatorBlocks.Length; index++)
            {
                Increment(counts, problem.operatorBlocks[index]);
            }

            for (int index = 0; index < used.Length; index++)
            {
                int remaining;
                if (!counts.TryGetValue(used[index], out remaining))
                {
                    return false;
                }

                if (problem.consumeOperatorBlocks)
                {
                    if (remaining <= 0)
                    {
                        return false;
                    }

                    counts[used[index]] = remaining - 1;
                }
            }

            return true;
        }

        private static void Increment<TKey>(Dictionary<TKey, int> counts, TKey key)
        {
            int value;
            counts.TryGetValue(key, out value);
            counts[key] = value + 1;
        }
    }

    public static class TargetNumberGenerator
    {
        public static TargetNumberProblem Generate(uint seed, int requestedDifficulty)
        {
            int difficulty = DifficultyRules.Clamp(requestedDifficulty);
            DeterministicRandom random = new DeterministicRandom(seed ^ 0xA341316Cu);
            TargetNumberProblem problem = new TargetNumberProblem
            {
                id = "tn-" + seed.ToString("X8", CultureInfo.InvariantCulture) + "-d" +
                     difficulty.ToString(CultureInfo.InvariantCulture),
                difficulty = difficulty,
                promptKo = "숫자와 연산 블록을 이어 목표 숫자를 만들어 보세요."
            };

            int[] requiredNumbers;
            int[] distractors;

            if (difficulty == 1)
            {
                int target = random.NextInt(20, 46);
                int left = random.NextInt(6, target - 5);
                int right = target - left;
                problem.target = target;
                problem.operatorBlocks = new[] { ArithmeticOperator.Add, ArithmeticOperator.Subtract };
                problem.minimumNumbersUsed = 2;
                problem.maximumNumbersUsed = 2;
                problem.knownSolutionExpression = Format(left) + " + " + Format(right);
                requiredNumbers = new[] { left, right };
                distractors = new[] { random.NextInt(2, 15), random.NextInt(3, 18) };
            }
            else if (difficulty == 2)
            {
                int left = random.NextInt(5, 11);
                int right = random.NextInt(5, 11);
                int offset = random.NextInt(5, 16);
                problem.target = left * right + offset;
                problem.operatorBlocks = new[]
                {
                    ArithmeticOperator.Multiply,
                    ArithmeticOperator.Add,
                    ArithmeticOperator.Subtract
                };
                problem.minimumNumbersUsed = 3;
                problem.maximumNumbersUsed = 3;
                problem.knownSolutionExpression = Format(left) + " × " + Format(right) + " + " + Format(offset);
                requiredNumbers = new[] { left, right, offset };
                distractors = new[] { random.NextInt(2, 16), random.NextInt(5, 21) };
            }
            else if (difficulty == 3)
            {
                int left = random.NextInt(6, 17);
                int right = random.NextInt(5, 16);
                int multiplier = random.NextInt(3, 8);
                problem.target = (left + right) * multiplier;
                problem.operatorBlocks = new[]
                {
                    ArithmeticOperator.Add,
                    ArithmeticOperator.Multiply,
                    ArithmeticOperator.Subtract
                };
                problem.minimumNumbersUsed = 3;
                problem.maximumNumbersUsed = 3;
                problem.knownSolutionExpression = "(" + Format(left) + " + " + Format(right) + ") × " + Format(multiplier);
                requiredNumbers = new[] { left, right, multiplier };
                distractors = new[] { random.NextInt(2, 20), random.NextInt(7, 25) };
            }
            else if (difficulty == 4)
            {
                int left = random.NextInt(10, 19);
                int right = random.NextInt(7, 13);
                int offset = random.NextInt(3, 21);
                problem.target = left * right - offset;
                problem.operatorBlocks = new[]
                {
                    ArithmeticOperator.Multiply,
                    ArithmeticOperator.Subtract,
                    ArithmeticOperator.Add,
                    ArithmeticOperator.Divide
                };
                problem.minimumNumbersUsed = 3;
                problem.maximumNumbersUsed = 3;
                problem.knownSolutionExpression = Format(left) + " × " + Format(right) + " - " + Format(offset);
                requiredNumbers = new[] { left, right, offset };
                distractors = new[] { random.NextInt(3, 25), random.NextInt(8, 31) };
            }
            else
            {
                int divisor = random.NextInt(2, 6);
                int baseValue = random.NextInt(20, 31);
                int left = baseValue * divisor;
                int multiplier = random.NextInt(5, 10);
                int offset = random.NextInt(10, 26);
                problem.target = baseValue * multiplier + offset;
                problem.operatorBlocks = new[]
                {
                    ArithmeticOperator.Multiply,
                    ArithmeticOperator.Divide,
                    ArithmeticOperator.Add,
                    ArithmeticOperator.Subtract
                };
                problem.minimumNumbersUsed = 4;
                problem.maximumNumbersUsed = 4;
                problem.knownSolutionExpression = "(" + Format(left) + " × " + Format(multiplier) + ") ÷ " +
                                                  Format(divisor) + " + " + Format(offset);
                requiredNumbers = new[] { left, multiplier, divisor, offset };
                distractors = new[] { random.NextInt(6, 35), random.NextInt(11, 41) };
            }

            problem.numberBlocks = Combine(requiredNumbers, distractors);
            random.Shuffle(problem.numberBlocks);

            ValidationResult generatedCheck = TargetNumberValidator.Validate(problem, problem.knownSolutionExpression);
            if (!generatedCheck.IsSolved)
            {
                throw new InvalidOperationException("Generated target-number problem is not solvable: " + generatedCheck.feedbackCode);
            }

            return problem;
        }

        private static int[] Combine(int[] left, int[] right)
        {
            int[] combined = new int[left.Length + right.Length];
            Array.Copy(left, 0, combined, 0, left.Length);
            Array.Copy(right, 0, combined, left.Length, right.Length);
            return combined;
        }

        private static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
