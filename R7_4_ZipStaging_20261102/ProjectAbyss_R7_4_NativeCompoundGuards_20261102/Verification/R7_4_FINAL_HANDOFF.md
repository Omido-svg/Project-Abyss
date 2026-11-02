# Project Abyss R7-4 — 네이티브 복합 효과/조건·굴림 계약 정리 (2026-11-02)

## 명확한 범위
- 기준: 전투 시스템 규칙(1029)(3).md (2026-10-29).
- R7-0 콘텐츠 145개, R7-3까지 신규 직접 이관 4개.
- 현재 7개의 혼합/출혈 네이티브 SO가 이미 존재했고, 그 가운데 **피해+상태 카드 2개**는 R7 이전 작업에서 구현되어 있었다.
- 이번 R7-4에서는 이 경로의 **조건·굴림 단위 필터와 검증 계약을 실제 코드에 추가**했다.
- 중요한 구분: **이번 단계에서 새로 네이티브화한 SkillDefinition은 0개**. 기존 네이티브 그래프 7개를 유지·보강했으며, 실행 대상인 피해+상태 복합 카드 2개를 다시 이관한 것으로 중복 집계하지 않는다.
- R7-0의 아직 미이관 38개(직전 R7-3 이후 기준)는 대부분 올라프/유진 전용 스킬 및 다중 효과이므로 R7-5/6에서 처리한다. 히후미 고유 효과는 R7-7.

## 구현
- R7MixedOperationEntry에 네이티브 필드 추가: RestrictToRoll, RollNumber, Condition, RequiredStatus, RequiredNumericValue. 기존 스키마2 SO들은 신규 필드가 없는 경우도 무조건 실행/무필터의 기존 의미를 유지한다.
- 새로운 R7NativeMixedGate20261102는 레거시 SkillEffectEntry/Condition을 읽지 않고 즉시 판정한다.
- 조건 종류: None, RollSucceeded, RollFailed, CriticalRoll, ClashOnly, OneSidedOnly, TargetHasStatus, TargetNumericStatusAtLeast.
- 상태 조건은 StatusEffectFactory.HasStatus / GetNumericTotal만 사용한다. Bespoke 혈상(Bleeding)을 NumericTimed N/T처럼 해석하는 조건을 거부한다.
- 굴림 번호 불일치는 NotScheduled, 조건 실패는 ConditionFailed이며 기존 레거시/어댑터로 되돌아가지 않는다.
- 기존 R7NativeMixedAbilityEngine의 R5 typed command 및 DamageManager/BattleEffectResolver가 유일한 게임 상태 적용 경로로 유지된다.
- 복합 카드 내 원본 순서: RolledPartDamage → ApplyBleeding; 다른 5개 카드는 출혈 단일 효과.
- 피해 계산식, 스택, 지속시간, Source GUID, SkillDefinition, native SO 데이터는 이번에 수정하지 않았다.
- 기존 혼합 실행은 OnExecute/RollUsing 트리거에 한정한다. 임의의 이벤트로 확장하는 것은 이번 구현 범위가 아니다.

## 현재 원본 내 실제 조건
- 기존 7개 native mixed SO의 새 Condition은 모두 None, RestrictToRoll은 false.
- 실제 R7-4 조건/굴림 필터를 사용하는 새로운 카드 수: 0. 이 조건 엔진은 후속 카드 이관 시 사용할 기능이다.
- 별도 R7-5/6에서 확정된 전용 키워드/고유 자원 등을 이 조건 엔진으로 대체하지 않는다.
- 새 조건 엔진이 있다는 이유만으로 기존 PhaseE 효과 정책을 임의 확정하지 않는다.

## 최소 검증 결과
- R7Stage4NativeCompoundGate20261102 EditMode DATA PASS 59 / FAIL 0.
- R7NativeMixedSchema2Migration20261031.Verify() 기존 7개 소스 ↔ 네이티브 그래프 수치/파라미터 일치 PASS.
- 등록 그래프 7개, 그중 damage+status 2개, single status 5개, 실행 명령 총 9개.
- 조건/굴림 필터 단위 데이터 검사: 올바른 롤번호, 롤 불일치 NotScheduled, 조건 실패, 숫자 상태 제한, Bespoke 차단, invalid roll 차단.
- 변경 스크립트 5개의 정적 검사 오류 0.
- R9 전체 회귀: 실행하지 않음 (사용자 요청).
- PlayMode/전투 동등성 테스트: 실행하지 않음 (사용자 요청).
- 따라서 실전 전투의 조건 분기/재진입/재생 동등성은 아직 입증되지 않았다.
- 원본 SO, GUID, Battle Test Scene Dirty=true 보존. Git commit/push 없음.

## 파일
- 새 코드: Assets/1. Scripts/Runtime/Abilities/Native/R7NativeMixedGate20261102.cs
- 새 전용 Gate: Assets/1. Scripts/Editor/Verification/R7Stage4NativeCompoundGate20261102.cs
- 수정: R7NativeMixedAbilityDefinition.cs, R7NativeMixedAbilityEngine.cs, R7ProductionLegacyStatusMigration.cs
- R7_4_NATIVE_COMPOUND_OPERATION_LEDGER.csv — 원본 Guid, native graph, operation order, 필터를 확인할 수 있는 자료.

## 다음 작업
R7-5 올라프 전용 효과: 혈상/공포/광기와 조건·굴림 효과를 단일/복합 네이티브 그래프로 실제 이관.
R7-6 유진 전용 효과: 표식/지정/살수의 감 및 무기 조건.
R7-7 히후미 전용. R7-12 최종 전체 회귀 및 레거시 경로 청산.
