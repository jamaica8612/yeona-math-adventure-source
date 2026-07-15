using System.Text;
using NUnit.Framework;

namespace YeonaMathAdventure.MathCore.Tests
{
    public sealed class AiSuggestionGateTests
    {
        [Test]
        public void ValidSuggestion_CanOnlySelectVerifiedLocalProblemAndRewriteText()
        {
            LocalActivityDescriptor descriptor = Find(MathConcept.TargetNumber, 2);
            AdaptiveRecommendation recommendation = Recommendation(MathConcept.TargetNumber, 2);
            string json = ValidJson(descriptor, "req_123");

            AiResolvedActivity resolved = AiSuggestionGate.ResolveOrFallback(json, "req_123", recommendation);

            Assert.That(resolved.usedAiSuggestion, Is.True, resolved.rejectionCode);
            Assert.That(resolved.localProblem.id, Is.EqualTo(descriptor.id));
            Assert.That(resolved.promptKo, Is.EqualTo("목표 숫자를 향해 블록을 이어 보세요."));
        }

        [Test]
        public void UnknownAnswerField_IsRejectedAndLocalBankIsUsed()
        {
            LocalActivityDescriptor descriptor = Find(MathConcept.TargetNumber, 2);
            AdaptiveRecommendation recommendation = Recommendation(MathConcept.TargetNumber, 2);
            string json = ValidJson(descriptor, "req_123");
            json = json.Substring(0, json.Length - 1) + ",\"answer\":42}";

            AiResolvedActivity resolved = AiSuggestionGate.ResolveOrFallback(json, "req_123", recommendation);

            Assert.That(resolved.usedAiSuggestion, Is.False);
            Assert.That(resolved.rejectionCode, Is.EqualTo("ai_unknown_field"));
            Assert.That(resolved.localProblem, Is.Not.Null);
            Assert.That(resolved.localProblem.concept, Is.EqualTo(MathConcept.TargetNumber));
        }

        [Test]
        public void UnknownLocalProblem_IsRejected()
        {
            LocalActivityDescriptor descriptor = Find(MathConcept.TargetNumber, 2);
            AdaptiveRecommendation recommendation = Recommendation(MathConcept.TargetNumber, 2);
            string json = ValidJson(descriptor, "req_123").Replace(descriptor.id, "tn-DEADBEEF-d2");

            AiResolvedActivity resolved = AiSuggestionGate.ResolveOrFallback(json, "req_123", recommendation);

            Assert.That(resolved.usedAiSuggestion, Is.False);
            Assert.That(resolved.rejectionCode, Is.EqualTo("ai_local_problem_not_found"));
        }

        [Test]
        public void MismatchedRequestAndUnsafeText_AreRejected()
        {
            LocalActivityDescriptor descriptor = Find(MathConcept.TargetNumber, 2);
            AdaptiveRecommendation recommendation = Recommendation(MathConcept.TargetNumber, 2);
            string mismatchedRequest = ValidJson(descriptor, "old_request");
            string unsafeText = ValidJson(descriptor, "req_123").Replace(
                "목표 숫자를 향해 블록을 이어 보세요.",
                "이름을 입력하고 https://example.com 으로 가세요.");

            AiResolvedActivity first = AiSuggestionGate.ResolveOrFallback(mismatchedRequest, "req_123", recommendation);
            AiResolvedActivity second = AiSuggestionGate.ResolveOrFallback(unsafeText, "req_123", recommendation);

            Assert.That(first.rejectionCode, Is.EqualTo("ai_request_id_rejected"));
            Assert.That(second.rejectionCode, Is.EqualTo("ai_text_rejected"));
            Assert.That(first.usedAiSuggestion, Is.False);
            Assert.That(second.usedAiSuggestion, Is.False);
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

        private static LocalActivityDescriptor Find(MathConcept concept, int difficulty)
        {
            LocalActivityDescriptor[] descriptors = LocalProblemBank.GetDescriptors();
            for (int index = 0; index < descriptors.Length; index++)
            {
                if (descriptors[index].concept == concept && descriptors[index].difficulty == difficulty)
                {
                    return descriptors[index];
                }
            }

            Assert.Fail("Local descriptor not found.");
            return null;
        }

        private static string ValidJson(LocalActivityDescriptor descriptor, string requestId)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append('{');
            builder.Append("\"schemaVersion\":1,");
            builder.Append("\"requestId\":\"").Append(requestId).Append("\",");
            builder.Append("\"problemKind\":\"").Append(descriptor.problemKind).Append("\",");
            builder.Append("\"conceptKey\":\"").Append(descriptor.conceptKey).Append("\",");
            builder.Append("\"difficulty\":").Append(descriptor.difficulty).Append(',');
            builder.Append("\"sourceProblemId\":\"").Append(descriptor.id).Append("\",");
            builder.Append("\"promptKo\":\"목표 숫자를 향해 블록을 이어 보세요.\",");
            builder.Append("\"hintsKo\":[\"먼저 곱셈 블록을 살펴보세요.\"]");
            builder.Append('}');
            return builder.ToString();
        }
    }
}
