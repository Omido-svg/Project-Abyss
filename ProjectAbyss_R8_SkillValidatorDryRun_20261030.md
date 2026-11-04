# R8 Skill Authoring Validator — Read-Only Dry Run (2026-10-30)
No ScriptableObjects, asset IDs, prefabs, or scenes have been modified.

- SkillDefinition assets checked: 164
- Blocking issues: 0
- Unique stable SkillId duplicates: 4
- Warnings: 133
- Gate status: NO_STRUCTURAL_BLOCKERS

## Problem distribution
- APPROVED_DUPLICATE_GUID_CONTRACT: 8 (WARNING)
- COLOR_UNSET_FALLBACK: 49 (WARNING)
- LEGACY_EFFECTS_NEED_MANUAL_MIGRATION: 11 (WARNING)
- MIXED_LEGACY_AND_TEMPLATE_EFFECTS: 11 (WARNING)
- PHASE_A_SCHEMA_NOT_MARKED: 27 (WARNING)
- PRESENTATION_ASSET_UNASSIGNED: 27 (WARNING)

## Blocking assets

## Migration policy
1. Read-only scan first. User approval required for any content edits.
2. Back up each original asset AND .meta before a migration.
3. Keep GUID/stable-ID compatibility and reconcile duplicate IDs explicitly.
4. Only commit an effect when specialist reports Applied.
5. Test G0/R1/R5/R6 and character Golden before switching any asset.
6. Roll back the exact modified asset on any parity failure.
