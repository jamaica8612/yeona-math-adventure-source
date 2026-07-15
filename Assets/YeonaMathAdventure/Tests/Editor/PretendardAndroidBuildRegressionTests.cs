#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using YeonaMathAdventure.Editor;

namespace YeonaMathAdventure.Tests
{
    /// <summary>
    /// Guards the Android regression where runtime-created TMP font assets left
    /// every Korean label invisible and repeatedly raised exceptions on device.
    /// </summary>
    public sealed class PretendardAndroidBuildRegressionTests
    {
        private const string GeneratedFontAssetPath =
            "Assets/YeonaMathAdventure/Resources/YeonaMathAdventure/Fonts/Pretendard-Dynamic.asset";
        private const string GeneratedFontResourcePath =
            "YeonaMathAdventure/Fonts/Pretendard-Dynamic";
        private const string BuildScriptPath =
            "Assets/YeonaMathAdventure/Editor/YeonaMathAndroidBuild.cs";

        [OneTimeSetUp]
        public void ConfigureDedicatedProject()
        {
            YeonaMathAndroidBuild.SetupProject();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [Test]
        public void SetupProject_GeneratesPersistentDynamicPretendardFontAsset()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GeneratedFontAssetPath);

            Assert.That(fontAsset, Is.Not.Null,
                "SetupProject must serialize Pretendard-Dynamic before the Android player build.");
            Assert.That(EditorUtility.IsPersistent(fontAsset), Is.True,
                "The player must not depend on an in-memory TMP font created at runtime.");
            Assert.That(fontAsset.sourceFontFile, Is.Not.Null,
                "A Dynamic TMP font requires its source OTF in the player.");
            Assert.That(AssetDatabase.GetAssetPath(fontAsset.sourceFontFile),
                Is.EqualTo("Assets/YeonaMathAdventure/Resources/Fonts/Pretendard-Regular.otf"));
            Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static),
                "런타임 글리프 래스터라이즈(Dynamic)는 기기별 간헐 실패로 전체 라벨을 지운다 — 정적으로 고정.");
            Assert.That(fontAsset.isMultiAtlasTexturesEnabled, Is.True);

            Assert.That(fontAsset.material, Is.Not.Null);
            Assert.That(fontAsset.material.shader, Is.Not.Null,
                "The serialized material must retain the TextMesh Pro distance-field shader.");
            Assert.That(fontAsset.material.shader.name, Does.Contain("TextMeshPro"));
            Assert.That(EditorUtility.IsPersistent(fontAsset.material), Is.True);

            Assert.That(fontAsset.atlasTextures, Is.Not.Null.And.Not.Empty);
            Assert.That(fontAsset.atlasTextures[0], Is.Not.Null);
            Assert.That(EditorUtility.IsPersistent(fontAsset.atlasTextures[0]), Is.True,
                "At least the initial TMP atlas must be serialized into the project.");
            Assert.That(fontAsset.atlasTextures[0].width, Is.GreaterThan(1),
                "The Android player must receive a populated atlas, not TMP's empty 1x1 runtime seed.");
            Assert.That(fontAsset.HasCharacter('연'), Is.True);
            Assert.That(fontAsset.HasCharacter('아'), Is.True);
            Assert.That(fontAsset.HasCharacter('×'), Is.True);
            Assert.That(fontAsset.HasCharacter('랗'), Is.True,
                "만 4세 트랙에서 추가된 안내 문구의 글리프도 정적 아틀라스에 있어야 한다.");
            Assert.That(fontAsset.HasCharacter('빛'), Is.True);

            var serializedFont = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicData = serializedFont.FindProperty("m_ClearDynamicDataOnBuild");
            Assert.That(clearDynamicData, Is.Not.Null);
            Assert.That(clearDynamicData.boolValue, Is.False,
                "TMP must not erase the pre-rendered Korean atlas immediately before the player build.");

            TMP_FontAsset resourceAsset = Resources.Load<TMP_FontAsset>(GeneratedFontResourcePath);
            Assert.That(resourceAsset, Is.Not.Null,
                "KoreanFontProvider's first Resources path must resolve the generated asset.");
            Assert.That(AssetDatabase.GetAssetPath(resourceAsset), Is.EqualTo(GeneratedFontAssetPath));
        }

        [Test]
        public void SetupProject_ConfiguresUpgradableNonDevelopmentAndroidPlayer()
        {
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo("0.2.0"));
            Assert.That(PlayerSettings.Android.bundleVersionCode, Is.EqualTo(3));
            Assert.That(PlayerSettings.Android.targetArchitectures, Is.EqualTo(AndroidArchitecture.ARM64),
                "The fast test APK intentionally targets modern 64-bit Android devices only.");
            Assert.That(EditorUserBuildSettings.development, Is.False,
                "A child-facing APK must not expose Unity's development console.");
            Assert.That(EditorUserBuildSettings.allowDebugging, Is.False);

            Assert.That(YeonaMathAndroidBuild.DefaultRelativeApkPath, Does.EndWith(".apk"));
            Assert.That(YeonaMathAndroidBuild.DefaultRelativeApkPath, Does.Contain("v0.2.0"));
            Assert.That(YeonaMathAndroidBuild.DefaultRelativeApkPath.ToLowerInvariant(),
                Does.Not.Contain("debug"));
        }

        [Test]
        public void BuildAndroid_DoesNotRequestDevelopmentOrScriptDebuggingOptions()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);

            string absoluteBuildScriptPath = Path.Combine(projectRoot, BuildScriptPath);
            Assert.That(File.Exists(absoluteBuildScriptPath), Is.True);
            string source = File.ReadAllText(absoluteBuildScriptPath);

            Assert.That(source, Does.Not.Contain("BuildOptions.Development"));
            Assert.That(source, Does.Not.Contain("BuildOptions.AllowDebugging"));
        }
    }
}
#endif
