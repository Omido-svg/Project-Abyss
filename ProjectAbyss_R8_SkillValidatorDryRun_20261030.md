# R8 Skill Authoring Validator — Read-Only Dry Run (2026-10-30)
No ScriptableObjects, asset IDs, prefabs, or scenes have been modified.

- SkillDefinition assets checked: 145
- Blocking issues: 8
- Unique stable SkillId duplicates: 4
- Warnings: 85
- Gate status: BLOCKED

## Problem distribution
- COLOR_UNSET_FALLBACK: 47 (WARNING)
- DUPLICATE_STABLE_ID: 8 (BLOCKER)
- LEGACY_EFFECTS_NEED_MANUAL_MIGRATION: 11 (WARNING)
- MIXED_LEGACY_AND_TEMPLATE_EFFECTS: 11 (WARNING)
- PHASE_A_SCHEMA_NOT_MARKED: 8 (WARNING)
- PRESENTATION_ASSET_UNASSIGNED: 8 (WARNING)

## Blocking assets
- DUPLICATE_STABLE_ID | olaf.preparation.bloto | Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_ShowOff.asset
- DUPLICATE_STABLE_ID | olaf.duel.standard | Assets/2. Data/Characters/Design2026/Olaf/Skills/Olaf_Standard.asset
- DUPLICATE_STABLE_ID | yujin.preparation.hwanhyeong.attack | Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_공격.asset
- DUPLICATE_STABLE_ID | yujin.preparation.hwanhyeong.defense | Assets/2. Data/Characters/Design2026/Yujin/Skills/환형_수비.asset
- DUPLICATE_STABLE_ID | olaf.duel.standard | Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Olaf_D_01.asset
- DUPLICATE_STABLE_ID | olaf.preparation.bloto | Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Preparation/Olaf_P_01.asset
- DUPLICATE_STABLE_ID | yujin.preparation.hwanhyeong.attack | Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Yujin_P_03.asset
- DUPLICATE_STABLE_ID | yujin.preparation.hwanhyeong.defense | Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Yujin_P_04.asset

## Migration policy
1. Read-only scan first. User approval required for any content edits.
2. Back up each original asset AND .meta before a migration.
3. Keep GUID/stable-ID compatibility and reconcile duplicate IDs explicitly.
4. Only commit an effect when specialist reports Applied.
5. Test G0/R1/R5/R6 and character Golden before switching any asset.
6. Roll back the exact modified asset on any parity failure.
