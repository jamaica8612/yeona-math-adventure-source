# Yeona Math Adventure integration manifest

This is the authoritative allowlist for integrating the staged work and the later premium slice.
Every new file destination is below `Assets/YeonaMathAdventure`; Unity creates the `.meta` files.
Rows labelled as premium/generated use the project file itself as the source of truth and must never
depend on a temporary image-generation cache path. There is one required,
deliberately narrow privacy patch to an existing Antura source file, documented separately below.
Do not copy a staging directory as a whole.

## Runtime math-core assembly (`Yeona.Math.Core`)

| Staged source | Exact destination |
|---|---|
| `staging_math_core/Runtime/AiSuggestionGate.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/AiSuggestionGate.cs` |
| `staging_math_core/Runtime/ArithmeticExpressionEvaluator.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/ArithmeticExpressionEvaluator.cs` |
| `staging_math_core/Runtime/DeterministicRandom.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/DeterministicRandom.cs` |
| `staging_math_core/Runtime/DomainTypes.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/DomainTypes.cs` |
| `staging_math_core/Runtime/FairShareDomain.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/FairShareDomain.cs` |
| `staging_math_core/Runtime/LocalProblemBank.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/LocalProblemBank.cs` |
| `staging_math_core/Runtime/MathTeacherAI.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/MathTeacherAI.cs` |
| `staging_math_core/Runtime/PatternSpaceDomain.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/PatternSpaceDomain.cs` |
| `staging_math_core/Runtime/ProgressAndAdaptiveTeacher.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/ProgressAndAdaptiveTeacher.cs` |
| `staging_math_core/Runtime/TargetNumberDomain.cs` | `Assets/YeonaMathAdventure/Runtime/MathCore/TargetNumberDomain.cs` |
| `staging_math_core/Runtime/Yeona.Math.Core.asmdef` | `Assets/YeonaMathAdventure/Runtime/MathCore/Yeona.Math.Core.asmdef` |

The asmdef is pure C#, `noEngineReferences: true`, and `autoReferenced: true`. Its namespace is
`YeonaMathAdventure.MathCore`; its assembly name is `Yeona.Math.Core`.

## Assembly-CSharp runtime files (no additional asmdef)

| Staged source | Exact destination |
|---|---|
| `staging_math_ui/Runtime/FairShareGameView.cs` | `Assets/YeonaMathAdventure/Runtime/FairShareGameView.cs` |
| `staging_math_ui/Runtime/MathJourneyBootstrap.cs` | `Assets/YeonaMathAdventure/Runtime/MathJourneyBootstrap.cs` |
| `staging_math_ui/Runtime/MathJourneyContracts.cs` | `Assets/YeonaMathAdventure/Runtime/MathJourneyContracts.cs` |
| `staging_math_ui/Runtime/MathMiniGameViewBase.cs` | `Assets/YeonaMathAdventure/Runtime/MathMiniGameViewBase.cs` |
| `staging_math_ui/Runtime/MathUiKit.cs` | `Assets/YeonaMathAdventure/Runtime/MathUiKit.cs` |
| `staging_math_ui/Runtime/PatternSpaceGameView.cs` | `Assets/YeonaMathAdventure/Runtime/PatternSpaceGameView.cs` |
| `staging_math_ui/Runtime/TargetNumberGameView.cs` | `Assets/YeonaMathAdventure/Runtime/TargetNumberGameView.cs` |
| premium runtime visuals | `Assets/YeonaMathAdventure/Runtime/PremiumMathVisuals.cs` |
| `staging_antura_bridge/MathAnturaFallbackContract.cs` | `Assets/YeonaMathAdventure/Runtime/MathAnturaFallbackContract.cs` |
| `staging_antura_bridge/MathAnturaBridge.cs` | `Assets/YeonaMathAdventure/Runtime/MathAnturaBridge.cs` |
| `staging_antura_bridge/DirectMathAnturaRewardBridge.cs` | `Assets/YeonaMathAdventure/Runtime/DirectMathAnturaRewardBridge.cs` |
| `staging_startup_audit/Runtime/MathAnturaUiIsolation.cs` | `Assets/YeonaMathAdventure/Runtime/MathAnturaUiIsolation.cs` |

These files intentionally stay in predefined `Assembly-CSharp`: the UI consumes the
auto-referenced `Yeona.Math.Core` assembly, while the direct bridge/startup isolation consume
Antura types that already live in Assembly-CSharp. Creating a new UI/bridge asmdef would make it
illegal to reference those predefined-assembly Antura types.

The exact scene bootstrap is `YeonaMathAdventure.MathJourneyBootstrap`. There is no
`YeonaMathAdventure.Runtime.MathJourneyBootstrap` fallback type.

## Editor files (`Assembly-CSharp-Editor`)

