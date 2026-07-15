# Yeona Math Adventure

`Yeona Math Adventure` is a separate, Korean-first mathematics game built in a fork of Antura with Unity 6. It has its own Android package (`com.yeona.mathadventure`), persistent-data directory, scenes, question models, adaptive records, and APK. It does not read or modify data from `Yeona Snow Festival`.

The Korean in-app display title is exactly **연아의 별다리 모험**. `연아` is a local display
constant used only in a few greetings and recommendation messages; the app has no name-entry field
and does not transmit the display name.

## First playable flow

The first launch goes from the start screen into a short game-shaped placement check. The child plays exactly two rounds in each of the three activities (six rounds total), then the local teacher selects a starting level. Later launches open the Math Journey home screen with a recommended next activity.

1. **Make the target number** — place colorful number and `+`, `-`, `×`, `÷` blocks to light the missing pieces of a floating star bridge. The engine evaluates the expression, so every mathematically valid solution is accepted.
2. **Share fairly** — drag clay berries from a forest basket onto character plates and, when needed, into a visible remainder area. The engine checks equal groups and remainder separately.
3. **Pattern and space** — place or rotate clay blocks in a rooftop workshop to complete numeric/shape sequences, rotations, and symmetry puzzles.

There are no lives, no separate “wrong answer” screen, and no child-facing check button. A board is
validated automatically after the final placement or a complete target expression. A mismatch keeps
the construction in place, reacts visually, and offers progressively more concrete hints. The
original star friend `반디` acts as the guide through speech bubbles, floating reactions, sparkles,
and success celebrations. Success changes the world, awards a local star, and recommends the next
activity.

## Premium vertical slice v0.2.0

The v0.2.0 vertical slice replaces the earlier flat assessment shell with a game-first world:

- clean aspect-filled clay backplates with a centered 4:3 safe composition and extended scenery
  on 16:9 and 20:9 devices;
- one shared animated guide, `반디`, instead of repeated modal instructions;
- large clay-style interaction pieces with press, lift, drop, and sparkle reactions;
- a six-round placement path ordered as two Fair Share, two Target Number, and two Pattern/Space
  adventures, without displaying a school-like placement level to the child;
- a local reward choice after placement and a one-time migration from the v0.1 prototype flow that
  preserves concept records and rewards.

When upgrading an existing v0.1.x install, experience migration version 2 preserves concept history
and earned reward tokens but resets the prototype placement flag once so the child can experience
the new six-part opening adventure and choose the first island reward.

The runtime artwork and exact generation provenance are recorded in
`Documentation/ART_PROVENANCE.md`. The flattened concept references are documentation-only and are
not packed through Unity `Resources`.

## Adaptive learning and storage

The offline `AdaptiveTeacher` is a deterministic path selector, not generative AI. For every
concept it keeps the latest ten attempts: whether the puzzle was correct on the first automatic
validation attempt, total solve time, retries, hints, difficulty, and scaffold level. Every round can still be
completed after supportive retries; that completion does not inflate first-check accuracy. Strong
performance increases number range or step count. Struggle first increases visual scaffolding and
intermediate steps instead of reducing content to counting 1–10.

Progress is JSON under Unity's package-specific `Application.persistentDataPath`, using the file
`YeonaMathAdventure/yeona_math_progress_v1_yeona_local.json`. First-run placement metadata uses
`YeonaMathAdventure/math_runtime_meta_v1.json`, and optional Antura bridge metadata uses
`YeonaMathAdventure/antura_bridge_v1.json`. The parent view reduces each concept to `익숙함`,
`연습 중`, or `도움이 필요함`. No child name, voice, free text, or other personal data is sent
off-device.

The Antura base creates its analytics component at runtime and normally initializes Unity Services
from `Awake()`. The integration therefore includes a required package-specific guard in that one
existing Antura source file. For `com.yeona.mathadventure`, the guard returns before Unity Services
initialization and keeps analytics disabled; see `Documentation/INTEGRATION_MANIFEST.md`. This must
be applied before building the APK.

The dedicated Android build also clears Antura's legacy forced internet/external-storage flags.
Antura's NativeGallery Java bridge remains available for startup compatibility, while a package-
guarded Gradle manifest processor removes its obsolete `READ_EXTERNAL_STORAGE` and
`WRITE_EXTERNAL_STORAGE` declarations and disables legacy shared-storage mode. Math Journey writes
only below its package-private `Application.persistentDataPath`. Addressables build-with-player is
disabled only for the Math APK build and restored in `finally`, so the upstream language project
setting is not left changed.

## Optional generative-AI boundary

