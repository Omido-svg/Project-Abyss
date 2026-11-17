# 능력 저작과 확장

전투 규칙의 우선순위는 `전투 시스템 규칙(1112).md`다. 이 문서는 콘텐츠 수치를 정하지 않으며, 새 능력을 현재 런타임에 연결하는 방법을 설명한다.

C#로 신규 스킬을 작성할 때는 [읽기 쉬운 스킬 작성 가이드](SkillAuthoringGuide.md)에서 시작한다. `SkillRules` / `AbilityConditions` / `AbilityEffects`는 아래의 상세 데이터를 만드는 작성 함수다. 기존 Inspector 저작·독립 Handler·상세 설정과 함께 사용할 수 있다. 「각인의 계약」은 이 방식으로 작성되어 있으며 같은 실행 규칙을 생성한다. Inspector에는 규칙 요약과 상세 데이터 편집이 함께 제공된다.

## 저작 경로

1. Project 창에서 원본 `SkillDefinition`을 선택하고 `Assets > Project Abyss > Create Ability Rule Draft`를 실행한다.
2. 자동 로드를 원하면 `Resources/ProjectAbyss/AbilityRules` 아래에 저장한다. 직접 참조하는 패시브는 `CharacterData.AbilityRules`에 연결한다.
3. 원본 GUID·StableId·이름을 유지하고, Rules에 Trigger / Condition / TargetSelectorId / Operations / Lifetime을 작성한다.
4. Inspector의 등록 목록과 검증 결과를 확인한다. `(미정)` 값이 필요하면 `RequiresConfiguredValue`와 사유를 남기고 `Enabled=false`로 둔다.
5. 확정된 값과 유효한 정의만 `Enabled=true`로 설정한다. 활성 RuleSet은 그 스킬의 효과 라우트를 담당하므로 기존 EffectEntries와 중복 합성하지 않는다. 필요한 효과 전체를 새 정의에 넣는다.

규칙은 전투에 등록할 때 컴파일한다. 원본 SO의 조건·수치·식별자를 전투 도중 수정해도 이미 시작된 전투의 컴파일 결과는 바뀌지 않는다. 다음 전투에서 다시 로드한다. Settings SO는 `IAbilityRuleSettingsDefinition.CaptureSettings()`에서 불변 실행 설정을 반환해야 한다.

일반 카드의 공식 발동 시점과 결투 게이트는 기존 정본 계약을 따른다. 내부 이벤트를 쓰는 카드 규칙도 `SourceKind=Skill` 소유권과 결투 게이트를 유지한다. 순수 패시브는 별도 `SourceKind=Passive` 정의로 바인딩하고 내부 이벤트를 저작할 때 `CardAuthored=false`를 명시한다. 이 값만 끈다고 카드 규칙이 패시브로 바뀌지 않는다. 굴림 결과가 아직 없으면 실패 결과로 간주하지 않는다.

## 실행과 판정 시점

`BattleAbilityRuntime`은 기존 `AbilityRunner`로 읽기 전용 계획을 만들고, 기존 `CombatCommandApplyService`가 명령을 적용하게 한다. 실제 HP·상태·자원 변경은 기존 도메인 서비스가 담당한다. `RegisteredEffect`는 이 경로의 명령 종류이며 별도의 피해 계산기를 만들지 않는다.

| Snapshot | 캡처 경계 |
|---|---|
| LiveAtTrigger | 해당 발동 시점 |
| LockAtActionStart | 행동 시작 이벤트의 관찰자 실행 전 |
| LockAtTargetReveal | 실제 계획 수락/적 계획 공개 시 |
| LockAtScheduleCreation | 예약 생성 시; ScheduledOnly 규칙 |
| LockAtReactionStart | 반응 실행 시작 시; 골단은 비용 지불 전 |
| AfterCommittedDamage | 해당 피해의 모든 결과가 확정된 뒤, 통지 전 |
| TurnEndFinalSnapshot | TurnEnding 패시브 처리 경계; 같은 경계의 앞 명령이 소급 변경하지 않음 |

