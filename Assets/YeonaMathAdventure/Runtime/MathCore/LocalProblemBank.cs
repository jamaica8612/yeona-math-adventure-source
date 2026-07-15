using System;
using System.Collections.Generic;

namespace YeonaMathAdventure.MathCore
{
    public static class ProblemKindKeys
    {
        public const string TargetNumber = "target_number";
        public const string FairShare = "fair_share";
        public const string PatternSpace = "pattern_space";
    }

    public static class ConceptKeys
    {
        public const string TargetNumber = "target_number";
        public const string DivisionAndRemainder = "division_remainder";
        public const string NumericPattern = "numeric_pattern";
        public const string ShapePattern = "shape_pattern";
        public const string Rotation = "rotation";
        public const string Symmetry = "symmetry";
        public const string SpatialReasoning = "spatial_reasoning";

        public static string FromConcept(MathConcept concept)
        {
            switch (concept)
            {
                case MathConcept.TargetNumber:
                    return TargetNumber;
                case MathConcept.DivisionAndRemainder:
                    return DivisionAndRemainder;
                case MathConcept.NumericPattern:
                    return NumericPattern;
                case MathConcept.ShapePattern:
                    return ShapePattern;
                case MathConcept.Rotation:
                    return Rotation;
                case MathConcept.Symmetry:
                    return Symmetry;
                case MathConcept.SpatialReasoning:
                    return SpatialReasoning;
                default:
                    throw new ArgumentOutOfRangeException("concept");
            }
        }
    }

    [Serializable]
    public sealed class LocalActivityDescriptor
    {
        public string id;
        public string problemKind;
        public MathConcept concept;
        public string conceptKey;
        public int difficulty;
        public string defaultPromptKo;
        public string[] defaultHintsKo = new string[0];

        public LocalActivityDescriptor Clone()
        {
            return new LocalActivityDescriptor
            {
                id = id,
                problemKind = problemKind,
                concept = concept,
                conceptKey = conceptKey,
                difficulty = difficulty,
                defaultPromptKo = defaultPromptKo,
                defaultHintsKo = defaultHintsKo == null ? new string[0] : (string[])defaultHintsKo.Clone()
            };
        }
    }

    public sealed class LocalProblemEntry
    {
        public LocalActivityDescriptor descriptor;
        public TargetNumberProblem targetNumber;
        public FairShareProblem fairShare;
        public PatternSpaceProblem patternSpace;
    }

    /// <summary>
    /// Offline-first curated bank. Entries are regenerated from fixed seeds on every request,
    /// so callers cannot mutate the canonical bank.
    /// </summary>
    public static class LocalProblemBank
    {
        private static readonly BankSpec[] Specs = CreateSpecs();

        public static LocalActivityDescriptor[] GetDescriptors()
        {
            LocalActivityDescriptor[] descriptors = new LocalActivityDescriptor[Specs.Length];
            for (int index = 0; index < Specs.Length; index++)
            {
                descriptors[index] = CreateEntry(Specs[index]).descriptor;
            }

            return descriptors;
        }

        public static bool TryGetDescriptor(string id, out LocalActivityDescriptor descriptor)
        {
            LocalProblemEntry entry;
            if (TryCreateEntry(id, out entry))
            {
                descriptor = entry.descriptor;
                return true;
            }

            descriptor = null;
            return false;
        }

