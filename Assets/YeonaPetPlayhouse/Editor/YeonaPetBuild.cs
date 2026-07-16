#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace YeonaPetPlayhouse.Editor
{
    /// <summary>
    /// 연아의 별냥이 놀이집 — 셋업과 안드로이드 빌드.
    /// 배치 모드:
    ///   Unity -batchmode -projectPath <경로> -buildTarget Android
    ///         -executeMethod YeonaPetPlayhouse.Editor.YeonaPetBuild.BuildAndroid
    /// </summary>
    public static class YeonaPetBuild
    {
        public const string ProductName = "연아의 별냥이 놀이집";
        public const string PackageName = "com.yeona.petplayhouse";
        public const string ScenePath = "Assets/YeonaPetPlayhouse/Scenes/PetPlayhouse.unity";
        public const string DefaultRelativeApkPath = "Builds/YeonaPetPlayhouse/YeonaPetPlayhouse-v0.1.0.apk";

        public const string JuaSourceFontPath =
            "Assets/YeonaPetPlayhouse/Resources/Fonts/Jua-Regular.ttf";
        public const string JuaTmpAssetPath =
            "Assets/YeonaPetPlayhouse/Resources/YeonaPetPlayhouse/Fonts/Jua-Static.asset";

        [MenuItem("Yeona Pet/Setup/Configure Project")]
        public static void SetupProject()
        {
            ConfigurePlayerSettings();
            CreatePersistentJuaFontAsset();
            CreateMinimalScene();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Yeona Pet Build] Project configured. Product: " + ProductName + " / " + PackageName);
        }

        [MenuItem("Yeona Pet/Build/Android Debug APK")]
        public static void BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                throw new BuildFailedException(
                    "Android is not the active build target. Start Unity with '-buildTarget Android'.");
            }

            SetupProject();

            string outputPath = ResolveOutputPath();
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new BuildFailedException("Could not determine the APK output directory from '" + outputPath + "'.");
            }

            Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                targetGroup = BuildTargetGroup.Android,
                target = BuildTarget.Android,
                options = BuildOptions.CompressWithLz4
            };

            Debug.Log("[Yeona Pet Build] Building Android APK at '" + outputPath + "'.");
            BuildReport report = UnityEditor.BuildPipeline.BuildPlayer(options);
            ValidateBuildReport(report, outputPath);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Yeona Family";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;

            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.startInFullscreen = true;
            // 아이 대상 앱: 네트워크·외부 저장소 권한을 요구하지 않는다.
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
        }

        /// <summary>
        /// Jua 폰트를 빌드 시점에 정적(Static) TMP 아틀라스로 굽는다.
        /// 필요한 글리프(런타임 코드의 모든 문자열 + 기본 문자)를 전부 미리 래스터라이즈하므로
        /// 기기에서 런타임 폰트 초기화가 실패해 라벨이 사라지는 회귀가 원천 차단된다.
        /// 새 한국어 문자열을 추가하면 반드시 이 셋업을 다시 실행할 것(빌드 메뉴가 자동 실행).
        /// </summary>
        private static void CreatePersistentJuaFontAsset()
        {
            EnsureAssetFolder("Assets/YeonaPetPlayhouse/Resources/YeonaPetPlayhouse/Fonts");

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(JuaSourceFontPath);
            if (sourceFont == null)
            {
                throw new BuildFailedException("Jua source font was not found at '" + JuaSourceFontPath + "'.");
            }

            TrueTypeFontImporter importer = AssetImporter.GetAtPath(JuaSourceFontPath) as TrueTypeFontImporter;
            if (importer != null && !importer.includeFontData)
            {
                importer.includeFontData = true;
                importer.SaveAndReimport();
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (fontAsset == null)
            {
                throw new BuildFailedException("TextMesh Pro could not create a font asset from Jua.");
            }

            fontAsset.name = "Jua-Static";
            fontAsset.isMultiAtlasTexturesEnabled = true;

            string requiredCharacters = CollectRequiredFontCharacters();
            string missingCharacters;
            if (!fontAsset.TryAddCharacters(requiredCharacters, out missingCharacters))
            {
                string missingKoreanOrDigits = new string(missingCharacters
                    .Where(character => (character >= '가' && character <= '힣')
                                        || (character >= '0' && character <= '9'))
                    .Distinct()
                    .ToArray());
                if (!string.IsNullOrEmpty(missingKoreanOrDigits))
                {
                    UnityEngine.Object.DestroyImmediate(fontAsset);
                    throw new BuildFailedException(
                        "Jua could not pre-render required glyphs: " + missingKoreanOrDigits);
                }

                Debug.LogWarning("[Yeona Pet Build] Jua lacks optional symbols: " + missingCharacters);
            }

            if (fontAsset.material == null || fontAsset.material.shader == null)
            {
                UnityEngine.Object.DestroyImmediate(fontAsset);
                throw new BuildFailedException("Jua TMP material/shader was not created.");
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures == null || atlasTextures.Length == 0 || atlasTextures[0] == null)
            {
                UnityEngine.Object.DestroyImmediate(fontAsset);
                throw new BuildFailedException("Jua TMP atlas was not created.");
            }

            // 전부 구운 뒤 정적 모드로 고정 — 런타임 래스터라이즈 경로 제거.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.DeleteAsset(JuaTmpAssetPath);
            AssetDatabase.CreateAsset(fontAsset, JuaTmpAssetPath);
            fontAsset.material.name = "Jua-Static Atlas Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            for (int index = 0; index < atlasTextures.Length; index++)
            {
                if (atlasTextures[index] == null)
                {
                    continue;
                }

                atlasTextures[index].name = index == 0 ? "Jua-Static Atlas" : "Jua-Static Atlas " + (index + 1);
                AssetDatabase.AddObjectToAsset(atlasTextures[index], fontAsset);
            }

            var serializedFont = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicData = serializedFont.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicData != null)
            {
                clearDynamicData.boolValue = false;
                serializedFont.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(JuaTmpAssetPath, ImportAssetOptions.ForceSynchronousImport);

            TMP_FontAsset persisted = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JuaTmpAssetPath);
            if (persisted == null || persisted.material == null || persisted.atlasTextures == null
                || persisted.atlasTextures.Length == 0 || persisted.atlasTextures[0] == null)
            {
                throw new BuildFailedException("Serialized Jua TMP asset validation failed.");
            }

            Debug.Log("[Yeona Pet Build] Jua static font baked with "
                      + persisted.characterTable.Count + " characters.");
        }

        private static string CollectRequiredFontCharacters()
        {
            var characters = new System.Collections.Generic.SortedSet<char>();
            const string baseline =
                " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~×÷·…~♥★";
            foreach (char character in baseline)
            {
                characters.Add(character);
            }

            string runtimeRoot = Path.Combine(Application.dataPath, "YeonaPetPlayhouse", "Runtime");
            if (!Directory.Exists(runtimeRoot))
            {
                throw new BuildFailedException("Runtime source folder was not found at '" + runtimeRoot + "'.");
            }

            AddCharactersFromFiles(characters, runtimeRoot, "*.cs");

            string documentationRoot = Path.Combine(
                Application.dataPath,
                "YeonaPetPlayhouse",
                "Documentation");
            if (!Directory.Exists(documentationRoot))
            {
                throw new BuildFailedException(
                    "Documentation folder was not found at '" + documentationRoot + "'.");
            }

            AddCharactersFromFiles(characters, documentationRoot, "*.md");
            return new string(characters.ToArray());
        }

        private static void AddCharactersFromFiles(
            System.Collections.Generic.SortedSet<char> characters,
            string root,
            string searchPattern)
        {
            foreach (string filePath in Directory.EnumerateFiles(root, searchPattern, SearchOption.AllDirectories))
            {
                foreach (char character in File.ReadAllText(filePath, Encoding.UTF8))
                {
                    if (!char.IsControl(character) && !char.IsSurrogate(character))
                    {
                        characters.Add(character);
                    }
                }
            }
        }

        private static void CreateMinimalScene()
        {
            EnsureAssetFolder("Assets/YeonaPetPlayhouse/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("PetPlayhouseRoot");
            root.AddComponent<PlayhouseBootstrap>();

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PetPalette.Cream;
            camera.orthographic = true;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new BuildFailedException("Unity could not save the generated scene at '" + ScenePath + "'.");
            }
        }

        private static string ResolveOutputPath()
        {
            string requested = Environment.GetEnvironmentVariable("YEONA_PET_APK_PATH");
            if (string.IsNullOrWhiteSpace(requested))
            {
                requested = DefaultRelativeApkPath;
            }

            requested = requested.Trim().Trim('"');
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new BuildFailedException("Could not resolve the Unity project root.");
            }

            string fullPath = Path.IsPathRooted(requested)
                ? Path.GetFullPath(requested)
                : Path.GetFullPath(Path.Combine(projectRoot, requested));
            if (!string.Equals(Path.GetExtension(fullPath), ".apk", StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException("The Android output must end in .apk: '" + fullPath + "'.");
            }

            return fullPath;
        }

        private static void ValidateBuildReport(BuildReport report, string expectedOutputPath)
        {
            if (report == null)
            {
                throw new BuildFailedException("BuildPipeline.BuildPlayer returned a null BuildReport.");
            }

            BuildSummary summary = report.summary;
            Debug.Log("[Yeona Pet Build] Result: " + summary.result + ", errors: " + summary.totalErrors
                      + ", output: " + summary.outputPath);
            if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0)
            {
                throw new BuildFailedException("Yeona Pet Android build failed: " + summary.result);
            }

            if (!File.Exists(expectedOutputPath))
            {
                throw new BuildFailedException("APK was not found at '" + expectedOutputPath + "'.");
            }

            Debug.Log("[Yeona Pet Build] APK ready: " + expectedOutputPath);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            string name = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new BuildFailedException("Invalid asset folder path '" + assetPath + "'.");
            }

            EnsureAssetFolder(parent);
            if (string.IsNullOrWhiteSpace(AssetDatabase.CreateFolder(parent, name)))
            {
                throw new BuildFailedException("Unity could not create asset folder '" + normalized + "'.");
            }
        }
    }
}
#endif
