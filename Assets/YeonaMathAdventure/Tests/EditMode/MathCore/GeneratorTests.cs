using System;
using NUnit.Framework;

namespace YeonaMathAdventure.MathCore.Tests
{
    public sealed class GeneratorTests
    {
        [Test]
        public void TargetGenerator_IsDeterministicAndAlwaysSolvable()
        {
            for (int difficulty = 1; difficulty <= 5; difficulty++)
            {
                uint seed = (uint)(700 + difficulty);
                TargetNumberProblem first = TargetNumberGenerator.Generate(seed, difficulty);
                TargetNumberProblem second = TargetNumberGenerator.Generate(seed, difficulty);

                Assert.That(second.id, Is.EqualTo(first.id));
                Assert.That(second.target, Is.EqualTo(first.target));
                Assert.That(second.numberBlocks, Is.EqualTo(first.numberBlocks));
                Assert.That(second.knownSolutionExpression, Is.EqualTo(first.knownSolutionExpression));
                Assert.That(TargetNumberValidator.Validate(first, first.knownSolutionExpression).IsSolved, Is.True);
            }
        }

        [Test]
        public void HighestTargetDifficulty_ReachesHundreds()
        {
            TargetNumberProblem problem = TargetNumberGenerator.Generate(42u, 5);

            Assert.That(problem.target, Is.GreaterThanOrEqualTo(100));
            Assert.That(problem.maximumNumbersUsed, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void FairShareGenerator_ProducesEngineVerifiedRemainderProblems()
        {
            for (int difficulty = 1; difficulty <= 5; difficulty++)
            {
                FairShareProblem problem = FairShareGenerator.Generate((uint)(900 + difficulty), difficulty);
                Assert.That(problem.expectedEach, Is.EqualTo(problem.totalItems / problem.recipientCount));
                Assert.That(problem.expectedRemainder, Is.EqualTo(problem.totalItems % problem.recipientCount));
                if (difficulty > 1)
                {
                    Assert.That(problem.expectedRemainder, Is.GreaterThan(0));
                    Assert.That(problem.expectedRemainder, Is.LessThan(problem.recipientCount));
                }
            }
        }

        [TestCase(PatternPuzzleKind.NumericSequence)]
        [TestCase(PatternPuzzleKind.ShapePattern)]
        [TestCase(PatternPuzzleKind.Rotation)]
        [TestCase(PatternPuzzleKind.Symmetry)]
        [TestCase(PatternPuzzleKind.SpatialFill)]
        public void PatternGenerators_HaveACompletePlacementSolution(PatternPuzzleKind kind)
        {
            PatternSpaceProblem problem = PatternSpaceGenerator.Generate(1234u + (uint)kind, 4, kind);
            PatternTile[] placed = new PatternTile[problem.expectedTiles.Length];
            for (int index = 0; index < placed.Length; index++)
            {
                placed[index] = problem.expectedTiles[index].Clone();
            }

            ValidationResult result = PatternSpaceValidator.Validate(problem, new PatternSpaceAttempt
            {
                slotIndices = (int[])problem.blankIndices.Clone(),
                placedTiles = placed
            });

            Assert.That(result.IsSolved, Is.True, kind.ToString());
        }

        [Test]
        public void RotationValidator_NormalizesFullTurnsButRejectsQuarterTurnMismatch()
        {
            PatternSpaceProblem problem = PatternSpaceGenerator.Generate(99u, 3, PatternPuzzleKind.Rotation);
            PatternTile equivalent = problem.expectedTiles[0].Clone();
            equivalent.rotationQuarterTurns += 4;
            PatternTile mismatch = problem.expectedTiles[0].Clone();
            mismatch.rotationQuarterTurns += 1;

            ValidationResult equivalentResult = PatternSpaceValidator.Validate(problem, new PatternSpaceAttempt
            {
                slotIndices = new[] { problem.blankIndices[0] },
                placedTiles = new[] { equivalent }
            });
            ValidationResult mismatchResult = PatternSpaceValidator.Validate(problem, new PatternSpaceAttempt
            {
                slotIndices = new[] { problem.blankIndices[0] },
                placedTiles = new[] { mismatch }
            });

            Assert.That(equivalentResult.IsSolved, Is.True);
            Assert.That(mismatchResult.IsSolved, Is.False);
            Assert.That(mismatchResult.hintKey, Is.EqualTo("rotate_tile"));
        }

        [Test]
        public void LocalBank_ReturnsFreshDeterministicEntries()
        {
            LocalActivityDescriptor[] descriptors = LocalProblemBank.GetDescriptors();
            Assert.That(descriptors.Length, Is.EqualTo(35));

            LocalProblemEntry first;
            LocalProblemEntry second;
            Assert.That(LocalProblemBank.TryCreateEntry(descriptors[0].id, out first), Is.True);
            Assert.That(LocalProblemBank.TryCreateEntry(descriptors[0].id, out second), Is.True);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.descriptor.id, Is.EqualTo(first.descriptor.id));
        }
    }
}
