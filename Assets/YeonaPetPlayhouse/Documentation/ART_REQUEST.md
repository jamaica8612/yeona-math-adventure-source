# 아트 발주서 — GPT 이미지 생성용 상세 프롬프트

모든 그림 자리는 "파일이 있으면 그림, 없으면 코드 도형" 폴백 구조다.
아래 경로에 정확한 파일명으로 넣으면 다음 실행부터 자동 반영된다.

- 저장 루트: `Assets/YeonaPetPlayhouse/Resources/YeonaPetPlayhouse/Art/`
- 규격: 1024×1024 (배경 제외 전부), **투명 PNG** (초록 배경 제거 후 저장)
- 캐릭터가 캔버스의 85%를 채우게. 발밑 그림자 금지(게임이 그림자를 따로 그림)

## 진행 순서 (캐릭터 일관성이 생명)

1. **1단계**: 아래 "기준 컷" 프롬프트로 `Kitty-Happy.png`를 먼저 생성한다.
   마음에 들 때까지 이 한 장만 반복 생성 (얼굴·비율·색이 이후 전부의 기준이 됨).
2. **2단계**: 확정된 Happy 이미지를 **대화에 첨부한 상태로** 나머지 표정 4장을
   하나씩 요청한다. 프롬프트에 반드시 "the exact same character as the attached
   image"가 들어가야 한다.
3. **3단계**: 아이콘·배경 생성 (캐릭터 없어도 되므로 순서 무관).
4. 각 이미지는 초록 배경(#00FF00)으로 생성 → 배경 제거 → 투명 PNG 저장.

## 1단계 — 기준 컷: `Kitty-Happy.png`

GPT에 그대로 붙여넣기:

```
Create a character illustration for a toddler's pet-care game.

CHARACTER: A baby star-kitten named "Nabi" — a chubby round kitten,
cream-colored (#FFF1DC) soft fur, peach-colored (#FFC9A3) inner-ear and
ear tips, a small golden five-pointed star marking on its chest,
big sparkly dark-purple eyes with white highlights, tiny pink triangle nose,
three short whiskers on each cheek, rosy pink round blush cheeks,
a short stubby tail curled beside its body. Sitting upright facing the viewer,
happy open-mouth smile showing a tiny tongue.

STYLE: soft pastel storybook illustration for ages 3-5, chubby rounded shapes,
thick clean dark-plum outlines (#4A3B5C), flat cel shading with one soft
shadow tone, warm and friendly, similar to Toca Boca / Sago Mini character style.

FRAMING: full body centered, fills 85% of a square canvas,
isolated on a plain solid green background (#00FF00).
NO text, NO watermark, NO ground shadow, NO background objects.
```

## 2단계 — 표정 4장 (Happy 확정본을 첨부하고 하나씩)

공통 머리말 (각 프롬프트 앞에 붙이기):

```
Using the attached image as the exact character reference — the same baby
star-kitten "Nabi", identical proportions, colors, star chest marking,
outline style and framing (85% of square canvas, solid #00FF00 background,
no shadow, no text) — draw the SAME character with ONLY the pose/expression
changed as follows:
```

| 파일명 | 뒤에 붙일 표정 지시 |
|---|---|
| `Kitty-Hungry.png` | now looking hungry: both front paws holding its round tummy, mouth slightly open in a small "waah" shape, pleading puppy-dog eyes looking up, one ear drooping slightly |
| `Kitty-Dirty.png` | now a bit messy: three small light-brown mud smudges on cheek, tummy and paw, sheepish embarrassed smile, sweat-drop near the ear |
| `Kitty-Sleepy.png` | now sleepy: heavy half-closed droopy eyelids, yawning with one paw near its mouth, tiny tear in one eye corner, ears relaxed downward |
| `Kitty-Sleeping.png` | now asleep: curled up in a ball on its side, eyes fully closed as gentle curved lines, peaceful tiny smile, tail wrapped around its body |

## 3단계 — 아이콘·배경

스타일 머리말 (각 프롬프트 앞에 붙이기):

```
Same art style as before: soft pastel storybook illustration for toddlers,
chubby rounded shapes, thick dark-plum outlines, flat cel shading.
Single object centered, 85% of a square canvas, solid #00FF00 background,
no text, no shadow, no watermark.
```

| 파일명 | 내용 지시 |
|---|---|
| `Icon-Carrot.png` | a plump cartoon carrot, warm orange (#FF9F4A) body with cute horizontal line details, two fresh green (#6FC46D) leaves on top |
| `Icon-Bath.png` | a mint-green (#8FDCC4) rounded soap bar with two glossy light-blue (#BDE7FF) bubbles floating above it, one bubble with a star-shaped sparkle |
| `Icon-Moon.png` | a smiling golden (#FFD97A) crescent moon with closed happy eyes and rosy cheek, one tiny four-pointed star beside it |
| `Room-Wall.png` | (규격 예외: 1920×1080, 배경이므로 초록 배경 불필요·불투명 저장) a toddler's nursery wall in soft pastel pink (#FFE3EC base), one big round window with light blue sky and a smiling sun, a few tiny star and cloud wall stickers, VERY low detail and empty in the center-bottom area so a game character stays readable in front of it. No floor, no furniture, no characters, no text |

## 반입 후 확인

1. 위 경로에 저장 → Unity 실행 → 나비가 그림으로 나오는지 확인
   (안 나오면 파일명·폴더 오타. 대소문자 구분 주의)
2. `THIRD_PARTY_NOTICES.md`에 "캐릭터/아이콘: AI 생성(GPT), 2026-07" 한 줄 추가
3. 표정 전환 확인: 방치해서 배고파지면 Hungry로 바뀌는지

## 대안 (AI 생성이 잘 안 나올 때)

- Kenney.nl (CC0), Noto Emoji(당근 🥕/달 🌙/비누 있음, Apache) — 아이콘 대체 가능
- Kitkit School (CC BY, github.com/XPRIZE/GLEXP-Team-KitkitSchool) — 고품질 동물
  캐릭터·소품. 사용 시 출처 표기 필수
