using System;
using System.Collections.Generic;

namespace YeonaMathAdventure.MathCore
{
    [Serializable]
    public sealed class AttemptRecord
    {
        public string problemId;
        public MathConcept concept;
        // True means the first explicit answer check solved the puzzle. A child can complete a
        // round after retries while this remains false; retry/hint/time fields preserve the rest.
        public bool successful;
        public int elapsedMilliseconds;
        public int expectedDurationMilliseconds;
        public int retryCount;
        public int hintsUsed;
        public int numberDifficulty;
        public int stepDifficulty;
        public ScaffoldLevel scaffoldLevel;
        public long completedAtUnixSeconds;
    }

    [Serializable]
    public sealed class ConceptProgressData
    {
        public MathConcept concept;
        public int currentNumberDifficulty = DifficultyRules.Minimum;
        public int currentStepDifficulty = DifficultyRules.Minimum;
        public ScaffoldLevel currentScaffoldLevel = ScaffoldLevel.VisualCue;
        public int totalAttempts;
        public int totalSuccessful;
        public List<AttemptRecord> recentAttempts = new List<AttemptRecord>();

        public void AddAttempt(AttemptRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            if (record.concept != concept)
            {
                throw new ArgumentException("Attempt concept does not match progress concept.", "record");
            }

            if (recentAttempts == null)
            {
                recentAttempts = new List<AttemptRecord>();
            }

            recentAttempts.Add(record);
            while (recentAttempts.Count > AdaptiveTeacher.RecentAttemptLimit)
            {
                recentAttempts.RemoveAt(0);
            }

            totalAttempts++;
            if (record.successful)
            {
                totalSuccessful++;
            }
        }

        public void Normalize()
        {
            currentNumberDifficulty = DifficultyRules.Clamp(currentNumberDifficulty);
            currentStepDifficulty = DifficultyRules.Clamp(currentStepDifficulty);
            currentScaffoldLevel = DifficultyRules.ClampScaffold((int)currentScaffoldLevel);
            if (recentAttempts == null)
            {
                recentAttempts = new List<AttemptRecord>();
            }

            for (int index = recentAttempts.Count - 1; index >= 0; index--)
            {
                AttemptRecord record = recentAttempts[index];
                if (record == null || record.concept != concept)
                {
                    recentAttempts.RemoveAt(index);
                }
            }

            while (recentAttempts.Count > AdaptiveTeacher.RecentAttemptLimit)
            {
                recentAttempts.RemoveAt(0);
            }

            if (totalAttempts < recentAttempts.Count)
            {
                totalAttempts = recentAttempts.Count;
            }

            if (totalSuccessful < 0)
            {
                totalSuccessful = 0;
            }

            if (totalSuccessful > totalAttempts)
            {
                totalSuccessful = totalAttempts;
            }
        }
    }

    [Serializable]
    public sealed class MathProgressData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string localProfileId;
        public int journeyNodeIndex;
        public int rewardTokens;
        public List<ConceptProgressData> concepts = new List<ConceptProgressData>();

        public ConceptProgressData GetOrCreateConcept(MathConcept concept)
        {
            if (concepts == null)
            {
                concepts = new List<ConceptProgressData>();
            }

            for (int index = 0; index < concepts.Count; index++)
            {
                if (concepts[index] != null && concepts[index].concept == concept)
                {
                    concepts[index].Normalize();
                    return concepts[index];
                }
            }

            ConceptProgressData created = new ConceptProgressData { concept = concept };
            concepts.Add(created);
            return created;
        }

