using System;
using System.Collections.Generic;
using System.Globalization;

namespace YeonaMathAdventure.MathCore
{
    public enum PatternPuzzleKind
    {
        NumericSequence = 0,
        ShapePattern = 1,
        Rotation = 2,
        Symmetry = 3,
        SpatialFill = 4
    }

    [Serializable]
    public sealed class PatternTile
    {
        public string shapeId;
        public int colorIndex;
        public int rotationQuarterTurns;
        public bool reflected;
        public int value;

        public PatternTile Clone()
        {
            return new PatternTile
            {
                shapeId = shapeId,
                colorIndex = colorIndex,
                rotationQuarterTurns = rotationQuarterTurns,
                reflected = reflected,
                value = value
            };
        }
    }

    [Serializable]
    public sealed class PatternSpaceProblem
    {
        public string id;
        public MathConcept concept;
        public PatternPuzzleKind kind;
        public int difficulty;
        public string promptKo;
        public int rows;
        public int columns;
        public PatternTile[] boardTiles = new PatternTile[0];
        public int[] blankIndices = new int[0];
        public PatternTile[] expectedTiles = new PatternTile[0];
        public PatternTile[] choiceTiles = new PatternTile[0];
    }

    [Serializable]
    public sealed class PatternSpaceAttempt
    {
        public int[] slotIndices = new int[0];
        public PatternTile[] placedTiles = new PatternTile[0];
    }

    public static class PatternSpaceValidator
    {
        public static ValidationResult Validate(PatternSpaceProblem problem, PatternSpaceAttempt attempt)
        {
            string integrityError;
            if (!HasValidProblemShape(problem, out integrityError))
            {
                return ValidationResult.Adjust("pattern_integrity_failed", "새 퍼즐을 준비하고 있어요.", integrityError);
            }

            if (attempt == null || attempt.slotIndices == null || attempt.placedTiles == null ||
                attempt.slotIndices.Length != attempt.placedTiles.Length)
            {
                return ValidationResult.Continue("placement_incomplete", "빈칸에 블록을 놓아 보세요.", "show_blank_slots");
            }

            Dictionary<int, PatternTile> placements = new Dictionary<int, PatternTile>();
            for (int index = 0; index < attempt.slotIndices.Length; index++)
            {
                int slotIndex = attempt.slotIndices[index];
                if (!Contains(problem.blankIndices, slotIndex) || placements.ContainsKey(slotIndex))
                {
                    return ValidationResult.Adjust("invalid_slot", "빛나는 빈칸에 블록을 놓아 주세요.", "highlight_blank_slots");
                }

                if (attempt.placedTiles[index] == null)
                {
                    return ValidationResult.Continue("tile_not_placed", "고른 블록을 빈칸까지 옮겨 보세요.", "animate_drag_path");
                }

                placements.Add(slotIndex, attempt.placedTiles[index]);
            }

            for (int expectedIndex = 0; expectedIndex < problem.blankIndices.Length; expectedIndex++)
            {
                int slotIndex = problem.blankIndices[expectedIndex];
                PatternTile placed;
                if (!placements.TryGetValue(slotIndex, out placed))
                {
                    return ValidationResult.Continue("blank_remaining", "아직 빈칸이 남아 있어요.", "pulse_next_blank");
                }

                if (!TilesMatch(problem.expectedTiles[expectedIndex], placed))
                {
                    string hint = problem.kind == PatternPuzzleKind.Rotation
                        ? "rotate_tile"
                        : problem.kind == PatternPuzzleKind.Symmetry
                            ? "show_symmetry_axis"
                            : "show_pattern_neighbors";
                    return ValidationResult.Adjust("tile_needs_adjustment", "주변 블록의 규칙을 보고 돌리거나 옮겨 보세요.", hint);
                }
            }

            return ValidationResult.Solved("pattern_complete", "규칙을 찾아 퍼즐을 완성했어요!");
        }

