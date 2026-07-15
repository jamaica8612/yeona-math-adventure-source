using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using YeonaMathAdventure.MathCore;

namespace YeonaMathAdventure
{
    public enum MathActivityKind
    {
        TargetNumber = 0,
        FairShare = 1,
        PatternSpace = 2
    }

    [Serializable]
    public sealed class MathAssessmentOutcomeData
    {
        public MathActivityKind activity;
        public int difficulty = 2;
        public int elapsedMilliseconds;
        public int retries;
        public int hints;

        public bool Normalize()
        {
            if ((int)activity < (int)MathActivityKind.TargetNumber ||
                (int)activity > (int)MathActivityKind.PatternSpace)
            {
                return false;
            }

            difficulty = DifficultyRules.Clamp(difficulty);
            elapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            retries = Math.Max(0, retries);
            hints = Math.Max(0, hints);
            return true;
        }
    }

    [Serializable]
    public sealed class MathRuntimeMetaData
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaximumAssessmentRounds = 6;

        public int schemaVersion = CurrentSchemaVersion;
        public int experienceVersion;
        public bool assessmentComplete;
        public bool assessmentInProgress;
        public int assessmentNextRound;
        public List<MathAssessmentOutcomeData> assessmentOutcomes = new List<MathAssessmentOutcomeData>();
        public int placementLevel = 2;
        public MathActivityKind recommendedActivity = MathActivityKind.TargetNumber;
        public int completedSessions;
        public string selectedRewardId = string.Empty;
        public int seedCounter = 1;
        public long lastSavedAtUnixSeconds;

