# Yeona Math Adventure — Unity source overlay

`연아의 별다리 모험` v0.2.0의 **소스 전용 저장소**입니다. Unity 캐시, Android SDK, 빌드 중간 파일과 APK는 포함하지 않습니다.

이 저장소는 Antura 전체를 다시 복제한 독립 Unity 프로젝트가 아니라, 지정된 Antura 원본 위에 덮어쓰는 작은 소스 오버레이입니다. 게임 전용 C# 코드, 씬, 아트, 폰트, 테스트, 문서와 필요한 설정만 담았습니다.

## 기준 프로젝트

- Antura: <https://github.com/vgwb/Antura>
- 기준 커밋: `a60a8ba9e87d054aea5d757188cc2050e9289fb1`
- Unity: `6000.4.11f1`
- Android 패키지: `com.yeona.mathadventure`
- 앱 버전: `0.2.0` (`versionCode 3`)

## 프로젝트 복원

1. Antura를 별도 폴더에 복제하고 기준 커밋으로 이동합니다.

   ```powershell
   git clone https://github.com/vgwb/Antura.git
   Set-Location Antura
   git checkout a60a8ba9e87d054aea5d757188cc2050e9289fb1
   ```

2. 이 저장소의 `Assets`와 `ProjectSettings` 폴더 내용을 Antura 프로젝트의 같은 경로에 덮어씁니다. 기존 Antura 폴더를 삭제하지 마세요.

   ```powershell
   Copy-Item "<이 저장소>\Assets\*" "<Antura>\Assets" -Recurse -Force
   Copy-Item "<이 저장소>\ProjectSettings\*" "<Antura>\ProjectSettings" -Recurse -Force
   ```

3. Unity Hub에서 Android Build Support, SDK/NDK Tools, OpenJDK가 설치된 Unity `6000.4.11f1`로 Antura 프로젝트를 엽니다.
4. Unity 메뉴에서 `Yeona Math > Setup > Configure Project`를 실행합니다.
5. Android가 활성 빌드 타깃인 상태에서 `Yeona Math > Build > Android Debug APK`를 실행합니다.

기본 APK 출력 경로는 다음과 같습니다.

```text
Builds/YeonaMathAdventure/YeonaMathAdventure-v0.2.0.apk
```

## 포함된 핵심 범위

- `Assets/YeonaMathAdventure/`: Math Journey, 문제 모델, 적응형 기록, 게임 UI와 미니게임 3종
- `Assets/YeonaMathAdventure/Tests/`: 문제 생성·검증·난이도·레이아웃 EditMode 테스트
- `Assets/YeonaMathAdventure/Resources/`: 게임용 이미지와 런타임 리소스
- `Assets/_core/.../OnlineAnalytics.cs`: 전용 패키지에서 Antura 온라인 분석 초기화를 차단하는 최소 패치
- `ProjectSettings/`: 전용 씬, 패키지명, 가로 화면, ARM64 Android 설정

## 제외된 항목

- Antura 원본 전체 소스와 Git 기록
- `Library`, `Temp`, `Logs`, `Builds`, `BuildTools`, `TestResults`
- APK/AAB, Android SDK/NDK, Gradle 및 IL2CPP 캐시
- API 키와 서명용 keystore

## 개인정보와 AI

게임은 AI API 없이 완전히 동작하며 진행도는 앱 전용 로컬 저장소에 기록됩니다. API 키는 소스나 APK에 포함하지 않습니다. AI 확장 경계와 JSON 스키마는 `Assets/YeonaMathAdventure/Documentation`에서 확인할 수 있습니다.

## 라이선스와 출처

Antura의 코드·에셋 조건은 `ANTURA_LICENSE.md`, Pretendard는 `Assets/YeonaMathAdventure/ThirdParty/Pretendard/OFL-1.1.txt`, 전체 출처와 생성 아트 기록은 `Assets/YeonaMathAdventure/Documentation`에서 확인할 수 있습니다. 프로젝트 고유 코드와 아트에는 별도로 명시되지 않는 한 추가 사용 허락을 부여하지 않습니다.

플레이용 APK는 [공식 v0.2.0 릴리스](https://github.com/jamaica8612/yeona-math-adventure/releases/tag/v0.2.0)에서 받을 수 있습니다.

