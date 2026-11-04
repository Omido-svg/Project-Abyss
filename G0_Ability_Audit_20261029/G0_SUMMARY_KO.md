# G0 — 능력 5요소 아키텍처 기준선 감사

- 생성 시각(UTC): `2026-11-04T05:59:40.2742343Z`
- Unity: `6000.0.56f1`
- 브랜치: `Default-Battle-Test`
- 커밋: `bc6168ccc94f1ad65f84d912a01c956fe8aa7871`
- 프로젝트: `F:\GitCorp\Project-Abyss`

## 1. 요약

| 항목 | 값 |
|---|---|
| SkillDefinition 에셋 | 164 |
| 감사 대상 EffectEntry | 118 |
| SkillEffectDefinition 에셋 | 159 |
| 미참조 EffectDefinition | 57 |
| EmotionAugmentDefinition 에셋 | 72 |
| Missing Script 에셋/씬/프리팹 (FATAL) | 0 |
| Missing Script 경고(m_EditorClassIdentifier 복원 가능) | 2 |
| 중복 GUID 그룹 | 0 |
| 수동 검토 필요 Entry | 20 |
| RuntimeEventWake Entry | 0 |
| 키워드/상태 저장 감사 행 | 32 |
| 키워드 수동 검토 대상 | 3 |
| 필수 키워드 목록 | 14 |
| 필수 키워드 미구현 | 5 |
| 필수 키워드 정책 미정 | 10 |
| 용어집 키워드 | 40 |
| 스킬 저작 키워드 Entry | 0 |
| 커스텀 자원 정의 | 0 |
| 실행 호출 그래프 간선 | 64 |
| 가중치 2 이상 스킬 | 0 |
| 가중치 보호 목록 스킬 | 0 |

## 2. 정본 트리거 사용 현황

| 트리거 | Entry 수 |
|---|---|
| AfterDamage(3) | 0 |
| OnBattleStart(16) | 0 |
| OnClashEnd(34) | 0 |
| OnDuelMatched(31) | 4 |
| OnExchangeLose(8) | 2 |
| OnExchangeWin(7) | 51 |
| OnExecute(0) | 61 |
| OnRollFailure(24) | 0 |
| OnRollLose(15) | 0 |
| OnRollSuccess(23) | 0 |
| OnRollWin(14) | 0 |

### 원본 에셋에 기록된 `timing:` 필드 분포

| 값 | 표시 | 개수 |
|---|---|---|
| 0 | 사용시 (굴림별) | 150 |

## 3. Entry 출처(Scope) 분포

| Scope | 개수 |
|---|---|
| DefinitionEffectEntries | 102 |
| LegacyEffects | 16 |

## 4. 위험 경로

### 4.1 RuntimeAfterDamage / CanWakeFromRuntimeEvent

기존 구조는 Authoring Trigger 5종 외에 결과 사건 조건이 `CanWakeFromRuntimeEvent()`를 통해 같은 Entry를 다시 깨운다. 아래 Entry는 R1에서 원본 굴림 Result 연결 여부를 반드시 확인해야 한다.

- 해당 Entry 없음

소스 참조:
- `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs:19: /// 3) Missing Script / 중복 GUID / RuntimeAfterDamage 재점화 경로 / BattleEvent 직접 구독 목록`
- `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs:104: ScanSourceMatches(@"RuntimeAfterDamage");`
- `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs:229: RuntimeAfterDamageReferences = runtimeAfterDamageReferences,`
- `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs:2968: builder.AppendLine($"### 4.1 RuntimeAfterDamage / CanWakeFromRuntimeEvent");`
- `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs:2996: foreach (string reference in summary.RuntimeAfterDamageReferences)`
- `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs:3687: public List<string> RuntimeAfterDamageReferences;`
- `Assets/1. Scripts/Runtime/Skills/Effects/SkillEffectCondition.cs:38: RuntimeAfterDamage = 29,`
- `Assets/1. Scripts/Runtime/Skills/Effects/SkillEffectCondition.cs:243: case SkillEffectConditionType.RuntimeAfterDamage:`
- `Assets/1. Scripts/Runtime/Skills/Effects/SkillEffectCondition.cs:271: SkillEffectConditionType.RuntimeAfterDamage =>`

### 4.2 캐릭터 메커닉 / 시스템의 BattleEvent 직접 구독