        public void Normalize()
        {
            schemaVersion = CurrentSchemaVersion;
            experienceVersion = Math.Max(0, experienceVersion);
            placementLevel = DifficultyRules.Clamp(placementLevel);
            if ((int)recommendedActivity < 0 || (int)recommendedActivity > 2)
            {
                recommendedActivity = MathActivityKind.TargetNumber;
            }

            if (completedSessions < 0)
            {
                completedSessions = 0;
            }

            if (selectedRewardId == null)
            {
                selectedRewardId = string.Empty;
            }

            if (seedCounter < 1)
            {
                seedCounter = 1;
            }

            if (assessmentOutcomes == null)
            {
                assessmentOutcomes = new List<MathAssessmentOutcomeData>();
            }

            for (int index = assessmentOutcomes.Count - 1; index >= 0; index--)
            {
                if (assessmentOutcomes[index] == null || !assessmentOutcomes[index].Normalize())
                {
                    assessmentOutcomes.RemoveAt(index);
                }
            }

            if (assessmentComplete || !assessmentInProgress)
            {
                assessmentInProgress = false;
                assessmentNextRound = 0;
                assessmentOutcomes.Clear();
                return;
            }

            assessmentNextRound = Math.Max(0, Math.Min(MaximumAssessmentRounds, assessmentNextRound));
            while (assessmentOutcomes.Count > assessmentNextRound)
            {
                assessmentOutcomes.RemoveAt(assessmentOutcomes.Count - 1);
            }

            if (assessmentNextRound > assessmentOutcomes.Count)
            {
                assessmentNextRound = assessmentOutcomes.Count;
            }
        }
    }

    /// <summary>
    /// Writes within one directory using primary, temporary, and backup names. A process stop at
    /// any rename boundary leaves at least one complete UTF-8 payload for the next launch.
    /// </summary>
    public static class MathAtomicTextFile
    {
        public static bool TryRead(string path, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string[] candidates = { path, path + ".tmp", path + ".bak" };
            for (int index = 0; index < candidates.Length; index++)
            {
                try
                {
                    if (!File.Exists(candidates[index]))
                    {
                        continue;
                    }

                    value = File.ReadAllText(candidates[index], Encoding.UTF8);
                    return true;
                }
                catch (Exception)
                {
                    // Try the next complete recovery candidate.
                }
            }

            value = string.Empty;
            return false;
        }

        public static void Write(string path, string value)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A save path is required.", "path");
            }

            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("The save path must include a directory.", "path");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";

            // Recover the previous primary before beginning another write.
            if (!File.Exists(path) && File.Exists(backupPath))
            {
                File.Move(backupPath, path);
            }

            File.WriteAllText(temporaryPath, value ?? string.Empty, new UTF8Encoding(false));
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            if (File.Exists(path))
            {
                File.Move(path, backupPath);
            }

            try
            {
                File.Move(temporaryPath, path);
            }
            catch
            {
                if (!File.Exists(path) && File.Exists(backupPath))
                {
                    File.Move(backupPath, path);
                }

                throw;
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
    }

    /// <summary>
    /// JsonUtility adapter used by the pure math ProgressRepository. No Antura profile
    /// singleton is required, so the math journey can boot and persist independently.
    /// </summary>
    public sealed class UnityJsonProgressCodec : IProgressCodec
    {
        public bool TryDecode(string text, out MathProgressData progress)
        {
            progress = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                progress = JsonUtility.FromJson<MathProgressData>(text);
                return progress != null;
            }
            catch (Exception)
            {
                progress = null;
                return false;
            }
        }

        public string Encode(MathProgressData progress)
        {
            return progress == null ? string.Empty : JsonUtility.ToJson(progress, true);
        }
    }

    /// <summary>
    /// Keeps every math save under its own persistent-data directory. The key is
    /// sanitized before becoming a file name and writes use a temporary file.
    /// </summary>
    public sealed class MathJsonFileStore : IProgressTextStore
    {
        private readonly string directory;

        public MathJsonFileStore()
        {
            directory = Path.Combine(Application.persistentDataPath, "YeonaMathAdventure");
        }

        public string DirectoryPath
        {
            get { return directory; }
        }

        public bool TryLoad(string key, out string value)
        {
            value = string.Empty;
            try
            {
                string path = Path.Combine(directory, Sanitize(key) + ".json");
                return MathAtomicTextFile.TryRead(path, out value);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Yeona Math progress load skipped: " + exception.Message);
                value = string.Empty;
                return false;
            }
        }

        public void Save(string key, string value)
        {
            string path = Path.Combine(directory, Sanitize(key) + ".json");
            MathAtomicTextFile.Write(path, value);
        }

        private static string Sanitize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "default";
            }

            char[] characters = key.ToCharArray();
            for (int index = 0; index < characters.Length; index++)
            {
                char character = characters[index];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    characters[index] = '_';
                }
            }

            return new string(characters);
        }
    }

    public sealed class MathRuntimeMetaStore
    {
        private readonly string path;

        public MathRuntimeMetaStore(string directory)
        {
            path = Path.Combine(directory, "math_runtime_meta_v1.json");
        }

        public MathRuntimeMetaData LoadOrCreate()
        {
            try
            {
                string json;
                if (MathAtomicTextFile.TryRead(path, out json))
                {
                    MathRuntimeMetaData loaded = JsonUtility.FromJson<MathRuntimeMetaData>(
                        json);
                    if (loaded != null && loaded.schemaVersion == MathRuntimeMetaData.CurrentSchemaVersion)
                    {
                        loaded.Normalize();
                        return loaded;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Yeona Math session metadata load skipped: " + exception.Message);
            }

            return new MathRuntimeMetaData();
        }

        public void Save(MathRuntimeMetaData data)
        {
            if (data == null)
            {
                return;
            }

            data.Normalize();
            data.lastSavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            MathAtomicTextFile.Write(path, JsonUtility.ToJson(data, true));
        }
    }

    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform target;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void Awake()
        {
            target = transform as RectTransform;
            Apply();
        }

        private void Update()
        {
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || size != lastScreenSize)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (target == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            Vector2 minimum;
            Vector2 maximum;
            CalculateAnchors(safeArea, Screen.width, Screen.height, out minimum, out maximum);
            target.anchorMin = minimum;
            target.anchorMax = maximum;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }

        public static void CalculateAnchors(Rect safeArea, int screenWidth, int screenHeight, out Vector2 minimum,
            out Vector2 maximum)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                minimum = Vector2.zero;
                maximum = Vector2.one;
                return;
            }

            minimum = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            maximum = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);
        }
    }

    public static class KoreanFontProvider
    {
        private const string PrebuiltFontResourcePath = "YeonaMathAdventure/Fonts/Pretendard-Dynamic";

        private static TMP_FontAsset cached;
        private static bool resolutionAttempted;
        private static bool fallbackReported;

        public static TMP_FontAsset Resolve()
        {
            if (resolutionAttempted)
            {
                return cached;
            }

            resolutionAttempted = true;

            cached = Resources.Load<TMP_FontAsset>(PrebuiltFontResourcePath);
            if (cached != null)
            {
                ConfigureDynamicFont(cached);
                return cached;
            }

            string[] assetPaths =
            {
                "YeonaMathAdventure/Fonts/Pretendard-Regular SDF",
                "YeonaMathAdventure/Fonts/Pretendard",
                "Fonts/Pretendard-Dynamic",
                "Fonts/Pretendard-Regular SDF",
                "Fonts/Pretendard",
                "Pretendard-Dynamic",
                "Pretendard-Regular SDF",
                "Pretendard"
            };
            for (int index = 0; index < assetPaths.Length; index++)
            {
                cached = Resources.Load<TMP_FontAsset>(assetPaths[index]);
                if (cached != null)
                {
                    ConfigureDynamicFont(cached);
                    return cached;
                }
            }

            string[] fontPaths =
            {
                "YeonaMathAdventure/Fonts/Pretendard",
                "YeonaMathAdventure/Fonts/Pretendard-Regular",
                "Fonts/Pretendard",
                "Fonts/Pretendard-Regular",
                "Pretendard-Regular",
                "Pretendard"
            };
            bool sourceFontFound = false;
            Exception creationFailure = null;
            for (int index = 0; index < fontPaths.Length; index++)
            {
                Font source = Resources.Load<Font>(fontPaths[index]);
                if (source == null)
                {
                    continue;
                }

                sourceFontFound = true;
                try
                {
                    TMP_FontAsset runtimeFont = TMP_FontAsset.CreateFontAsset(source);
                    if (runtimeFont == null)
                    {
                        break;
                    }

                    ConfigureDynamicFont(runtimeFont);
                    runtimeFont.name = "Pretendard Runtime Dynamic";
                    cached = runtimeFont;
                    return cached;
                }
                catch (Exception exception)
                {
                    creationFailure = exception;
                    break;
                }
            }

            cached = TMP_Settings.defaultFontAsset;
            ReportFallbackOnce(sourceFontFound, creationFailure, cached);
            return cached;
        }

        private static void ConfigureDynamicFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset.sourceFontFile == null)
            {
                return;
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
        }

        private static void ReportFallbackOnce(bool sourceFontFound, Exception creationFailure,
            TMP_FontAsset fallback)
        {
            if (fallbackReported)
            {
                return;
            }

            fallbackReported = true;
            string reason = creationFailure != null
                ? $"runtime TMP font creation failed: {creationFailure.GetType().Name}: {creationFailure.Message}"
                : sourceFontFound
                    ? "TMP_FontAsset.CreateFontAsset returned null"
                    : "no Pretendard source font was found in Resources";
            string fallbackDescription = fallback != null
                ? $"TMP default font '{fallback.name}'"
                : "no font (TMP default font is also missing)";

            Debug.LogError(
                $"[Yeona Math Adventure] Korean font unavailable. Expected prebuilt TMP font at " +
                $"Resources/{PrebuiltFontResourcePath}.asset; {reason}. Falling back to {fallbackDescription}. " +
                "Korean text may not render until the prebuilt Pretendard TMP font asset is generated.");
        }
    }

    public interface IMathRewardBridge
    {
        bool TryAward(int amount);
    }

    public sealed class NoOpMathRewardBridge : IMathRewardBridge
    {
        public bool TryAward(int amount)
        {
            return false;
        }
    }

}
