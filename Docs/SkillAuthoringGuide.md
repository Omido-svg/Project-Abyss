# 읽기 쉬운 스킬 작성 가이드

정본의 발동 시점과 미정 수치는 그대로 유지한다. 아래 숫자는 작성법을 설명하는 교육용 값이다. 처음에는 이 문서와 `EngravedContractRules.cs`를 읽고, 고유 계산을 작성해야 할 때 `EngravedContractModule.cs`와 `EngravedContractWalkthrough.md`의 Plan/Commit 설명으로 넘어간다.

## 1. 가장 작은 예제

다음은 에디터의 자산 생성 메서드 안에서 실행하는 코드다. source는 원본 SkillDefinition, sourceGuid는 그 자산의 GUID다.

```csharp
using ProjectAbyss.Abilities;

var definition = SkillRules.CreateDraft(source, sourceGuid);
definition.Rules = new[]
{
    SkillRules.OnExchangeWin("energy_on_win", AbilityEffects.Energy(1))
        .To(AbilityTargets.Self)
        .OncePerSkillExecution()
};
```

의미는 **실제 교환에서 승리하면 자신에게 빛 1, 이 스킬 실행에서 한 번**이다. To를 생략하면 기본값은 실제 대상이다. OncePerSkillExecution을 생략하면 각 승리 굴림마다 평가한다.

생성 결과는 기존 AbilityRuleSetDefinition과 AbilityRuleData다. 정의는 Enabled=false로 생성된다. 설정을 끝내고 검증한 뒤 활성화한다. 새로운 실행기나 별도 스킬 상태 객체가 생기지는 않는다.

## 2. 자주 쓰는 함수

| 질문 | 작성 코드 | 의미 |
|---|---|---|
| 언제? | `SkillRules.OnUse(...)` | 각 굴림 사용 시 |
| 언제? | `SkillRules.OnExchangeWin(...)` | 실제 교환 승리 시; 일방타격은 승리로 처리하지 않음 |
| 언제? | `SkillRules.OnExchangeLoss(...)` | 실제 교환 패배 시 |
| 언제? | `SkillRules.OnAttackResolutionStarting(...)` | 공격 처리 시작 시 |
| 언제? | `SkillRules.OnClashCompleted(...)` | 합 종료 시 |
| 언제? | `SkillRules.OnPreparationActivated(...)` | 준비 발동 시 |
| 어느 상태? | `.AtActionStart()` | 행동 시작 스냅샷에서 상태를 읽음 |
| 어떤 조건? | `.When(condition)` | 조건 지정; 기존 조건을 교체함 |
| 누구에게? | `.To(AbilityTargets.Self)` | 자신. Actual/Aimed/SourcePart/AllEnemies/AllAllies/EnemyParts도 제공 |
| 몇 번? | `.OncePerSkillExecution()` | 적용 성공 후 스킬 실행당 한 번 |
| 몇 번? | `.OncePerTurn()` | 적용 성공 후 턴당 한 번 |
| 쿨다운? | `.WithCooldown(2)` | 발동 다음의 온전한 두 턴을 차단 |
| 무엇을? | `AbilityEffects.Energy(1)` | 기존 빛 변경 경로 |
| 무엇을? | `AbilityEffects.Block(3)` | 기존 방어도 경로 |
| 무엇을? | `AbilityEffects.AddStatus(status, stacks, duration)` | 기존 상태 부여 경로 |
| 무엇을? | `AbilityEffects.FixedDamage(amount)` | 기존 고정 피해 연산; 일반 공격 판정 전체를 대체하지 않음 |

여러 효과는 함수의 마지막 인수에 나란히 전달한다.

```csharp
SkillRules.OnExchangeWin("win_reward",
        AbilityEffects.Block(3),
        AbilityEffects.Energy(1))
    .To(AbilityTargets.Self);
```

## 3. 피해 결과 조건

```csharp
var condition = AbilityConditions.All(
    AbilityConditions.AppliedHpAtLeast(6),
    AbilityConditions.PartWeakenedOrBroken());

var rule = SkillRules.AfterWinningHit("reward_after_winning_hit", condition,
    AbilityEffects.Block(3));
```

