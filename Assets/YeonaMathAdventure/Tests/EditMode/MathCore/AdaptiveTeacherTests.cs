using NUnit.Framework;

namespace YeonaMathAdventure.MathCore.Tests
{
    public sealed class AdaptiveTeacherTests
    {
        [Test]
        public void StrongRecentPerformance_RaisesRangeAndStepsAndRemovesScaffold()
        {
            ConceptProgressData progress = new ConceptProgressData
            {
                concept = MathConcept.TargetNumber,
                currentNumberDifficulty = 2,
                currentStepDifficulty = 2,
                currentScaffoldLevel = ScaffoldLevel.GuidedSteps
            };

            for (int index = 0; index < 6; index++)
            {
                progress.AddAttempt(CreateAttempt(MathConcept.TargetNumber, true, 0, 0, 50000, 60000));
            }

            AdaptiveRecommendation recommendation = AdaptiveTeacher.Recommend(progress);

            Assert.That(recommendation.numberDifficulty, Is.EqualTo(3));
            Assert.That(recommendation.stepDifficulty, Is.EqualTo(3));
            Assert.That(recommendation.scaffoldLevel, Is.EqualTo(ScaffoldLevel.VisualCue));
            Assert.That(recommendation.parentStatus, Is.EqualTo(ParentConceptStatus.Familiar));
        }

        [Test]
        public void Struggle_KeepsNumberRangeAndAddsScaffold()
        {
            ConceptProgressData progress = new ConceptProgressData
            {
                concept = MathConcept.DivisionAndRemainder,
                currentNumberDifficulty = 4,
                currentStepDifficulty = 3,
                currentScaffoldLevel = ScaffoldLevel.VisualCue
            };

            for (int index = 0; index < 5; index++)
            {
                progress.AddAttempt(CreateAttempt(MathConcept.DivisionAndRemainder, index == 0, 3, 2, 150000, 60000));
            }

            AdaptiveRecommendation recommendation = AdaptiveTeacher.Recommend(progress);

            Assert.That(recommendation.numberDifficulty, Is.EqualTo(4), "Support must not automatically reset number range.");
            Assert.That(recommendation.stepDifficulty, Is.EqualTo(3));
            Assert.That(recommendation.scaffoldLevel, Is.EqualTo(ScaffoldLevel.GuidedSteps));
            Assert.That(recommendation.parentStatus, Is.EqualTo(ParentConceptStatus.NeedsHelp));
            Assert.That(recommendation.reasonCode, Is.EqualTo("add_visual_scaffold"));
        }

        [Test]
        public void ProgressRetainsOnlyMostRecentTenAttempts()
        {
            ConceptProgressData progress = new ConceptProgressData { concept = MathConcept.Rotation };
            for (int index = 0; index < 12; index++)
            {
                AttemptRecord attempt = CreateAttempt(MathConcept.Rotation, true, 0, 0, 30000, 60000);
                attempt.problemId = "problem-" + index;
                progress.AddAttempt(attempt);
            }

            Assert.That(progress.recentAttempts.Count, Is.EqualTo(10));
            Assert.That(progress.recentAttempts[0].problemId, Is.EqualTo("problem-2"));
            Assert.That(progress.recentAttempts[9].problemId, Is.EqualTo("problem-11"));
            Assert.That(progress.totalAttempts, Is.EqualTo(12));
        }

        [Test]
        public void NoEvidence_IsReportedAsPracticingWithoutChangingDifficulty()
        {
            ConceptProgressData progress = new ConceptProgressData
            {
                concept = MathConcept.Symmetry,
                currentNumberDifficulty = 3,
                currentStepDifficulty = 2,
                currentScaffoldLevel = ScaffoldLevel.VisualCue
            };

            AdaptiveRecommendation recommendation = AdaptiveTeacher.Recommend(progress);

            Assert.That(recommendation.numberDifficulty, Is.EqualTo(3));
            Assert.That(recommendation.stepDifficulty, Is.EqualTo(2));
            Assert.That(recommendation.parentStatus, Is.EqualTo(ParentConceptStatus.Practicing));
        }

        private static AttemptRecord CreateAttempt(
            MathConcept concept,
            bool successful,
            int retries,
            int hints,
            int elapsedMilliseconds,
            int expectedMilliseconds)
        {
            return new AttemptRecord
            {
                problemId = "test",
                concept = concept,
                successful = successful,
                retryCount = retries,
                hintsUsed = hints,
                elapsedMilliseconds = elapsedMilliseconds,
                expectedDurationMilliseconds = expectedMilliseconds,
                numberDifficulty = 2,
                stepDifficulty = 2,
                scaffoldLevel = ScaffoldLevel.VisualCue
            };
        }
    }
}
