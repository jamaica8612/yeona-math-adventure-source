# 작업 이어하기 문서 (HANDOFF)

이 문서는 작업이 중단됐을 때 **다른 세션·다른 AI·미래의 나**가 이어서 작업할 수 있도록
현재 상태와 다음 단계를 기록한다. 작업을 이어받으면 이 문서부터 읽고, 끝나면 갱신한다.

## 프로젝트 한 줄 요약

연아(2022-02-22생, 만 4세) 전용 수학 학습 안드로이드 게임. Antura를 빌드 호스트로 쓰는
소스 오버레이이며, 게임 전체가 `Assets/YeonaMathAdventure/`에 독립적으로 들어 있다.
복원·빌드 방법은 저장소 루트 `README.md` 참고 (Antura 기준 커밋 + Unity 6000.4.11f1).

## 현재 브랜치: `claude/age4-track-and-tts`

### 이 브랜치에서 완료한 것 (2026-07-15)

1. **만 4세 커리큘럼 트랙** — 난이도 1~2를 유아 수준으로 하향.
   - FairShare (`MathCore/FairShareDomain.cs`): d1 = 접시 2곳 × 2~3개(총 4~6, 나머지 0),
     d2 = 2~3곳 × 2~3개(총 ≤ 9, 나머지 0), d3 = 나머지 0 유지, **나머지 개념은 d4부터**.
   - TargetNumber (`MathCore/TargetNumberDomain.cs`): d1 = 덧셈만으로 3~6 만들기(블록 2개),
     d2 = 10 이하 덧셈. d3~5 기존 유지 (d5는 세 자리 목표 유지 — 테스트가 이를 단언).
   - PatternSpace (`MathCore/PatternSpaceDomain.cs`): 숫자열 d1 = 1씩 이어 세기(1 2 3 _ 5),
     d2 = 1~2씩 뛰기.
   - FairShare 뷰 (`FairShareGameView.cs`): `IsCountingLevel()` (나머지 없음 + 총 ≤ 10)이면
     수식 힌트(`17 = 4 × 4 + 1`) 대신 세기 언어 사용.
2. **음성 안내** (`Runtime/YeonaVoice.cs`, 신규) — 글 못 읽는 아이 대응.
   - 우선 `Resources/YeonaMathAdventure/Voice/{key}` 녹음 클립 재생, 없으면 **기기 내장
     한국어 TTS**(android.speech.tts.TextToSpeech, 오프라인, API 키 없음), 에디터에선 로그.
   - 연결 지점: 미니게임 시작(인사+과제 낭독), 피드백/힌트/칭찬(`MathMiniGameViewBase`),
     시작 화면 인사, 성공 축하, 배치 결과 (`MathJourneyBootstrap`).
3. **아트 교체 준비** — `PremiumMathVisuals.TryApplyItemSprite()`: FairShare 조각이
   `Resources/YeonaMathAdventure/Art/Items/Item-*.png`가 있으면 그 그림을, 없으면 기존
   코드 생성 원을 사용. 발주서는 `ART_REQUEST_CODEX.md`.
4. 테스트: `GeneratorTests`에 만 4세 밴드 테스트 3종 추가, 나머지 도입 시점 d4로 갱신,
   `BoundaryFuzzTests` target 하한을 난이도별로 완화, `Tests/Editor/YeonaVoiceAndAge4Tests.cs`
   신규(음성 문자열 정리 + 세기 언어 게이팅).

### 검증 상태

- MathCore 순수 로직: 외부 하니스에서 mono로 컴파일해 난이도 1~5 × 수백 시드 밴드·해결
  가능성·결정성 검증 완료 (클라우드 세션, Unity 없음).
- **Unity 컴파일·EditMode 테스트·APK는 아직 미검증** — 로컬 PC에서 아래 체크리스트 수행.

### 로컬 검증 체크리스트 (Windows PC)