        public static bool TryCreateEntry(string id, out LocalProblemEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int index = 0; index < Specs.Length; index++)
            {
                LocalProblemEntry candidate = CreateEntry(Specs[index]);
                if (string.Equals(candidate.descriptor.id, id, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public static LocalActivityDescriptor SelectFallback(MathConcept concept, int requestedDifficulty)
        {
            int difficulty = DifficultyRules.Clamp(requestedDifficulty);
            LocalActivityDescriptor best = null;
            int bestDistance = int.MaxValue;

            for (int index = 0; index < Specs.Length; index++)
            {
                LocalActivityDescriptor candidate = CreateEntry(Specs[index]).descriptor;
                if (candidate.concept != concept)
                {
                    continue;
                }

                int distance = Math.Abs(candidate.difficulty - difficulty);
                if (distance < bestDistance ||
                    (distance == bestDistance && best != null && string.CompareOrdinal(candidate.id, best.id) < 0))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            if (best != null)
            {
                return best;
            }

            // A missing concept mapping must never prevent offline play.
            return CreateEntry(Specs[0]).descriptor;
        }

        private static BankSpec[] CreateSpecs()
        {
            List<BankSpec> specs = new List<BankSpec>();
            for (int difficulty = DifficultyRules.Minimum; difficulty <= DifficultyRules.Maximum; difficulty++)
            {
                specs.Add(new BankSpec
                {
                    kind = BankProblemKind.TargetNumber,
                    seed = (uint)(0x1000 + difficulty),
                    difficulty = difficulty
                });
                specs.Add(new BankSpec
                {
                    kind = BankProblemKind.FairShare,
                    seed = (uint)(0x2000 + difficulty),
                    difficulty = difficulty
                });
                for (int patternKind = 0; patternKind <= (int)PatternPuzzleKind.SpatialFill; patternKind++)
                {
                    specs.Add(new BankSpec
                    {
                        kind = BankProblemKind.PatternSpace,
                        seed = (uint)(0x3000 + difficulty * 16 + patternKind),
                        difficulty = difficulty,
                        patternKind = (PatternPuzzleKind)patternKind
                    });
                }
            }

            return specs.ToArray();
        }

        private static LocalProblemEntry CreateEntry(BankSpec spec)
        {
            if (spec.kind == BankProblemKind.TargetNumber)
            {
                TargetNumberProblem problem = TargetNumberGenerator.Generate(spec.seed, spec.difficulty);
                return new LocalProblemEntry
                {
                    targetNumber = problem,
                    descriptor = new LocalActivityDescriptor
                    {
                        id = problem.id,
                        problemKind = ProblemKindKeys.TargetNumber,
                        concept = problem.concept,
                        conceptKey = ConceptKeys.FromConcept(problem.concept),
                        difficulty = problem.difficulty,
                        defaultPromptKo = problem.promptKo,
                        defaultHintsKo = new[]
                        {
                            "목표보다 큰지 작은지 먼저 비교해 보세요.",
                            "곱셈과 나눗셈 블록을 먼저 계산해 보세요."
                        }
                    }
                };
            }

            if (spec.kind == BankProblemKind.FairShare)
            {
                FairShareProblem problem = FairShareGenerator.Generate(spec.seed, spec.difficulty);
                return new LocalProblemEntry
                {
                    fairShare = problem,
                    descriptor = new LocalActivityDescriptor
                    {
                        id = problem.id,
                        problemKind = ProblemKindKeys.FairShare,
                        concept = problem.concept,
                        conceptKey = ConceptKeys.FromConcept(problem.concept),
                        difficulty = problem.difficulty,
                        defaultPromptKo = problem.promptKo,
                        defaultHintsKo = new[]
                        {
                            "각 칸에 하나씩 차례로 놓아 보세요.",
                            "칸마다 놓인 수가 같은지 나란히 비교해 보세요."
                        }
                    }
                };
            }

            PatternSpaceProblem pattern = PatternSpaceGenerator.Generate(spec.seed, spec.difficulty, spec.patternKind);
            return new LocalProblemEntry
            {
                patternSpace = pattern,
                descriptor = new LocalActivityDescriptor
                {
                    id = pattern.id,
                    problemKind = ProblemKindKeys.PatternSpace,
                    concept = pattern.concept,
                    conceptKey = ConceptKeys.FromConcept(pattern.concept),
                    difficulty = pattern.difficulty,
                    defaultPromptKo = pattern.promptKo,
                    defaultHintsKo = new[]
                    {
                        "빈칸의 앞뒤 블록을 함께 살펴보세요.",
                        "블록을 돌려 선이나 방향이 이어지는지 확인해 보세요."
                    }
                }
            };
        }

        private enum BankProblemKind
        {
            TargetNumber,
            FairShare,
            PatternSpace
        }

        private sealed class BankSpec
        {
            public BankProblemKind kind;
            public uint seed;
            public int difficulty;
            public PatternPuzzleKind patternKind;
        }
    }
}