PreTarget / CandidateTarget / PostDamageResult는 별도 조건 평가 단계다. `SelectedTarget` 조건은 CandidateTarget에서 검사한다. 확정 피해 조건은 HP를 다시 비교해서 추론하지 않고 해당 타격의 결과 증거를 사용한다. 관찰자마다 자기 고유 자원·표식·무기 상태를 같은 경계에서 보존한다.

## 원인과 소유권

`EffectCause`의 RootActionId는 연관 관계다. 직접 보상은 ParentEventId와 SourceAbilityId·SourceActorId·SourceRollIndex·OriginKind가 맞는지 검사한다. `cause.direct`는 ObservedCause와 ObservedParent를 검사한다. 부모가 없는 피해는 Unknown을 유지하며, 실제 사건 ID만 발급한다. 없는 원래 굴림 ID를 만들어 직접 보상에 사용하지 않는다.

`CombatEventSequence.Provenance`는 사건과 부모, 합산 폭발에 기여한 덫 설치 원인을 보존한다. 예약은 생성 당시 원인과 굴림을 보존한다. 중첩 발화 뒤에도 바깥 효과가 자기 지역 원인을 계속 사용한다.

상태 범위는 Actor / BodyPart / WeaponInstance / SkillExecution / CounterExecution / ClashExecution / Turn / Battle / Run이다. enum의 기존 숫자는 유지했다. `state.add/set`의 메모리는 능력 소유자에 귀속된다. 기본 키는 `DefinitionId/key`로 격리되며, 공유가 필요할 때만 `@shared/key`를 사용한다. 합 중 받은 피해는 `@combat/hp_received`, `@combat/stagger_received`에 기록한다.

무기 보정은 무기 식별자를, 실행 보정은 해당 실행 ID를 가져야 한다. `modifier.add`는 선택한 대상의 소유 범위에 등록된다. 합·반격·스킬 실행 ID의 숫자가 우연히 같아도 서로 다른 범위의 보정을 해제하지 않는다.

Run 상태는 `RunProgressionState`에 남는다. 저장 계층에서는 `CaptureAbilityState()` 결과를 저장하고 `TryRestoreAbilityState()`로 복원한다. 버전·키·중복을 검증한 뒤 한 번에 교체하므로 실패 시 기존 상태를 손상시키지 않는다. 새 런의 `Reset()`은 성장도 함께 지운다. 임의의 런타임 적 ID를 영구 소유자 ID로 저장하지 않는다.

## 순서와 수명

규칙 순서는 Phase → Priority → StableId → 전투의 소유자 순서 → 규칙 등록 순서다. 명령은 기존 중앙 적용기의 정렬·중복·UnknownCommit 계약을 따른다. 반응은 현재 명령 적용을 마친 뒤 반복형 FIFO 큐로 처리한다.

다음 턴 예약은 대상과 부위를 생성 시 고정한다. 사망·부위 파괴 시 다른 부위로 재선정하지 않는다. 예약 중 다시 생성한 다음 턴 예약은 별도 DueTurn을 가져 현재 Drain이나 같은 턴의 재호출에 섞이지 않는다. `Scheduled`와 실제 `EffectCommitted`는 구분한다. 예약 자체로 발동 수명을 소비할 능력은 `Lifetime.ConsumeOnSchedule=true`를 명시한다.

OncePerTurn / SkillExecution / CounterExecution / RollUse / Battle와 FullCooldownTurns를 조합할 수 있다. 쿨다운 N은 발동 다음의 N개 온전한 턴을 막는다. 적용 실패를 성공 발동으로 세지 않는다.

전염의 새 사건·재감염은 같은 루트라는 이유로 제거하지 않는다. 기술 처리 예산을 초과하면 root/event/parent/seed와 남은 작업을 보존한 오류를 발생시킨다. 임의의 밸런스 상한이나 성공 처리를 만들지 않는다.