| Staged source | Exact destination |
|---|---|
| `staging_unity_editor/YeonaAndroidExternalTools.cs` | `Assets/YeonaMathAdventure/Editor/YeonaAndroidExternalTools.cs` |
| `staging_unity_editor/YeonaMathAndroidBuild.cs` | `Assets/YeonaMathAdventure/Editor/YeonaMathAndroidBuild.cs` |
| `staging_startup_audit/Editor/AnturaAppManagerSceneInstaller.cs` | `Assets/YeonaMathAdventure/Editor/AnturaAppManagerSceneInstaller.cs` |

The build helper calls `AnturaAppManagerSceneInstaller.TryInstallCoreServices(scene, out status)`
after attaching the math bootstrap and before creating the camera/saving the scene. Failure is
logged and remains local-only; it does not invalidate math play. `BuildAndroid` separately requires
the exact bootstrap to exist and be attached, so it cannot ship the interactive placeholder.

## Tests

| Staged source | Exact destination |
|---|---|
| `staging_math_core/Tests/EditMode/AdaptiveTeacherTests.cs` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/AdaptiveTeacherTests.cs` |
| `staging_math_core/Tests/EditMode/AiSuggestionGateTests.cs` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/AiSuggestionGateTests.cs` |
| `staging_math_core/Tests/EditMode/ArithmeticAndValidatorTests.cs` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/ArithmeticAndValidatorTests.cs` |
| `staging_math_core/Tests/EditMode/BoundaryFuzzTests.cs` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/BoundaryFuzzTests.cs` |
| `staging_math_core/Tests/EditMode/GeneratorTests.cs` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/GeneratorTests.cs` |
| `staging_math_core/Tests/EditMode/MathTeacherAITests.cs` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/MathTeacherAITests.cs` |
| `staging_math_core/Tests/EditMode/Yeona.Math.Core.Tests.asmdef` | `Assets/YeonaMathAdventure/Tests/EditMode/MathCore/Yeona.Math.Core.Tests.asmdef` |
| `staging_math_ui/Tests/Editor/MathUiLayoutTests.cs` | `Assets/YeonaMathAdventure/Tests/Editor/MathUiLayoutTests.cs` |
| `staging_antura_bridge/Tests/MathAnturaFallbackContractTests.cs` | `Assets/YeonaMathAdventure/Tests/Editor/MathAnturaFallbackContractTests.cs` |
| premium art/layout regression tests | `Assets/YeonaMathAdventure/Tests/Editor/PremiumVisualTests.cs` |
| Android/font regression tests | `Assets/YeonaMathAdventure/Tests/Editor/PretendardAndroidBuildRegressionTests.cs` |

Only the math-core tests use the named `Yeona.Math.Core.Tests` test asmdef. UI and bridge tests must
remain below the plain `Editor` folder without an asmdef so predefined `Assembly-CSharp-Editor` can
reference the corresponding Assembly-CSharp types. Do not add an asmdef to
`Assets/YeonaMathAdventure/Tests/Editor`.

## Fonts, artwork, and documentation

| Staged source | Exact destination |
|---|---|
| `staging_fonts/Pretendard-1.3.9/Pretendard-Regular.otf` | `Assets/YeonaMathAdventure/Resources/Fonts/Pretendard-Regular.otf` |
| `staging_fonts/Pretendard-1.3.9/Pretendard-Bold.otf` | `Assets/YeonaMathAdventure/Resources/Fonts/Pretendard-Bold.otf` |
| `staging_fonts/Pretendard-1.3.9/OFL-1.1.txt` | `Assets/YeonaMathAdventure/ThirdParty/Pretendard/OFL-1.1.txt` |
| `staging_artwork/YeonaMathAdventure-AppIcon-FullBleed.png` | `Assets/YeonaMathAdventure/Artwork/YeonaMathAdventure-AppIcon-FullBleed.png` |
| generated premium backplates and guide | `Assets/YeonaMathAdventure/Resources/YeonaMathAdventure/Art/*.png` |
| non-runtime concept references | `Assets/YeonaMathAdventure/Documentation/ReferenceArt/*.png` |
| `staging_docs/README.md` | `Assets/YeonaMathAdventure/Documentation/README.md` |
| generated-art provenance | `Assets/YeonaMathAdventure/Documentation/ART_PROVENANCE.md` |
| `staging_docs/Documentation/THIRD_PARTY_NOTICES.md` | `Assets/YeonaMathAdventure/Documentation/THIRD_PARTY_NOTICES.md` |
| `staging_docs/Documentation/ai-suggestion.schema.json` | `Assets/YeonaMathAdventure/Documentation/ai-suggestion.schema.json` |
| `staging_docs/Documentation/INTEGRATION_MANIFEST.md` | `Assets/YeonaMathAdventure/Documentation/INTEGRATION_MANIFEST.md` |

The full-bleed icon is the only icon source allowed into the project. It is 1254x1254, 24-bit RGB,
SHA-256 `4E63DCD41DECC7C829635C801EA60D399698542A986B6D25ECC85D3D4DD2AB12`.
`YeonaMathAndroidBuild` assigns it to Android `IconKind.Application` slots. It deliberately does
not configure adaptive icons because one flattened RGB image cannot safely supply independent
foreground/background layers.

