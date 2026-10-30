# Project Abyss — R7 145 SkillDefinition Dry Run (2026-10-30)

This is a READ-ONLY manifest. No SO, GUID, prefab, scene or combat runtime content was changed.
A CANDIDATE_METADATA_ONLY entry is NOT migrated, balanced, or parity-verified.

- Total SkillDefinitions: 145
- Total authored EffectEntries: 102
- SkillDefinitions with legacy effect references: 11
- Duplicate stable SkillId values: 4
- Skill assets affected by duplicate SkillIds: 8
- Simple normal no-effect metadata pilot candidates: 9

## Action type breakdown
- Duel: 57
- NormalAttack: 37
- Preparation: 33
- Prestige: 18

## Migration classification
- BLOCKED_DUPLICATE_ID: 8
- CANDIDATE_METADATA_ONLY: 9
- MANUAL_CONTRACT_REVIEW: 43
- MANUAL_NO_EFFECT_POLICY: 20
- REQUIRES_EFFECT_MAPPING: 65

## Blocking duplicate IDs
- olaf.duel.standard (2 assets)
  - Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_Standard.asset [d685378dcfe71d3418bf9cde7ff0542d]
  - Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Olaf_D_01.asset [cd90424506385934784ae6ead386e62b]
- olaf.preparation.bloto (2 assets)
  - Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_ShowOff.asset [0cd53d9b56e64a24b82911598f800af9]
  - Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Preparation/Olaf_P_01.asset [3b904fc8be62e1d4e9f69e195c2f1c9f]
- yujin.preparation.hwanhyeong.attack (2 assets)
  - Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_공격.asset [0a914f0a1b2c3d4e5f60718293a4b5c6]
  - Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Yujin_P_03.asset [271781acc71e5ca46aa05fd44090435c]
- yujin.preparation.hwanhyeong.defense (2 assets)
  - Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_수비.asset [0b914f0b1c2d3e4f5061728394a5b6c7]
  - Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Yujin_P_04.asset [e13cd646b4403b545a8e6ca6def71493]

## Manual migration rules
1. Preserve asset GUIDs; do not auto-regenerate or overwrite stable skill IDs.
2. Resolve each duplicate with explicit authoring signoff and save compatibility plan.
3. Introduce AbilityRunner by vertical slice, compare original damage, statuses, resources, event timing.
4. Keep Hifumi chained Chinchiro reactive rolls on their specialist contract until real Golden parity.
5. Legacy-to-new dispatch must be exclusive per effect; never double-apply.
6. This audit never upgrades or migrates an asset automatically.