## 수치 보정

`CombatModifierDefinition`으로 단계, 종류, 값, 우선순위, 지속 턴을 작성한다. 단계는 굴림 위력 / 가한 HP 피해 / 받은 HP 피해 / 가한·받은 흐트러짐 피해 / 빛 획득 / 회복 / 속도다. 동일 단계에서는 ReplaceBase → AddFlat → OverrideFinal 순으로 적용하고, 각 종류 안에서는 우선순위·출처·등록 순서를 따른다. 새로운 곱셈 규칙을 임의로 추가하지 않는다.

## 독립 C# 핸들러

데이터 조합으로 표현할 수 없는 계산은 `IAbilityRuleHandler`를 구현한다. 공통 대상별 연산은 `TargetedAbilityRuleHandler`를 상속하면 된다.

- Plan에는 `AbilityRuleReadContext`, 불변 `AbilityOperationSpec`, 대상 참조와 명령 sink만 들어온다. 여기서 피해·자원 변경이나 주사위 재굴림을 실행하지 않는다.
- Commit에는 도메인 서비스가 있는 `AbilityRuleCommitContext`가 들어온다. 실제 적용 전후 값을 보고 Applied / Rejected / Scheduled 등의 receipt를 반환한다.
- Validate는 미정 수치·누락 설정·지원하지 않는 조합을 차단한다. 모든 효과를 문자열 수식 언어로 만들지 않는다.

같은 런타임 어셈블리의 모듈에는 `[UnityEngine.Scripting.Preserve]`, `[AbilityRuleModule("unique.module.id")]`를 붙이고 `IAbilityRuleModule.Register`에서 operation / predicate / selector를 등록한다. 외부 어셈블리는 시작 시 `AbilityRuleModules.RegisterFactory`로 등록한다. 중복 ID는 오류다. 매 턴 reflection으로 찾지 않는다.

신규 일반 스킬과 핸들러는 코어·Presenter의 스킬 이름 분기 없이 연결한다. 기존 네이티브 계열과 유진·올라프 어댑터는 호환 경계로 유지하며 같은 실행 프레임·중앙 명령 계약을 사용한다. 새로운 전투 단위나 기존에 없는 도메인 규칙 자체를 도입한다면 해당 계약의 확장은 필요하다.

## 실제 복잡한 능력

유진 성장의 시작 표식 판정, 집행의 직접 발화 후 재부여·초과분 보존·강화 발화 반복, 양 축 진행 저장은 구현되어 있다. 전염은 다른 적들의 생존 부위 전체 중 표식 상위 두 부위를 고르고, 동률은 전투 시드를 사용한다. 원본 덫 2턴과 파생 덫 생성 턴 만료는 별도 항목이다. 손질은 원래 무기에 다음 턴 동안 남는다. 히후미 반격은 별도 실행 문맥에서 시작 뼈·가불·구간·만개를 고정하고 새 뼈를 보존한다.

여기서 필요한 미정 표식량·손질 보너스·발화/공명/폭발 수치·성장 임계치는 설정으로 남아 있다. 해당 값을 확정한 실콘텐츠 자산이 만들어졌다는 뜻은 아니다. 판단 축의 미정 무기별 추가 효과도 이 공통 조건·결과·메모리 및 등록 핸들러 경로로 저작하며, 이번 패치에서 임의 수치로 활성화하지 않는다.

## 검증

`Tools > Project Abyss > Verification`에서 `Ability Extensibility`, `Ability Interaction Contracts`, `Confirmed Combat Rules`를 실행한다. 결과는 프로젝트 `Logs`에 기록된다. 확장 검증은 실제 Skill 디스패처, 피해 서비스, 반격, 예약 큐, 독립 핸들러와 SO 저장/재로드를 사용하며 원본 콘텐츠 자산을 수정하지 않는다. 새 효과에는 실제 도메인 결과와 직접성·수명·재진입 조건을 확인하는 회귀 사례를 추가한다.