아래 목록은 `BattleEvent`를 직접 구독하는 코드다. 한 사건에서 두 구독자가 같은 DamageRequest를 만들지 G0 감사 대상이다.

| 파일 | 행 | 이벤트 |
|---|---|---|
| `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs` | 2272 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Editor/Verification/AbilityBaselineAuditReport.cs` | 2435 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Editor/Verification/AbilityExtensionContractVerification.cs` | 29 | `OnTurnStart` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 212 | `OnBodyPartWeakened` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 213 | `OnBodyPartDestroyed` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 288 | `OnBodyPartWeakened` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 289 | `OnBodyPartDestroyed` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 369 | `OnBodyPartWeakened` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 370 | `OnBodyPartDestroyed` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 443 | `OnBodyPartDestroyed` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 511 | `OnDamageResolved` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 598 | `OnDamageResolved` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 599 | `OnDamageTaken` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 662 | `OnDamageResolved` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 698 | `OnDamageResolved` |
| `Assets/1. Scripts/Editor/Verification/DamageIntegrationCompletionVerification.cs` | 725 | `OnDamageResolved` |
| `Assets/1. Scripts/Editor/Verification/DamageTurnEndSettlementVerification.cs` | 104 | `OnCharacterDeath` |
| `Assets/1. Scripts/Editor/Verification/DamageTurnEndSettlementVerification.cs` | 146 | `OnCharacterDeath` |
| `Assets/1. Scripts/Editor/Verification/HifumiDeferredParityVerification.cs` | 93 | `OnReactiveRollResolved` |
| `Assets/1. Scripts/Runtime/Abilities/Runtime/SkillMigrationObserver.cs` | 99 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 45 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 53 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 61 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 69 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 77 | `OnClashWin` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 85 | `OnClashLose` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 93 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 101 | `OnStatusApplyResolved` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 109 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 117 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Characters/CharacterBase/ReactiveCombatMechanic.cs` | 125 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 250 | `OnBattleStarted` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 251 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 252 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 253 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 254 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 255 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 256 | `OnClashStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 257 | `OnClashResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 258 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 259 | `OnCombatResourceChanged` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 260 | `OnStatusApplyResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 261 | `OnStatusTicked` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 262 | `OnStatusRemovedDetailed` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 263 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 264 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 265 | `OnBodyPartRecovered` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 266 | `OnCharacterDeathResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Analysis/BattleDynamicAnalysisRecorder.cs` | 267 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs` | 789 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs` | 790 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs` | 791 | `OnCombatResourceChanged` |
| `Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs` | 792 | `OnClashResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs` | 793 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Diagnostics/Simulation/BattleBatchSimulationRunner.cs` | 794 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1006 | `OnBattleStarted` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1008 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1010 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1012 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1014 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1016 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1018 | `OnClashStart` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1020 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1022 | `OnClashResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1024 | `OnDamageResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1026 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1028 | `OnStatusTicked` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1030 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1032 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1034 | `OnCharacterDeathResolved` |
| `Assets/1. Scripts/Runtime/Diagnostics/Verification/CharacterVerificationAutoBattleDriver.cs` | 1036 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1139 | `OnBattleStarted` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1141 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1143 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1145 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1147 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1149 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1151 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1153 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1155 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Skills/Base/Skill.cs` | 1157 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Systems/Stats/BattleRewardService.cs` | 25 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Systems/Stats/BattleRewardService.cs` | 26 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs` | 63 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs` | 68 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs` | 72 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs` | 76 | `OnClashResolved` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs` | 80 | `OnDamageResolved` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/Canonical0922/NormalEnemy0922RoleMechanic.cs` | 84 | `OnCharacterDeath` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/EliteEnemy/EliteEnemyMechanic.cs` | 14 | `OnClashWin` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/EliteEnemy/EliteEnemyMechanic.cs` | 19 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/EliteEnemy/EliteEnemyMechanic.cs` | 24 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/EliteEnemy/EnemyPostureMechanic.cs` | 109 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Characters/Enemies/NormalEnemy/NormalEnemyBloodScentMechanic.cs` | 10 | `OnClashWin` |
| `Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs` | 259 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs` | 264 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs` | 269 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs` | 274 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs` | 279 | `OnClashResolved` |
| `Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs` | 284 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafImmortalFuryMechanic.cs` | 21 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafImmortalFuryMechanic.cs` | 26 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafMadnessMechanic.cs` | 46 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafMadnessMechanic.cs` | 51 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafMadnessMechanic.cs` | 56 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafRulebreakerMechanic.cs` | 36 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafRulebreakerMechanic.cs` | 40 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafRulebreakerMechanic.cs` | 45 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafRulebreakerMechanic.cs` | 50 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs` | 173 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs` | 178 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs` | 183 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs` | 188 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs` | 193 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs` | 198 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleAbilityInspector.cs` | 304 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleAbilityInspector.cs` | 305 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleAbilityInspector.cs` | 306 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleAbilityInspector.cs` | 307 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleAbilityInspector.cs` | 308 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 150 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 155 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 160 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 165 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 170 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 175 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 180 | `OnStatusApplyResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 185 | `OnStatusTicked` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 190 | `OnStatusRemovedDetailed` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 195 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 200 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 205 | `OnBodyPartRecovered` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 210 | `OnCharacterDeathResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Core/BattleUIEventBinder.cs` | 215 | `OnClashResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleActionOrderRailUI.cs` | 283 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleActionOrderRailUI.cs` | 284 | `OnReactiveRollStarted` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleActionOrderRailUI.cs` | 285 | `OnReactiveRollResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleEnergyUI.cs` | 122 | `OnCombatResourceChanged` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleEnergyUI.cs` | 123 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleEnergyUI.cs` | 124 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleEnergyUI.cs` | 125 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleEnergyUI.cs` | 126 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 111 | `OnStatusApplyResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 112 | `OnStatusTicked` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 113 | `OnStatusRemovedDetailed` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 114 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 115 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 116 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 117 | `OnActionEnd` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 119 | `OnReactiveRollStarted` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 120 | `OnReactiveRollResolved` |
| `Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleReactionHud.cs` | 121 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/CharacterView/CharacterViewEventBinder.cs` | 133 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/CharacterView/CharacterViewEventBinder.cs` | 138 | `OnBodyPartWeakenResolved` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/CharacterView/CharacterViewEventBinder.cs` | 143 | `OnBodyPartBreakResolved` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/CharacterView/CharacterViewEventBinder.cs` | 148 | `OnBodyPartRecovered` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/CharacterView/CharacterViewEventBinder.cs` | 153 | `OnCharacterDeathResolved` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/Director/BattleStatusVisualDirector.cs` | 119 | `OnStatusApplyResolved` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/Director/BattleStatusVisualDirector.cs` | 121 | `OnStatusTicked` |
| `Assets/1. Scripts/Runtime/Presentation/Visual/Director/BattleStatusVisualDirector.cs` | 123 | `OnStatusRemovedDetailed` |
| `Assets/1. Scripts/Runtime/Systems/BattleFlow/Test/RunFlowLiveBattleBridge.cs` | 213 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 152 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 160 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 169 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 177 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 186 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 195 | `OnBodyPartWeakened` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 199 | `OnBodyPartDestroyed` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 203 | `OnBodyPartRecovered` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/CanonicalEmotionTierEffectDefinition.cs` | 214 | `OnClashStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs` | 90 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs` | 95 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs` | 100 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs` | 105 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/Canonical0922/EmotionAugmentCanonical0922EffectDefinition.cs` | 110 | `OnBattleEnded` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 125 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 133 | `OnDamageEventResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 142 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 150 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 159 | `OnKillResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 168 | `OnBodyPartWeakened` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 172 | `OnBodyPartDestroyed` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 176 | `OnBodyPartRecovered` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 187 | `OnClashStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 1052 | `OnTurnStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 1057 | `OnTurnEnd` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 1062 | `OnActionStart` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 1067 | `OnExchangeResolved` |
| `Assets/1. Scripts/Runtime/Systems/Progression/R7NativeEmotions/NativeEmotionMechanics.cs` | 1072 | `OnBattleEnded` |

