# R7 migration handover, 2026-11-02
Inventory is read-only; native coverage means an existing,
verified SO + runtime native router, NOT full R7 completion.

- Source SkillDefinitions: 145
- Exact GUID/ID/name matched native SOs: 36 (Resource 15, Status 9, Mixed 7, Unique Gauge 5)
- Preparation all 3 effect mutations native; no legacy entry gates: 1
- Metadata-only identity observers (not gameplay migrated): 9
- Blocked duplicate stable SkillIDs: 4 (8 assets)
- Remaining full native gameplay not proven: 109 skill assets

## Remaining by classification
- BLOCKED_DUPLICATE_ID: 8
- METADATA_IDENTITY_PILOT_ONLY: 9
- NATIVE_MIXED: 7
- NATIVE_RESOURCE: 15
- NATIVE_STATUS: 9
- NATIVE_UNIQUE_GAUGE: 5
- PENDING_CHARACTER_SPECIALIST: 42
- PENDING_NO_EFFECT_POLICY: 49
- PREPARATION_FULL_NATIVE_R5: 1

## Guardrails
1. Never infer native coverage from a metadata observer.
2. Never migrate colliding IDs by ID alone.
3. Remove legacy Entry.TryApply/adapter execution only when native R5 command parity is proven.
4. Preserve original authored assets, GUIDs and per-roll conditions.
5. Re-run R6 stepwise reaction and damage 8 Gates after every gameplay migration.
