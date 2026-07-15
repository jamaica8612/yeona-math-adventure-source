using Antura.Core;
using Antura.Profile;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace YeonaMathAdventure.AnturaBridge
{
    /// <summary>
    /// Optional bridge to Antura's profile/reward shell. Math play is local-first: Awake marks
    /// local JSON play ready immediately, and Antura attachment happens asynchronously.
    /// This file is intended for the existing Assembly-CSharp assembly (no asmdef).
    /// </summary>
    [DefaultExecutionOrder(-700)]
    public sealed class MathAnturaBridge : MonoBehaviour
    {
        public const string ExpectedPackageIdentifier = "com.yeona.mathadventure";
        public const string InternalProfileMarker = "__yeona_math_internal_v1__";
        public const string MathLocalProfileId = "yeona_local";
        public const int MaximumBonesPerSuccess = 5;

        private const string DataFolderName = "YeonaMathAdventure";
        private const string BridgeStateFileName = "antura_bridge_v1.json";
        // Must match ProgressRepository("yeona_local") through MathJsonFileStore.Sanitize.
        private const string LocalProgressFileName = "yeona_math_progress_v1_yeona_local.json";

        public static MathAnturaBridge Instance { get; private set; }

        [SerializeField]
        [Min(0f)]
        private float anturaWaitTimeoutSeconds = 30f;

        [SerializeField]
        private bool tryAttachToAntura = true;

        public bool IsReadyForMathPlay { get; private set; }
        public bool AnturaAttemptFinished { get; private set; }
        public MathAnturaMode Mode { get; private set; }
        public string StatusCode { get; private set; }
        public string LocalProgressPath { get; private set; }

        public event Action<MathAnturaAttachOutcome> AnturaAttachmentChanged;

        private MathAnturaBridgeState state;
        private IMathAnturaStateStore stateStore;
        private AnturaProfileGateway gateway;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBridgeExists()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject bridgeObject = new GameObject("[YeonaMathAnturaBridge]");
            bridgeObject.AddComponent<MathAnturaBridge>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Math gameplay is never gated by Antura. Local JSON is the primary durable path.
            IsReadyForMathPlay = true;
            Mode = MathAnturaMode.LocalJsonOnly;
            StatusCode = "local_json_ready";
            state = new MathAnturaBridgeState();
            state.Normalize(ExpectedPackageIdentifier, InternalProfileMarker);

            if (!HasExpectedPackageIdentifier())
            {
                // Do not write anything when this component is accidentally included in another APK.
                StatusCode = "package_identifier_mismatch";
                AnturaAttemptFinished = true;
                return;
            }

            string dataFolder = Path.Combine(Application.persistentDataPath, DataFolderName);
            LocalProgressPath = Path.Combine(dataFolder, LocalProgressFileName);
            stateStore = new UnityJsonStateStore(Path.Combine(dataFolder, BridgeStateFileName));

            string stateError;
            MathAnturaBridgeState loadedState;
            if (stateStore.TryLoad(out loadedState, out stateError) && loadedState != null)
            {
                state = loadedState;
                state.Normalize(ExpectedPackageIdentifier, InternalProfileMarker);
            }

            string progressError;
            if (!EnsureLocalProgressFile(LocalProgressPath, out progressError))
            {
                // The game can still retain this session in memory and retry persistence later.
                StatusCode = progressError;
            }
        }

        private void Start()
        {
            if (Instance != this || AnturaAttemptFinished)
            {
                return;
            }

            if (!tryAttachToAntura)
            {
                CompleteAsLocalOnly("antura_attach_disabled");
                return;
            }

            gateway = new AnturaProfileGateway();
            StartCoroutine(TryAttachWhenLoaded());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Called by a completed math activity. Antura rewards are optional; false means the
        /// caller should keep the reward only in local MathProgressData.
        /// </summary>
        public bool TryGrantMathSuccessBones(int requestedBones = 2)
        {
            if (Mode != MathAnturaMode.AnturaAttached || gateway == null || !gateway.IsReady)
            {
                return false;
            }

            int bones = Mathf.Clamp(requestedBones, 1, MaximumBonesPerSuccess);
            string errorCode;
            if (!gateway.TryAddBonesAndSave(bones, out errorCode))
            {
                StatusCode = string.IsNullOrEmpty(errorCode) ? "antura_reward_failed" : errorCode;
                return false;
            }

            state.totalAnturaBonesGranted += bones;
            state.lastStatusCode = "antura_reward_saved";
            StatusCode = state.lastStatusCode;
            SaveStateBestEffort();
            return true;
        }

        /// <summary>
        /// Optional route into Antura UI. It is unavailable in the single-scene Math APK build,
        /// and will only execute when app_Home is included and no transition is already running.
        /// </summary>
        public bool TryNavigateToAnturaHome()
        {
            if (Mode != MathAnturaMode.AnturaAttached || gateway == null || !gateway.CanNavigateHome)
            {
                return false;
            }

            string errorCode;
            if (!gateway.TryNavigateHome(out errorCode))
            {
                StatusCode = string.IsNullOrEmpty(errorCode) ? "antura_navigation_failed" : errorCode;
                return false;
            }

            StatusCode = "antura_navigation_started";
            return true;
        }

        private IEnumerator TryAttachWhenLoaded()
        {
            float startedAt = Time.realtimeSinceStartup;
            while (!gateway.IsReady)
            {
                if (Time.realtimeSinceStartup - startedAt >= anturaWaitTimeoutSeconds)
                {
                    break;
                }

                yield return null;
            }

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackageIdentifier,
                Application.identifier,
                InternalProfileMarker,
                state,
                gateway);

            Mode = outcome.mode;
            StatusCode = outcome.statusCode;
            AnturaAttemptFinished = true;
            SaveStateBestEffort();

            Action<MathAnturaAttachOutcome> handler = AnturaAttachmentChanged;
            if (handler != null)
            {
                handler(outcome);
            }
        }

        private void CompleteAsLocalOnly(string statusCode)
        {
            Mode = MathAnturaMode.LocalJsonOnly;
            StatusCode = statusCode;
            state.anturaAttachedLastRun = false;
            state.lastStatusCode = statusCode;
            AnturaAttemptFinished = true;
            SaveStateBestEffort();
        }

        private bool HasExpectedPackageIdentifier()
        {
            return string.Equals(Application.identifier, ExpectedPackageIdentifier, StringComparison.Ordinal);
        }

        private void SaveStateBestEffort()
        {
            if (stateStore == null || state == null || !HasExpectedPackageIdentifier())
            {
                return;
            }

            string ignoredError;
            stateStore.TrySave(state, out ignoredError);
        }

        private static bool EnsureLocalProgressFile(string path, out string errorCode)
        {
            errorCode = string.Empty;
            try
            {
                if (File.Exists(path))
                {
                    return true;
                }

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                LocalProgressSeed seed = new LocalProgressSeed();
                string json = JsonUtility.ToJson(seed);
                File.WriteAllText(path, json, new UTF8Encoding(false));
                return true;
            }
            catch (Exception)
            {
                errorCode = "local_progress_file_unavailable";
                return false;
            }
        }

        [Serializable]
        private sealed class LocalProgressSeed
        {
            public int schemaVersion = 1;
            public string localProfileId = MathLocalProfileId;
            public int journeyNodeIndex = 0;
            public int rewardTokens = 0;
            public int[] concepts = new int[0];
        }

        private sealed class UnityJsonStateStore : IMathAnturaStateStore
        {
            private readonly string path;

            public UnityJsonStateStore(string path)
            {
                this.path = path;
            }

            public bool TryLoad(out MathAnturaBridgeState loadedState, out string errorCode)
            {
                loadedState = null;
                errorCode = string.Empty;
                try
                {
                    if (!File.Exists(path))
                    {
                        loadedState = new MathAnturaBridgeState();
                        return true;
                    }

                    string json = File.ReadAllText(path, Encoding.UTF8);
                    loadedState = JsonUtility.FromJson<MathAnturaBridgeState>(json);
                    if (loadedState == null || loadedState.schemaVersion != MathAnturaBridgeState.CurrentSchemaVersion)
                    {
                        loadedState = new MathAnturaBridgeState();
                    }

                    return true;
                }
                catch (Exception)
                {
                    loadedState = new MathAnturaBridgeState();
                    errorCode = "bridge_state_load_failed";
                    return false;
                }
            }

            public bool TrySave(MathAnturaBridgeState bridgeState, out string errorCode)
            {
                errorCode = string.Empty;
                try
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string json = JsonUtility.ToJson(bridgeState);
                    File.WriteAllText(path, json, new UTF8Encoding(false));
                    return true;
                }
                catch (Exception)
                {
                    errorCode = "bridge_state_save_failed";
                    return false;
                }
            }
        }

        /// <summary>Direct adapter for Antura 6000.4 branch APIs; intentionally no reflection.</summary>
        private sealed class AnturaProfileGateway : IMathAnturaGateway
        {
            public bool IsReady
            {
                get
                {
                    try
                    {
                        AppManager app = AppManager.I;
                        return app != null &&
                               app.Loaded &&
                               app.PlayerProfileManager != null &&
                               app.AppSettingsManager != null &&
                               app.AppSettings != null &&
                               app.AppSettings.SavedPlayers != null &&
                               app.DB != null &&
                               app.Teacher != null &&
                               app.NavigationManager != null &&
                               app.RewardSystemManager != null;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            public AnturaProfileReference[] GetProfiles()
            {
                if (!IsReady)
                {
                    return new AnturaProfileReference[0];
                }

                List<PlayerProfilePreview> saved = AppManager.I.AppSettings.SavedPlayers;
                AnturaProfileReference[] result = new AnturaProfileReference[saved.Count];
                for (int index = 0; index < saved.Count; index++)
                {
                    result[index] = new AnturaProfileReference
                    {
                        uuid = saved[index].Uuid,
                        playerName = saved[index].PlayerName
                    };
                }

                return result;
            }

            public bool TryPreparePrivateMode(out string errorCode)
            {
                errorCode = string.Empty;
                if (!IsReady)
                {
                    errorCode = "antura_privacy_setup_unavailable";
                    return false;
                }

                try
                {
                    // CreatePlayerProfile ends with TrackCompletedRegistration. Antura's analytics
                    // implementation honors this flag before recording age/avatar attributes.
                    AppManager.I.AppSettingsManager.EnableShareAnalytics(false);
                    return !AppManager.I.AppSettings.ShareAnalyticsEnabled;
                }
                catch (Exception)
                {
                    errorCode = "antura_privacy_setup_exception";
                    return false;
                }
            }

            public bool TryLoadProfile(string uuid, out string errorCode)
            {
                errorCode = string.Empty;
                if (!IsReady || string.IsNullOrEmpty(uuid))
                {
                    errorCode = "antura_profile_load_unavailable";
                    return false;
                }

                try
                {
                    PlayerProfile current = AppManager.I.Player;
                    if (current == null || !string.Equals(current.Uuid, uuid, StringComparison.Ordinal))
                    {
                        // Use the manager's public DB load + CurrentPlayer assignment, but skip
                        // SetPlayerAsCurrentByUUID's language-journey version migration. Math Journey
                        // does not own or rewrite Antura's language progression.
                        current = AppManager.I.PlayerProfileManager.GetPlayerProfileByUUID(uuid);
                        if (current != null)
                        {
                            AppManager.I.PlayerProfileManager.CurrentPlayer = current;
                        }
                    }

                    if (current == null || !string.Equals(current.Uuid, uuid, StringComparison.Ordinal))
                    {
                        errorCode = "antura_profile_load_unavailable";
                        return false;
                    }

                    return true;
                }
                catch (Exception)
                {
                    errorCode = "antura_profile_load_exception";
                    return false;
                }
            }

            public bool TryCreateProfile(string profileMarker, out string uuid, out string errorCode)
            {
                uuid = string.Empty;
                errorCode = string.Empty;
                if (!IsReady)
                {
                    errorCode = "antura_profile_create_unavailable";
                    return false;
                }

                HashSet<string> existingUuids = SnapshotProfileUuids();
                try
                {
                    AppManager app = AppManager.I;
                    uuid = app.PlayerProfileManager.CreatePlayerProfile(
                        0,
                        true,
                        1,
                        PlayerGender.F,
                        PlayerTint.Purple,
                        new Color(1.0f, 0.82f, 0.68f, 1.0f),
                        new Color(0.24f, 0.16f, 0.12f, 1.0f),
                        new Color(0.45f, 0.76f, 0.96f, 1.0f),
                        8,
                        app.AppEdition.editionID,
                        app.ContentEdition.ContentID,
                        app.AppEdition.AppVersion,
                        false);
                    return !string.IsNullOrEmpty(uuid) && app.Player != null && app.Player.Uuid == uuid;
                }
                catch (Exception)
                {
                    // CreatePlayerProfile performs analytics last. Recover only a newly-created UUID,
                    // never whichever unrelated profile happened to be current before the call.
                    string recoveredUuid = FindNewProfileUuid(existingUuids);
                    if (!string.IsNullOrEmpty(recoveredUuid) &&
                        AppManager.I.Player != null &&
                        string.Equals(AppManager.I.Player.Uuid, recoveredUuid, StringComparison.Ordinal))
                    {
                        uuid = recoveredUuid;
                        return true;
                    }

                    errorCode = "antura_profile_create_exception";
                    return false;
                }
            }

            public bool TryPersistCurrentProfileMarker(string profileMarker, out string errorCode)
            {
                errorCode = string.Empty;
                if (!IsReady || string.IsNullOrEmpty(profileMarker) || AppManager.I.Player == null)
                {
                    errorCode = "antura_profile_save_unavailable";
                    return false;
                }

                try
                {
                    AppManager.I.Player.PlayerName = profileMarker;
                    AppManager.I.PlayerProfileManager.UpdateCurrentPlayerIconDataInSettings();
                    AppManager.I.PlayerProfileManager.SavePlayerProfile(AppManager.I.Player);
                    AppManager.I.AppSettingsManager.SaveSettings();
                    return true;
                }
                catch (Exception)
                {
                    errorCode = "antura_profile_save_exception";
                    return false;
                }
            }

            public bool TryAddBonesAndSave(int amount, out string errorCode)
            {
                errorCode = string.Empty;
                if (!IsReady || AppManager.I.Player == null || amount <= 0 || amount > MaximumBonesPerSuccess)
                {
                    errorCode = "antura_reward_unavailable";
                    return false;
                }

                try
                {
                    AppManager.I.Player.AddBones(amount);
                    // AddBones calls PlayerProfile.Save. Keep the manager call explicit so this
                    // bridge remains correct if that implementation changes upstream.
                    AppManager.I.PlayerProfileManager.SavePlayerProfile(AppManager.I.Player);
                    return true;
                }
                catch (Exception)
                {
                    errorCode = "antura_reward_exception";
                    return false;
                }
            }

            public bool CanNavigateHome
            {
                get
                {
                    try
                    {
                        if (!IsReady || AppManager.I.Player == null ||
                            AppManager.I.NavigationManager.IsTransitioningScenes)
                        {
                            return false;
                        }

                        string sceneName = SceneHelper.GetSceneName(AppScene.Home);
                        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            public bool TryNavigateHome(out string errorCode)
            {
                errorCode = string.Empty;
                if (!CanNavigateHome)
                {
                    errorCode = "antura_navigation_unavailable";
                    return false;
                }

                try
                {
                    AppManager.I.NavigationManager.GoToHome(debugMode: true);
                    return true;
                }
                catch (Exception)
                {
                    errorCode = "antura_navigation_exception";
                    return false;
                }
            }

            private static HashSet<string> SnapshotProfileUuids()
            {
                HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
                List<PlayerProfilePreview> profiles = AppManager.I.AppSettings.SavedPlayers;
                for (int index = 0; index < profiles.Count; index++)
                {
                    if (!string.IsNullOrEmpty(profiles[index].Uuid))
                    {
                        result.Add(profiles[index].Uuid);
                    }
                }

                return result;
            }

            private static string FindNewProfileUuid(HashSet<string> existingUuids)
            {
                List<PlayerProfilePreview> profiles = AppManager.I.AppSettings.SavedPlayers;
                for (int index = 0; index < profiles.Count; index++)
                {
                    string uuid = profiles[index].Uuid;
                    if (!string.IsNullOrEmpty(uuid) && !existingUuids.Contains(uuid))
                    {
                        return uuid;
                    }
                }

                return string.Empty;
            }
        }
    }
}
