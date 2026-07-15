#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YeonaMathAdventure.Editor;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure.Tests
{
    public sealed class MathUiLayoutTests
    {
        [TestCase(1920, 1080)]
        [TestCase(2400, 1080)]
        [TestCase(2560, 1080)]
        [TestCase(1440, 1080)]
        public void ReferenceScale_KeepsLargeTouchTargetsReadable(int width, int height)
        {
            float physicalTouchHeight = MathUiKit.LargeTouchHeight *
                                        MathUiKit.ComputeReferenceScale(width, height);
            Assert.That(physicalTouchHeight, Is.GreaterThanOrEqualTo(88f));
        }

        [Test]
        public void CanvasFactory_UsesScaleWithScreenSizeForPhoneAndTabletRatios()
        {
            GameObject root = new GameObject("LayoutTestRoot");
            try
            {
                RectTransform safeRoot;
                RectTransform dragLayer;
                Canvas canvas = MathUiKit.CreateCanvas(root.transform, out safeRoot, out dragLayer);
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(1f).Within(0.001f));
                Assert.That(safeRoot.GetComponent<SafeAreaFitter>(), Is.Not.Null);
                Assert.That(dragLayer.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(dragLayer.anchorMax, Is.EqualTo(Vector2.one));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SafeAreaAnchors_ConvertLandscapeNotchToNormalizedInsets()
        {
            Vector2 minimum;
            Vector2 maximum;
            SafeAreaFitter.CalculateAnchors(new Rect(80f, 0f, 2240f, 1080f), 2400, 1080,
                out minimum, out maximum);
            Assert.That(minimum.x, Is.EqualTo(80f / 2400f).Within(0.0001f));
            Assert.That(minimum.y, Is.Zero.Within(0.0001f));
            Assert.That(maximum.x, Is.EqualTo(2320f / 2400f).Within(0.0001f));
            Assert.That(maximum.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void FirstRunAssessment_HasTwoRoundsPerMiniGame()
        {
            Assert.That(MathJourneyBootstrap.AssessmentRoundsPerGame, Is.EqualTo(2));
            Assert.That(MathJourneyBootstrap.AssessmentTotalRoundCount,
                Is.EqualTo(MathRuntimeMetaData.MaximumAssessmentRounds));
        }

        [Test]
        public void Placement_PerfectActualSixRoundSchedule_StartsAtLevelThree()
        {
            MathAssessmentOutcomeData[] outcomes = CreatePerfectAssessmentSchedule();
            int[] actualDifficulties = new int[outcomes.Length];
            for (int index = 0; index < outcomes.Length; index++)
            {
                actualDifficulties[index] = outcomes[index].difficulty;
            }

            Assert.That(actualDifficulties, Is.EqualTo(new[] { 1, 2, 1, 2, 1, 2 }));
            Assert.That(MathJourneyBootstrap.CalculatePlacementLevel(outcomes), Is.EqualTo(3));
        }

        [Test]
        public void FirstRunAssessment_OpensWithForestFeastAndAvoidsHardArithmetic()
        {
            Assert.That(MathJourneyBootstrap.AssessmentActivityForRound(0), Is.EqualTo(MathActivityKind.FairShare));
            Assert.That(MathJourneyBootstrap.AssessmentActivityForRound(1), Is.EqualTo(MathActivityKind.FairShare));
            Assert.That(MathJourneyBootstrap.AssessmentActivityForRound(2), Is.EqualTo(MathActivityKind.TargetNumber));
            Assert.That(MathJourneyBootstrap.AssessmentDifficultyForRound(0), Is.EqualTo(1));
            Assert.That(MathJourneyBootstrap.AssessmentDifficultyForRound(2), Is.EqualTo(1));
        }

        [Test]
        public void Placement_IntermediateSupport_StartsAtLevelTwo()
        {
            MathAssessmentOutcomeData[] outcomes = CreatePerfectAssessmentSchedule();
            outcomes[0].retries = 1;
            outcomes[1].hints = 1;
            outcomes[2].retries = 1;
            outcomes[2].hints = 1;
            outcomes[3].retries = 1;
            outcomes[4].hints = 1;
            outcomes[5].retries = 1;

            Assert.That(MathJourneyBootstrap.CalculatePlacementLevel(outcomes), Is.EqualTo(2));
        }

        [Test]
        public void Placement_ManyRetriesHintsAndSlowRounds_StartsAtLevelOne()
        {
            MathAssessmentOutcomeData[] outcomes = CreatePerfectAssessmentSchedule();
            for (int index = 0; index < outcomes.Length; index++)
            {
                outcomes[index].retries = 4;
                outcomes[index].hints = 2;
                outcomes[index].elapsedMilliseconds = 120001;
            }

            Assert.That(MathJourneyBootstrap.CalculatePlacementLevel(outcomes), Is.EqualTo(1));
        }

        [Test]
        public void Placement_EmptyEvidence_DefaultsToLevelTwo()
        {
            Assert.That(MathJourneyBootstrap.CalculatePlacementLevel(null), Is.EqualTo(2));
            Assert.That(MathJourneyBootstrap.CalculatePlacementLevel(
                new MathAssessmentOutcomeData[0]), Is.EqualTo(2));
        }

        [Test]
        public void Placement_SlowPerfectRounds_AreNotTreatedAsLevelThreeMastery()
        {
            MathAssessmentOutcomeData[] outcomes = CreatePerfectAssessmentSchedule();
            for (int index = 0; index < outcomes.Length; index++)
            {
                outcomes[index].elapsedMilliseconds = 120001;
            }

            Assert.That(MathJourneyBootstrap.CalculatePlacementLevel(outcomes), Is.EqualTo(2));
        }

        [Test]
        public void ScaffoldRouting_AssessmentUsesVisualCueAndTeacherDecisionIsPreserved()
        {
            MathTeacherDecision decision = new MathTeacherDecision
            {
                numberDifficulty = 3,
                scaffoldLevel = ScaffoldLevel.WorkedExample
            };

            Assert.That(MathJourneyBootstrap.ResolveSupportLevel(true, decision),
                Is.EqualTo(ScaffoldLevel.VisualCue));
            Assert.That(MathJourneyBootstrap.ResolveSupportLevel(false, decision),
                Is.EqualTo(ScaffoldLevel.WorkedExample));
            Assert.That(decision.numberDifficulty, Is.EqualTo(3),
                "Selecting visual support must not lower the number difficulty.");
        }

        [Test]
        public void TargetNumber_GuidedAndWorkedSupportGiveConcreteIntermediateStrategies()
        {
            TargetNumberProblem problem = new TargetNumberProblem
            {
                target = 64,
                numberBlocks = new[] { 99, 8, 4, 4 },
                knownSolutionExpression = "8 × (4 + 4)"
            };

            string guided = TargetNumberGameView.BuildSupportMessage(ScaffoldLevel.GuidedSteps, problem);
            string worked = TargetNumberGameView.BuildSupportMessage(ScaffoldLevel.WorkedExample, problem);
            StringAssert.Contains("1단계", guided);
            StringAssert.Contains("64", guided);
            StringAssert.Contains("숫자 하나", guided);
            StringAssert.DoesNotContain("99", guided,
                "A shuffled first block can be a distractor and must not be prescribed as the starting value.");
            StringAssert.Contains("따라 해 볼 예", worked);
            StringAssert.Contains(problem.knownSolutionExpression, worked);
        }

        [Test]
        public void FairShare_GuidedAndWorkedSupportGiveConcreteIntermediateStrategies()
        {
            FairShareProblem problem = new FairShareProblem
            {
                totalItems = 17,
                recipientCount = 4,
                expectedEach = 4,
                expectedRemainder = 1
            };

            string guided = FairShareGameView.BuildSupportMessage(ScaffoldLevel.GuidedSteps, problem);
            string worked = FairShareGameView.BuildSupportMessage(ScaffoldLevel.WorkedExample, problem);
            StringAssert.Contains("1단계", guided);
            StringAssert.Contains("4곳", guided);
            StringAssert.Contains("남은 1개", guided);
            StringAssert.Contains("17 = 4 × 4 + 1", worked);
            Assert.That(FairShareGameView.DetermineSupportDestination(
                new[] { 4, 3, 4, 4 }, 4, 2, true), Is.EqualTo(FairShareSupportDestination.Recipients));
            Assert.That(FairShareGameView.DetermineSupportDestination(
                new[] { 4, 4, 4, 4 }, 4, 1, true), Is.EqualTo(FairShareSupportDestination.Remainder));
            Assert.That(FairShareGameView.DetermineSupportDestination(
                new[] { 4, 4, 4, 4 }, 4, 0, true), Is.EqualTo(FairShareSupportDestination.None));
        }

        [Test]
        public void PatternSpace_GuidedAndWorkedSupportGiveConcreteIntermediateStrategies()
        {
            PatternSpaceProblem problem = new PatternSpaceProblem
            {
                kind = PatternPuzzleKind.NumericSequence,
                rows = 1,
                columns = 5,
                blankIndices = new[] { 1, 3 },
                boardTiles = new[]
                {
                    new PatternTile { shapeId = "number", value = 10 },
                    new PatternTile { shapeId = "number", value = 15 },
                    new PatternTile { shapeId = "number", value = 20 },
                    new PatternTile { shapeId = "number", value = 25 },
                    new PatternTile { shapeId = "number", value = 30 }
                }
            };

            string guided = PatternSpaceGameView.BuildSupportMessage(ScaffoldLevel.GuidedSteps, problem);
            string worked = PatternSpaceGameView.BuildSupportMessage(ScaffoldLevel.WorkedExample, problem);
            StringAssert.Contains("1단계", guided);
            StringAssert.Contains("5씩", guided);
            StringAssert.Contains("한 칸 변화 5", worked);

            var rotation = new PatternSpaceProblem { kind = PatternPuzzleKind.Rotation };
            string rotationVisual = PatternSpaceGameView.BuildSupportMessage(ScaffoldLevel.VisualCue, rotation);
            string rotationGuided = PatternSpaceGameView.BuildSupportMessage(ScaffoldLevel.GuidedSteps, rotation);
            StringAssert.Contains("90도", rotationVisual);
            StringAssert.Contains("맞을 때", rotationGuided);
            StringAssert.DoesNotContain("한 번", rotationVisual);
            StringAssert.DoesNotContain("한 번", rotationGuided);

            var spatial = new PatternSpaceProblem { kind = PatternPuzzleKind.SpatialFill };
            string spatialVisual = PatternSpaceGameView.BuildSupportMessage(ScaffoldLevel.VisualCue, spatial);
            string spatialGuided = PatternSpaceGameView.BuildSupportMessage(ScaffoldLevel.GuidedSteps, spatial);
            StringAssert.Contains("선의 방향", spatialVisual);
            StringAssert.DoesNotContain("반복", spatialVisual);
            StringAssert.Contains("닿는 선", spatialGuided);
            Assert.That(PatternSpaceGameView.UsesPersistentBlankCue(ScaffoldLevel.VisualCue), Is.True);
            Assert.That(PatternSpaceGameView.UsesPersistentBlankCue(ScaffoldLevel.None), Is.False);
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(3, false)]
        public void CompletionEvidence_RecordsWhetherFirstCheckWasCorrect(int retryCount, bool expected)
        {
            Assert.That(MathJourneyBootstrap.WasCorrectOnFirstCheck(retryCount), Is.EqualTo(expected));
        }

        [Test]
        public void KoreanDisplayTitle_UsesLocalChildDisplayName()
        {
            Assert.That(MathJourneyBootstrap.ChildDisplayNameKo, Is.EqualTo("연아"));
            Assert.That(MathJourneyBootstrap.KoreanDisplayTitle, Is.EqualTo("연아의 별다리 모험"));
        }

        [Test]
        public void AllMiniGames_FitInsideBodyPreferredHeightWithoutFlexibleSurplus()
        {
            Assert.That(MathGameLayoutBudget.TargetPreferredContentHeight,
                Is.LessThanOrEqualTo(MathGameLayoutBudget.BodyPreferredHeight),
                "Target Number fixed preferred heights would clip at the 4:3 baseline.");
            Assert.That(MathGameLayoutBudget.FairPreferredContentHeight,
                Is.LessThanOrEqualTo(MathGameLayoutBudget.BodyPreferredHeight),
                "Fair Share fixed preferred heights would clip at the 4:3 baseline.");
            Assert.That(MathGameLayoutBudget.PatternPreferredContentHeight,
                Is.LessThanOrEqualTo(MathGameLayoutBudget.BodyPreferredHeight),
                "Pattern/Space fixed preferred heights would clip at the 4:3 baseline.");

            Assert.That(MathGameLayoutBudget.TargetPreferredContentHeight, Is.EqualTo(652f));
            Assert.That(MathGameLayoutBudget.FairPreferredContentHeight, Is.EqualTo(644f));
            Assert.That(MathGameLayoutBudget.PatternPreferredContentHeight, Is.EqualTo(647f));
        }

        [Test]
        public void UltraWideLandscape_HasEnoughActualBodyHeightForEveryMiniGame()
        {
            float bodyAtTwentyByNine = MathGameLayoutBudget.AvailableBodyHeight(2400, 1080);
            float bodyAtUltraWide = MathGameLayoutBudget.AvailableBodyHeight(2560, 1080);
            float minimumBody = Mathf.Min(bodyAtTwentyByNine, bodyAtUltraWide);

            Assert.That(bodyAtTwentyByNine, Is.EqualTo(798f).Within(0.1f));
            Assert.That(bodyAtUltraWide, Is.EqualTo(798f).Within(0.1f));
            Assert.That(MathGameLayoutBudget.TargetPreferredContentHeight, Is.LessThanOrEqualTo(minimumBody));
            Assert.That(MathGameLayoutBudget.FairPreferredContentHeight, Is.LessThanOrEqualTo(minimumBody));
            Assert.That(MathGameLayoutBudget.PatternPreferredContentHeight, Is.LessThanOrEqualTo(minimumBody));
        }

        [Test]
        public void ConservativeLandscapeSafeArea_StillFitsGamesAndTopLevelScreens()
        {
            const int conservativeSafeHeight = 960;
            float gameBody = MathGameLayoutBudget.AvailableBodyHeightForSafeArea(
                2560, 1080, conservativeSafeHeight);
            Assert.That(gameBody, Is.EqualTo(678f).Within(0.1f));
            Assert.That(MathGameLayoutBudget.TargetPreferredContentHeight, Is.LessThanOrEqualTo(gameBody));
            Assert.That(MathGameLayoutBudget.FairPreferredContentHeight, Is.LessThanOrEqualTo(gameBody));
            Assert.That(MathGameLayoutBudget.PatternPreferredContentHeight, Is.LessThanOrEqualTo(gameBody));

            const float startPreferredHeight = 848f;
            const float placementPreferredHeight = 936f;
            const float homeMinimumReadableHeight = 940f;
            Assert.That(startPreferredHeight, Is.LessThanOrEqualTo(conservativeSafeHeight));
            Assert.That(placementPreferredHeight, Is.LessThanOrEqualTo(conservativeSafeHeight));
            Assert.That(homeMinimumReadableHeight, Is.LessThanOrEqualTo(conservativeSafeHeight));
        }

        [Test]
        public void FourByThreeHeightMatch_KeepsWidestGameBoardsInsideBody()
        {
            float width = MathGameLayoutBudget.AvailableGameContentWidth(1440, 1080, 56f);
            const float targetBlockAreaWidth = 1208f;
            const float widestPatternBoardWidth = 1232f;
            Assert.That(width, Is.EqualTo(1300f).Within(0.1f));
            Assert.That(targetBlockAreaWidth, Is.LessThanOrEqualTo(width));
            Assert.That(widestPatternBoardWidth, Is.LessThanOrEqualTo(width));
        }

        [Test]
        public void AssessmentResumeMetadata_RoundTripsCompletedRounds()
        {
            MathRuntimeMetaData source = new MathRuntimeMetaData
            {
                assessmentInProgress = true,
                assessmentNextRound = 2
            };
            source.assessmentOutcomes.Add(new MathAssessmentOutcomeData
            {
                activity = MathActivityKind.TargetNumber,
                difficulty = 2,
                elapsedMilliseconds = 42000,
                retries = 0,
                hints = 1
            });
            source.assessmentOutcomes.Add(new MathAssessmentOutcomeData
            {
                activity = MathActivityKind.TargetNumber,
                difficulty = 3,
                elapsedMilliseconds = 51000,
                retries = 1,
                hints = 0
            });

            MathRuntimeMetaData decoded = JsonUtility.FromJson<MathRuntimeMetaData>(JsonUtility.ToJson(source));
            decoded.Normalize();

            Assert.That(decoded.assessmentInProgress, Is.True);
            Assert.That(decoded.assessmentNextRound, Is.EqualTo(2));
            Assert.That(decoded.assessmentOutcomes.Count, Is.EqualTo(2));
            Assert.That(decoded.assessmentOutcomes[1].retries, Is.EqualTo(1));
        }

        [Test]
        public void AtomicTextFile_RecoversCompleteTemporaryPayloadWhenPrimaryIsMissing()
        {
            string directory = Path.Combine(Path.GetTempPath(), "yeona-math-ui-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "progress.json");
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(path + ".tmp", "resume-payload", new UTF8Encoding(false));
                string recovered;
                Assert.That(MathAtomicTextFile.TryRead(path, out recovered), Is.True);
                Assert.That(recovered, Is.EqualTo("resume-payload"));

                MathAtomicTextFile.Write(path, "committed-payload");
                Assert.That(MathAtomicTextFile.TryRead(path, out recovered), Is.True);
                Assert.That(recovered, Is.EqualTo("committed-payload"));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void NumericPatternHint_ComputesPerCellStepAcrossHiddenCells()
        {
            PatternSpaceProblem problem = new PatternSpaceProblem
            {
                rows = 1,
                columns = 5,
                blankIndices = new[] { 1, 3 },
                boardTiles = new[]
                {
                    new PatternTile { shapeId = "number", value = 10 },
                    new PatternTile { shapeId = "number", value = 15 },
                    new PatternTile { shapeId = "number", value = 20 },
                    new PatternTile { shapeId = "number", value = 25 },
                    new PatternTile { shapeId = "number", value = 30 }
                }
            };

            int step;
            Assert.That(PatternSpaceGameView.TryComputeVisibleNumericStep(problem, out step), Is.True);
            Assert.That(step, Is.EqualTo(5));
        }

        [Test]
        public void NumericPatternTile_CannotGainInvisibleRotationOnBlankTap()
        {
            PatternTile number = new PatternTile
            {
                shapeId = "number",
                value = 24,
                rotationQuarterTurns = 0
            };
            Assert.That(PatternSpaceGameView.CanRotatePlacedTile(
                PatternPuzzleKind.NumericSequence, number), Is.False);

            PatternTile arrow = new PatternTile { shapeId = "arrow", rotationQuarterTurns = 0 };
            Assert.That(PatternSpaceGameView.CanRotatePlacedTile(
                PatternPuzzleKind.Rotation, arrow), Is.True);
            Assert.That(PatternSpaceGameView.CanRotatePlacedTile(
                PatternPuzzleKind.Symmetry, arrow), Is.False);
            Assert.That(PatternSpaceGameView.CanRotatePlacedTile(
                PatternPuzzleKind.SpatialFill, arrow), Is.True);
        }

        [TestCase(1, "(", false, false)]
        [TestCase(1, "+", true, false)]
        [TestCase(1, "12", false, true)]
        [TestCase(0, "12", false, false)]
        public void ClosingParenthesis_RequiresCompletedContent(
            int unmatchedOpens,
            string previousToken,
            bool previousIsOperator,
            bool expected)
        {
            Assert.That(TargetNumberGameView.CanAppendClosingParenthesis(
                unmatchedOpens, previousToken, previousIsOperator), Is.EqualTo(expected));
        }

        [Test]
        public void TargetNumberAutoValidation_WaitsForRequiredBlocksThenTreatsCompleteExpressionAsAttempt()
        {
            TargetNumberProblem problem = new TargetNumberProblem
            {
                target = 48,
                numberBlocks = new[] { 6, 8, 1 },
                operatorBlocks = new[] { ArithmeticOperator.Multiply, ArithmeticOperator.Add },
                minimumNumbersUsed = 3,
                maximumNumbersUsed = 3
            };

            ExpressionEvaluation early;
            string error;
            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("6 × 8", out early, out error), Is.True);
            Assert.That(early.value, Is.EqualTo(RationalNumber.FromInteger(48)));
            Assert.That(TargetNumberValidator.Validate(problem, "6 × 8").IsSolved, Is.False);
            Assert.That(TargetNumberGameView.ShouldScheduleAutomaticValidation(problem, early), Is.False);

            ExpressionEvaluation complete;
            Assert.That(ArithmeticExpressionEvaluator.TryEvaluate("6 × 8 + 1", out complete, out error), Is.True);
            Assert.That(TargetNumberGameView.ShouldScheduleAutomaticValidation(problem, complete), Is.True);
            Assert.That(TargetNumberValidator.Validate(problem, "6 × 8 + 1").IsSolved, Is.False);
        }

        [Test]
        public void ExpandedHitTarget_AndPointerDownProvideImmediateLargeTouchResponse()
        {
            GameObject root = new GameObject("HitTargetTest", typeof(RectTransform));
            try
            {
                Button button = MathUiKit.CreateButton(root.transform, "Small", "?", Color.white, Color.black,
                    null, 76f, 64f, 24f);
                MathUiKit.ExpandHitTarget(button, 16f);
                RectTransform expanded = button.transform.Find("ExpandedHitTarget") as RectTransform;
                Assert.That(expanded, Is.Not.Null);
                Assert.That(expanded.offsetMin, Is.EqualTo(new Vector2(-16f, -16f)));
                Assert.That(expanded.offsetMax, Is.EqualTo(new Vector2(16f, 16f)));
                Assert.That(expanded.GetComponent<Image>().raycastTarget, Is.True);

                TouchDragItem item = button.gameObject.AddComponent<TouchDragItem>();
                bool pressed = false;
                item.pressed = delegate { pressed = true; };
                item.OnPointerDown(null);
                Assert.That(pressed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProgressJson_RoundTripsRecentLearningEvidence()
        {
            MathProgressData source = new MathProgressData { localProfileId = "yeona_local", rewardTokens = 7 };
            ConceptProgressData concept = source.GetOrCreateConcept(MathConcept.TargetNumber);
            concept.AddAttempt(new AttemptRecord
            {
                problemId = "tn-layout-test",
                concept = MathConcept.TargetNumber,
                successful = true,
                elapsedMilliseconds = 42000,
                expectedDurationMilliseconds = 90000,
                retryCount = 1,
                hintsUsed = 0,
                numberDifficulty = 2,
                stepDifficulty = 2,
                scaffoldLevel = ScaffoldLevel.VisualCue
            });

            UnityJsonProgressCodec codec = new UnityJsonProgressCodec();
            MathProgressData decoded;
            Assert.That(codec.TryDecode(codec.Encode(source), out decoded), Is.True);
            Assert.That(decoded.rewardTokens, Is.EqualTo(7));
            Assert.That(decoded.GetOrCreateConcept(MathConcept.TargetNumber).recentAttempts.Count, Is.EqualTo(1));
            Assert.That(decoded.GetOrCreateConcept(MathConcept.TargetNumber).recentAttempts[0].retryCount,
                Is.EqualTo(1));
        }

        [Test]
        public void AndroidManifestSanitizer_RemovesLegacyStorageWithoutDroppingExistingToolsReplace()
        {
            string directory = Path.Combine(Path.GetTempPath(), "yeona-manifest-" + Guid.NewGuid().ToString("N"));
            string manifestPath = Path.Combine(directory, "AndroidManifest.xml");
            Directory.CreateDirectory(directory);
            try
            {
                const string source =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" " +
                    "xmlns:tools=\"http://schemas.android.com/tools\" package=\"com.yeona.mathadventure\">" +
                    "<uses-permission android:name=\"android.permission.READ_EXTERNAL_STORAGE\"/>" +
                    "<uses-permission android:name=\"android.permission.WRITE_EXTERNAL_STORAGE\"/>" +
                    "<application android:requestLegacyExternalStorage=\"true\" " +
                    "tools:replace=\"android:allowBackup,android:requestLegacyExternalStorage\"/>" +
                    "</manifest>";
                File.WriteAllText(manifestPath, source, new UTF8Encoding(false));

                YeonaAndroidManifestSanitizer.SanitizeGeneratedManifest(manifestPath);

                byte[] bytes = File.ReadAllBytes(manifestPath);
                Assert.That(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), Is.False,
                    "The generated manifest must stay UTF-8 without a BOM.");

                var document = new XmlDocument();
                document.Load(manifestPath);
                var namespaces = new XmlNamespaceManager(document.NameTable);
                namespaces.AddNamespace("android", "http://schemas.android.com/apk/res/android");
                namespaces.AddNamespace("tools", "http://schemas.android.com/tools");

                XmlNodeList permissions = document.SelectNodes("/manifest/uses-permission", namespaces);
                Assert.That(permissions, Is.Not.Null);
                Assert.That(permissions.Count, Is.EqualTo(2));
                foreach (XmlElement permission in permissions.OfType<XmlElement>())
                {
                    Assert.That(permission.GetAttribute("node", "http://schemas.android.com/tools"),
                        Is.EqualTo("remove"));
                }

                XmlElement application = document.SelectSingleNode("/manifest/application", namespaces) as XmlElement;
                Assert.That(application, Is.Not.Null);
                Assert.That(application.GetAttribute("requestLegacyExternalStorage",
                    "http://schemas.android.com/apk/res/android"), Is.EqualTo("false"));

                string[] replacements = application.GetAttribute("replace", "http://schemas.android.com/tools")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .ToArray();
                Assert.That(replacements, Does.Contain("android:allowBackup"));
                Assert.That(replacements.Count(value => value == "android:requestLegacyExternalStorage"),
                    Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static MathAssessmentOutcomeData[] CreatePerfectAssessmentSchedule()
        {
            var outcomes = new MathAssessmentOutcomeData[MathJourneyBootstrap.AssessmentTotalRoundCount];
            for (int index = 0; index < outcomes.Length; index++)
            {
                outcomes[index] = new MathAssessmentOutcomeData
                {
                    activity = MathJourneyBootstrap.AssessmentActivityForRound(index),
                    difficulty = MathJourneyBootstrap.AssessmentDifficultyForRound(index),
                    elapsedMilliseconds = 60000,
                    retries = 0,
                    hints = 0
                };
            }

            return outcomes;
        }
    }
}
#endif
