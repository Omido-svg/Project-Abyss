# Project Abyss R7-0 — content migration inventory and handoff (2026-11-02)

## Scope / policy
This is an **inventory-only** R7-0 handoff. No SkillDefinition, ScriptableObject, prefab, serialized GUID, scene, or runtime implementation was altered.
No R9 integrated regression and no PlayMode verification was run for this stage.
Only the existing R7-0 EditMode data ledger's 12 simple inventory checks were executed.
Asset path ownership and recommended phases are *routing estimates*; not verified gameplay equivalence.

## Skills
Total: 145 SkillDefinition assets, each associated with Asset GUID, SkillId, physical path, effect counts, and current migration category.

- BLOCKED_DUPLICATE_ID: 8
- METADATA_OBSERVER_ONLY: 9
- PENDING_EFFECT_NATIVE: 42
- PENDING_NO_EFFECT_POLICY: 49
- VERIFIED_NATIVE_GRAPH: 36
- VERIFIED_PREPARATION_NATIVE_GRAPH: 1

Recommended primary next phase by asset-path owner (including already-native R7-12 rechecks):
- R7-12: 37
- R7-2: 8
- R7-5: 29
- R7-6: 39
- R7-7: 20
- R7-8: 12

- Duplicate SkillId: 4 groups / 8 assets. Exact GUID pairs are in R7_0_DUPLICATE_SKILL_IDS.csv. R7-0 does NOT rename IDs, change GUIDs, or resolve save compatibility.
- Entries in R7_0_SKILL_EFFECT_REFERENCES.csv: 118 (all presently referenced authored/legacy definitions in SkillDefinition and Rolls).
- Definition-level native coverage is a source identity/schema categorization, NOT a claim that all original effects or timings passed an equivalence test in this stage.
- Effect-policy-missing assets are routed to owner-specific stages as **policy review**; absence of authored entries is not proof that a skill has no intended effect.
- The 9 metadata-only assets require an explicit R7-8 policy review and may have further character-specific follow-up.
- R7-3/R7-4 are effect-family migration techniques; skill ownership routing to R7-5/6/7 does not bypass reusable generic effect implementation when appropriate.
 
## Other content
- EmotionAugmentDefinition: 63; 1 has unresolved canonical design value (emotion.awe.t3.01 — Strength N). Do not guess or override it. R7-10.
- RunItemDefinition: 60; all 60 currently use TempBalanceRunItem. Do not treat these as fully native. R7-11.
- CharacterAugment ScriptableObject: 1 (Yujin_HwanhyeongFlow), typed asset recorded in R7_0_CHARACTER_PASSIVE_ASSETS.csv.
- CharacterAuthoringBundle: 6; loadout/augment references, override policy and empty lists recorded in R7_0_CHARACTER_BUNDLE_PASSIVE_LOADOUTS.csv.
- Passive/mechanic code scope: 26 *source candidates*, including existing original 18 filename-heuristic candidates and extra mechanic-folder/authoring pipeline files. Not 26 proven gameplay passives.
- Actual passive lifecycle, event subscriptions, enemy role mechanics and registrations remain R7-9 work.
- Yujin_HwanhyeongFlow is linked in Yujin_TODO_Bundle, which overrides its loadout.
- R7-1's existing source and SO are *not modified* in this stage.

## Verification
- Existing R7-0 inventory-only check: 12 PASS / 0 FAIL.
- R9 full regression: NOT RUN (per user request).
- PlayMode tests: NOT RUN.
- Unity compilation: NOT REQUIRED because this stage adds only CSV/MD reports and packages already-present ledger source unmodified.
- Scene save: NOT PERFORMED. Git commit/push: NOT PERFORMED.

## Outputs
- R7_0_ALL_145_SKILLS.csv — source skill inventory
- R7_0_OWNER_PHASE_ROUTING.csv — single primary routing stage per skill with explicit policy flags
- R7_0_SKILL_EFFECT_REFERENCES.csv — effect reference slots and provenance
- R7_0_DUPLICATE_SKILL_IDS.csv — all 8 collision assets
- R7_0_ALL_63_AUGMENTS.csv and R7_0_ALL_60_ITEMS.csv
- R7_0_CHARACTER_PASSIVE_ASSETS.csv
- R7_0_CHARACTER_BUNDLE_PASSIVE_LOADOUTS.csv
- R7_0_PASSIVE_CODE_SCOPE_EXPANDED.csv
- R7_0_PASSIVE_MECHANIC_SCRIPT_CANDIDATES.csv — original 18 filename candidates

## Next step
R7-2: decide source SkillId collisions and save-compatible GUID policy; do not automatically rename or merge sources.