The first APK makes no math-AI network requests and needs no API key. A future service may select
only a verified local `sourceProblemId` within the teacher's concept/difficulty boundary and rewrite
bounded Korean `promptKo`/`hintsKo` presentation text. Its response must match
`Documentation/ai-suggestion.schema.json`. The Unity engine recreates the selected local problem
and recomputes every expression and constraint; unknown fields, answer-bearing fields, invalid
metadata, or unsafe text discard the response and use the bundled local bank. Never put an API key
in an APK.

## Antura integration boundary

Antura's language-learning route remains intact. Math Journey adds parallel math question packs and validators because Antura's existing `IQuestionPack` is explicitly typed around `ILivingLetterData`, and its `TeacherAI` is coupled to vocabulary, `MiniGameCode`, and language-play sessions. Where the Antura systems are available, the bridge can reuse its local profile/reward services; the math game remains playable if those services are not initialized.

## Development and build

Verified Antura baseline: upstream `https://github.com/vgwb/Antura.git`, branch `main`, commit
`a60a8ba9e87d054aea5d757188cc2050e9289fb1`, imported with Unity `6000.4.11f1`. Math Journey
integration and APK evidence should be evaluated against that immutable commit.

Required local toolchain for the verified workstation:

- Unity Editor `6000.4.11f1`
- Android SDK API 35/36 and Build Tools 36
- Android NDK `27.2.12479018`
- OpenJDK 17 from Unity Android Build Support

Run EditMode tests and build from PowerShell (paths can be overridden with the `YEONA_ANDROID_*_ROOT` environment variables):

```powershell
& $unity -batchmode -nographics -projectPath $repo -runTests -testPlatform EditMode -testFilter YeonaMathAdventure -testResults "$repo\TestResults\editmode.xml" -logFile "$repo\Logs\editmode-tests.log"
& $unity -batchmode -nographics -quit -buildTarget Android -projectPath $repo -executeMethod YeonaMathAdventure.Editor.YeonaMathAndroidBuild.BuildAndroid -logFile "$repo\Logs\yeona-android-build.log"
```

The EditMode command intentionally omits `-quit`. Unity Test Framework 1.6.0 owns the command-line
test-run shutdown and warns that combining `-quit` with `-runTests` prevents the tests from running.

Default APK output:

```text
Builds/YeonaMathAdventure/YeonaMathAdventure-v0.2.0.apk
```

The test APK uses Android debug signing and contains no secret key. Development-build and player-debugging
flags are disabled, but this APK is still intended for direct testing rather than Play Store release.

### Verified Android APK v0.2.0 (2026-07-15 KST)

- Unity EditMode tests: 89 total, 89 passed, 0 failed, 0 skipped. Coverage includes math
  generators/validators, adaptive selection and persistence, the six-round placement order,
  automatic validation, delayed-callback cancellation, enlarged child touch targets, Korean TMP,
  generated-art import, and 16:9 / 20:9 / 4:3 aspect-fill calculations.
- Unity successfully generated the IL2CPP Android player and Gradle project. The first final
  packaging attempt was interrupted only by transient SSL resets while Google Maven served two
  Android annotation-tool artifacts. Those exact official artifacts were fetched locally and the
  same generated project then completed `assembleRelease` offline: `BUILD SUCCESSFUL in 40s`,
  116 actionable tasks (15 executed, 101 up-to-date).
- APK path: `Builds/YeonaMathAdventure/YeonaMathAdventure-v0.2.0.apk`.
- Public release: <https://github.com/jamaica8612/yeona-math-adventure/releases/tag/v0.2.0>.
- Direct APK download: <https://github.com/jamaica8612/yeona-math-adventure/releases/download/v0.2.0/YeonaMathAdventure-v0.2.0.apk>.
- APK size: 136,169,655 bytes (129.86 MiB).
- APK SHA-256: `7096D4A28E389320095E2C10191B523E825FD2B8CDF88335F2DECEEA016B0043`.
- Android metadata: package `com.yeona.mathadventure`, version `0.2.0` (`versionCode` 3),
  minimum API 25, target/compile API 36, landscape, ARM64 (`arm64-v8a`) only.
- Package checks: APK Signature Scheme v2 debug signature valid, 4-byte zip alignment valid,
  `android:debuggable=false`, and no legacy external-storage permission. The inherited
  `INTERNET`, `ACCESS_NETWORK_STATE`, and `POST_NOTIFICATIONS` permissions remain; Math Journey
  itself makes no network request and its package-specific analytics guard remains active.
- No Android device was attached, so installation and final visual confirmation on the child's
  physical phone/tablet still require a short device test.