### 4.3 공유 Effect Template

| EffectDefinition GUID | Entry 수 | 사용 Skill 수 |
|---|---|---|
| `a200abf019adbe645837091fe29ea897` | 8 | 8 |
| `34bc95e903f945046a515c97cda52fab` | 5 | 5 |
| `59779eebfac2cbb4886580cec480c2c5` | 2 | 2 |
| `f5fd01df0145e1746a194b796bcd8bf5` | 2 | 2 |
| `c709f54ed5bb5934eae625088ed3d772` | 2 | 2 |
| `e997848a15141cc4294352e3aea52083` | 2 | 2 |
| `faacc00a12e67cb49a20b6c972ff1cd7` | 2 | 2 |

### 4.4 미참조 EffectDefinition

- `Assets/2. Data/Progression/PhaseE/CANONICAL_0916_CLOSURE_V1/YujinEffects/all_targets.asset` (Yujin0916SkillEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/CANONICAL_0916_CLOSURE_V1/YujinEffects/designate.asset` (Yujin0916SkillEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/CANONICAL_0916_CLOSURE_V1/YujinEffects/probe_weakness.asset` (Yujin0916SkillEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/CANONICAL_0916_CLOSURE_V1/YujinEffects/sharpen.asset` (Yujin0916SkillEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/CANONICAL_0916_CLOSURE_V1/YujinEffects/wanted.asset` (Yujin0916SkillEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_02_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_05_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_06_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_09_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_10_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_11_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_12_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_14_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_15_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_16_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_17_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_18_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Duel/Effects/Olaf_D_19_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Normal/Effects/Olaf_N_01_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Normal/Effects/Olaf_N_11_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Normal/Effects/Olaf_N_13_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Normal/Effects/Olaf_N_14_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Normal/Effects/Olaf_N_15_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Normal/Effects/Olaf_N_16_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Olaf/Preparation/Effects/Olaf_P_02_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_01_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_02_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_03_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_04_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_05_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_06_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_07_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_08_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_11_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_12_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_13_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Duel/Effects/Yujin_D_14_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_01_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_02_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_03_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_04_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_05_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_06_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_07_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_08_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Normal/Effects/Yujin_N_09_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_01_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_02_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_03_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_04_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_05_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_06_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Preparation/Effects/Yujin_P_07_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Prestige/Effects/Yujin_R_01_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Prestige/Effects/Yujin_R_02_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/Progression/PhaseE/TEMP_BALANCE_V1/Skills/Yujin/Prestige/Effects/Yujin_R_03_TEMP_BALANCE_V1_Effect.asset` (TempBalanceSkillProxyEffectDefinition)
- `Assets/2. Data/SkillEffects/Common/부위 파괴.asset` (ForceBreakOwnPartEffect)

## 5. Missing Script / 중복 GUID

- Missing Script(FATAL): 없음
- Warning 2건: `m_Script: {fileID: 0}` 이지만 `m_EditorClassIdentifier`가 채워진 기존 직렬화 특성. 실제 로드는 정상이며 FATAL이 아니다.
- 중복 GUID: 없음

## 6. G0-K — 캐릭터 전용 키워드 / 소스 / 상태 저장

`StatusEffect` 계열 클래스에서 파생한 실행 상태를 소스 기준으로 실사했다. `OwnerCharacterId=Shared` 이거나 안정 `KeywordId`가 없는 항목은 수동 검토 대상이다.

| 소유 | 키워드(안정 ID) | Carrier | Storage | ApplyPhase | RemovePolicy |
|---|---|---|---|---|---|
| Enemies | `EnemyBindStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Enemies | `EnemyCommanderStrengthAura` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Enemies | `EnemyCommanderSwiftAura` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Enemies | `EnemyShadeStatus` | Actor | PresenceTimed | None | Default(DurationPolicy/Manual) |
| Hifumi | `HifumiSpeedPenaltyStatus` | Actor | Bespoke | ModifySpeed | DurationPolicy=StatusEffectDurationPolicy.TurnEnd |
| Olaf | `OlafFearStatus` | Actor | Presence | ModifyRoll | Default(DurationPolicy/Manual) |
| Shared | `ArmDisabled` | Part | Bespoke | ModifyExchangeRollCount+CanUseSkill | Default(DurationPolicy/Manual) |
| Shared | `Bleeding` | Actor | Bespoke | OnTurnEnd | DurationPolicy=StatusEffectDurationPolicy.Permanent |
| Shared | `BrokenArm` | Part | Bespoke | ModifyRoll | Default(DurationPolicy/Manual) |
| Shared | `BrokenHead` | Part | Bespoke | OnApply+OnRemove+ModifyRoll+CanUseSkill | OnRemove=override |
| Shared | `BrokenLegs` | Part | Bespoke | ModifySpeed+CanUseSkill | Default(DurationPolicy/Manual) |
| Shared | `Burn` | Actor | Bespoke | None | Default(DurationPolicy/Manual) |
| Shared | `DataDrivenBrokenPart` | Part | Bespoke | OnApply+OnRemove+ModifyExchangeRollCount+CanUseSkill | OnRemove=override |
| Shared | `DataDrivenPartDisabled` | Part | Bespoke | ModifyExchangeRollCount+CanUseSkill | Default(DurationPolicy/Manual) |
| Shared | `DeferredStatusEffect` | Actor | Bespoke | OnTurnStart | DurationPolicy=StatusEffectDurationPolicy.TurnStart |
| Shared | `DisarmStatus` | Actor | NumericTimed | GetStaggerDamageTakenFlatModifier | Default(DurationPolicy/Manual) |
| Shared | `FractureStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Shared | `HeadDisabled` | Part | Bespoke | CanUseSkill | Default(DurationPolicy/Manual) |
| Shared | `HeatStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Shared | `LegsDisabled` | Part | Bespoke | CanUseSkill | Default(DurationPolicy/Manual) |
| Shared | `PainStatus` | Actor | Presence | ModifyHealing | Default(DurationPolicy/Manual) |
| Shared | `ProtectionStatus` | Actor | NumericTimed | ModifyDamageTaken | Default(DurationPolicy/Manual) |
| Shared | `RegenerationStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Shared | `RuptureStatus` | Actor | NumericTimed | ModifyDamageTaken | Default(DurationPolicy/Manual) |
| Shared | `StagnationStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Shared | `StrengthStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Shared | `Stun` | Actor | Bespoke | CanAct | Default(DurationPolicy/Manual) |
| Shared | `SturdyStatus` | Actor | NumericTimed | GetStaggerDamageTakenFlatModifier | Default(DurationPolicy/Manual) |
| Shared | `SwiftStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Shared | `WeaknessStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |
| Yujin | `SealedPartStatus` | Part | Bespoke | CanUseSkill+CanAct | DurationPolicy=StatusEffectDurationPolicy.TurnEnd |
| Yujin | `YujinDesignationStatus` | Actor | NumericTimed | None | Default(DurationPolicy/Manual) |

용어집 키워드 40건, 스킬 저작 키워드 Entry 0건, 커스텀 자원 정의 0건은 각 CSV 참조.

### 필수 키워드 커버리지 (문서 정본 대조)

| 키워드 | 소유(문서/코드) | Carrier | Storage | 상태 | 관측 식별자 |
|---|---|---|---|---|---|
| `Bleed` | Olaf | Actor | Bespoke | Found | Assets/1. Scripts/Runtime/Abilities/Native/NativeMixedAbilityEngine.cs:134 \| Assets/1. Scripts/Runtime/Abilities/Native/NativePreparationStatusGraph.cs:27 \| Assets/1. Scripts/Runtime/Abilities/Native/NativePreparationStatusGraph.cs:43 \| Assets/1. Scripts/Runtime/Abilities/Native/NativeStatusAbilityEngine.cs:86 |
| `Fear` | Olaf | Actor | Presence | Found | Assets/1. Scripts/Runtime/Characters/CharacterBase/Character.cs:2094 \| Assets/1. Scripts/Runtime/Skills/Effects/SkillEffectCondition.cs:333 \| Assets/1. Scripts/Runtime/Status/Common/CommonStatusAlgebra.cs:122 \| Assets/1. Scripts/Runtime/Characters/Olaf/Mechanics/OlafMadnessMechanic.cs:452 |
| `Madness` | Olaf | Actor | Bespoke | FoundButPolicyUndecided | Assets/1. Scripts/Runtime/Abilities/Native/NativeUniqueGaugeEngine.cs:28 \| Assets/1. Scripts/Runtime/Abilities/Native/NativeUniqueGaugeEngine.cs:51 \| Assets/1. Scripts/Runtime/Abilities/Runtime/OlafNativeAbilityRuntimeBinding.cs:12 \| Assets/1. Scripts/Runtime/Abilities/Runtime/OlafNativeAbilityRuntimeBinding.cs:99 |
| `Mark` | Yujin | Actor | Bespoke | FoundButPolicyUndecided | Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs:839 \| Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs:851 \| Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs:857 \| Assets/1. Scripts/Runtime/Characters/Yujin/Mechanics/YujinMechanic.cs:886 |
| `Designation` | Yujin | Actor | NumericTimed | Found | Assets/1. Scripts/Runtime/Abilities/Runtime/YujinNativeAbilityRuntimeBinding.cs:231 \| Assets/1. Scripts/Runtime/Abilities/Runtime/YujinNativeAbilityRuntimeBinding.cs:275 \| Assets/1. Scripts/Runtime/Characters/Yujin/Status/YujinDesignationStatus.cs:7 \| Assets/1. Scripts/Runtime/Characters/Yujin/Status/YujinDesignationStatus.cs:15 |
| `AssassinSense` | Yujin | Actor | Bespoke | Missing | (코드에 'AssassinSense' 없음) |
| `BloodPrice` | Hifumi(문서) / Olaf(코드) | Actor | Bespoke | FoundButPolicyUndecided | Assets/1. Scripts/Runtime/Characters/Olaf/OlafSkillIds.cs:40 \| Assets/1. Scripts/Runtime/Characters/Olaf/OlafSkillIds.cs:85 |
| `Taunt` | Hifumi | Actor | Bespoke | FoundButPolicyUndecided | Assets/1. Scripts/Runtime/Abilities/Targeting/TargetPlanningRules.cs:8 |
| `Bone` | Hifumi | Actor | Bespoke | Found | Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs:208 \| Assets/1. Scripts/Runtime/Presentation/UI/Components/BattlePassiveDescriptionFormatter.cs:145 \| Assets/1. Scripts/Runtime/Systems/AI/Advisors/HifumiAutoPlanAdvisor.cs:158 \| Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs:525 |
| `Advance` | Hifumi | Actor | Bespoke | FoundButPolicyUndecided | Assets/1. Scripts/Runtime/Characters/Hifumi/Mechanics/HifumiMechanic.cs:208 \| Assets/1. Scripts/Runtime/Presentation/UI/Components/BattlePassiveDescriptionFormatter.cs:145 \| Assets/1. Scripts/Runtime/Systems/AI/Advisors/HifumiAutoPlanAdvisor.cs:158 |
| `Stealth` | Yujin | Actor | Bespoke | Missing | (코드에 'Stealth' 없음) |
| `ExplosiveTrap` | Yujin | Part | Bespoke | Missing | (코드에 'ExplosiveTrap' 없음) |
| `ResonanceTrap` | Yujin | Part | Bespoke | Missing | (코드에 'ResonanceTrap' 없음) |
| `ContagionTrap` | Yujin | Part | Bespoke | Missing | (코드에 'ContagionTrap' 없음) |

### 미정/미구현 — 구현 차단 항목

- `Madness` (FoundButPolicyUndecided) — 광기 '받는 피해 +1'의 정확한 피해 채널(HP/흐트러짐/고정) 미정
- `Mark` (FoundButPolicyUndecided) — 예약 표식 발화 시 무기 판정 기준 미정
- `AssassinSense` (Missing) — 런타임 구현 없음 — 신규 콘텐츠
- `BloodPrice` (FoundButPolicyUndecided) — 문서상 히후미 키워드이나 코드에는 Olaf 스킬 ID만 존재 — 소유/의미 충돌 미정
- `Taunt` (FoundButPolicyUndecided) — 런타임 구현 없음 — 은신과의 목표 충돌 우선순위 미정
- `Advance` (FoundButPolicyUndecided) — '가불' 의미와 턴 종료 정산 시점은 기획 확인 필요
- `Stealth` (Missing) — 런타임 구현 없음 — 도발과의 충돌 규칙 미정
- `ExplosiveTrap` (Missing) — 현재 YujinMarkTrap 단일 프록시만 존재 — 3종 분리 미정
- `ResonanceTrap` (Missing) — 런타임 구현 없음 — §18.4 확장 검증 대상
- `ContagionTrap` (Missing) — 런타임 구현 없음 — §18.7 확장 검증 대상

### 수동 검토 대상(고유 키워드 안정 ID 없음)

- `OlafFearStatus` (Olaf) — 고유 키워드이지만 안정 ID(`UniqueKeywordId`)가 없어 구현 전 기획 확인 필요.
- `Bleeding` (Shared) — 고유 키워드이지만 안정 ID(`UniqueKeywordId`)가 없어 구현 전 기획 확인 필요.
- `YujinDesignationStatus` (Yujin) — 고유 키워드이지만 안정 ID(`UniqueKeywordId`)가 없어 구현 전 기획 확인 필요.

## 7. G0-I — 실행 구조 / 반격 / 연출 순서

호출 그래프는 `ActionResolver → ClashManager → BattleEvent → Mechanic` 순서를 소스 간선으로 고정한 것이다. 실제 적용 시간과 UI 재생 시간은 분리해 Golden Baseline의 Trace로 고정한다.

| 구분 | 간선 수 |
|---|---|
| CounterHook | 1 |
| CounterSequence | 3 |
| DamageCommit | 25 |
| EventRaise | 6 |
| HitSignal | 3 |
| OutcomeSignal | 4 |
| Resolution | 12 |
| SkillExecution | 10 |

### 반격/추가타 종류

| 종류 | 출현 수 | 현재 코드 대응 | 관측 식별자 |
|---|---|---|---|
| HifumiCounterRoll | 11 | HifumiMechanic.ResolveCounterSequence / BuildCounterPreviewForVerification / IsCounterSkill | ResolveCounterSequence \| HifumiCounterPreview \| IsCounterSkill |
| EnemyCounterRoll | 2 | BattleReactiveRollSourceKind.Counter — 적 반격은 별도 클래스 없이 반응 굴림으로 표현 | BattleReactiveRollSourceKind.Counter |
| SecondaryHit | 39 | AttackWeightHitResult / ClashResultContext.SecondaryDamageContexts — 공격 가중치 추가 피격 | AttackWeightHitResult \| SecondaryDamageContexts |
| FollowUpRoll | 1 | BattleReactiveRollSourceKind.FollowUp — 후속 굴림 | BattleReactiveRollSourceKind.FollowUp |
| TriggeredSkillAction | 0 | R1-I 신규 반응 스킬 삽입 지점. 현재 런타임 코드 없음(신규 기능) |  |

정본 구분: `HifumiCounterRoll`(합 종료 직후 반격 묶음), `EnemyCounterRoll`(일방 피격 후 반격), `SecondaryHit`(추가타), `FollowUpRoll`(후속 굴림), `TriggeredSkillAction`(R1-I 신규 삽입). 서로 다른 발생 시점을 하나의 '즉시' 이벤트로 합치지 않는다.

## 8. G0-W — 공격 가중치 기준선 / 보호 목록

현재 `AttackWeightSettings.Weight`는 **메인 포함 서로 다른 캐릭터 수**다. v4의 `AttackWeightBudget`(슬롯 예산)과 의미가 다르므로 Legacy 결과를 보존한 채 분리한다.

| SkillId | ActionType | Weight | SecondaryPartMode | 배율 | RollCount | 보호 |
|---|---|---|---|---|---|---|
| - | - | 1 | - | - | - | - |

### 보호 목록

- 없음

`AttackWeight` 소스 참조 284건 (UI 표시/자동 계획/Undo 갱신 여부는 G0-W 5항목에서 별도 확인).

## 9. G0 판정

- **PASS** — 감사표 생성 완료, FATAL Missing Script/중복 GUID 없음.

- G0-K: PASS (키워드/상태 행 32, 수동 검토 3)
- G0-I: PASS (호출 간선 64, 반격 종류 5)
- G0-W: PASS (가중치 스킬 164, 보호 0)

## 10. 산출물

- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_AUDIT.json`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_ENTRIES.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_TRIGGER_STATS.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_K_KEYWORDS.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_K_REQUIRED.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_K_GLOSSARY.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_K_SKILL_KEYWORDS.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_K_RESOURCES.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_I_CALL_GRAPH.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_I_COUNTER_KINDS.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_W_ATTACK_WEIGHTS.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_W_PROTECTED.csv`
- `F:/GitCorp/Project-Abyss/G0_Ability_Audit_20261029/G0_GOLDEN_BASELINE.json` (Play Mode 실행 시)
