#if UNITY_EDITOR
using NUnit.Framework;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure.Tests
{
    public sealed class YeonaVoiceAndAge4Tests
    {
        [Test]
        public void VoiceSanitize_RemovesDecorationsAndCollapsesWhitespace()
        {
            Assert.That(YeonaVoice.Sanitize("★  +1\n별다리가 깨어났어!"), Is.EqualTo("+1 별다리가 깨어났어!"));
            Assert.That(YeonaVoice.Sanitize("친구 1  ·  0개"), Is.EqualTo("친구 1 0개"));
            Assert.That(YeonaVoice.Sanitize("  "), Is.EqualTo(string.Empty));
            Assert.That(YeonaVoice.Sanitize(null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void FairShare_CountingLevel_UsesCountingLanguageInsteadOfEquations()
        {
            FairShareProblem age4 = new FairShareProblem
            {
                totalItems = 6,
                recipientCount = 2,
                expectedEach = 3,
                expectedRemainder = 0,
                useRemainderTray = false,
                recipientNameKo = "접시"
            };

            Assert.That(FairShareGameView.IsCountingLevel(age4), Is.True);
            string worked = FairShareGameView.BuildSupportMessage(ScaffoldLevel.WorkedExample, age4);
            StringAssert.DoesNotContain("=", worked);
            StringAssert.DoesNotContain("×", worked);
            StringAssert.Contains("3", worked);

            FairShareProblem older = new FairShareProblem
            {
                totalItems = 17,
                recipientCount = 4,
                expectedEach = 4,
                expectedRemainder = 1,
                useRemainderTray = true,
                recipientNameKo = "상자"
            };

            Assert.That(FairShareGameView.IsCountingLevel(older), Is.False);
            StringAssert.Contains("17 = 4 × 4 + 1",
                FairShareGameView.BuildSupportMessage(ScaffoldLevel.WorkedExample, older));
        }
    }
}
#endif