        public void Normalize(string fallbackProfileId)
        {
            schemaVersion = CurrentSchemaVersion;
            if (string.IsNullOrEmpty(localProfileId))
            {
                localProfileId = fallbackProfileId;
            }

            if (journeyNodeIndex < 0)
            {
                journeyNodeIndex = 0;
            }

            if (rewardTokens < 0)
            {
                rewardTokens = 0;
            }

            if (concepts == null)
            {
                concepts = new List<ConceptProgressData>();
            }

            for (int index = concepts.Count - 1; index >= 0; index--)
            {
                if (concepts[index] == null)
                {
                    concepts.RemoveAt(index);
                }
                else
                {
                    concepts[index].Normalize();
                }
            }
        }
    }

    [Serializable]
    public sealed class AdaptiveRecommendation
    {
        public MathConcept concept;
        public int numberDifficulty;
        public int stepDifficulty;
        public ScaffoldLevel scaffoldLevel;
        public ParentConceptStatus parentStatus;
        public string reasonCode;
        public int evidenceCount;
    }

    public static class AdaptiveTeacher
    {
        public const int RecentAttemptLimit = 10;

        public static AdaptiveRecommendation Recommend(ConceptProgressData progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            progress.Normalize();
            int evidenceCount = progress.recentAttempts.Count;
            Metrics metrics = CalculateMetrics(progress.recentAttempts);

            int numberDifficulty = progress.currentNumberDifficulty;
            int stepDifficulty = progress.currentStepDifficulty;
            ScaffoldLevel scaffold = progress.currentScaffoldLevel;
            ParentConceptStatus status = ParentConceptStatus.Practicing;
            string reasonCode = "keep_practicing";

            bool strong = evidenceCount >= 5 &&
                          metrics.successRate >= 0.80 &&
                          metrics.averageRetries <= 0.50 &&
                          metrics.averageHints <= 0.30 &&
                          metrics.averageTimeRatio <= 1.20;

            bool needsSupport = evidenceCount >= 3 &&
                                (metrics.successRate < 0.60 ||
                                 metrics.averageRetries >= 2.0 ||
                                 metrics.averageHints >= 1.5 ||
                                 metrics.averageTimeRatio >= 2.0);

            if (strong)
            {
                numberDifficulty = DifficultyRules.Clamp(numberDifficulty + 1);
                stepDifficulty = DifficultyRules.Clamp(stepDifficulty + 1);
                scaffold = DifficultyRules.ClampScaffold((int)scaffold - 1);
                status = ParentConceptStatus.Familiar;
                reasonCode = "raise_range_and_steps";
            }
            else if (needsSupport)
            {
                // Keep the mathematical range stable. Add visual and intermediate scaffolds first,
                // instead of sending the child back to an artificially easy number range.
                scaffold = DifficultyRules.ClampScaffold((int)scaffold + 1);
                status = ParentConceptStatus.NeedsHelp;
                reasonCode = "add_visual_scaffold";
            }

            return new AdaptiveRecommendation
            {
                concept = progress.concept,
                numberDifficulty = numberDifficulty,
                stepDifficulty = stepDifficulty,
                scaffoldLevel = scaffold,
                parentStatus = status,
                reasonCode = reasonCode,
                evidenceCount = evidenceCount
            };
        }

        public static void Apply(ConceptProgressData progress, AdaptiveRecommendation recommendation)
        {
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            if (recommendation == null || recommendation.concept != progress.concept)
            {
                throw new ArgumentException("Recommendation does not match progress concept.", "recommendation");
            }

            progress.currentNumberDifficulty = DifficultyRules.Clamp(recommendation.numberDifficulty);
            progress.currentStepDifficulty = DifficultyRules.Clamp(recommendation.stepDifficulty);
            progress.currentScaffoldLevel = DifficultyRules.ClampScaffold((int)recommendation.scaffoldLevel);
        }

        private static Metrics CalculateMetrics(List<AttemptRecord> records)
        {
            if (records == null || records.Count == 0)
            {
                return new Metrics
                {
                    successRate = 0.0,
                    averageRetries = 0.0,
                    averageHints = 0.0,
                    averageTimeRatio = 1.0
                };
            }

            int successCount = 0;
            double retries = 0.0;
            double hints = 0.0;
            double timeRatios = 0.0;
            int timeRatioCount = 0;

            int start = Math.Max(0, records.Count - RecentAttemptLimit);
            int count = records.Count - start;
            for (int index = start; index < records.Count; index++)
            {
                AttemptRecord record = records[index];
                if (record == null)
                {
                    count--;
                    continue;
                }

                if (record.successful)
                {
                    successCount++;
                }

                retries += Math.Max(0, record.retryCount);
                hints += Math.Max(0, record.hintsUsed);
                if (record.expectedDurationMilliseconds > 0 && record.elapsedMilliseconds >= 0)
                {
                    timeRatios += (double)record.elapsedMilliseconds / record.expectedDurationMilliseconds;
                    timeRatioCount++;
                }
            }

            if (count <= 0)
            {
                return new Metrics
                {
                    successRate = 0.0,
                    averageRetries = 0.0,
                    averageHints = 0.0,
                    averageTimeRatio = 1.0
                };
            }

            return new Metrics
            {
                successRate = (double)successCount / count,
                averageRetries = retries / count,
                averageHints = hints / count,
                averageTimeRatio = timeRatioCount == 0 ? 1.0 : timeRatios / timeRatioCount
            };
        }

        private struct Metrics
        {
            public double successRate;
            public double averageRetries;
            public double averageHints;
            public double averageTimeRatio;
        }
    }

    [Serializable]
    public sealed class ParentConceptSummary
    {
        public MathConcept concept;
        public ParentConceptStatus status;
        public string statusLabelKo;
        public int recentEvidenceCount;
    }

    public static class ParentProgressSummaryBuilder
    {
        public static ParentConceptSummary[] Build(MathProgressData data)
        {
            if (data == null)
            {
                return new ParentConceptSummary[0];
            }

            data.Normalize(data.localProfileId);
            List<ParentConceptSummary> summaries = new List<ParentConceptSummary>();
            for (int index = 0; index < data.concepts.Count; index++)
            {
                AdaptiveRecommendation recommendation = AdaptiveTeacher.Recommend(data.concepts[index]);
                summaries.Add(new ParentConceptSummary
                {
                    concept = recommendation.concept,
                    status = recommendation.parentStatus,
                    statusLabelKo = GetLabel(recommendation.parentStatus),
                    recentEvidenceCount = recommendation.evidenceCount
                });
            }

            return summaries.ToArray();
        }

        private static string GetLabel(ParentConceptStatus status)
        {
            switch (status)
            {
                case ParentConceptStatus.Familiar:
                    return "익숙함";
                case ParentConceptStatus.NeedsHelp:
                    return "도움이 필요함";
                default:
                    return "연습 중";
            }
        }
    }

    public interface IProgressTextStore
    {
        bool TryLoad(string key, out string value);
        void Save(string key, string value);
    }

    public interface IProgressCodec
    {
        bool TryDecode(string text, out MathProgressData progress);
        string Encode(MathProgressData progress);
    }

    public sealed class ProgressRepository
    {
        private const string KeyPrefix = "yeona_math_progress_v1_";
        private readonly IProgressTextStore store;
        private readonly IProgressCodec codec;

        public ProgressRepository(IProgressTextStore store, IProgressCodec codec)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            if (codec == null)
            {
                throw new ArgumentNullException("codec");
            }

            this.store = store;
            this.codec = codec;
        }

        public MathProgressData LoadOrCreate(string localProfileId)
        {
            string normalizedProfileId = NormalizeProfileId(localProfileId);
            string payload;
            MathProgressData progress;
            if (store.TryLoad(KeyPrefix + normalizedProfileId, out payload) &&
                !string.IsNullOrEmpty(payload) &&
                codec.TryDecode(payload, out progress) &&
                progress != null &&
                progress.schemaVersion == MathProgressData.CurrentSchemaVersion)
            {
                progress.Normalize(normalizedProfileId);
                return progress;
            }

            return new MathProgressData { localProfileId = normalizedProfileId };
        }

        public void Save(MathProgressData progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException("progress");
            }

            string profileId = NormalizeProfileId(progress.localProfileId);
            progress.Normalize(profileId);
            string payload = codec.Encode(progress);
            if (string.IsNullOrEmpty(payload))
            {
                throw new InvalidOperationException("Progress codec returned an empty payload.");
            }

            store.Save(KeyPrefix + profileId, payload);
        }

        private static string NormalizeProfileId(string localProfileId)
        {
            if (string.IsNullOrWhiteSpace(localProfileId))
            {
                return "default";
            }

            string trimmed = localProfileId.Trim();
            if (trimmed.Length > 64)
            {
                trimmed = trimmed.Substring(0, 64);
            }

            char[] safe = new char[trimmed.Length];
            for (int index = 0; index < trimmed.Length; index++)
            {
                char value = trimmed[index];
                safe[index] = char.IsLetterOrDigit(value) || value == '-' || value == '_' ? value : '_';
            }

            return new string(safe);
        }
    }
}