        public static bool TilesMatch(PatternTile expected, PatternTile actual)
        {
            if (expected == null || actual == null)
            {
                return expected == actual;
            }

            return string.Equals(expected.shapeId, actual.shapeId, StringComparison.Ordinal) &&
                   expected.colorIndex == actual.colorIndex &&
                   NormalizeQuarterTurns(expected.rotationQuarterTurns) == NormalizeQuarterTurns(actual.rotationQuarterTurns) &&
                   expected.reflected == actual.reflected &&
                   expected.value == actual.value;
        }

        private static bool HasValidProblemShape(PatternSpaceProblem problem, out string errorCode)
        {
            errorCode = string.Empty;
            if (problem == null || problem.rows <= 0 || problem.columns <= 0 || problem.boardTiles == null ||
                problem.blankIndices == null || problem.expectedTiles == null || problem.choiceTiles == null)
            {
                errorCode = "problem_missing";
                return false;
            }

            long expectedBoardCount = (long)problem.rows * problem.columns;
            if (expectedBoardCount > int.MaxValue || problem.boardTiles.Length != (int)expectedBoardCount ||
                problem.blankIndices.Length == 0 || problem.blankIndices.Length != problem.expectedTiles.Length ||
                problem.choiceTiles.Length == 0)
            {
                errorCode = "board_shape_invalid";
                return false;
            }

            HashSet<int> seen = new HashSet<int>();
            for (int index = 0; index < problem.blankIndices.Length; index++)
            {
                int blank = problem.blankIndices[index];
                if (blank < 0 || blank >= problem.boardTiles.Length || !seen.Add(blank) || problem.expectedTiles[index] == null)
                {
                    errorCode = "blank_shape_invalid";
                    return false;
                }
            }

            for (int index = 0; index < problem.boardTiles.Length; index++)
            {
                if (problem.boardTiles[index] == null)
                {
                    errorCode = "board_tile_missing";
                    return false;
                }
            }

            bool[] consumedChoices = new bool[problem.choiceTiles.Length];
            for (int expectedIndex = 0; expectedIndex < problem.expectedTiles.Length; expectedIndex++)
            {
                bool found = false;
                for (int choiceIndex = 0; choiceIndex < problem.choiceTiles.Length; choiceIndex++)
                {
                    if (!consumedChoices[choiceIndex] &&
                        TilesMatch(problem.expectedTiles[expectedIndex], problem.choiceTiles[choiceIndex]))
                    {
                        consumedChoices[choiceIndex] = true;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    errorCode = "choice_inventory_invalid";
                    return false;
                }
            }

            return true;
        }

        private static int NormalizeQuarterTurns(int value)
        {
            int normalized = value % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        private static bool Contains(int[] values, int target)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == target)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class PatternSpaceGenerator
    {
        public static PatternSpaceProblem Generate(uint seed, int requestedDifficulty)
        {
            int difficulty = DifficultyRules.Clamp(requestedDifficulty);
            PatternPuzzleKind kind = (PatternPuzzleKind)(difficulty - 1);
            return Generate(seed, difficulty, kind);
        }

        public static PatternSpaceProblem Generate(uint seed, int requestedDifficulty, PatternPuzzleKind kind)
        {
            int difficulty = DifficultyRules.Clamp(requestedDifficulty);
            DeterministicRandom random = new DeterministicRandom(seed ^ 0xAD90777Du ^ (uint)kind * 0x9E3779B9u);
            PatternSpaceProblem problem;

            switch (kind)
            {
                case PatternPuzzleKind.NumericSequence:
                    problem = GenerateNumericSequence(random, difficulty);
                    break;
                case PatternPuzzleKind.ShapePattern:
                    problem = GenerateShapePattern(random, difficulty);
                    break;
                case PatternPuzzleKind.Rotation:
                    problem = GenerateRotation(random, difficulty);
                    break;
                case PatternPuzzleKind.Symmetry:
                    problem = GenerateSymmetry(random, difficulty);
                    break;
                case PatternPuzzleKind.SpatialFill:
                    problem = GenerateSpatialFill(random, difficulty);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("kind");
            }

            problem.id = "ps-" + ((int)kind).ToString(CultureInfo.InvariantCulture) + "-" +
                         seed.ToString("X8", CultureInfo.InvariantCulture) + "-d" +
                         difficulty.ToString(CultureInfo.InvariantCulture);
            problem.kind = kind;
            problem.difficulty = difficulty;
            return problem;
        }

        private static PatternSpaceProblem GenerateNumericSequence(DeterministicRandom random, int difficulty)
        {
            int length = difficulty >= 4 ? 7 : 6;
            int start = random.NextInt(8, 26 + difficulty * 5);
            int step = random.NextInt(2 + difficulty, 6 + difficulty * 2);
            PatternTile[] cells = new PatternTile[length];
            for (int index = 0; index < length; index++)
            {
                cells[index] = NumberTile(start + step * index);
            }

            int firstBlank = random.NextInt(1, length - 1);
            int[] blanks;
            if (difficulty >= 3)
            {
                int secondBlank = firstBlank == length - 2 ? 1 : length - 2;
                if (secondBlank == firstBlank)
                {
                    secondBlank = 2;
                }

                blanks = new[] { firstBlank, secondBlank };
                Array.Sort(blanks);
            }
            else
            {
                blanks = new[] { firstBlank };
            }

            PatternTile[] expected = SelectTiles(cells, blanks);
            List<PatternTile> choices = CloneList(expected);
            choices.Add(NumberTile(expected[0].value - step));
            choices.Add(NumberTile(expected[0].value + step));

            return new PatternSpaceProblem
            {
                concept = MathConcept.NumericPattern,
                promptKo = "숫자가 변하는 규칙을 찾아 빈칸을 채워 보세요.",
                rows = 1,
                columns = length,
                boardTiles = cells,
                blankIndices = blanks,
                expectedTiles = expected,
                choiceTiles = choices.ToArray()
            };
        }

        private static PatternSpaceProblem GenerateShapePattern(DeterministicRandom random, int difficulty)
        {
            int length = difficulty >= 4 ? 8 : 6;
            string[] shapes = { "circle", "triangle", "square" };
            int cycleLength = difficulty <= 2 ? 2 : 3;
            int colorOffset = random.NextInt(0, 3);
            PatternTile[] cells = new PatternTile[length];
            for (int index = 0; index < length; index++)
            {
                cells[index] = new PatternTile
                {
                    shapeId = shapes[index % cycleLength],
                    colorIndex = (index + colorOffset) % cycleLength,
                    rotationQuarterTurns = difficulty >= 3 ? index % 4 : 0,
                    reflected = false,
                    value = 0
                };
            }

            int[] blanks = difficulty >= 3 ? new[] { 2, length - 1 } : new[] { length - 2 };
            PatternTile[] expected = SelectTiles(cells, blanks);
            List<PatternTile> choices = CloneList(expected);
            choices.Add(new PatternTile
            {
                shapeId = shapes[(expected[0].value + 1) % cycleLength],
                colorIndex = (expected[0].colorIndex + 1) % cycleLength,
                rotationQuarterTurns = expected[0].rotationQuarterTurns,
                reflected = false,
                value = 0
            });

            return new PatternSpaceProblem
            {
                concept = MathConcept.ShapePattern,
                promptKo = "모양과 색의 반복 규칙을 찾아 블록을 놓아 보세요.",
                rows = 1,
                columns = length,
                boardTiles = cells,
                blankIndices = blanks,
                expectedTiles = expected,
                choiceTiles = choices.ToArray()
            };
        }

        private static PatternSpaceProblem GenerateRotation(DeterministicRandom random, int difficulty)
        {
            int length = difficulty >= 4 ? 6 : 5;
            int direction = random.NextBool() ? 1 : -1;
            int startRotation = random.NextInt(0, 4);
            PatternTile[] cells = new PatternTile[length];
            for (int index = 0; index < length; index++)
            {
                cells[index] = new PatternTile
                {
                    shapeId = "arrow",
                    colorIndex = 1,
                    rotationQuarterTurns = startRotation + direction * index,
                    reflected = false,
                    value = 0
                };
            }

            int[] blanks = new[] { length - 1 };
            PatternTile[] expected = SelectTiles(cells, blanks);
            PatternTile distractor = expected[0].Clone();
            distractor.rotationQuarterTurns += 1;
            return new PatternSpaceProblem
            {
                concept = MathConcept.Rotation,
                promptKo = "화살표가 도는 방향을 보고 마지막 블록을 돌려 놓아 보세요.",
                rows = 1,
                columns = length,
                boardTiles = cells,
                blankIndices = blanks,
                expectedTiles = expected,
                choiceTiles = new[] { expected[0].Clone(), distractor }
            };
        }

        private static PatternSpaceProblem GenerateSymmetry(DeterministicRandom random, int difficulty)
        {
            const int rows = 3;
            int columns = difficulty >= 4 ? 6 : 4;
            int half = columns / 2;
            PatternTile[] cells = new PatternTile[rows * columns];
            List<int> blankList = new List<int>();

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < half; column++)
                {
                    PatternTile source = new PatternTile
                    {
                        shapeId = ((row + column) % 2 == 0) ? "leaf" : "kite",
                        colorIndex = (row + column + random.NextInt(0, 2)) % 3,
                        rotationQuarterTurns = (row + column) % 4,
                        reflected = false,
                        value = 0
                    };
                    int leftIndex = row * columns + column;
                    int rightIndex = row * columns + (columns - 1 - column);
                    cells[leftIndex] = source;
                    PatternTile mirrored = source.Clone();
                    mirrored.reflected = true;
                    cells[rightIndex] = mirrored;
                    blankList.Add(rightIndex);
                }
            }

            blankList.Sort();
            int[] blanks = blankList.ToArray();
            PatternTile[] expected = SelectTiles(cells, blanks);
            return new PatternSpaceProblem
            {
                concept = MathConcept.Symmetry,
                promptKo = "가운데 선을 거울처럼 생각하고 반대쪽을 완성해 보세요.",
                rows = rows,
                columns = columns,
                boardTiles = cells,
                blankIndices = blanks,
                expectedTiles = expected,
                choiceTiles = CloneList(expected).ToArray()
            };
        }

