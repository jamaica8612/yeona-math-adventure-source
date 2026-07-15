# 연아의 별냥이 놀이집 (Yeona Pet Playhouse)

만 4세 연아를 위한 **무실패 펫 돌보기 게임**. 별냥이(아기 고양이) "나비"에게 당근을 주고, 목욕시키고,
재우고, 쓰다듬는 토카보카식 장난감 상자. 점수·실패·시간제한·광고·네트워크 없음.

- Unity `6000.4.11f1` / Android (ARM64, IL2CPP), 가로 화면 전용
- 패키지: `com.yeona.petplayhouse`, v0.1.0
- 모든 UI는 코드로 조립. 그림·음성 파일을 지정 경로에 넣으면 자동 반영(폴백 구조)

## 프로젝트 열기 (처음 한 번)

이 저장소는 소스만 담는다. Unity 캐시(Library 등)는 커밋하지 않는다.

1. Unity Hub → New Project → **2D (Built-In)** 템플릿, Unity `6000.4.11f1`,
   Android Build Support 포함.
2. 생성된 프로젝트 폴더에 이 저장소의 `Assets/` 폴더를 복사(덮어쓰기).
   또는 이 저장소를 클론한 뒤 그 안에서 Unity로 열어도 된다(Unity가 나머지를 생성).
3. 에디터 메뉴 `Yeona Pet > Setup > Configure Project` 실행
   (플레이어 설정 + **Jua 폰트 정적 아틀라스 굽기** + 씬 생성).
4. Test Runner에서 EditMode 테스트(`Yeona.Pet.Core.Tests`) 실행 — 전부 통과해야 함.
5. Android가 활성 빌드 타깃인 상태에서 `Yeona Pet > Build > Android Debug APK`.
   출력: `Builds/YeonaPetPlayhouse/YeonaPetPlayhouse-v0.1.0.apk`

배치 모드 빌드:

```
Unity -batchmode -quit -projectPath <프로젝트> -buildTarget Android ^
  -executeMethod YeonaPetPlayhouse.Editor.YeonaPetBuild.BuildAndroid -logFile build.log
```

## 콘텐츠 넣기 (코드 수정 불필요)

- **음성**: `Assets/YeonaPetPlayhouse/Documentation/VOICE_SCRIPT.md` 대본을 AI 음성으로
  생성해 `Resources/YeonaPetPlayhouse/Voice/{키}.wav`로 저장.
- **그림**: `Documentation/ART_REQUEST.md` 발주서대로 생성해
  `Resources/YeonaPetPlayhouse/Art/`에 저장. 없으면 코드 도형으로 표시된다.

## 구조

```
Assets/YeonaPetPlayhouse/
  Runtime/PetCore/      순수 C# 시뮬레이션 (배고픔·씻기·졸림, 무실패 규칙) + asmdef
  Runtime/              UI 킷, 연출(주스), 음성 재생, 메인 부트스트랩
  Editor/               셋업·빌드 스크립트 (Jua 폰트 정적 굽기 포함)
  Tests/EditMode/       PetCore 테스트
  Resources/Fonts/      Jua-Regular.ttf (SIL OFL, Google Fonts)
  Documentation/        대본·아트 발주서·이어하기 문서
```

## 라이선스 표기

- Jua 폰트: © woowahan brothers, SIL Open Font License 1.1 (Google Fonts 배포본)
- 게임 코드·디자인: 개인 프로젝트 (별도 허락 없음)
