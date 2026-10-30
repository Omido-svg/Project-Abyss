Project Abyss R7 NativeMixed runtime patch — 2026-10-31

WHAT CHANGED
- Seven existing NativeMixed SOs upgraded to schema 2 with authored rolled damage flags and source can-break semantics.
- R7NativeMixedAbilityEngine now creates typed R5 damage/status Commands from native SO only.
- R7ProductionLegacyStatusMigration is now a graph catalog routing boundary; SkillEffectEntry/Definition only supplies UI diagnostic metadata. No legacy entry.TryApply/definition.Apply at runtime for the seven routes.
- Existing native 15 resource + 9 bleeding SO and other systems left intact.

CHECKED IN UNITY
- Mixed V2 data gate: 7 valid graphs, no source asset mutations.
- Old resource/status 24 Data Gate: PASS=11 FAIL=0.
- 7 mixed cards direct parity: PASS=50 FAIL=0.
- Actual ClashManager parity: PASS=10 FAIL=0.
- Edit mode compilation/console: no reported errors.
- Battle Test Scene remains dirty, not saved. No Git operations.

APPLY
Extract this ZIP; under its ONE top-level folder, merge Assets/ into Unity project's Assets/ preserving .meta. Backups/ are not assets to import. Existing project already has the patch applied.

ROLLBACK
The prior routing source and meta are in Backups/Assets/. The previous stage versions of NativeMixed SOs plus scripts/meta are in Backups/PreviousStage/Assets/. Restore those corresponding original files (preserve meta GUID), and remove the newly added R7NativeMixedAbilityEngine.cs and R7NativeMixedSchema2Migration20261031.cs plus their meta files. Refresh Unity. Never import Backups into game Assets.

SCOPE LIMIT
Only seven selected mixed/bleeding cards use this native runtime path. The outer SkillEffectDispatcher, old SkillDefinition identity and UI result metadata remain as transitional integration boundaries. R7 total content conversion, passives/emotions/items and R9 gates are unfinished.