        private static PatternSpaceProblem GenerateSpatialFill(DeterministicRandom random, int difficulty)
        {
            int rows = difficulty >= 4 ? 3 : 2;
            const int columns = 3;
            PatternTile[] cells = new PatternTile[rows * columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    cells[row * columns + column] = new PatternTile
                    {
                        shapeId = (row + column) % 2 == 0 ? "corner" : "bridge",
                        colorIndex = (row * 2 + column + random.NextInt(0, 2)) % 3,
                        rotationQuarterTurns = (row + column) % 4,
                        reflected = false,
                        value = 0
                    };
                }
            }

            int[] blanks = rows == 2 ? new[] { 1, 4 } : new[] { 1, 4, 7 };
            PatternTile[] expected = SelectTiles(cells, blanks);
            List<PatternTile> choices = CloneList(expected);
            PatternTile rotatedDistractor = expected[0].Clone();
            rotatedDistractor.rotationQuarterTurns += 1;
            choices.Add(rotatedDistractor);

            return new PatternSpaceProblem
            {
                concept = MathConcept.SpatialReasoning,
                promptKo = "길이 끊기지 않도록 블록을 놓고 방향을 맞춰 보세요.",
                rows = rows,
                columns = columns,
                boardTiles = cells,
                blankIndices = blanks,
                expectedTiles = expected,
                choiceTiles = choices.ToArray()
            };
        }

        private static PatternTile NumberTile(int value)
        {
            return new PatternTile
            {
                shapeId = "number",
                colorIndex = 0,
                rotationQuarterTurns = 0,
                reflected = false,
                value = value
            };
        }

        private static PatternTile[] SelectTiles(PatternTile[] cells, int[] indices)
        {
            PatternTile[] selected = new PatternTile[indices.Length];
            for (int index = 0; index < indices.Length; index++)
            {
                selected[index] = cells[indices[index]].Clone();
            }

            return selected;
        }

        private static List<PatternTile> CloneList(PatternTile[] tiles)
        {
            List<PatternTile> clones = new List<PatternTile>();
            for (int index = 0; index < tiles.Length; index++)
            {
                clones.Add(tiles[index].Clone());
            }

            return clones;
        }
    }
}
