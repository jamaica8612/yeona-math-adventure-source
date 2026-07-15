using System;
using NUnit.Framework;

namespace YeonaMathAdventure.AnturaBridge.Tests
{
    public sealed class MathAnturaFallbackContractTests
    {
        private const string ExpectedPackage = "com.yeona.mathadventure";
        private const string Marker = "__yeona_math_internal_v1__";

        [Test]
        public void PackageMismatch_UsesLocalModeWithoutTouchingGateway()
        {
            FakeGateway gateway = new FakeGateway { ready = true };
            MathAnturaBridgeState state = new MathAnturaBridgeState();

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                "org.antura.learnwithantura",
                Marker,
                state,
                gateway);

            Assert.That(outcome.mode, Is.EqualTo(MathAnturaMode.LocalJsonOnly));
            Assert.That(outcome.statusCode, Is.EqualTo("package_identifier_mismatch"));
            Assert.That(gateway.totalCalls, Is.EqualTo(0));
        }

        [Test]
        public void MissingAntura_UsesLocalMode()
        {
            FakeGateway gateway = new FakeGateway { ready = false };

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                new MathAnturaBridgeState(),
                gateway);

            Assert.That(outcome.IsAttached, Is.False);
            Assert.That(outcome.statusCode, Is.EqualTo("antura_not_ready"));
        }

        [Test]
        public void SavedUuid_LoadsExactMathProfileAndPersistsMarker()
        {
            FakeGateway gateway = new FakeGateway
            {
                ready = true,
                profiles = new[]
                {
                    Profile("unrelated", "student"),
                    Profile("math-uuid", Marker)
                }
            };
            MathAnturaBridgeState state = new MathAnturaBridgeState { anturaProfileUuid = "math-uuid" };

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                state,
                gateway);

            Assert.That(outcome.IsAttached, Is.True);
            Assert.That(outcome.createdProfile, Is.False);
            Assert.That(gateway.loadedUuid, Is.EqualTo("math-uuid"));
            Assert.That(gateway.persistedMarker, Is.EqualTo(Marker));
            Assert.That(state.anturaAttachedLastRun, Is.True);
        }

        [Test]
        public void MissingStateUuid_FindsMarkerInsteadOfUnrelatedProfile()
        {
            FakeGateway gateway = new FakeGateway
            {
                ready = true,
                profiles = new[]
                {
                    Profile("unrelated", "student"),
                    Profile("marked", Marker)
                }
            };

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                new MathAnturaBridgeState(),
                gateway);

            Assert.That(outcome.IsAttached, Is.True);
            Assert.That(gateway.loadedUuid, Is.EqualTo("marked"));
            Assert.That(gateway.createCalls, Is.EqualTo(0));
        }

        [Test]
        public void NoMathProfile_CreatesDedicatedProfile()
        {
            FakeGateway gateway = new FakeGateway
            {
                ready = true,
                profiles = new[] { Profile("unrelated", "student") },
                createdUuid = "new-math-uuid"
            };
            MathAnturaBridgeState state = new MathAnturaBridgeState();

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                state,
                gateway);

            Assert.That(outcome.IsAttached, Is.True);
            Assert.That(outcome.createdProfile, Is.True);
            Assert.That(outcome.profileUuid, Is.EqualTo("new-math-uuid"));
            Assert.That(state.anturaProfileUuid, Is.EqualTo("new-math-uuid"));
            Assert.That(gateway.createCalls, Is.EqualTo(1));
        }

        [Test]
        public void ProfileFailure_FallsBackWithoutThrowing()
        {
            FakeGateway gateway = new FakeGateway
            {
                ready = true,
                profiles = new[] { Profile("math-uuid", Marker) },
                loadSucceeds = false
            };

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                new MathAnturaBridgeState { anturaProfileUuid = "math-uuid" },
                gateway);

            Assert.That(outcome.IsAttached, Is.False);
            Assert.That(outcome.statusCode, Is.EqualTo("antura_profile_load_failed"));
        }

        [Test]
        public void GatewayException_FallsBackWithoutEscapingException()
        {
            FakeGateway gateway = new FakeGateway { ready = true, throwOnProfiles = true };

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                new MathAnturaBridgeState(),
                gateway);

            Assert.That(outcome.IsAttached, Is.False);
            Assert.That(outcome.statusCode, Is.EqualTo("antura_exception_fallback"));
        }

        [Test]
        public void PrivacySetupFailure_RefusesAnturaAndKeepsLocalMode()
        {
            FakeGateway gateway = new FakeGateway { ready = true, privacySucceeds = false };

            MathAnturaAttachOutcome outcome = MathAnturaFallbackCoordinator.TryAttach(
                ExpectedPackage,
                ExpectedPackage,
                Marker,
                new MathAnturaBridgeState(),
                gateway);

            Assert.That(outcome.IsAttached, Is.False);
            Assert.That(outcome.statusCode, Is.EqualTo("antura_privacy_setup_failed"));
            Assert.That(gateway.createCalls, Is.EqualTo(0));
        }

        private static AnturaProfileReference Profile(string uuid, string name)
        {
            return new AnturaProfileReference { uuid = uuid, playerName = name };
        }

        private sealed class FakeGateway : IMathAnturaGateway
        {
            public bool ready;
            public AnturaProfileReference[] profiles = new AnturaProfileReference[0];
            public bool throwOnProfiles;
            public bool loadSucceeds = true;
            public bool privacySucceeds = true;
            public string createdUuid = "created";
            public int totalCalls;
            public int createCalls;
            public string loadedUuid;
            public string persistedMarker;

            public bool IsReady
            {
                get
                {
                    totalCalls++;
                    return ready;
                }
            }

            public AnturaProfileReference[] GetProfiles()
            {
                totalCalls++;
                if (throwOnProfiles)
                {
                    throw new InvalidOperationException("simulated");
                }

                return profiles;
            }

            public bool TryPreparePrivateMode(out string errorCode)
            {
                totalCalls++;
                errorCode = privacySucceeds ? string.Empty : "antura_privacy_setup_failed";
                return privacySucceeds;
            }

            public bool TryLoadProfile(string uuid, out string errorCode)
            {
                totalCalls++;
                loadedUuid = uuid;
                errorCode = loadSucceeds ? string.Empty : "antura_profile_load_failed";
                return loadSucceeds;
            }

            public bool TryCreateProfile(string profileMarker, out string uuid, out string errorCode)
            {
                totalCalls++;
                createCalls++;
                uuid = createdUuid;
                errorCode = string.Empty;
                return true;
            }

            public bool TryPersistCurrentProfileMarker(string profileMarker, out string errorCode)
            {
                totalCalls++;
                persistedMarker = profileMarker;
                errorCode = string.Empty;
                return true;
            }

            public bool TryAddBonesAndSave(int amount, out string errorCode)
            {
                totalCalls++;
                errorCode = string.Empty;
                return true;
            }

            public bool CanNavigateHome
            {
                get { return false; }
            }

            public bool TryNavigateHome(out string errorCode)
            {
                totalCalls++;
                errorCode = "unavailable";
                return false;
            }
        }
    }
}
