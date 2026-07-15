using System;
using System.Collections.Generic;

namespace YeonaMathAdventure.MathCore
{
    /// <summary>
    /// Math Journey activity families. These are deliberately separate from Antura's
    /// language-focused MiniGameCode and PlaySessionData models.
    /// </summary>
    public enum MathJourneyActivity
    {
        TargetNumber = 0,
        FairShare = 1,
        PatternSpace = 2
    }

    [Serializable]
    public sealed class MathTeacherDecision
    {
        public MathJourneyActivity activity;
        public MathConcept concept;
        public int numberDifficulty;
        public int stepDifficulty;
        public ScaffoldLevel scaffoldLevel;
        public ParentConceptStatus parentStatus;
        public int evidenceCount;
        public string localProblemId;
        public string reasonCode;
    }

    /// <summary>
    /// Math-specific facade for the role played by Antura's TeacherAI: choose what to play next
    /// and which difficulty/scaffold to use from recent performance. It never reads or mutates
    /// Antura's language journey, MiniGameCode, vocabulary QuestionPack, or language score DB.
    /// </summary>
    public sealed class MathTeacherAI
    {
        private static readonly MathConcept[] AllConcepts =
        {
            MathConcept.TargetNumber,
            MathConcept.DivisionAndRemainder,
            MathConcept.NumericPattern,
            MathConcept.ShapePattern,
            MathConcept.Rotation,
            MathConcept.Symmetry,
            MathConcept.SpatialReasoning
        };

        private static readonly MathConcept[] TargetNumberConcepts =
        {
            MathConcept.TargetNumber
        };

        private static readonly MathConcept[] FairShareConcepts =
        {
            MathConcept.DivisionAndRemainder
        };

        private static readonly MathConcept[] PatternSpaceConcepts =
        {
            MathConcept.NumericPattern,
            MathConcept.ShapePattern,
            MathConcept.Rotation,
            MathConcept.Symmetry,
            MathConcept.SpatialReasoning
        };

        /// <summary>
        /// Selects the next Math Journey activity across every playable concept. The operation is
        /// read-only with respect to difficulty: repeated UI refreshes cannot raise difficulty.
        /// </summary>
        public MathTeacherDecision SelectNext(MathProgressData progress)
        {
            EnsureProgress(progress);
            return SelectBest(progress, AllConcepts, progress.journeyNodeIndex);
        }

        /// <summary>
        /// Selects a concept and current settings inside an activity chosen by the child or flow.
        /// PatternSpace considers all five implemented pattern/spatial concepts.
        /// </summary>
        public MathTeacherDecision SelectForActivity(MathProgressData progress, MathJourneyActivity activity)
        {
            EnsureProgress(progress);
            MathConcept[] candidates;
            switch (activity)
            {
                case MathJourneyActivity.TargetNumber:
                    candidates = TargetNumberConcepts;
                    break;
                case MathJourneyActivity.FairShare:
                    candidates = FairShareConcepts;
                    break;
                case MathJourneyActivity.PatternSpace:
                    candidates = PatternSpaceConcepts;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("activity");
            }

            return SelectBest(progress, candidates, progress.journeyNodeIndex);
        }

        /// <summary>
        /// Records one completed problem and applies exactly one adaptive transition. Callers
        /// should use this method instead of separately calling AddAttempt, Recommend and Apply.
        /// </summary>
        public AdaptiveRecommendation RecordAttemptAndAdapt(MathProgressData progress, AttemptRecord attempt)
        {
            EnsureProgress(progress);
            if (attempt == null)
            {
                throw new ArgumentNullException("attempt");
            }

            ConceptProgressData conceptProgress = progress.GetOrCreateConcept(attempt.concept);
            attempt.numberDifficulty = DifficultyRules.Clamp(attempt.numberDifficulty);
            attempt.stepDifficulty = DifficultyRules.Clamp(attempt.stepDifficulty);
            attempt.scaffoldLevel = DifficultyRules.ClampScaffold((int)attempt.scaffoldLevel);
            conceptProgress.AddAttempt(attempt);

            AdaptiveRecommendation recommendation = AdaptiveTeacher.Recommend(conceptProgress);
            AdaptiveTeacher.Apply(conceptProgress, recommendation);
            return recommendation;
        }

