#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YeonaMathAdventure.Tests
{
    public sealed class PremiumVisualTests
    {
        [TestCase(PremiumMathVisuals.ForestFeastBackplate)]
        [TestCase(PremiumMathVisuals.StarBridgeBackplate)]
        [TestCase(PremiumMathVisuals.MirrorGardenBackplate)]
        public void PremiumBackplates_AreHighResolutionLandscapeResources(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            Assert.That(texture, Is.Not.Null, "Missing Resources texture: " + resourcePath);
            Assert.That(texture.width, Is.GreaterThanOrEqualTo(1536));
            Assert.That(texture.height, Is.GreaterThanOrEqualTo(864));
            Assert.That(texture.width / (float)texture.height, Is.InRange(1.7f, 1.82f));
        }

        [Test]
        public void StarCompanion_HasTransparentCornersForLayeredPresentation()
        {
            string path = Path.Combine(Application.dataPath,
                "YeonaMathAdventure/Resources/YeonaMathAdventure/Art/StarCompanion.png");
            Assert.That(File.Exists(path), Is.True);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false), Is.True);
                Assert.That(texture.GetPixel(0, 0).a, Is.LessThan(0.05f));
                Assert.That(texture.GetPixel(texture.width - 1, 0).a, Is.LessThan(0.05f));
                Assert.That(texture.GetPixel(0, texture.height - 1).a, Is.LessThan(0.05f));
                Assert.That(texture.GetPixel(texture.width - 1, texture.height - 1).a, Is.LessThan(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void AspectFill_UsesCenteredCropForTabletAndWidePhone()
        {
            Rect tablet = RawImageAspectFill.CalculateUvRect(1672, 941, 1440f, 1080f);
            Assert.That(tablet.x, Is.GreaterThan(0f));
            Assert.That(tablet.width, Is.LessThan(1f));
            Assert.That(tablet.center.x, Is.EqualTo(0.5f).Within(0.0001f));

            Rect widePhone = RawImageAspectFill.CalculateUvRect(1672, 941, 2400f, 1080f);
            Assert.That(widePhone.y, Is.GreaterThan(0f));
            Assert.That(widePhone.height, Is.LessThan(1f));
            Assert.That(widePhone.center.y, Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
#endif
