using System;
using System.Globalization;

namespace YeonaMathAdventure.MathCore
{
    [Serializable]
    public sealed class FairShareProblem
    {
        public string id;
        public MathConcept concept = MathConcept.DivisionAndRemainder;
        public int difficulty;
        public string promptKo;
        public string itemNameKo;
        public string recipientNameKo;
        public int totalItems;
        public int recipientCount;
        public int expectedEach;
        public int expectedRemainder;
        public bool useRemainderTray;
    }

    [Serializable]
    public sealed class FairShareAttempt
    {
        public int[] recipientItemCounts = new int[0];
        public int remainderCount;
    }

    public static class FairShareValidator
    {
        public static ValidationResult Validate(FairShareProblem problem, FairShareAttempt attempt)
        {
            if (problem == null || attempt == null)
            {
                return ValidationResult.Adjust("share_attempt_missing", "나눌 물건을 다시 준비하고 있어요.", "reload_problem");
            }

            if (problem.totalItems < 0 || problem.recipientCount <= 0)
            {
                return ValidationResult.Adjust("problem_integrity_failed", "새 문제를 준비하고 있어요.", "discard_problem");
            }

            int engineEach = problem.totalItems / problem.recipientCount;
            int engineRemainder = problem.totalItems % problem.recipientCount;
            if (problem.expectedEach != engineEach || problem.expectedRemainder != engineRemainder ||
                problem.useRemainderTray != (engineRemainder > 0))
            {
                return ValidationResult.Adjust("problem_integrity_failed", "새 문제를 준비하고 있어요.", "discard_problem");
            }

            if (attempt.recipientItemCounts == null || attempt.recipientItemCounts.Length != problem.recipientCount)
            {
                return ValidationResult.Continue("recipient_count_incomplete", "모든 상자에 물건을 놓아 보세요.", "show_empty_recipients");
            }

            if (attempt.remainderCount < 0)
            {
                return ValidationResult.Adjust("negative_remainder", "남는 칸의 물건 수를 다시 살펴보세요.", "highlight_remainder_tray");
            }

            long allocated = attempt.remainderCount;
            for (int index = 0; index < attempt.recipientItemCounts.Length; index++)
            {
                if (attempt.recipientItemCounts[index] < 0)
                {
                    return ValidationResult.Adjust("negative_share", "상자 안의 물건을 다시 세어 보세요.", "highlight_recipients");
                }

                allocated += attempt.recipientItemCounts[index];
            }

            if (allocated < problem.totalItems)
            {
                return ValidationResult.Continue("items_remaining", "아직 나누지 않은 물건이 있어요.", "highlight_unplaced_items");
            }

            if (allocated > problem.totalItems)
            {
                return ValidationResult.Adjust("too_many_items", "놓인 물건 수가 처음보다 많아요. 하나씩 다시 세어 볼까요?", "count_all_items");
            }

            int firstCount = attempt.recipientItemCounts[0];
            for (int index = 1; index < attempt.recipientItemCounts.Length; index++)
            {
                if (attempt.recipientItemCounts[index] != firstCount)
                {
                    return ValidationResult.Adjust("shares_not_equal", "상자마다 같은 수가 되도록 옮겨 보세요.", "compare_recipient_counts");
                }
            }

            if (firstCount != engineEach || attempt.remainderCount != engineRemainder)
            {
                return ValidationResult.Adjust("share_needs_adjustment", "한 번씩 차례로 나누어 주면 어떻게 될까요?", "deal_one_by_one");
            }

            return ValidationResult.Solved("fair_share_complete", "모두에게 공평하게 나누었어요!");
        }
    }

    public static class FairShareGenerator
    {
        private static readonly string[] ItemNames = { "귤", "별 스티커", "색연필", "구슬" };
        private static readonly string[] RecipientNames = { "상자", "친구", "바구니", "접시" };

        public static FairShareProblem Generate(uint seed, int requestedDifficulty)
        {
            int difficulty = DifficultyRules.Clamp(requestedDifficulty);
            DeterministicRandom random = new DeterministicRandom(seed ^ 0xC8013EA4u);

            int recipientMinimum = difficulty <= 2 ? 2 : 3;
            int recipientMaximumExclusive = difficulty == 1 ? 5 : Math.Min(10, difficulty + 5);
            int recipients = random.NextInt(recipientMinimum, recipientMaximumExclusive);
            int quotientMinimum = difficulty == 1 ? 3 : 4 + difficulty;
            int quotientMaximumExclusive = difficulty == 1 ? 9 : 9 + difficulty * 4;
            int each = random.NextInt(quotientMinimum, quotientMaximumExclusive);
            int remainder = difficulty == 1 ? 0 : random.NextInt(1, recipients);
            int total = each * recipients + remainder;
            string item = ItemNames[random.NextInt(0, ItemNames.Length)];
            string recipient = RecipientNames[random.NextInt(0, RecipientNames.Length)];

            string remainderSentence = remainder == 0
                ? "모두 남김없이 나누어 주세요."
                : "남는 것은 남는 칸에 놓아 주세요.";

            return new FairShareProblem
            {
                id = "fs-" + seed.ToString("X8", CultureInfo.InvariantCulture) + "-d" +
                     difficulty.ToString(CultureInfo.InvariantCulture),
                difficulty = difficulty,
                itemNameKo = item,
                recipientNameKo = recipient,
                totalItems = total,
                recipientCount = recipients,
                expectedEach = total / recipients,
                expectedRemainder = total % recipients,
                useRemainderTray = remainder > 0,
                promptKo = item + " " + total.ToString(CultureInfo.InvariantCulture) + "개를 " + recipient + " " +
                           recipients.ToString(CultureInfo.InvariantCulture) + "곳에 똑같이 나누어 보세요. " +
                           remainderSentence
            };
        }
    }
}