의미는 **승리한 원래 타격의 실제 HP 피해가 6 이상이고, 그 타격으로 부위가 새로 약화되거나 파괴됐다면 자신에게 방어도 3**이다. AfterWinningHit는 기본 수령자가 자신이다.

All/Any/Not은 기존 조건 트리를 생성한다. AfterWinningHit는 DamageSettled / ExchangeWon 원인 / 확정 피해 스냅샷 / PostDamageResult / 결투 게이트를 함께 구성한다. 발화·덫의 파생 피해가 같은 행동에서 나왔다는 사실만으로 조건을 통과하지 않는다. 사용시 효과를 피해 후 검사하려면 AfterUsingHit를 사용하며, 원래 사용에서 이어진 일방타격도 기존 규칙에 따라 처리된다.

두 함수는 전달한 조건을 그대로 사용한다. 약화·파괴 조건을 자동으로 추가하지 않는다. `.When(a).When(b)`는 b로 교체하므로 두 조건을 요구하려면 `.When(AbilityConditions.All(a, b))`로 작성한다. AtActionStart는 읽는 시점만 바꾸며 발동 시점을 바꾸지는 않는다. 피해 후 규칙의 스냅샷을 잘못 변경하면 기존 검증이 오류를 보고한다.

## 4. 복합 예제: 각인의 계약

실제 EngravedContractRules.Create는 다음 방식으로 작성한다. Settings와 고유 표식 Handler는 기존 것을 그대로 사용한다. 아래 코드는 해당 메서드의 조립 부분이다.

```csharp
var definition = SkillRules.CreateDraft(source, sourceGuid);
var nextTurn = SkillRules.NextTurnReward(
    definition, DeliveryRule, AbilityEffects.Energy(energyNextTurn));

definition.Rules = new[]
{
    // A: 부여량은 시작 스냅샷에 따라 고유 Handler가 계산한다.
    SkillRules.OnExchangeWin(MarkRule,
        AbilityEffects.Custom(EngravedContractModule.MarkOperation, settings: settings))
        .AtActionStart(),

    // B: 내부 이벤트와 직접 원인 조건을 함께 구성한다.
    SkillRules.OnOwnDirectMarkBurst(BurstRule, source.SkillId,
        AbilityEffects.Block(blockOnBurst))
        .OncePerSkillExecution(),

    // C: 승리 타격의 확정 결과를 보고 다음 턴 보상을 예약한다.
    SkillRules.AfterWinningHit(ReserveRule,
        AbilityConditions.All(
            AbilityConditions.AppliedHpAtLeast(minimumAppliedHp),
            AbilityConditions.PartWeakenedOrBroken()),
        nextTurn.Schedule)
        .OncePerSkillExecution(consumeOnSchedule: true),

    // D: 지급 규칙도 같은 정의에 포함한다.
    nextTurn.Delivery
};

reservation = nextTurn.Reservation;
```

NextTurnReward는 다음 세 객체를 연결한다. 호출만으로 전투 예약이 생기지는 않으며 C가 실제 적용될 때 예약된다.

| 멤버 | 용도 |
|---|---|
| Schedule | C의 효과로 넣는 기존 schedule.next_turn 연산 |
| Delivery | Rules에 포함할 기존 예약 전용 지급 규칙 |
| Reservation | 둘을 연결하는 기존 AbilityScheduleDefinition 자산 |

Delivery는 Rules에 한 번 포함한다. C에는 consumeOnSchedule: true를 지정하여 예약 시 이번 스킬의 횟수를 소비한다. 다음 턴 지급까지 횟수 소비를 미루면 여러 굴림이 중복 예약할 수 있다.

Delivery의 대상과 스냅샷은 예약할 때 고정된다. 이후 사망·부위 유효성·빛 상한은 기존 런타임이 처리한다. 다른 대상에게 지급하려면 예약을 만드는 C의 대상 선택도 함께 설계한다. Delivery의 To만 바꿔 예약 대상을 재선정할 수는 없다.

## 5. 자산 저장과 게임 연결

에디터에서는 예약 설정까지 함께 저장하는 함수를 사용한다.