        private static MathTeacherDecision SelectBest(
            MathProgressData progress,
            MathConcept[] candidates,
            int rotationSeed)
        {
            if (candidates == null || candidates.Length == 0)
            {
                throw new ArgumentException("At least one math concept is required.", "candidates");
            }

            int start = PositiveModulo(rotationSeed, candidates.Length);
            MathTeacherDecision best = null;
            long bestPriority = long.MaxValue;

            for (int offset = 0; offset < candidates.Length; offset++)
            {
                MathConcept concept = candidates[(start + offset) % candidates.Length];
                ConceptProgressData conceptProgress = progress.GetOrCreateConcept(concept);
                AdaptiveRecommendation status = AdaptiveTeacher.Recommend(conceptProgress);
                long priority = CalculatePriority(conceptProgress, status);
                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    best = CreateDecision(conceptProgress, status);
                }
            }

            return best;
        }

        private static long CalculatePriority(
            ConceptProgressData progress,
            AdaptiveRecommendation status)
        {
            long statusBand;
            switch (status.parentStatus)
            {
                case ParentConceptStatus.NeedsHelp:
                    statusBand = 0L;
                    break;
                case ParentConceptStatus.Practicing:
                    statusBand = 100000L;
                    break;
                default:
                    statusBand = 200000L;
                    break;
            }

            // Within the same status, prefer concepts with less recent evidence. Lifetime
            // successes are only a deterministic final tie-breaker and cannot outweigh status.
            return statusBand
                   + Math.Max(0, status.evidenceCount) * 1000L
                   + Math.Min(999, Math.Max(0, progress.totalSuccessful));
        }

        private static MathTeacherDecision CreateDecision(
            ConceptProgressData progress,
            AdaptiveRecommendation status)
        {
            LocalActivityDescriptor local = LocalProblemBank.SelectFallback(
                progress.concept,
                progress.currentNumberDifficulty);

            return new MathTeacherDecision
            {
                activity = ActivityForConcept(progress.concept),
                concept = progress.concept,
                numberDifficulty = DifficultyRules.Clamp(progress.currentNumberDifficulty),
                stepDifficulty = DifficultyRules.Clamp(progress.currentStepDifficulty),
                scaffoldLevel = DifficultyRules.ClampScaffold((int)progress.currentScaffoldLevel),
                parentStatus = status.parentStatus,
                evidenceCount = status.evidenceCount,
                localProblemId = local == null ? string.Empty : local.id,
                reasonCode = SelectionReason(status)
            };
        }

        private static string SelectionReason(AdaptiveRecommendation status)
        {
            if (status.parentStatus == ParentConceptStatus.NeedsHelp)
            {
                return "support_with_scaffold";
            }

            if (status.evidenceCount == 0)
            {
                return "introduce_unseen_concept";
            }

            if (status.parentStatus == ParentConceptStatus.Familiar)
            {
                return "spiral_familiar_concept";
            }

            return "continue_balanced_practice";
        }

        private static MathJourneyActivity ActivityForConcept(MathConcept concept)
        {
            switch (concept)
            {
                case MathConcept.TargetNumber:
                    return MathJourneyActivity.TargetNumber;
                case MathConcept.DivisionAndRemainder:
                    return MathJourneyActivity.FairShare;
                case MathConcept.NumericPattern:
                case MathConcept.ShapePattern:
                case MathConcept.Rotation:
                case MathConcept.Symmetry:
                case MathConcept.SpatialReasoning:
                    return MathJourneyActivity.PatternSpace;
                default:
                    throw new ArgumentOutOfRangeException("concept");
            }
        }

        private static void EnsureProgress(MathProgressData progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            for (int index = 0; index < AllConcepts.Length; index++)
            {
                progress.GetOrCreateConcept(AllConcepts[index]);
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