1. 이 브랜치를 pull 받아 README 절차대로 Antura 위에 덮어쓴다.
2. Unity 6000.4.11f1로 열어 컴파일 에러 없는지 확인.
3. Test Runner에서 EditMode 테스트 전부 실행 (`Yeona.Math.Core.Tests` + Editor 테스트).
4. `Yeona Math > Build > Android Debug APK` 빌드 후 폰 설치.
5. 확인 항목:
   - 새 프로필(앱 데이터 삭제) 시작 → 배치 검사 첫 라운드가 "접시 2곳에 2~3개" 수준인지.
   - 반디 인사말과 과제가 **음성으로 재생**되는지 (기기에 한국어 TTS 엔진 필요 —
     설정 > 접근성 > TTS에서 한국어 데이터 확인).
   - 힌트를 3번 눌러도 수식이 안 나오는지 (저난이도).
   - 성공 시 축하 음성.
6. 문제 발견 시 이 문서의 "알려진 리스크"에 추가하고 수정.

### 알려진 리스크 / 주의

- TTS는 기기 TTS 엔진 품질에 좌우된다. 어색하면 녹음 클립으로 교체:
  `Resources/YeonaMathAdventure/Voice/`에 아래 키 이름으로 오디오 파일(wav/ogg/mp3)을
  넣으면 코드 수정 없이 클립이 우선 재생된다.
  - `opening_fairshare` "연아야, 친구들이 똑같이 먹게 도와줘!"
  - `opening_patternspace` "연아야, 맞는 조각을 놓아 길을 고쳐 줘!"
  - `opening_targetnumber` "연아야, 별조각으로 다리를 깨워 줘!"
  - `start_first_visit` "연아야, 별다리가 잠들었어! 우리 손으로 다시 반짝이게 해 주자."
  - `start_returning` "연아야, 오늘은 어떤 별섬을 깨워 볼까?"
  - `placement_result` "연아야, 첫 별섬이 깨어났어! 마음에 드는 선물을 하나 골라 봐."
  - `success_fairshare` / `success_patternspace` / `success_targetnumber` (성공 축하 문장)
  - 동적 문장(과제·힌트·피드백)은 클립 키가 없어 항상 TTS로 나간다.
- `TextToSpeech` 생성은 UI 스레드에서 하고, 초기화 콜백(`onInit`)은 자바 프록시로 받는다.
  기기별 이슈가 보이면 `YeonaVoice.cs`의 LogWarning 출력부터 확인.
- 기존 설치 사용자: 적응형 교사가 저장된 난이도에서 이어가므로, 밴드 하향의 효과는
  새 프로필에서 가장 잘 보인다.

## 다음 단계 (우선순위 순)

1. **로컬 검증** — 위 체크리스트. 실패 시 수정 커밋을 이 브랜치에 추가.
2. **아트 반입** — `ART_REQUEST_CODEX.md`대로 이미지 생성 → `Art/Items/`에 저장 →
   빌드하면 FairShare 조각이 자동으로 그림으로 바뀐다. 확인 후 커밋.
3. **주스(연출) 강화** — 정답 시 파티클·카운트업, 드롭 시 스케일 펀치. (별도 브랜치 권장)
4. **TargetNumber·PatternSpace 뷰의 저난이도 단순화** — 블록 수 4개 유지 중; 만 4세용으로
   보기 3개까지 줄이는 것 검토.
5. **녹음 클립 제작** — 위 키 목록 기준. 고품질 한국어 TTS로 일괄 생성해 넣는 방법 추천.

## 새 세션에서 이어받는 프롬프트 (복붙용)

> jamaica8612/yeona-math-adventure-source 레포를 세션에 추가하고 클론한 뒤,
> `Assets/YeonaMathAdventure/Documentation/HANDOFF.md`를 읽고 "다음 단계"의 첫 항목부터
> 이어서 작업해 줘. 브랜치는 `claude/age4-track-and-tts`에서 시작하되, 이미 main에
> 머지됐으면 main에서 새 브랜치를 파서 진행해. 작업 후 이 HANDOFF 문서를 갱신하고
> 커밋·푸시·PR까지 해 줘. 이 환경에는 Unity가 없으니 MathCore 순수 로직은 mono 하니스로
> 검증하고, Unity 검증 항목은 체크리스트로 정리해서 알려 줘.