```csharp
AbilityRuleSetEditor.SaveNewDraft(
    definition, "Assets/Resources/ProjectAbyss/AbilityRules/MySkillRules.asset");
```

상위 폴더는 먼저 생성되어 있어야 한다. 함수는 새 RuleSet과 같은 정의를 참조하는 미저장 예약 설정을 함께 저장한다. 기존 자산을 덮어쓰지 않으며 지급 규칙이 빠진 예약은 저장 전에 오류로 알려 준다. 이미 저장한 자산은 Inspector에서 편집한다. 이 함수는 Editor 전용이므로 런타임 코드에서 호출하지 않는다.

저장이 검증 통과를 뜻하지는 않는다. Inspector의 오류와 미정 수치를 확인한 뒤 Enabled를 켠다. SkillDefinition의 실제 로드아웃 연결과 RuleSet 등록 경로도 기존처럼 필요하다.

Project 창에서 원본 SkillDefinition을 선택하고 기존 `Assets > Project Abyss > Create Ability Rule Draft`로 시작할 수도 있다. Inspector에는 규칙별 발동·조건·대상·효과·스냅샷·횟수 요약이 표시된다. `상세 데이터 편집`에서 기존 모든 필드를 계속 편집할 수 있다.

## 6. 특수 스킬의 확장

헬퍼는 원래 데이터를 반환하므로 고급 필드도 그대로 설정할 수 있다.

```csharp
var rule = SkillRules.OnInternalEvent("custom_reaction",
        AbilityEventKind.CounterCompleted,
        AbilityEffects.Custom("my.registered.operation", settings: customSettings))
    .To("my.registered.selector")
    .When(AbilityConditions.Custom("my.registered.condition"));

rule.Phase = configuredPhase;
rule.Priority = configuredPriority;
rule.Policy.OwnerScope = AbilityOwnerScope.CounterExecution;
rule.Lifetime.OncePerCounterExecution = true;
```

위 ID들은 별도로 등록한 Handler·Predicate·Selector를 뜻한다. Custom 호출만으로 새 계산이 자동 구현되지는 않는다. Duration/StateScope/Subject 같은 필드는 반환된 원래 데이터에 설정한다. 없는 ID는 기존 Registry가 거부한다. 의도하지 않은 가변 데이터 공유를 피하려면 규칙마다 새 데이터를 생성한다.

반복되는 의미 있는 조합이 늘어날 때만 작은 작성 함수를 추가한다. 새로운 전투 판정 자체가 필요하면 기존처럼 도메인 계약도 확장해야 한다. SkillRules는 스킬 소유 규칙을 만들며 순수 패시브는 기존 SourceKind=Passive 저작을 사용한다. OnInternalEvent의 CardAuthored=false만으로 스킬이 패시브로 변하지 않는다.

## 7. 성능과 검증

헬퍼는 저작 시 기존 AbilityRuleData / AbilityOperationData / AbilityConditionData / AbilityScheduleDefinition을 만든다. 기존 컴파일·실행 경로로 전달하며 사건마다 실행하는 별도 해석기나 람다는 추가하지 않는다. 저장된 SO에서 시작하는 일반 전투에서는 작성 함수가 실행되지 않는다. 작성 코드를 Update나 사건 콜백 안으로 옮겨 매번 RuleSet을 생성하지 않는다.

이번 확인은 동일 데이터와 동일 실행 경로를 유지했는지에 대한 검증이다. 전투 시간의 ms 단위 벤치마크나 전체 빌드·IL2CPP 검증을 수행했다는 뜻은 아니다.

- `Tools > Project Abyss > Verification > Skill Rule Authoring`: 이전 규칙의 모든 필드와 새 출력 비교, 고급 확장, GUID, 예약 저장·재로드, 잘못된 지정의 차단.
- `Tools > Project Abyss > Examples > Verify Engraved Contract`: 실제 전투 디스패치, 직접성, 시작 스냅샷, 결투 게이트, 다음 턴 실행.
- 기존 Ability Extensibility / Ability Interaction Contracts / Confirmed Combat Rules: 공통 회귀 검증.