## 피해 후 조건을 검사하는 카드 저작

`SourceKind`는 규칙의 소유자, `CardAuthored`는 직접 카드 트리거의 저작 여부다. 두 값은 다른 의미다. `CardAuthored=false`로 내부 이벤트를 사용해도 `SourceKind=Skill`이면 스킬의 결투 게이트를 상속한다. 무기·캐릭터 등의 순수 패시브는 `SourceKind=Passive`인 별도 정의로 바인딩한다. 패시브는 관찰한 공격이 결투 효과 비활성 상태여도 자기 조건에 따라 반응할 수 있다.

「사용시/승리시 효과지만 그 굴림의 피해 처리 후 약화·파괴 여부를 평가」하는 카드는 다음 구성을 사용한다.

| 필드 | 값과 의미 |
|---|---|
| SourceKind | Skill: 원래 스킬에 귀속 |
| Trigger | DamageSettled: 조건을 읽는 내부 실행 시점 |
| Policy.CardAuthored | false: DamageSettled를 7번째 공식 카드 발동으로 추가하지 않음 |
| Policy.DamageConditionOrigin | RollUsing(사용시) 또는 ExchangeWon(실제 교환 승리시) |
| Policy.Snapshot | AfterCommittedDamage |
| Policy.ConditionPhase | PostDamageResult |
| RequiresDuelGate | true: 원래 결투 카드의 자격을 보존 |
| Condition | 예: Any(damage.weakened, damage.broken) |
| TargetSelectorId | 보상의 수령자. 자신에게 주려면 self |

검사 대상은 실제로 피해를 받은 적의 부위이며, 보상 수령자는 별도다. 적을 약화시켰다는 조건을 자신에게 받은 피해로 검사하지 않는다. 판정 시점에 캡처한 결투 자격은 이후 관찰자가 행동 값을 바꿔도 되살아나지 않는다.

RuleSet Inspector의 「피해 후 조건 판정 규칙 추가」에서 기본 구성을 생성할 수 있다. 생성 뒤 정의는 비활성 초안으로 돌아가므로 의도한 연산과 확정된 수치를 채운 후 검증한다. 코드 저작은 다음과 같다. `configuredAmount`는 기획에서 확정한 값을 전달하며, 예제가 기본 밸런스 값을 정하지 않는다.

```csharp
var reward = AbilityRuleAuthoringTemplates.AfterDamageEvaluation(
    "weaken_or_break_reward",
    AbilityDamageConditionOrigin.ExchangeWon,
    new AbilityOperationData { TypeId = "resource.energy", Amount = configuredAmount });
definition.SourceKind = AbilityRuleSourceKind.Skill;
definition.Rules = new[] { reward };
```

템플릿 조건은 그 굴림이 적 부위를 약화시키거나 파괴했는지를 본다. 원하는 판정에 맞게 조건 트리와 보상 연산을 교체한다. `RollUsing`은 실제 사용에서 이어진 일방타격도 가능하다. `ExchangeWon`은 실제로 승리한 굴림만 인정하며 남은 일방타격을 승리로 취급하지 않는다. 같은 승리 굴림의 공격 가중치 보조 피해는 그 굴림의 승리를 유지한다. 덫·추가타·반격 등 파생 피해는 같은 RootActionId라는 이유로 원래 카드의 조건을 깨우지 않는다.

선행 공식 발동을 지정하지 않은 Skill의 비예약 DamageSettled 규칙, 결투 게이트를 끈 피해 후 카드 규칙, 패시브에 카드 발동 원인을 선언한 정의, 확정 스냅샷·조건 단계를 빠뜨린 정의는 저작 검증에서 차단한다. 기존 자산에 이런 내부 카드 규칙이 있다면 원래 사용시/승리시 의미를 명시해야 한다. 순수 패시브는 DamageConditionOrigin=Unspecified를 유지한다. 예약 전용 역사적 피해 규칙은 카드의 즉시 피해 후 보상과 구분된다.
