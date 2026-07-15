using NUnit.Framework;

namespace YeonaMathAdventure.MathCore.Tests
{
    public sealed class ArithmeticAndValidatorTests
    {
        [Test]
        public void Evaluator_UsesPrecedenceAndExactFractions()
        {
            ExpressionEvaluation evaluation;
            string error;

            bool parsed = ArithmeticExpressionEvaluator.TryEvaluate("8 ÷ 3 × 3 + 2", out evaluation, out error);

            Assert.That(parsed, Is.True, error);
            Assert.That(evaluation.value, Is.EqualTo(RationalNumber.FromInteger(10)));
            Assert.That(evaluation.usedNumbers, Is.EqualTo(new[] { 8, 3, 3, 2 }));
            Assert.That(evaluation.usedOperators, Is.EqualTo(new[]
            {
                ArithmeticOperator.Divide,
                ArithmeticOperator.Multiply,
                ArithmeticOperator.Add
            }));
        }

        [Test]
        public void TargetValidator_AcceptsDifferentInventoryValidSolutions()
        {
            TargetNumberProblem problem = new TargetNumberProblem
            {
                target = 12,
                numberBlocks = new[] { 2, 3, 4, 6 },
                operatorBlocks = new[] { ArithmeticOperator.Multiply },
                minimumNumbersUsed = 2,
                maximumNumbersUsed = 2,
                consumeOperatorBlocks = true
            };

            ValidationResult first = TargetNumberValidator.Validate(problem, "3 × 4");
            ValidationResult second = TargetNumberValidator.Validate(problem, "6 * 2");

            Assert.That(first.IsSolved, Is.True);
            Assert.That(second.IsSolved, Is.True);
        }

        [Test]
        public void TargetValidator_RejectsReusingUnavailableNumberBlock()
        {
            TargetNumberProblem problem = new TargetNumberProblem
            {
                target = 9,
                numberBlocks = new[] { 3, 4 },
                operatorBlocks = new[] { ArithmeticOperator.Multiply },
                minimumNumbersUsed = 2,
                maximumNumbersUsed = 2
            };

            ValidationResult result = TargetNumberValidator.Validate(problem, "3 × 3");

            Assert.That(result.IsSolved, Is.False);
            Assert.That(result.feedbackCode, Is.EqualTo("number_block_unavailable"));
        }

        [Test]
        public void Evaluator_RejectsDivisionByZeroAndUnaryShortcut()
        {
            ExpressionEvaluation ignored;
            string divisionError;
            string unaryError;

            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("8 ÷ 0", out ignored, out divisionError), Is.False);
            Assert.That(divisionError, Is.EqualTo("division_by_zero"));
            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("-3 + 5", out ignored, out unaryError), Is.False);
            Assert.That(unaryError, Is.EqualTo("number_expected"));
        }

        [Test]
        public void FairShareValidator_RecomputesQuotientAndRemainder()
        {
            FairShareProblem problem = new FairShareProblem
            {
                totalItems = 17,
                recipientCount = 5,
                expectedEach = 3,
                expectedRemainder = 2,
                useRemainderTray = true
            };

            ValidationResult solved = FairShareValidator.Validate(problem, new FairShareAttempt
            {
                recipientItemCounts = new[] { 3, 3, 3, 3, 3 },
                remainderCount = 2
            });
            ValidationResult rebalance = FairShareValidator.Validate(problem, new FairShareAttempt
            {
                recipientItemCounts = new[] { 4, 3, 3, 3, 2 },
                remainderCount = 2
            });

            Assert.That(solved.IsSolved, Is.True);
            Assert.That(rebalance.IsSolved, Is.False);
            Assert.That(rebalance.feedbackCode, Is.EqualTo("shares_not_equal"));
        }
    }
}
