using System;

namespace YeonaMathAdventure.AnturaBridge
{
    public enum MathAnturaMode
    {
        LocalJsonOnly = 0,
        AnturaAttached = 1
    }

    [Serializable]
    public sealed class MathAnturaBridgeState
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string expectedPackageIdentifier;
        public string anturaProfileUuid;
        public string profileMarker;
        public bool anturaAttachedLastRun;
        public int totalAnturaBonesGranted;
        public string lastStatusCode;

        public void Normalize(string expectedPackage, string expectedMarker)
        {
            schemaVersion = CurrentSchemaVersion;
            expectedPackageIdentifier = expectedPackage ?? string.Empty;
            profileMarker = expectedMarker ?? string.Empty;
            anturaProfileUuid = anturaProfileUuid ?? string.Empty;
            lastStatusCode = lastStatusCode ?? string.Empty;
            if (totalAnturaBonesGranted < 0)
            {
                totalAnturaBonesGranted = 0;
            }
        }
    }

    [Serializable]
    public sealed class AnturaProfileReference
    {
        public string uuid;
        public string playerName;
    }

    public interface IMathAnturaGateway
    {
        bool IsReady { get; }
        bool TryPreparePrivateMode(out string errorCode);
        AnturaProfileReference[] GetProfiles();
        bool TryLoadProfile(string uuid, out string errorCode);
        bool TryCreateProfile(string profileMarker, out string uuid, out string errorCode);
        bool TryPersistCurrentProfileMarker(string profileMarker, out string errorCode);
        bool TryAddBonesAndSave(int amount, out string errorCode);
        bool CanNavigateHome { get; }
        bool TryNavigateHome(out string errorCode);
    }

    public interface IMathAnturaStateStore
    {
        bool TryLoad(out MathAnturaBridgeState state, out string errorCode);
        bool TrySave(MathAnturaBridgeState state, out string errorCode);
    }

    [Serializable]
    public sealed class MathAnturaAttachOutcome
    {
        public MathAnturaMode mode;
        public string statusCode;
        public string profileUuid;
        public bool createdProfile;

        public bool IsAttached
        {
            get { return mode == MathAnturaMode.AnturaAttached; }
        }
    }

    /// <summary>
    /// Pure C# decision layer. All Antura calls are behind IMathAnturaGateway, and every
    /// exception or rejected operation resolves to LocalJsonOnly instead of blocking play.
    /// </summary>
    public static class MathAnturaFallbackCoordinator
    {
        public static MathAnturaAttachOutcome TryAttach(
            string expectedPackageIdentifier,
            string currentPackageIdentifier,
            string profileMarker,
            MathAnturaBridgeState state,
            IMathAnturaGateway gateway)
        {
            if (state == null)
            {
                state = new MathAnturaBridgeState();
            }

            state.Normalize(expectedPackageIdentifier, profileMarker);

            if (string.IsNullOrEmpty(expectedPackageIdentifier) ||
                !string.Equals(expectedPackageIdentifier, currentPackageIdentifier, StringComparison.Ordinal))
            {
                return LocalOnly(state, "package_identifier_mismatch");
            }

            if (gateway == null)
            {
                return LocalOnly(state, "antura_gateway_missing");
            }

            try
            {
                if (!gateway.IsReady)
                {
                    return LocalOnly(state, "antura_not_ready");
                }

                string operationError;
                if (!gateway.TryPreparePrivateMode(out operationError))
                {
                    return LocalOnly(state, NormalizeGatewayError("antura_privacy_setup_failed", operationError));
                }

                AnturaProfileReference[] profiles = gateway.GetProfiles() ?? new AnturaProfileReference[0];
                string selectedUuid = FindByUuid(profiles, state.anturaProfileUuid);
                if (string.IsNullOrEmpty(selectedUuid))
                {
                    selectedUuid = FindByMarker(profiles, profileMarker);
                }

                bool created = false;
                if (string.IsNullOrEmpty(selectedUuid))
                {
                    if (!gateway.TryCreateProfile(profileMarker, out selectedUuid, out operationError) ||
                        string.IsNullOrEmpty(selectedUuid))
                    {
                        return LocalOnly(state, NormalizeGatewayError("antura_profile_create_failed", operationError));
                    }

                    created = true;
                }
                else if (!gateway.TryLoadProfile(selectedUuid, out operationError))
                {
                    return LocalOnly(state, NormalizeGatewayError("antura_profile_load_failed", operationError));
                }

                if (!gateway.TryPersistCurrentProfileMarker(profileMarker, out operationError))
                {
                    return LocalOnly(state, NormalizeGatewayError("antura_profile_save_failed", operationError));
                }

                state.anturaProfileUuid = selectedUuid;
                state.anturaAttachedLastRun = true;
                state.lastStatusCode = created ? "antura_profile_created" : "antura_profile_loaded";
                return new MathAnturaAttachOutcome
                {
                    mode = MathAnturaMode.AnturaAttached,
                    statusCode = state.lastStatusCode,
                    profileUuid = selectedUuid,
                    createdProfile = created
                };
            }
            catch (Exception)
            {
                return LocalOnly(state, "antura_exception_fallback");
            }
        }

        private static MathAnturaAttachOutcome LocalOnly(MathAnturaBridgeState state, string statusCode)
        {
            state.anturaAttachedLastRun = false;
            state.lastStatusCode = statusCode;
            return new MathAnturaAttachOutcome
            {
                mode = MathAnturaMode.LocalJsonOnly,
                statusCode = statusCode,
                profileUuid = state.anturaProfileUuid ?? string.Empty,
                createdProfile = false
            };
        }

        private static string FindByUuid(AnturaProfileReference[] profiles, string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
            {
                return string.Empty;
            }

            for (int index = 0; index < profiles.Length; index++)
            {
                AnturaProfileReference profile = profiles[index];
                if (profile != null && string.Equals(profile.uuid, uuid, StringComparison.Ordinal))
                {
                    return profile.uuid;
                }
            }

            return string.Empty;
        }

        private static string FindByMarker(AnturaProfileReference[] profiles, string marker)
        {
            if (string.IsNullOrEmpty(marker))
            {
                return string.Empty;
            }

            for (int index = 0; index < profiles.Length; index++)
            {
                AnturaProfileReference profile = profiles[index];
                if (profile != null &&
                    !string.IsNullOrEmpty(profile.uuid) &&
                    string.Equals(profile.playerName, marker, StringComparison.Ordinal))
                {
                    return profile.uuid;
                }
            }

            return string.Empty;
        }

        private static string NormalizeGatewayError(string fallback, string gatewayError)
        {
            if (string.IsNullOrEmpty(gatewayError) || gatewayError.Length > 80)
            {
                return fallback;
            }

            for (int index = 0; index < gatewayError.Length; index++)
            {
                char value = gatewayError[index];
                bool safe = (value >= 'a' && value <= 'z') || value == '_' || value == '-';
                if (!safe)
                {
                    return fallback;
                }
            }

            return gatewayError;
        }
    }
}
