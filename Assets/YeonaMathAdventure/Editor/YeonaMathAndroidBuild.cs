#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace YeonaMathAdventure.Editor
{
    /// <summary>
    /// Creates a minimal Math Journey scene, applies the Android settings for
    /// the first playable APK, and builds a single debug APK.
    ///
    /// Command line entry points:
    ///   YeonaMathAdventure.Editor.YeonaMathAndroidBuild.SetupProject
    ///   YeonaMathAdventure.Editor.YeonaMathAndroidBuild.BuildAndroid
    /// </summary>
    public static class YeonaMathAndroidBuild
    {
        public const string MathScenePath = "Assets/YeonaMathAdventure/Scenes/math_Journey.unity";
        public const string PackageName = "com.yeona.mathadventure";
        public const string ProductName = "Yeona Math Adventure";
        public const string DefaultRelativeApkPath =
            "Builds/YeonaMathAdventure/YeonaMathAdventure-v0.2.0.apk";
        public const string PretendardSourceFontPath =
            "Assets/YeonaMathAdventure/Resources/Fonts/Pretendard-Regular.otf";
        public const string PretendardTmpAssetPath =
            "Assets/YeonaMathAdventure/Resources/YeonaMathAdventure/Fonts/Pretendard-Dynamic.asset";
        public const string ApplicationIconPath =
            "Assets/YeonaMathAdventure/Artwork/YeonaMathAdventure-AppIcon-FullBleed.png";
        private const string AnalyticsSourceRelativeToAssets =
            "_core/_scripts/_Core/Services/Analytics/OnlineAnalytics.cs";

        private static readonly string[] RuntimeBootstrapTypeNames =
        {
            "YeonaMathAdventure.MathJourneyBootstrap"
        };

        [MenuItem("Yeona Math/Setup/Configure Project")]
        public static void SetupProject()
        {
            ConfigurePlayerSettings();
            CreatePersistentPretendardFontAsset();
            CreateMinimalMathScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MathScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Yeona Math Build] Project configured.\n" +
                $"Product: {ProductName}\n" +
                $"Package: {PackageName}\n" +
                $"Only build scene: {MathScenePath}");
        }

        [MenuItem("Yeona Math/Build/Android Debug APK")]
        public static void BuildAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                throw new BuildFailedException(
                    "Android is not the active build target. Start Unity with '-buildTarget Android' before invoking BuildAndroid; switching here can trigger a domain reload and abort -executeMethod.");
            }

            SetupProject();
            RequirePlayableRuntime();
            RequirePrivacyGuard();
            YeonaAndroidExternalTools.ConfigureFromEnvironment();

            string outputPath = ResolveOutputPath();
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new BuildFailedException($"Could not determine the APK output directory from '{outputPath}'.");
            }
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MathScenePath },
                locationPathName = outputPath,
                targetGroup = BuildTargetGroup.Android,
                target = BuildTarget.Android,
                options = BuildOptions.DetailedBuildReport
                          | BuildOptions.CompressWithLz4
            };

            Debug.Log($"[Yeona Math Build] Building Android APK at '{outputPath}'.");
            BuildReport report;
            AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettings.PlayerBuildOption previousAddressablesBuildOption =
                AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            bool restoreAddressablesBuildOption = false;
            try
            {
                if (addressableSettings != null)
                {
                    previousAddressablesBuildOption = addressableSettings.BuildAddressablesWithPlayerBuild;
                    restoreAddressablesBuildOption = previousAddressablesBuildOption !=
                                                     AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                    if (restoreAddressablesBuildOption)
                    {
                        // Math Journey has no addressable dependency. Avoid bundling every Antura
                        // language pack, but restore the original language-project setting after
                        // this dedicated build so the upstream learning route remains intact.
                        addressableSettings.BuildAddressablesWithPlayerBuild =
                            AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                        EditorUtility.SetDirty(addressableSettings);
                        AssetDatabase.SaveAssets();
                        Debug.Log("[Yeona Math Build] Temporarily disabled Addressables player building.");
                    }
                }

                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                if (restoreAddressablesBuildOption && addressableSettings != null)
                {
                    addressableSettings.BuildAddressablesWithPlayerBuild = previousAddressablesBuildOption;
                    EditorUtility.SetDirty(addressableSettings);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[Yeona Math Build] Restored the upstream Addressables player-build setting.");
                }
            }

            ValidateBuildReport(report, outputPath);
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Yeona Learning";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            // Keep the playable test APK on the modern 64-bit Android path. This halves
            // native compilation/package work and matches current phones, tablets, and Play policy.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            ConfigureApplicationIcon();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.Android.bundleVersionCode = 3;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Debug signing: never depend on the upstream machine-specific keystore.
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = string.Empty;
            PlayerSettings.Android.keystorePass = string.Empty;
            PlayerSettings.Android.keyaliasName = string.Empty;
            PlayerSettings.Android.keyaliasPass = string.Empty;

            // Produce one installable APK, not an AAB, per-ABI APKs, or APK+OBB.
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = false;
            PlayerSettings.Android.resizeableActivity = true;
            PlayerSettings.Android.minifyDebug = false;
            // The dedicated child-facing APK is local-first and stores data only in its
            // package-private persistent-data directory. Do not inherit Antura's legacy
            // project-wide requests for network or external-storage access.
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;

            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.androidBuildType = AndroidBuildType.Debug;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;

            string configuredPackage = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (!string.Equals(configuredPackage, PackageName, StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    $"Android package isolation check failed. Expected '{PackageName}', configured '{configuredPackage}'.");
            }
        }

        /// <summary>
        /// Serializes Pretendard's TMP material and atlas before the player build.
        /// Creating this object from the OTF on Android caused TMP's runtime font
        /// setup to return null on some devices, leaving every label invisible and
        /// producing a repeated NullReferenceException in development builds.
        /// </summary>
        private static void CreatePersistentPretendardFontAsset()
        {
            EnsureAssetFolder("Assets/YeonaMathAdventure/Resources/YeonaMathAdventure/Fonts");

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(PretendardSourceFontPath);
            if (sourceFont == null)
            {
                throw new BuildFailedException(
                    $"Pretendard source font was not found at '{PretendardSourceFontPath}'.");
            }

            TrueTypeFontImporter importer = AssetImporter.GetAtPath(PretendardSourceFontPath) as TrueTypeFontImporter;
            if (importer == null || !importer.includeFontData)
            {
                throw new BuildFailedException(
                    "Pretendard must be imported with Include Font Data enabled so its serialized TMP asset " +
                    "can retain a valid source face on Android.");
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                64,
                6,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
            {
                throw new BuildFailedException(
                    $"TextMesh Pro could not create a font asset from '{PretendardSourceFontPath}'.");
            }

            fontAsset.name = "Pretendard-Dynamic";
            fontAsset.isMultiAtlasTexturesEnabled = true;

            string requiredCharacters = CollectRequiredFontCharacters();
            if (!fontAsset.TryAddCharacters(requiredCharacters, out string missingCharacters))
            {
                string missingKoreanOrDigits = new string(missingCharacters
                    .Where(character => (character >= '\uAC00' && character <= '\uD7A3')
                                        || (character >= '0' && character <= '9'))
                    .Distinct()
                    .ToArray());
                if (!string.IsNullOrEmpty(missingKoreanOrDigits))
                {
                    UnityEngine.Object.DestroyImmediate(fontAsset);
                    throw new BuildFailedException(
                        "Pretendard could not pre-render required Korean or numeric glyphs: " +
                        missingKoreanOrDigits);
                }

                Debug.LogWarning(
                    "[Yeona Math Build] Pretendard does not contain some optional source symbols: " +
                    missingCharacters);
            }

            if (fontAsset.material == null || fontAsset.material.shader == null)
            {
                UnityEngine.Object.DestroyImmediate(fontAsset);
                throw new BuildFailedException(
                    "Pretendard TMP material or its distance-field shader was not created.");
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures == null || atlasTextures.Length == 0 || atlasTextures[0] == null)
            {
                UnityEngine.Object.DestroyImmediate(fontAsset);
                throw new BuildFailedException("Pretendard TMP atlas was not created.");
            }

            // 필요한 글리프를 전부 구운 뒤에는 정적 아틀라스로 고정한다. Dynamic 모드는
            // 실행 시마다 FontEngine이 소스 폰트를 다시 여는데, 일부 기기에서 간헐적으로
            // 실패해 모든 라벨이 사라지는 회귀(재실행 시 빈 텍스트)를 일으켰다. 새 문자열을
            // 추가하면 SetupProject가 다시 돌며 재수집하므로 정적으로도 누락이 없다.
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            // Recreate deterministically so SetupProject also repairs a stale or
            // partially generated font asset from an interrupted previous run.
            AssetDatabase.DeleteAsset(PretendardTmpAssetPath);
            AssetDatabase.CreateAsset(fontAsset, PretendardTmpAssetPath);

            fontAsset.material.name = "Pretendard-Dynamic Atlas Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            for (int index = 0; index < atlasTextures.Length; index++)
            {
                Texture2D atlas = atlasTextures[index];
                if (atlas == null)
                {
                    continue;
                }

                atlas.name = index == 0
                    ? "Pretendard-Dynamic Atlas"
                    : $"Pretendard-Dynamic Atlas {index + 1}";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            var serializedFont = new SerializedObject(fontAsset);
            SerializedProperty clearDynamicData = serializedFont.FindProperty("m_ClearDynamicDataOnBuild");
            if (clearDynamicData == null)
            {
                throw new BuildFailedException(
                    "Could not disable TextMesh Pro's pre-build dynamic-atlas clearing for Pretendard.");
            }
            clearDynamicData.boolValue = false;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(PretendardTmpAssetPath, ImportAssetOptions.ForceSynchronousImport);

            TMP_FontAsset persisted = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PretendardTmpAssetPath);
            if (persisted == null
                || persisted.sourceFontFile == null
                || persisted.material == null
                || persisted.material.shader == null
                || persisted.atlasTextures == null
                || persisted.atlasTextures.Length == 0
                || persisted.atlasTextures[0] == null)
            {
                throw new BuildFailedException(
                    $"Serialized Pretendard TMP asset validation failed at '{PretendardTmpAssetPath}'.");
            }

            Debug.Log(
                $"[Yeona Math Build] Serialized Pretendard TMP font with {persisted.characterTable.Count} " +
                $"pre-rendered character(s) and {persisted.atlasTextures.Length} atlas texture(s).");
        }

        private static string CollectRequiredFontCharacters()
        {
            var characters = new System.Collections.Generic.SortedSet<char>();
            const string baseline =
                " 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~" +
                "×÷±≠≤≥→←↔•…·₩℃°";
            foreach (char character in baseline)
            {
                characters.Add(character);
            }

            string runtimeRoot = Path.Combine(Application.dataPath, "YeonaMathAdventure", "Runtime");
            if (!Directory.Exists(runtimeRoot))
            {
                throw new BuildFailedException(
                    $"Math Journey runtime source folder was not found at '{runtimeRoot}'.");
            }

            foreach (string filePath in Directory.EnumerateFiles(runtimeRoot, "*.*", SearchOption.AllDirectories)
                         .Where(path => string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (char character in File.ReadAllText(filePath, Encoding.UTF8))
                {
                    if (!char.IsControl(character) && !char.IsSurrogate(character))
                    {
                        characters.Add(character);
                    }
                }
            }

            return new string(characters.ToArray());
        }

        private static void ConfigureApplicationIcon()
        {
            // Unity's default texture importer scales the 1254px NPOT source down to
            // 1024px. Preserve the independently verified source dimensions before
            // assigning it to Android icon slots.
            TextureImporter importer = AssetImporter.GetAtPath(ApplicationIconPath) as TextureImporter;
            if (importer != null)
            {
                bool importerChanged = importer.maxTextureSize < 2048
                                       || importer.npotScale != TextureImporterNPOTScale.None
                                       || importer.mipmapEnabled
                                       || importer.textureCompression != TextureImporterCompression.Uncompressed;
                if (importerChanged)
                {
                    importer.maxTextureSize = 2048;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(ApplicationIconPath);
            if (icon == null)
            {
                throw new BuildFailedException(
                    $"Yeona Math application icon was not found at '{ApplicationIconPath}'. " +
                    "Copy the staged independently-generated full-bleed PNG before setup/build.");
            }

            if (icon.width != 1254 || icon.height != 1254)
            {
                throw new BuildFailedException(
                    $"Unexpected Yeona Math application icon dimensions {icon.width}x{icon.height}; expected 1254x1254.");
            }

            int[] iconSizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Android, IconKind.Application);
            int slotCount = iconSizes == null || iconSizes.Length == 0 ? 1 : iconSizes.Length;
            Texture2D[] icons = Enumerable.Repeat(icon, slotCount).ToArray();
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);
            Debug.Log(
                $"[Yeona Math Build] Configured Android application/default icon from '{ApplicationIconPath}' " +
                $"for {slotCount} legacy application icon slot(s).");
        }

        private static void CreateMinimalMathScene()
        {
            EnsureAssetFolder("Assets/YeonaMathAdventure");
            EnsureAssetFolder("Assets/YeonaMathAdventure/Scenes");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("YeonaMathJourneyRoot");
            Type runtimeBootstrap = FindRuntimeBootstrapType();
            if (runtimeBootstrap != null)
            {
                root.AddComponent(runtimeBootstrap);
                Debug.Log($"[Yeona Math Build] Attached runtime bootstrap '{runtimeBootstrap.FullName}'.");
            }

            string anturaStartupStatus;
            if (!AnturaAppManagerSceneInstaller.TryInstallCoreServices(scene, out anturaStartupStatus))
            {
                Debug.LogWarning(
                    "[Yeona Math Build] Antura core services unavailable (" + anturaStartupStatus +
                    "); the APK will use the local math profile.");
            }

            CreateCamera();
            if (runtimeBootstrap == null)
            {
                CreatePlaceholderCanvas(root.transform);
                Debug.LogWarning(
                    "[Yeona Math Build] MathJourneyBootstrap is not compiled yet. The generated scene contains a responsive placeholder UI so the staging APK remains runnable.");
            }

            if (!EditorSceneManager.SaveScene(scene, MathScenePath))
            {
                throw new BuildFailedException($"Unity could not save the generated Math Journey scene at '{MathScenePath}'.");
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.09f, 0.18f, 1f);
            camera.orthographic = true;
        }

        private static void CreatePlaceholderCanvas(Transform parent)
        {
            var canvasObject = new GameObject(
                "Responsive Placeholder",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasObject.transform, false);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.055f, 0.09f, 0.18f, 1f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateText(
                background.transform,
                "Title",
                "Yeona Math Adventure",
                font,
                88,
                new Vector2(0.08f, 0.53f),
                new Vector2(0.92f, 0.76f),
                new Color(0.98f, 0.84f, 0.28f, 1f),
                FontStyle.Bold);
            CreateText(
                background.transform,
                "Subtitle",
                "Math Journey",
                font,
                48,
                new Vector2(0.12f, 0.37f),
                new Vector2(0.88f, 0.52f),
                Color.white,
                FontStyle.Normal);
            CreateText(
                background.transform,
                "Status",
                "Runtime content is being prepared",
                font,
                30,
                new Vector2(0.12f, 0.24f),
                new Vector2(0.88f, 0.36f),
                new Color(0.69f, 0.78f, 0.9f, 1f),
                FontStyle.Normal);
        }

        private static void CreateText(
            Transform parent,
            string objectName,
            string value,
            Font font,
            int fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color,
            FontStyle style)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = fontSize;
        }

        private static Type FindRuntimeBootstrapType()
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string typeName in RuntimeBootstrapTypeNames)
                {
                    Type type = assembly.GetType(typeName, false);
                    if (type != null
                        && typeof(MonoBehaviour).IsAssignableFrom(type)
                        && !type.IsAbstract)
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        private static void RequirePlayableRuntime()
        {
            Type runtimeBootstrap = FindRuntimeBootstrapType();
            if (runtimeBootstrap == null ||
                !string.Equals(runtimeBootstrap.FullName, RuntimeBootstrapTypeNames[0], StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "Playable runtime bootstrap 'YeonaMathAdventure.MathJourneyBootstrap' is not compiled. " +
                    "Refusing to produce a placeholder APK.");
            }

            Scene scene = EditorSceneManager.OpenScene(MathScenePath, OpenSceneMode.Single);
            bool attached = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren(runtimeBootstrap, true))
                .Any();
            if (!attached)
            {
                throw new BuildFailedException(
                    $"Generated scene '{MathScenePath}' does not contain '{runtimeBootstrap.FullName}'.");
            }
        }

        private static void RequirePrivacyGuard()
        {
            string analyticsSourcePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                AnalyticsSourceRelativeToAssets.Replace('/', Path.DirectorySeparatorChar)));
            if (!File.Exists(analyticsSourcePath))
            {
                throw new BuildFailedException(
                    "Antura OnlineAnalytics source was not found. Refusing to build without the " +
                    "dedicated child-facing package privacy guard.");
            }

            string source = File.ReadAllText(analyticsSourcePath);
            int packageDeclaration = source.IndexOf(
                "private const string YeonaMathPackageIdentifier = \"com.yeona.mathadventure\";",
                StringComparison.Ordinal);
            int guardStart = source.IndexOf("if (IsDedicatedYeonaMathPackage)", StringComparison.Ordinal);
            int servicesInitialization = source.IndexOf("UnityServices.InitializeAsync", StringComparison.Ordinal);
            bool guardPrecedesInitialization = packageDeclaration >= 0
                                               && guardStart >= 0
                                               && servicesInitialization > guardStart;
            if (guardPrecedesInitialization)
            {
                string guardBody = source.Substring(guardStart, servicesInitialization - guardStart);
                guardPrecedesInitialization = guardBody.IndexOf("enabled = false;", StringComparison.Ordinal) >= 0
                                               && guardBody.IndexOf("return;", StringComparison.Ordinal) >= 0;
            }

            if (!guardPrecedesInitialization)
            {
                throw new BuildFailedException(
                    "Required OnlineAnalytics package guard is absent or does not return before Unity Services " +
                    "initialization. Apply staging_privacy_patch/OnlineAnalytics.PackageGuard.patch first.");
            }

            Debug.Log("[Yeona Math Build] Verified the package-specific OnlineAnalytics privacy guard.");
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
                throw new BuildFailedException($"Invalid asset folder path '{assetPath}'.");
            }

            EnsureAssetFolder(parent);
            string guid = AssetDatabase.CreateFolder(parent, name);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new BuildFailedException($"Unity could not create asset folder '{normalized}'.");
            }
        }

        private static string ResolveOutputPath()
        {
            string requested = GetCommandLineValue("-yeonaOutput");
            if (string.IsNullOrWhiteSpace(requested))
            {
                requested = Environment.GetEnvironmentVariable("YEONA_APK_PATH");
            }
            if (string.IsNullOrWhiteSpace(requested))
            {
                requested = DefaultRelativeApkPath;
            }

            requested = requested.Trim().Trim('"');
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new BuildFailedException("Could not resolve the Unity project root from Application.dataPath.");
            }

            string fullPath = Path.IsPathRooted(requested)
                ? Path.GetFullPath(requested)
                : Path.GetFullPath(Path.Combine(projectRoot, requested));
            if (!string.Equals(Path.GetExtension(fullPath), ".apk", StringComparison.OrdinalIgnoreCase))
            {
                throw new BuildFailedException($"The Android output must end in .apk. Resolved path: '{fullPath}'.");
            }

            return fullPath;
        }

        private static string GetCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void ValidateBuildReport(BuildReport report, string expectedOutputPath)
        {
            if (report == null)
            {
                throw new BuildFailedException("BuildPipeline.BuildPlayer returned a null BuildReport.");
            }

            BuildSummary summary = report.summary;
            Debug.Log(
                "[Yeona Math Build] Build report.\n" +
                $"Result: {summary.result}\n" +
                $"Errors: {summary.totalErrors}\n" +
                $"Warnings: {summary.totalWarnings}\n" +
                $"Time: {summary.totalTime}\n" +
                $"Size: {summary.totalSize} bytes\n" +
                $"Output: {summary.outputPath}");

            if (summary.result != BuildResult.Succeeded || summary.totalErrors > 0)
            {
                throw new BuildFailedException(
                    $"Yeona Math Android build failed. Result={summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}, output='{summary.outputPath}'.");
            }

            if (!File.Exists(expectedOutputPath))
            {
                throw new BuildFailedException(
                    $"Unity reported a successful build, but the APK was not found at '{expectedOutputPath}'. Reported output: '{summary.outputPath}'.");
            }

            Debug.Log($"[Yeona Math Build] APK ready: {expectedOutputPath}");
        }
    }

    /// <summary>
    /// Keeps the NativeGallery Java bridge available to Antura's optional services while
    /// removing its obsolete shared-storage permissions from this package's merged manifest.
    /// The math runtime writes only to Application.persistentDataPath.
    /// </summary>
    public sealed class YeonaAndroidManifestSanitizer : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string ToolsNamespace = "http://schemas.android.com/tools";
        private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

        private static readonly string[] RemovedPermissions =
        {
            "android.permission.READ_EXTERNAL_STORAGE",
            "android.permission.WRITE_EXTERNAL_STORAGE"
        };

        public int callbackOrder
        {
            get { return 10000; }
        }

        public void OnPostGenerateGradleAndroidProject(string basePath)
        {
            string packageName = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (!string.Equals(packageName, YeonaMathAndroidBuild.PackageName, StringComparison.Ordinal))
            {
                return;
            }

            string manifestPath = Path.Combine(basePath, "src", "main", "AndroidManifest.xml");
            SanitizeGeneratedManifest(manifestPath);

            Debug.Log(
                "[Yeona Math Build] Removed legacy shared-storage permissions from the generated Android manifest.");
        }

        public static void SanitizeGeneratedManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException(
                    $"Generated Android manifest was not found at '{manifestPath}'. " +
                    "Refusing to build without the Math Journey storage-permission guard.");
            }

            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);
            XmlElement manifest = document.DocumentElement;
            if (manifest == null || !string.Equals(manifest.LocalName, "manifest", StringComparison.Ordinal))
            {
                throw new BuildFailedException($"Invalid generated Android manifest at '{manifestPath}'.");
            }

            EnsureNamespaceDeclaration(document, manifest, "tools", ToolsNamespace);
            for (int index = 0; index < RemovedPermissions.Length; index++)
            {
                MarkPermissionForRemoval(document, manifest, RemovedPermissions[index]);
            }

            XmlElement application = manifest.ChildNodes
                .OfType<XmlElement>()
                .FirstOrDefault(element => string.Equals(element.LocalName, "application", StringComparison.Ordinal));
            if (application != null)
            {
                SetAttribute(document, application, "android", "requestLegacyExternalStorage", AndroidNamespace,
                    "false");
                string replacements = application.GetAttribute("replace", ToolsNamespace);
                const string legacyStorageAttribute = "android:requestLegacyExternalStorage";
                bool alreadyReplaced = replacements.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(value => string.Equals(value.Trim(), legacyStorageAttribute, StringComparison.Ordinal));
                if (!alreadyReplaced)
                {
                    replacements = string.IsNullOrWhiteSpace(replacements)
                        ? legacyStorageAttribute
                        : replacements + "," + legacyStorageAttribute;
                    SetAttribute(document, application, "tools", "replace", ToolsNamespace, replacements);
                }
            }

            var writerSettings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = false
            };
            using (XmlWriter writer = XmlWriter.Create(manifestPath, writerSettings))
            {
                document.Save(writer);
            }
        }

        private static void MarkPermissionForRemoval(XmlDocument document, XmlElement manifest, string permission)
        {
            XmlElement permissionElement = manifest.ChildNodes
                .OfType<XmlElement>()
                .FirstOrDefault(element =>
                    string.Equals(element.LocalName, "uses-permission", StringComparison.Ordinal) &&
                    string.Equals(element.GetAttribute("name", AndroidNamespace), permission,
                        StringComparison.Ordinal));
            if (permissionElement == null)
            {
                permissionElement = document.CreateElement("uses-permission");
                XmlElement application = manifest.ChildNodes
                    .OfType<XmlElement>()
                    .FirstOrDefault(element =>
                        string.Equals(element.LocalName, "application", StringComparison.Ordinal));
                if (application == null)
                {
                    manifest.AppendChild(permissionElement);
                }
                else
                {
                    manifest.InsertBefore(permissionElement, application);
                }
            }

            SetAttribute(document, permissionElement, "android", "name", AndroidNamespace, permission);
            SetAttribute(document, permissionElement, "tools", "node", ToolsNamespace, "remove");
        }

        private static void EnsureNamespaceDeclaration(
            XmlDocument document,
            XmlElement element,
            string prefix,
            string namespaceUri)
        {
            if (string.Equals(element.GetNamespaceOfPrefix(prefix), namespaceUri, StringComparison.Ordinal))
            {
                return;
            }

            XmlAttribute declaration = document.CreateAttribute("xmlns", prefix, XmlnsNamespace);
            declaration.Value = namespaceUri;
            element.SetAttributeNode(declaration);
        }

        private static void SetAttribute(
            XmlDocument document,
            XmlElement element,
            string prefix,
            string localName,
            string namespaceUri,
            string value)
        {
            XmlAttribute attribute = document.CreateAttribute(prefix, localName, namespaceUri);
            attribute.Value = value;
            element.SetAttributeNode(attribute);
        }
    }
}
#endif
