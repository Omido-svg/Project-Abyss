# Project Abyss R7-3 — General single effect migration (2026-11-02)

## Baseline and scope
Based on 전투 시스템 규칙(1029)(3).md (2026-10-29 canon), especially sections 3.3, 3.4, 8 and 11.
This stage does NOT invent new card mechanics or fill unresolved (미정) design values.
The original R7-0 inventory has 145 skill sources. Previously-native: Resource 15, single bleeding 9, Mixed 7, unique gauge 5, plus separately native Preparation 1.
Among remaining authored pending effects, this stage selects four existing **single, non-conditional** generic operations. The majority of remaining Olaf/Yujin effects are character-specialist or multi-entry and belong in R7-4~R7-7.
This patch does NOT claim all remaining effect or gameplay parity has been completed.

## R7-3 added native cohort — 4 exact source assets
- Olaf_D_06 (olaf.duel.hold_on), source GUID b785616cbce19214b8ad54bbd1b879ae, operation ApplyCanonicalStatus, target CurrentTarget, status Rupture, amount 2, duration 2, next turn True, event ExchangeWon
- Olaf_N_15 (olaf.normal.block_way), source GUID 148f3f1d401e1714f972f67064272da2, operation GainBlock, target Owner, amount 10, duration 1, next turn False, event RollUsing
- Olaf_N_14 (olaf.normal.choke), source GUID 4b7777e1d031bed4d834c47ac2b47cd8, operation ApplyCanonicalStatus, target CurrentTarget, status Pain, amount 1, duration 3, next turn True, event ExchangeWon
- Yujin_N_04 (yujin.normal.step_back), source GUID fb67fe25a3cf5af4bb383e8d7bfe492a, operation GainBlock, target Owner, amount 10, duration 1, next turn False, event RollUsing

## Architecture and behavior
- New R7NativeSingleEffectDefinition20261102 SOs are the ONLY runtime effect authoring for the four migrated skills; SkillEffectEntry is used for UI result metadata, not effect application or data.
- New R7NativeSingleEffectEngine20261102 maps one authored SO to one typed R5 command: ChangeResource to R7NativeResourceBackend for guard/block; ApplyCharacterStatus/ApplyPartStatus to R5CombatSpecialistBackend + BattleEffectResolver for statuses.
- Canonical N/T status uses StatusEffectFactory.CreateCanonical0922; next-turn status uses DeferredStatusEffect and the existing turn-start materialization flow. No custom status stack implementation or duplicate resource/health logic.
- IsRegisteredNativeSource fail-closed protection includes the new four resources; if approved route unavailable, no TryApply/legacy-adapter fallback.
- BattleRuntimeServices/BattleRuntimeFactory create and dispose the migration per battle.
- The four old EffectEntry ScriptableObjects, original SkillDefinition, GUIDs, .meta, UpgradeProfile and loadout references are unchanged.
- For Yujin_N_04, the original Yujin0916 StepBackBlock effect calls AddBlock(10) even though its serialized Amount is 1; R7-3 copies the actual literal behavior as SO Amount=10, not the unused field.
- For the three Olaf sources, amounts and statuses are read from original effect templates. EffectiveTiming (including authored timing overrides) is preserved.
- This is a migration of the currently authored PhaseE source behavior, not approval of its game-design balance under the updated 10/29 canon. Skill design/ability pool mismatches require separate R7-5/6/8 content decisions.
- The existing 15 resource and 9 Bleeding native effects are not recreated or rewritten. Bleeding is a character keyword; do NOT generalize it into canonical N/T status.

## Verification (minimal by request)
- Four assets were created in Unity Editor. R7Stage3SingleEffectMigration20261102.Verify: 14 checks passed (3 per asset, 2 overall).
- Additional EditMode checks: four graphs valid, identity guarded, production router recognises each exact SkillDefinition = PASS.
- New runtime/Editor scripts validated by Unity script checker (0 errors).
- Unity Editor console errors 0 and Battle Test Scene remains Dirty in EditMode.
- R9 full integration regression: NOT RUN.
- PlayMode tests and real battle-equivalence execution: NOT RUN. Dynamic effect mutation parity is still unproven until deliberate later testing.
- Git commit/push: NOT performed.
- No original SkillDefinition or SO serialized asset rewritten.

## Remaining
- 42 original pending authored effects: 4 newly routed by R7-3; 38 remain primarily character-specialist/multi effect. This arithmetic is source-level, not a guarantee that the 38 have no generic suboperations.
- R7-4: multi-effect operation extraction and damage+status ordering.
- R7-5/6/7: character-specific keywords, branching mechanics, and multi-entry effects.
- R7-8: no-effect/policy-unresolved assets.
- R7-12: full gameplay parity, legacy adapter disposal only after every remaining source is native.
- R7-0 CSV is deliberately unchanged; use R7_3_145_SKILL_DELTA.csv for the current progression state.
