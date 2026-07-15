using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace YeonaMathAdventure.MathCore.Tests
{
    public sealed class BoundaryFuzzTests
    {
        [Test]
        public void EvaluatorAndTargetInventory_HandleExactFractionsAndRejectReuseOrUnicodeDigits()
        {
            ExpressionEvaluation evaluation;
            string error;
            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("1 ÷ 3 + 1 ÷ 6", out evaluation, out error),
                Is.True, error);
            Assert.That(evaluation.value, Is.EqualTo(new RationalNumber(1, 2)));

            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("١ + 1", out evaluation, out error), Is.False);
            Assert.That(error, Is.EqualTo("number_expected"));
            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("8 ÷ (3 - 3)", out evaluation, out error), Is.False);
            Assert.That(error, Is.EqualTo("division_by_zero"));

            TargetNumberProblem consuming = new TargetNumberProblem
            {
                target = 2,
                numberBlocks = new[] { 8, 2, 2 },
                operatorBlocks = new[] { ArithmeticOperator.Divide },
                consumeOperatorBlocks = true,
                minimumNumbersUsed = 3,
                maximumNumbersUsed = 3
            };
            ValidationResult rejected = TargetNumberValidator.Validate(consuming, "8 ÷ 2 ÷ 2");
            Assert.That(rejected.feedbackCode, Is.EqualTo("operator_block_unavailable"));

            consuming.consumeOperatorBlocks = false;
            Assert.That(TargetNumberValidator.Validate(consuming, "8 ÷ 2 ÷ 2").IsSolved, Is.True);
        }

        [Test]
        public void TargetGenerator_FuzzesDeterminismSolvabilityRangesAndAllOperators()
        {
            HashSet<ArithmeticOperator> exposedOperators = new HashSet<ArithmeticOperator>();
            const int seedsPerDifficulty = 250;
            for (int difficulty = DifficultyRules.Minimum; difficulty <= DifficultyRules.Maximum; difficulty++)
            {
                for (int seedIndex = 0; seedIndex < seedsPerDifficulty; seedIndex++)
                {
                    uint seed = (uint)(difficulty * 100000 + seedIndex);
                    TargetNumberProblem first = TargetNumberGenerator.Generate(seed, difficulty);
                    TargetNumberProblem second = TargetNumberGenerator.Generate(seed, difficulty);

                    AssertTargetEqual(first, second);
                    Assert.That(first.target, Is.GreaterThanOrEqualTo(20), first.id);
                    Assert.That(first.difficulty, Is.EqualTo(difficulty));
                    Assert.That(first.minimumNumbersUsed, Is.GreaterThanOrEqualTo(2));
                    Assert.That(first.maximumNumbersUsed, Is.LessThanOrEqualTo(first.numberBlocks.Length));
                    Assert.That(TargetNumberValidator.Validate(first, first.knownSolutionExpression).IsSolved,
                        Is.True, first.id);
                    for (int index = 0; index < first.operatorBlocks.Length; index++)
                    {
                        exposedOperators.Add(first.operatorBlocks[index]);
                    }
                }
            }

            Assert.That(exposedOperators, Is.EquivalentTo(new[]
            {
                ArithmeticOperator.Add,
                ArithmeticOperator.Subtract,
                ArithmeticOperator.Multiply,
                ArithmeticOperator.Divide
            }));
        }

        [Test]
        public void FairShareGenerator_FuzzesQuotientRemainderDeterminismAndTrayIntegrity()
        {
            const int seedsPerDifficulty = 200;
            for (int difficulty = DifficultyRules.Minimum; difficulty <= DifficultyRules.Maximum; difficulty++)
            {
                for (int seedIndex = 0; seedIndex < seedsPerDifficulty; seedIndex++)
                {
                    uint seed = (uint)(difficulty * 200000 + seedIndex);
                    FairShareProblem first = FairShareGenerator.Generate(seed, difficulty);
                    FairShareProblem second = FairShareGenerator.Generate(seed, difficulty);
                    AssertFairShareEqual(first, second);
                    Assert.That(first.expectedEach, Is.EqualTo(first.totalItems / first.recipientCount), first.id);
                    Assert.That(first.expectedRemainder, Is.EqualTo(first.totalItems % first.recipientCount), first.id);
                    Assert.That(first.useRemainderTray, Is.EqualTo(first.expectedRemainder > 0), first.id);

                    int[] shares = new int[first.recipientCount];
                    for (int index = 0; index < shares.Length; index++)
                    {
                        shares[index] = first.expectedEach;
                    }

                    FairShareAttempt solvedAttempt = new FairShareAttempt
                    {
                        recipientItemCounts = shares,
                        remainderCount = first.expectedRemainder
                    };
                    Assert.That(FairShareValidator.Validate(first, solvedAttempt).IsSolved, Is.True, first.id);

                    first.useRemainderTray = !first.useRemainderTray;
                    ValidationResult corruptTray = FairShareValidator.Validate(first, solvedAttempt);
                    Assert.That(corruptTray.feedbackCode, Is.EqualTo("problem_integrity_failed"), first.id);
                }
            }
        }

        [Test]
        public void PatternGenerators_FuzzAllFiveKindsAcrossAllDifficulties()
        {
            const int seedsPerKindAndDifficulty = 64;
            for (int kindValue = (int)PatternPuzzleKind.NumericSequence;
                 kindValue <= (int)PatternPuzzleKind.SpatialFill;
                 kindValue++)
            {
                PatternPuzzleKind kind = (PatternPuzzleKind)kindValue;
                for (int difficulty = DifficultyRules.Minimum; difficulty <= DifficultyRules.Maximum; difficulty++)
                {
                    for (int seedIndex = 0; seedIndex < seedsPerKindAndDifficulty; seedIndex++)
                    {
                        uint seed = (uint)(kindValue * 1000000 + difficulty * 10000 + seedIndex);
                        PatternSpaceProblem first = PatternSpaceGenerator.Generate(seed, difficulty, kind);
                        PatternSpaceProblem second = PatternSpaceGenerator.Generate(seed, difficulty, kind);
                        AssertPatternEqual(first, second);

                        PatternTile[] placed = CloneTiles(first.expectedTiles);
                        Assert.That(PatternSpaceValidator.Validate(first, new PatternSpaceAttempt
                        {
                            slotIndices = (int[])first.blankIndices.Clone(),
                            placedTiles = placed
                        }).IsSolved, Is.True, first.id);

                        placed[0].rotationQuarterTurns += 4;
                        Assert.That(PatternSpaceValidator.Validate(first, new PatternSpaceAttempt
                        {
                            slotIndices = (int[])first.blankIndices.Clone(),
                            placedTiles = placed
                        }).IsSolved, Is.True, "Full turns must normalize for " + first.id);
                    }
                }
            }
        }

        [Test]
        public void PatternValidator_RejectsUnbuildableChoiceInventoryAndOverflowingBoardShape()
        {
            PatternSpaceProblem problem = PatternSpaceGenerator.Generate(445566u, 3, PatternPuzzleKind.Rotation);
            PatternTile wrong = problem.expectedTiles[0].Clone();
            wrong.rotationQuarterTurns += 1;
            problem.choiceTiles = new[] { wrong };

            ValidationResult unavailable = PatternSpaceValidator.Validate(problem, new PatternSpaceAttempt
            {
                slotIndices = (int[])problem.blankIndices.Clone(),
                placedTiles = CloneTiles(problem.expectedTiles)
            });
            Assert.That(unavailable.feedbackCode, Is.EqualTo("pattern_integrity_failed"));
            Assert.That(unavailable.hintKey, Is.EqualTo("choice_inventory_invalid"));

            PatternSpaceProblem overflow = new PatternSpaceProblem
            {
                rows = int.MaxValue,
                columns = 2,
                boardTiles = new PatternTile[0],
                blankIndices = new[] { 0 },
                expectedTiles = new[] { new PatternTile { shapeId = "square" } },
                choiceTiles = new[] { new PatternTile { shapeId = "square" } }
            };
            Assert.That(PatternSpaceValidator.Validate(overflow, new PatternSpaceAttempt()).feedbackCode,
                Is.EqualTo("pattern_integrity_failed"));
        }

        [Test]
        public void ProgressAndTeacher_FuzzRetainRecentTenAndRemainReadOnlyDeterministic()
        {
            MathProgressData data = new MathProgressData { localProfileId = "boundary_fuzz" };
            DeterministicRandom random = new DeterministicRandom(0xA55AA55Au);
            Array concepts = Enum.GetValues(typeof(MathConcept));
            for (int conceptIndex = 0; conceptIndex < concepts.Length; conceptIndex++)
            {
                MathConcept concept = (MathConcept)concepts.GetValue(conceptIndex);
                ConceptProgressData progress = data.GetOrCreateConcept(concept);
                progress.currentNumberDifficulty = 3;
                progress.currentStepDifficulty = 3;
                for (int attemptIndex = 0; attemptIndex < 50; attemptIndex++)
                {
                    progress.AddAttempt(new AttemptRecord
                    {
                        problemId = concept + "-" + attemptIndex,
                        concept = concept,
                        successful = random.NextBool(),
                        retryCount = random.NextInt(0, 4),
                        hintsUsed = random.NextInt(0, 3),
                        elapsedMilliseconds = random.NextInt(20000, 180001),
                        expectedDurationMilliseconds = 60000
                    });
                }

                Assert.That(progress.recentAttempts.Count, Is.EqualTo(AdaptiveTeacher.RecentAttemptLimit));
                Assert.That(progress.recentAttempts[0].problemId, Is.EqualTo(concept + "-40"));
                Assert.That(progress.recentAttempts[9].problemId, Is.EqualTo(concept + "-49"));

                AdaptiveRecommendation first = AdaptiveTeacher.Recommend(progress);
                AdaptiveRecommendation second = AdaptiveTeacher.Recommend(progress);
                AssertRecommendationEqual(first, second);
                Assert.That(first.numberDifficulty, Is.InRange(DifficultyRules.Minimum, DifficultyRules.Maximum));
                Assert.That(first.stepDifficulty, Is.InRange(DifficultyRules.Minimum, DifficultyRules.Maximum));
            }

            ConceptProgressData target = data.GetOrCreateConcept(MathConcept.TargetNumber);
            target.recentAttempts.Add(null);
            target.recentAttempts.Add(new AttemptRecord { concept = MathConcept.Rotation, problemId = "wrong" });
            target.Normalize();
            Assert.That(target.recentAttempts.Count, Is.EqualTo(AdaptiveTeacher.RecentAttemptLimit));
            Assert.That(target.recentAttempts, Has.All.Not.Null);
            for (int index = 0; index < target.recentAttempts.Count; index++)
            {
                Assert.That(target.recentAttempts[index].concept, Is.EqualTo(MathConcept.TargetNumber));
            }

            MathTeacherAI teacher = new MathTeacherAI();
            MathTeacherDecision firstDecision = teacher.SelectNext(data);
            MathTeacherDecision secondDecision = teacher.SelectNext(data);
            Assert.That(secondDecision.concept, Is.EqualTo(firstDecision.concept));
            Assert.That(secondDecision.activity, Is.EqualTo(firstDecision.activity));
            Assert.That(secondDecision.numberDifficulty, Is.EqualTo(firstDecision.numberDifficulty));
            Assert.That(secondDecision.localProblemId, Is.EqualTo(firstDecision.localProblemId));
        }

        [Test]
        public void AiGate_FuzzInvalidJsonAlwaysFallsBackAndValidLocalSelectionsRemainAccepted()
        {
            LocalActivityDescriptor[] descriptors = LocalProblemBank.GetDescriptors();
            for (int index = 0; index < descriptors.Length; index++)
            {
                LocalActivityDescriptor descriptor = descriptors[index];
                AdaptiveRecommendation matching = Recommendation(descriptor.concept, descriptor.difficulty);
                AiResolvedActivity accepted = AiSuggestionGate.ResolveOrFallback(
                    ValidJson(descriptor, "request_1"), "request_1", matching);
                Assert.That(accepted.usedAiSuggestion, Is.True, descriptor.id + ": " + accepted.rejectionCode);
                Assert.That(accepted.localProblem.id, Is.EqualTo(descriptor.id));
            }

            AdaptiveRecommendation recommendation = Recommendation(MathConcept.TargetNumber, 3);
            DeterministicRandom random = new DeterministicRandom(0x13579BDFu);
            char[] alphabet = { '{', '}', '[', ']', '"', '\\', ':', ',', '0', '1', '-', 'a', ' ', '\n', '\u0661', '\uD800', '한' };
            for (int sample = 0; sample < 256; sample++)
            {
                int length = random.NextInt(1, 160);
                StringBuilder malformed = new StringBuilder(length);
                for (int index = 0; index < length; index++)
                {
                    malformed.Append(alphabet[random.NextInt(0, alphabet.Length)]);
                }

                string payload = malformed.ToString();
                AiResolvedActivity first = AiSuggestionGate.ResolveOrFallback(payload, "request_1", recommendation);
                AiResolvedActivity second = AiSuggestionGate.ResolveOrFallback(payload, "request_1", recommendation);
                Assert.That(first, Is.Not.Null);
                Assert.That(first.usedAiSuggestion, Is.False, "Random malformed payload was unexpectedly accepted.");
                Assert.That(first.localProblem, Is.Not.Null);
                Assert.That(second.localProblem.id, Is.EqualTo(first.localProblem.id));
                Assert.That(second.rejectionCode, Is.EqualTo(first.rejectionCode));
            }

            LocalActivityDescriptor targetDescriptor = FindDescriptor(MathConcept.TargetNumber, 3);
            string nonAsciiInteger = ValidJson(targetDescriptor, "request_1")
                .Replace("\"schemaVersion\":1", "\"schemaVersion\":١");
            AiResolvedActivity rejected = AiSuggestionGate.ResolveOrFallback(
                nonAsciiInteger, "request_1", recommendation);
            Assert.That(rejected.usedAiSuggestion, Is.False);
            Assert.That(rejected.rejectionCode, Is.EqualTo("ai_value_type_not_allowed"));
        }

        private static AdaptiveRecommendation Recommendation(MathConcept concept, int difficulty)
        {
            return new AdaptiveRecommendation
            {
                concept = concept,
                numberDifficulty = difficulty,
                stepDifficulty = difficulty,
                scaffoldLevel = ScaffoldLevel.VisualCue,
                parentStatus = ParentConceptStatus.Practicing
            };
        }

        private static LocalActivityDescriptor FindDescriptor(MathConcept concept, int difficulty)
        {
            LocalActivityDescriptor[] descriptors = LocalProblemBank.GetDescriptors();
            for (int index = 0; index < descriptors.Length; index++)
            {
                if (descriptors[index].concept == concept && descriptors[index].difficulty == difficulty)
                {
                    return descriptors[index];
                }
            }

            Assert.Fail("Descriptor missing for " + concept + " d" + difficulty);
            return null;
        }

        private static string ValidJson(LocalActivityDescriptor descriptor, string requestId)
        {
            return "{" +
                   "\"schemaVersion\":1," +
                   "\"requestId\":\"" + requestId + "\"," +
                   "\"problemKind\":\"" + descriptor.problemKind + "\"," +
                   "\"conceptKey\":\"" + descriptor.conceptKey + "\"," +
                   "\"difficulty\":" + descriptor.difficulty + "," +
                   "\"sourceProblemId\":\"" + descriptor.id + "\"," +
                   "\"promptKo\":\"블록을 움직여 수학 퍼즐을 풀어 보세요.\"," +
                   "\"hintsKo\":[\"주변 규칙을 천천히 살펴보세요.\"]" +
                   "}";
        }

        private static void AssertTargetEqual(TargetNumberProblem first, TargetNumberProblem second)
        {
            Assert.That(second.id, Is.EqualTo(first.id));
            Assert.That(second.target, Is.EqualTo(first.target));
            Assert.That(second.difficulty, Is.EqualTo(first.difficulty));
            Assert.That(second.numberBlocks, Is.EqualTo(first.numberBlocks));
            Assert.That(second.operatorBlocks, Is.EqualTo(first.operatorBlocks));
            Assert.That(second.knownSolutionExpression, Is.EqualTo(first.knownSolutionExpression));
        }

        private static void AssertFairShareEqual(FairShareProblem first, FairShareProblem second)
        {
            Assert.That(second.id, Is.EqualTo(first.id));
            Assert.That(second.totalItems, Is.EqualTo(first.totalItems));
            Assert.That(second.recipientCount, Is.EqualTo(first.recipientCount));
            Assert.That(second.expectedEach, Is.EqualTo(first.expectedEach));
            Assert.That(second.expectedRemainder, Is.EqualTo(first.expectedRemainder));
            Assert.That(second.useRemainderTray, Is.EqualTo(first.useRemainderTray));
            Assert.That(second.promptKo, Is.EqualTo(first.promptKo));
        }

        private static void AssertPatternEqual(PatternSpaceProblem first, PatternSpaceProblem second)
        {
            Assert.That(second.id, Is.EqualTo(first.id));
            Assert.That(second.kind, Is.EqualTo(first.kind));
            Assert.That(second.concept, Is.EqualTo(first.concept));
            Assert.That(second.rows, Is.EqualTo(first.rows));
            Assert.That(second.columns, Is.EqualTo(first.columns));
            Assert.That(second.blankIndices, Is.EqualTo(first.blankIndices));
            AssertTilesEqual(first.boardTiles, second.boardTiles);
            AssertTilesEqual(first.expectedTiles, second.expectedTiles);
            AssertTilesEqual(first.choiceTiles, second.choiceTiles);
        }

        private static void AssertTilesEqual(PatternTile[] first, PatternTile[] second)
        {
            Assert.That(second.Length, Is.EqualTo(first.Length));
            for (int index = 0; index < first.Length; index++)
            {
                Assert.That(PatternSpaceValidator.TilesMatch(first[index], second[index]), Is.True,
                    "Tile mismatch at " + index);
            }
        }

        private static PatternTile[] CloneTiles(PatternTile[] tiles)
        {
            PatternTile[] clones = new PatternTile[tiles.Length];
            for (int index = 0; index < tiles.Length; index++)
            {
                clones[index] = tiles[index].Clone();
            }

            return clones;
        }

        private static void AssertRecommendationEqual(AdaptiveRecommendation first, AdaptiveRecommendation second)
        {
            Assert.That(second.concept, Is.EqualTo(first.concept));
            Assert.That(second.numberDifficulty, Is.EqualTo(first.numberDifficulty));
            Assert.That(second.stepDifficulty, Is.EqualTo(first.stepDifficulty));
            Assert.That(second.scaffoldLevel, Is.EqualTo(first.scaffoldLevel));
            Assert.That(second.parentStatus, Is.EqualTo(first.parentStatus));
            Assert.That(second.reasonCode, Is.EqualTo(first.reasonCode));
            Assert.That(second.evidenceCount, Is.EqualTo(first.evidenceCount));
        }
    }
}
