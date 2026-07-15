# Codex 이미지 발주서 (아트 생성 프롬프트)

Codex/GPT 이미지 생성으로 아래 파일을 만들어 지정 경로에 넣으면 코드 수정 없이
게임에 반영된다 (`PremiumMathVisuals.TryApplyItemSprite` 폴백 구조).
완료한 파일은 `ART_PROVENANCE.md`에 규격·SHA-256을 추가 기록할 것.

## 공통 스타일 가이드 (모든 프롬프트 앞에 붙일 것)

> 기존 게임 아트와 같은 세계관: 부드러운 점토(클레이) 질감의 3D 렌더 스타일,
> 파스텔+선명한 원색, 둥글둥글한 실루엣, 유아용(만 4세) 그림책 느낌.
> 기준 이미지: `Resources/YeonaMathAdventure/Art/StarCompanion.png` (노란 클레이 별 캐릭터
> 반디)를 참조 이미지로 첨부해 톤을 맞춘다.

영문 공통 프리픽스 (이미지 생성기에 그대로 사용):

```
A cute claymation-style 3D render for a toddler math game, soft clay texture,
pastel and bright colors, rounded silhouette, consistent with the attached
yellow clay star mascot. Single object centered, isolated on a plain solid
green chroma background (#00FF00), no shadow on the ground, no text, no UI,
no extra objects, no watermark.
```

- **후처리(필수)**: 초록 배경 제거 → 투명 PNG(RGBA). StarCompanion.png와 동일한 방식.
- **규격**: 1024×1024, 오브젝트가 캔버스의 80~90%를 채우도록.
- **저장 경로**: `Assets/YeonaMathAdventure/Resources/YeonaMathAdventure/Art/Items/`
  (Items 폴더는 새로 만든다. Unity가 .meta를 자동 생성.)

## 발주 목록 1 — FairShare 게임 조각 (코드 연결 완료, 넣는 즉시 적용)

| 파일명 (정확히) | 내용 프롬프트 (프리픽스 뒤에 추가) |
|---|---|
| `Item-Tangerine.png` | a plump clay tangerine (mandarin orange) with two tiny green leaves and a happy smiling face with rosy cheeks |
| `Item-Star.png` | a chubby golden clay star sticker with a proud smiling face and rosy cheeks |
| `Item-Pencil.png` | a short stubby clay colored pencil, sky blue body, with a friendly smiling face |
| `Item-Marble.png` | a round shiny clay marble, berry pink with a soft white swirl highlight and a cheerful face |

주의: 네 개 모두 **같은 세션/같은 스타일 참조로 연속 생성**해 톤을 통일할 것.
얼굴 스타일(눈·볼터치)은 반디(StarCompanion)와 같은 문법으로.

## 발주 목록 2 — 반디 표정 포즈 (다음 단계용, 코드 연결은 추후)

경로: `Assets/YeonaMathAdventure/Resources/YeonaMathAdventure/Art/`

| 파일명 | 내용 |
|---|---|
| `StarCompanion-Cheer.png` | 같은 반디 캐릭터가 두 팔을 번쩍 들고 환호하는 포즈, 눈웃음 |
| `StarCompanion-Think.png` | 같은 반디가 고개를 갸웃하며 한 손을 턱에 대고 생각하는 포즈 |

프롬프트에 반드시 원본 StarCompanion.png를 참조 이미지로 첨부하고
"the exact same character, same proportions, same scarf" 를 명시할 것.

## 발주 목록 3 — 배경 재생성 (선택, 스타일 충돌 완화)

현재 배경 3장은 실사에 가까운 밀도라 평면 UI와 충돌한다. 재생성 시:

```
... same claymation world, but LOW DETAIL: large simple shapes, soft gradients,
muted colors, strong depth blur in upper half, the central 65% almost empty and
calm so UI panels stay readable. 1672x941. No characters, no text, no UI.
```

| 파일명 (기존 덮어쓰기) | 장면 |
|---|---|
| `ForestFeast-Backplate.png` | 점토 숲속 빈터, 피크닉 느낌 |
| `StarBridge-Backplate.png` | 노을 하늘의 떠 있는 섬과 별다리, 소켓 6개 |
| `MirrorGarden-Backplate.png` | 맑은 낮의 점토 옥상 정원 작업대 |

## 반입 절차

1. 생성 → 초록 배경 제거(투명 PNG) → 위 경로에 정확한 파일명으로 저장.
2. Unity를 한 번 열어 .meta 생성 확인 후, 게임 실행해 FairShare에서 조각이
   그림으로 나오는지 확인 (없으면 파일명·경로 오타).
3. `ART_PROVENANCE.md`에 표 형식으로 파일·크기·SHA-256·프롬프트 요약 추가.
4. 커밋 메시지 예: `Add clay item sprites for FairShare pieces`.
