using NUnit.Framework;

namespace YeonaMathAdventure.MathCore.Tests
{
    public sealed class MathTeacherAITests
    {
        [Test]
        public void SelectNext_PrioritizesConceptThatNeedsSupport()
        {
            MathProgressData progress = NewProgress();
            ConceptProgressData division = progress.GetOrCreateConcept(MathConcept.DivisionAndRemainder);
            for (int index = 0; index < 3; index++)
            {
                division.AddAttempt(Attempt(MathConcept.DivisionAndRemainder, false, 3, 2, 150000));
            }

            MathTeacherDecision decision = new MathTeacherAI().SelectNext(progress);

            Assert.That(decision.activity, Is.EqualTo(MathJourneyActivity.FairShare));
            Assert.That(decision.concept, Is.EqualTo(MathConcept.DivisionAndRemainder));
            Assert.That(decision.parentStatus, Is.EqualTo(ParentConceptStatus.NeedsHelp));
            Assert.That(decision.reasonCode, Is.EqualTo("support_with_scaffold"));
        }

        [Test]
        public void RecordThenSelect_AppliesStrongTransitionOnlyOnce()
        {
            MathProgressData progress = NewProgress();
            ConceptProgressData target = progress.GetOrCreateConcept(MathConcept.TargetNumber);
            target.currentNumberDifficulty = 2;
            target.currentStepDifficulty = 2;
            target.currentScaffoldLevel = ScaffoldLevel.GuidedSteps;
            for (int index = 0; index < 4; index++)
            {
                target.AddAttempt(Attempt(MathConcept.TargetNumber, true, 0, 0, 50000));
            }

            MathTeacherAI teacher = new MathTeacherAI();
            AdaptiveRecommendation applied = teacher.RecordAttemptAndAdapt(
                progress,
                Attempt(MathConcept.TargetNumber, true, 0, 0, 50000));

            Assert.That(applied.reasonCode, Is.EqualTo("raise_range_and_steps"));
            Assert.That(target.currentNumberDifficulty, Is.EqualTo(3));
            Assert.That(target.currentStepDifficulty, Is.EqualTo(3));
            Assert.That(target.currentScaffoldLevel, Is.EqualTo(ScaffoldLevel.VisualCue));

            MathTeacherDecision firstRead = teacher.SelectForActivity(progress, MathJourneyActivity.TargetNumber);
            MathTeacherDecision secondRead = teacher.SelectForActivity(progress, MathJourneyActivity.TargetNumber);
            Assert.That(firstRead.numberDifficulty, Is.EqualTo(3));
            Assert.That(secondRead.numberDifficulty, Is.EqualTo(3));
            Assert.That(target.currentNumberDifficulty, Is.EqualTo(3),
                "Reading a recommendation must not re-apply the same evidence.");
        }

        [Test]
        public void Struggle_KeepsNumberRangeAndAddsScaffoldThroughFacade()
        {
            MathProgressData progress = NewProgress();
            ConceptProgressData division = progress.GetOrCreateConcept(MathConcept.DivisionAndRemainder);
            division.currentNumberDifficulty = 4;
            division.currentStepDifficulty = 3;
            division.currentScaffoldLevel = ScaffoldLevel.VisualCue;
            division.AddAttempt(Attempt(MathConcept.DivisionAndRemainder, false, 3, 2, 150000));
            division.AddAttempt(Attempt(MathConcept.DivisionAndRemainder, false, 3, 2, 150000));

            MathTeacherAI teacher = new MathTeacherAI();
            teacher.RecordAttemptAndAdapt(
                progress,
                Attempt(MathConcept.DivisionAndRemainder, false, 3, 2, 150000));
            MathTeacherDecision decision = teacher.SelectForActivity(progress, MathJourneyActivity.FairShare);

            Assert.That(decision.numberDifficulty, Is.EqualTo(4));
            Assert.That(decision.stepDifficulty, Is.EqualTo(3));
            Assert.That(decision.scaffoldLevel, Is.EqualTo(ScaffoldLevel.GuidedSteps));
        }

        [Test]
        public void PatternSelection_ConsidersShapeAndSpatialConcepts()
        {
            MathProgressData progress = NewProgress();
            MathConcept[] practiced =
            {
                MathConcept.NumericPattern,
                MathConcept.Rotation,
                MathConcept.Symmetry,
                MathConcept.SpatialReasoning
            };
            for (int conceptIndex = 0; conceptIndex < practiced.Length; conceptIndex++)
            {
                ConceptProgressData data = progress.GetOrCreateConcept(practiced[conceptIndex]);
                for (int index = 0; index < 3; index++)
                {
                    data.AddAttempt(Attempt(practiced[conceptIndex], true, 0, 0, 60000));
                }
            }

            MathTeacherDecision decision = new MathTeacherAI().SelectForActivity(
                progress,
                MathJourneyActivity.PatternSpace);

            Assert.That(decision.activity, Is.EqualTo(MathJourneyActivity.PatternSpace));
            Assert.That(decision.concept, Is.EqualTo(MathConcept.ShapePattern));
            Assert.That(decision.localProblemId, Is.Not.Empty);
        }

        [Test]
        public void EmptyEvidence_TieRotatesWithMathJourneyNode()
        {
            MathProgressData progress = NewProgress();
            progress.journeyNodeIndex = 2;

            MathTeacherDecision decision = new MathTeacherAI().SelectNext(progress);

            Assert.That(decision.concept, Is.EqualTo(MathConcept.NumericPattern));
            Assert.That(decision.reasonCode, Is.EqualTo("introduce_unseen_concept"));
        }

        [Test]
        public void CompletedAfterRetry_CountsAsAttemptButNotFirstCheckSuccess()
        {
            MathProgressData progress = NewProgress();
            ConceptProgressData target = progress.GetOrCreateConcept(MathConcept.TargetNumber);

            new MathTeacherAI().RecordAttemptAndAdapt(
                progress,
                Attempt(MathConcept.TargetNumber, false, 1, 0, 70000));

            Assert.That(target.totalAttempts, Is.EqualTo(1));
            Assert.That(target.totalSuccessful, Is.Zero);
            Assert.That(target.recentAttempts[0].successful, Is.False);
            Assert.That(target.recentAttempts[0].retryCount, Is.EqualTo(1));
        }

        private static MathProgressData NewProgress()
        {
            return new MathProgressData { localProfileId = "teacher_test" };
        }

        private static AttemptRecord Attempt(
            MathConcept concept,
            bool successful,
            int retries,
            int hints,
            int elapsedMilliseconds)
        {
            return new AttemptRecord
            {
                problemId = "teacher-test-" + concept,
                concept = concept,
                successful = successful,
                elapsedMilliseconds = elapsedMilliseconds,
                expectedDurationMilliseconds = 60000,
                retryCount = retries,
                hintsUsed = hints,
                numberDifficulty = 2,
                stepDifficulty = 2,
                scaffoldLevel = ScaffoldLevel.VisualCue,
                completedAtUnixSeconds = 1
            };
        }
    }
}
