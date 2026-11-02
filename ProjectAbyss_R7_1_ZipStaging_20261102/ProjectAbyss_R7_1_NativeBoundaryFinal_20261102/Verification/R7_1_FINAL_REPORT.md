# Project Abyss R7-1 — final native boundary closure (2026-11-02)
## 기준
- Current Unity source of truth: F:\GitCorp\Project-Abyss (branch commits unchanged)
- Design reference: 전투 시스템 규칙(1029)(3).md, 특히 2, 3.4, 8, 11장.
- R7-0 full inventory baseline: 145 SkillDefinition, 63 EmotionAugmentDefinition, 60 RunItemDefinition.
- R7-1 scope: already-native dispatch boundary removal and fail-closed protection, not R7-2..R7-12 migration.

## 완료 변경
1. R7ProductionPreparationStatusMigration.IsAuthoredSource: source signature centralized and used consistently by native router and dispatcher.
2. SkillEffectDispatcher.Execute: native resource/bleeding/unique-gauge/mixed/Preparation remain above opt-in adapter; when an approved source has no available native route, throw instead of executing old Entry.TryApply.
3. SkillEffectDispatcher.ExecuteOwner: native registered sources and Preparation do not call legacy entries on owner-lifecycle callbacks. Current 36 native skill sources have zero authored Owner-lifecycle entries in the current project.
4. Native registry: 36 Resource/Status/Mixed/UniqueGauge graphs guarded by stable ID + authored asset name, and 1 Preparation source separately guarded.
5. Legacy adapter remains in R7ExclusiveEntryMigrationScope for unmigrated effect families and verification pilots. Global deletion would break R7-3..R7-11.
6. R7NativePreparationStatusAssetMigration: removed an inert always-false check from source validation (no authoring SO changed).
7. R7Stage1NativeBoundaryGate: extended B01-B12 to B01-B20, including missing native service, all 36 native identities, owner-lifecycle and preserved legacy original reference checks.

## Confirmed status
- 36 baseline native graph skill sources; 1 separate native Preparation; no source GUID changes.
- Elite Preparation source: Assets/2. Data/Characters/Enemies/EliteEnemy/SkillS/전투 태세(도사림).asset.
- Preparation authored effects: 3 legacy references still preserved in SkillDefinition for safe rollback. The approved runtime execution does not use them.
- Preparation native status graph valid: True. Native setup graph valid: True.
- Source SkillId: 356e72e97ac84a3a8757d1a188237f43.
- Original source SHA256: 30155100f96569bc0a0630eaf6880b0a68c1cd495c3a0bb1c66d971306cde93c.
- Original source .meta SHA256: b12e600b34886b3a2f4a76466479cc8692d68bff4e27202c69c62030fd7ca236.
- Remaining source-code legacy call sites found: 9 (these are not proof of active migrated paths). See R7_1_REMAINING_LEGACY_BOUNDARIES.csv.
- Registered native signatures: 36 distinct ID/name pairs.

## 필수 검증만 실행
- R7-1 EditMode Boundary Gate: 20 PASS / 0 FAIL.
- Unity C# compilation: completed; script diagnostics 0 error (dispatcher and stage gate); Unity Editor console returned no errors.
- R9 full regression: NOT RUN (user request).
- PlayMode equivalence tests: NOT RUN in this iteration. Context 37 previous 23/23 and 9/9 are historical, not re-executed.
- Original Battle Test Scene not saved, source SkillDefinition and its .meta unchanged, no git commit/push.

## 정본 대비 유의사항 (R7-8 콘텐츠 정책 검토 항목)
- The current design document says Preparation activates during planning, without rolls (sections 2, 3.4, 8). Existing Preparation runtime code externally dispatches OnExecute, and its internal native graph labels the cause RollUsing even though the applied effect is immediate. This turn does not change timing semantics or serialized native SO trigger.
- The authored enemy elite card 전투 태세(도사림) with Bleeding/Burn/FlatSetup is not explicitly specified with the same 3 values in the current design document. Existing authored values remain unchanged. Do not claim canonical numeric parity.
- The design's Viljar Bleeding is a unique cumulative stack, not a generic N/T status (section 17.1); this elite's old Bleeding 1/3 source is retained as noncanonical/test content until design policy decision.
- If later converting or removing this enemy skill, handle it through R7-8; do not silently reinterpret it as a canonical card.

## 제약
- Fail-closed guard identifies approved authored source by StableId+AssetName at runtime. The Authoring GUID is checked by native graph/migration metadata and Unity Editor gates; AssetDatabase GUID lookup is unavailable in builds. Full save compatibility audit belongs to R7-2.
- The 36-source guard covers a missing registered route while a graph SO exists. Removing graph SO itself is a separate content-integrity violation and must be handled by asset validators/build checks.
- Only approved-native sources are denied fallback. Non-migrated Skills continue to use the old entry path safely until migrated.

## 후속
R7-2: resolve 4 duplicate SkillId groups / 8 source assets with unchanged GUID and save compatibility policy; do not auto-renumber.