### Previous verified Android APK v0.1.1 (2026-07-15 KST)

- Unity `6000.4.11f1` BuildReport: `Succeeded`, 0 errors, 11 non-blocking upstream/package
  warnings, process return code 0, build time `00:02:40.9457016`.
- Unity EditMode tests: 81 total, 81 passed, 0 failed, 0 skipped. This includes expression and
  constraint validation, adaptive level selection, persistence/AI fallback, all three activities,
  placement flow, safe-area/layout checks for 16:9, 20:9, and 4:3, and the persistent Korean TMP
  font asset used by Android builds.
- APK size: 147,359,681 bytes (140.53 MiB).
- APK SHA-256: `5CDA764DEAD7B0CAF44A6B7EDB100C5821B9281492CD9DFDAE9D6ACF52FDE9C4`.
- Android metadata: package `com.yeona.mathadventure`, version `0.1.1` (`versionCode` 2),
  minimum API 25, target API 36, landscape, one universal APK with `armeabi-v7a` and
  `arm64-v8a`.
- Android package checks: APK Signature Scheme v2 debug signature valid, 4-byte zip alignment
  valid, `android:debuggable=false`, only `armeabi-v7a` / `arm64-v8a` native libraries present,
  and legacy `READ_EXTERNAL_STORAGE` / `WRITE_EXTERNAL_STORAGE` permissions absent.
- Secret scan: the complete 147,359,681-byte APK and all 64 ZIP entries (293,979,879 bytes
  uncompressed) were checked with bounded Google/OpenAI/AWS/GitHub/Slack/Stripe/JWT/private-key/
  Bearer patterns; zero high-confidence key, token, or private-key matches.
- The APK declares `INTERNET`, `ACCESS_NETWORK_STATE`, and `POST_NOTIFICATIONS`
  through Unity/Antura packages. Math Journey itself makes no network request, contains no API
  key, does not request child input, and its package-specific analytics guard prevents Unity
  Services initialization. A later store build should remove development networking and any
  unused notification capability.
- Version `0.1.0` was withdrawn after a real-device test exposed a runtime TextMesh Pro font-asset
  creation failure that left the UI without Korean text. Version `0.1.1` generates and validates
  the Pretendard TMP material and 432-character atlas before the Android build, avoiding that
  runtime creation path and suppressing the development console in the distributable APK.
- No Android device was attached during the build, so installation was not automated. The APK
  itself and the responsive layouts were verified as described above; the repaired text rendering
  still requires confirmation on the same physical device that showed the `0.1.0` failure.

## Open-source provenance

- [Antura](https://github.com/vgwb/Antura): source code under BSD-2-Clause; assets are CC-BY-4.0 unless a file says otherwise. The upstream `LICENSE.md` and credits are retained. Math Journey is an addition; Antura's source/assets are not relicensed.
- [GCompris](https://github.com/gcompris/GCompris-qt): consulted only for the progression of mathematics concepts. No GCompris code, data, fonts, or art was copied. GCompris as a whole is AGPL-3.0 and contains file-specific asset licenses.
- [Matheor](https://github.com/satraul/matheor): consulted only for the general target-number play pattern. No code, fonts, audio, or art was copied. Its code is MIT; the repository's bundled asset rights were not sufficiently explicit for reuse here.
- [Pretendard 1.3.9](https://github.com/orioncactus/pretendard/releases/tag/v1.3.9): unmodified Regular and Bold font files under SIL Open Font License 1.1. The exact OFL text is included with the fonts.
- [Native Gallery 1.8.0](https://github.com/yasirkula/UnityNativeGallery): existing Antura package dependency at commit `ae2ebf6382c2cd0089d73e51274c40f1c155ae81`, MIT License. Its Java bridge is retained, while obsolete shared-storage permissions are removed only from the Math APK manifest.

The final Android icon is the independently generated original full-bleed RGB artwork
`Assets/YeonaMathAdventure/Artwork/YeonaMathAdventure-AppIcon-FullBleed.png` (1254x1254,
SHA-256 `4E63DCD41DECC7C829635C801EA60D399698542A986B6D25ECC85D3D4DD2AB12`). It is not copied from
Antura, GCompris, Matheor, or another asset pack. The earlier rounded-corner draft is not included
in the integrated project or build.

The v0.2.0 clay-world backplates and the original `반디` guide are also independently generated
project artwork. Exact filenames, hashes, prompt summaries, runtime status, and terms note are in
`Documentation/ART_PROVENANCE.md`.

See `Documentation/THIRD_PARTY_NOTICES.md` for distribution notices. All original Math Journey code and problem content in this fork is independently implemented for this project.