The two staged AI schema files are byte-identical after audit. Integrate only the documentation
copy shown above so there is one distributed schema.

## Generated, not copied

`YeonaMathAndroidBuild.SetupProject()` generates
`Assets/YeonaMathAdventure/Scenes/math_Journey.unity`, attaches the exact bootstrap, installs
Antura core services when available, creates the camera, enables only that scene, applies the
package/product/icon/Android settings, and saves assets. Do not copy a staged `.unity` scene.

The same setup generates `Assets/YeonaMathAdventure/Resources/Fonts/Pretendard-Dynamic.asset`
from the bundled unmodified Pretendard Regular font. The derived TMP font asset remains covered by
Pretendard's SIL Open Font License 1.1 and is validated before every Android build.

## Required privacy patch (the single existing-source exception)

Before compiling or building the dedicated child-facing APK, apply
`staging_privacy_patch/OnlineAnalytics.PackageGuard.patch` from the Antura repository root:

```powershell
git apply --check ..\staging_privacy_patch\OnlineAnalytics.PackageGuard.patch
git apply ..\staging_privacy_patch\OnlineAnalytics.PackageGuard.patch
```

It changes only the existing file
`Assets/_core/_scripts/_Core/Services/Analytics/OnlineAnalytics.cs`. For the exact package
`com.yeona.mathadventure`, `Analytics.Awake()` returns before `UnityServices.InitializeAsync()` and
all analytics event paths remain disabled. Other package identifiers retain the upstream behavior.
This guard must be applied before the first APK build: Antura creates `Analytics` with
`AddComponent`, so a scene-only disable would run after `Awake()` has already started.

Do not copy `staging_privacy_patch/OnlineAnalyticsPackageGuardCompileCheck.cs` into `Assets`; it is
staging-only compile evidence. See `staging_privacy_patch/README.md` for the rationale and rollback
boundary.

## Explicit denylist

Never copy any of the following into `Assets`:

- any staging `.verify` directory or generated DLL/EXE;
- any staging `Verification` directory or runner/script;
- `staging_antura_bridge/Verification/RewardInterfaceStub.cs` (duplicates the real
  `IMathRewardBridge` type);
- `staging_math_core/ai_activity_suggestion.schema.json` (canonical duplicate; integrate the docs
  copy only);
- staging README files other than the documentation mapping above;
- `staging_artwork/YeonaMathAdventure-AppIcon.png` (superseded rounded-corner draft);
- a second UI, bridge, or editor asmdef;
- any other file outside `Assets/YeonaMathAdventure`; the exact OnlineAnalytics patch above is the
  sole exception.

## Required existing package/API dependencies

No package-manifest edit is required. The imported project already contains:

- Unity `6000.4.11f1`;
- `com.unity.inputsystem` 1.19.0 (`InputSystemUIInputModule`);
- `com.unity.ugui` 2.0.0 (`UnityEngine.UI` and `Unity.TextMeshPro` assemblies);
- `com.unity.addressables` 2.9.1 (editor build setting plus existing Antura dependency);
- `com.unity.test-framework` 1.6.0 (NUnit EditMode tests).

Antura types remain in the existing predefined Assembly-CSharp. The startup installer additionally
requires existing `Antura.Core`, `Antura.Discover`, and `Antura.Discover.Audio`; runtime isolation
requires `Antura.UI`.

## Post-copy order and invariants

1. Confirm the upstream Unity process is closed and record the Antura working-tree baseline.
2. Apply the exact OnlineAnalytics package-guard patch above; confirm it is the only intentional
   modification outside `Assets/YeonaMathAdventure`.
3. Create only the destination folders listed above and copy only allowlisted files.
4. Let Unity import/compile; do not generate the scene until scripts and icon are imported.
5. Run EditMode tests, including the MathTeacherAI activity/concept selection coverage.
6. Run `YeonaMathAdventure.Editor.YeonaMathAndroidBuild.SetupProject`.
7. Verify the generated scene has one `YeonaMathAdventure.MathJourneyBootstrap` and an installed
   AppManager or a logged local-only startup status.
8. Run `BuildAndroid`; it re-runs setup and refuses a missing bootstrap, missing/late privacy guard,
   wrong package, missing or wrong-sized icon, failed build report, or missing APK.
9. Verify `Application.identifier == com.yeona.mathadventure`, only `math_Journey` is in build
   settings, and the APK path is
   `Builds/YeonaMathAdventure/YeonaMathAdventure-v0.2.0.apk` unless explicitly overridden.

Package/data isolation is based on Android's package-private sandbox plus the distinct package ID.
The authoritative files beneath `Application.persistentDataPath/YeonaMathAdventure` are:

- `yeona_math_progress_v1_yeona_local.json`;
- `math_runtime_meta_v1.json`;
- `antura_bridge_v1.json`.

The optional Antura profile/PlayerPrefs/database also live in the new package sandbox and use the
internal marker `__yeona_math_internal_v1__`; the bridge never adopts an unrelated profile.
