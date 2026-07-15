# Original art provenance

This file records the independently generated visual sources used by the `Yeona Math Adventure`
premium vertical slice. No Antura, GCompris, Matheor, stock-pack, or third-party character artwork
was supplied to the image generator or copied into these files. The project's own generated concept
mockups were supplied as visual references when deriving their corresponding clean backplates.

Generation date: 2026-07-15 KST  
Generation method: OpenAI built-in image generation, invoked for this project  
Post-processing: the green background of `StarCompanion.png` was removed locally to create a
transparent PNG; no third-party art was composited into the result.

## Runtime artwork

| File | Dimensions | Purpose | SHA-256 |
|---|---:|---|---|
| `Resources/YeonaMathAdventure/Art/ForestFeast-Backplate.png` | 1672×941 RGB | Empty clay-forest playfield for Fair Share | `15044A1ED43C2E63716484CC385B64E184DD55B47575E3464D886B7A812886D2` |
| `Resources/YeonaMathAdventure/Art/StarBridge-Backplate.png` | 1672×941 RGB | Empty floating-island bridge world and six bridge sockets | `4BEB9E3B53785CE4C8D53507B2568DF37EA0A902371BA0CE118A00338219D851` |
| `Resources/YeonaMathAdventure/Art/MirrorGarden-Backplate.png` | 1672×941 RGB | Empty clay rooftop workshop playfield for spatial puzzles | `A260068148859D62BFCA02A7C4B280CCA88008834BEA0E1A066CB63EE83C505E` |
| `Resources/YeonaMathAdventure/Art/StarCompanion.png` | 1254×1254 RGBA | Transparent yellow clay star guide, `반디` | `3D0EEC945F34307E4803E43F234707F34C38A368275B1B1ED92BC6E63EC9E6F3` |

Prompt summaries:

- a clean, empty premium clay forest clearing with the central 65% open and a centered 4:3-safe
  composition; no characters, UI, items, or text;
- a clean sunset floating-island bridge with six empty glowing sockets and a centered 4:3-safe
  composition; no character, hand, tiles, UI, or text;
- a clean sunny clay rooftop workshop platform with an empty central grid area; no characters,
  blocks, UI, or text;
- an original friendly yellow clay star companion with a purple scarf, full body, isolated on a
  green chroma background; no shadow, text, or extra objects.

The source outputs are represented by the project copies above. The APK has no runtime dependency
on Codex or on any image-generation service.

## Non-runtime concept references

`Documentation/ReferenceArt/ForestFeast-Concept.png`, `StarBridge-Concept.png`, and
`MirrorGarden-Concept.png` are flattened visual-direction mockups. They are kept outside
`Resources`, are not packed as runtime gameplay backgrounds, and are not treated as interactive
art. Their role is limited to documenting the approved clay-world direction.

| File | Dimensions | SHA-256 |
|---|---:|---|
| `Documentation/ReferenceArt/ForestFeast-Concept.png` | 1672×941 RGB | `61CD6909370EB7BE82F12E3254DAD8CEBE352FDB2920083C1162ED21A9298100` |
| `Documentation/ReferenceArt/StarBridge-Concept.png` | 1672×941 RGB | `582B5D3F5D49C58CA6BD25F89CC70620EB2C15BD706B3BDAD0956127B4ECB187` |
| `Documentation/ReferenceArt/MirrorGarden-Concept.png` | 1672×941 RGB | `FC1217AC39B3641EC40736E0C26236F9D3677F5A2189A2BC8E303D43A5C9E4AE` |

## Rights and distribution note

These images were generated specifically for this project through the user's OpenAI account and
are original project artwork rather than third-party open-source material. Their use and
distribution remain subject to the applicable OpenAI service terms. They do not alter the license
of Antura source or assets used elsewhere in the fork.
