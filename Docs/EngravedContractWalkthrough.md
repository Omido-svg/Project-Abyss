# 복합 스킬 구현 실습 — 각인의 계약

이 예제는 현재 Project Abyss API로 구현하고 Unity Editor에서 검증한 교육용 신규 스킬이다. 정본의 미정값을 확정한 콘텐츠가 아니다. 언어는 C#이다.

현재 예제의 C# 규칙 조립은 `SkillRules` 작성 함수를 사용한다. 처음 읽는 경우 [읽기 쉬운 스킬 작성 가이드](SkillAuthoringGuide.md)에서 시작한다. 아래 B/C의 상세 필드 코드는 생성 결과를 설명하는 자료이며, 부록에는 현재 소스를 수록했다. 전투 계산과 네 규칙의 의미는 동일하다.

## 1. 먼저 정확한 기획을 만든다

**각인의 계약**: 유진용 RED 결투, 3굴림, 빛 비용 2. 모든 숫자는 교육용 예시다.

- A: 실제 교환 승리마다 대상에게 표식을 부여한다. 스킬 시작 시 원래 대상에게 표식이 없었다면 +4, 있었다면 +8. 앞 굴림에서 새로 붙인 표식은 시작 조건을 바꾸지 않는다.
- A의 예외: 이 스킬 고유 표식이 직접 44 발화를 일으키면 44 초과분을 보존한다. 발화 결과 반복이나 재부여는 이 예제에 없다.
- B: 이 스킬 고유 표식이 직접 발화를 일으키면 자신에게 방어도 +3. 스킬 실행 하나당 한 번만 지급한다. 낙점·덫의 발화에는 지급하지 않는다.
- C: 이 스킬의 실제 승리 굴림에서 나온 원래 타격이 HP 피해를 6 이상 적용하고, 그 타격이 부위를 새로 약화시키거나 파괴하면 다음 턴 자신에게 빛 +1을 예약한다. 스킬 실행 하나당 예약 한 번이다.
- D: 다음 턴에 예약 수령자가 살아 있으면 빛을 적용한다. 빛 상한 등 기존 자원 규칙을 따른다.
- 결투 효과의 자격이 꺼진 행동에서는 A~C가 발동하지 않는다. 일방타격은 교환 승리로 인정하지 않는다. 비용은 기존 계획/행동 시스템이 담당한다.

유진의 낙점 등 기존 무기 효과는 별도로 처리된다. 아래 계산 예시는 먼저 이 카드 고유 표식만 분리해서 본다.

## 2. 네 가지 규칙과 하나의 특수 연산으로 나눈다

| 규칙 ID | 실제 평가 시점 | 읽는 상태 | 대상 | 연산 | 제한 |
|---|---|---|---|---|---|
| mark_on_win | ExchangeWon | LockAtActionStart | actual | example.engraved_contract.mark | 실제 이긴 굴림마다 |
| block_on_own_burst | MarkBurst | 확정 발화의 원인 | self | resource.block +3 | OncePerSkillExecution |
| reserve_energy_on_part_transition | DamageSettled, 선행 ExchangeWon | AfterCommittedDamage / PostDamageResult | self | schedule.next_turn | OncePerSkillExecution + ConsumeOnSchedule |
| deliver_energy_next_turn | 예약 큐가 다음 턴 실행 | LockAtScheduleCreation | 예약 당시 self | resource.energy +1 | ScheduledOnly |

A에만 새 C# 연산을 등록한다. 시작 스냅샷에서 표식량을 선택하고, 기존 표식 서비스에 초과분 보존 정책을 전달하는 작업이다. B의 방어도, C의 조건 조합·예약, D의 자원 지급은 기존 연산을 재사용한다.

여기서 Snapshot과 Trigger는 다른 질문에 답한다. A의 Trigger는 지금의 승리지만 조건에 쓰는 표식은 시작 시점 것이다. C는 승리에서 이어진 효과지만 조건 평가를 피해 확정 뒤로 미룬다.

## 3. 생성되는 객체의 수명과 책임

| 코드/객체 | 언제 생성하는가 | 보관하는 것 | 보관하면 안 되는 것 |
|---|---|---|---|
| SkillDefinition | 저작 시 | RED, 결투, 굴림 수, 비용, 원본 ID | 특정 전투의 HP나 이번 사용 횟수 |
| EngravedContractSettings | 저작 시 SO | 시작 표식 유무별 +4/+8 | 이번 대상의 표식, 보상 지급 여부 |
| EngravedContractRules | 저작 시 RuleSet 생성 | 네 규칙의 조합·조건·대상·수명 | 전투 중 진행 상태 |
| EngravedContractModule | 전투 런타임의 Registry 구성 시 | 연산 ID와 핸들러 연결 | 특정 캐릭터의 상태 |
| MarkHandler | Registry 구성 시 | 공유 가능한 무상태 로직 | 주인, 현재 대상, 이미 지급했는지 여부 |
| BattleAbilityRuntime/AbilityInstance | 전투/소유자별 실행 시 | 프레임, 명령 기록, 개별 수명·메모리 | 여러 런이 공유할 가변 SO 상태 |

같은 설정 자산을 두 캐릭터가 사용해도 한쪽 보상이 다른 쪽을 막으면 안 된다. 그래서 `alreadyRewarded` 같은 bool을 Settings SO나 공유 핸들러 필드에 넣지 않는다. 이미 있는 OncePerSkillExecution 수명을 사용한다.

## 4. 코드 파일의 역할

- `Runtime/Abilities/Examples/EngravedContractSettings.cs`: 수정 가능한 SO를 불변 실행 설정으로 복사한다.
- `Runtime/Abilities/Examples/EngravedContractModule.cs`: 새 표식 연산의 Validate / Plan / Commit.
- `Runtime/Abilities/Examples/EngravedContractRules.cs`: 네 규칙의 데이터 조립. 반환 정의는 비활성이다.
- `Editor/Verification/EngravedContractExample.cs`: 자산 생성 메뉴, 격리 fixture, 실제 ClashManager 검증, 명령 trace.

추가로 `AbilityRuleSetDefinition.cs`의 조건 자식 배열에 `[SerializeReference]`를 적용했다. 자산 저장/재로드 중 발견한 기존 재귀 조건 직렬화 경고를 해결하는 공통 수정이다. 스킬명 분기나 전투 계산은 수정하지 않았다. 변경 전 파일과 meta는 예제 묶음에 보관했다. 당시 프로젝트에 저장된 RuleSet 자산은 없음을 확인했으므로 기존 RuleSet 자산을 자동 이관하지 않았다.

## 5. Plan의 입력과 출력

MarkHandler.Plan에는 Character 객체나 DamageManager가 들어오지 않는다. 받은 것은 읽기 전용 프레임·스냅샷, 컴파일된 설정, 대상 참조, 명령 sink다.

실제 코드가 하는 일은 다음과 같다.

```csharp
var settings = (EngravedContractSpec)operation.Settings;
var original = view.Snapshot.OriginalTarget;
var scope = string.IsNullOrEmpty(original.PartId)
    ? ConditionScope.Actor : ConditionScope.Part;

bool marked = view.Snapshot.TryGet(
    original, scope, ConditionFact.KeywordStack,
    "yujin.mark", out int mark) && mark > 0;

var planned = operation.WithAmount(
    marked ? settings.Marked : settings.Unmarked);

base.Plan(view, planned, targets, sink);
```

`WithAmount`는 원래 SO를 수정하지 않고 명령용 값 복사본을 만든다. `base.Plan`은 대상마다 원인이 포함된 AbilityRuleCommand를 sink에 넣는다. 여기까지 실제 표식은 변하지 않는다.

`marked`는 현재 화면에 보이는 표식이 아니다. LockAtActionStart로 받은 시작 스냅샷의 값이다. 시작에 0이었다면 첫 굴림 뒤 표식이 생겨도 다음 굴림 역시 +4다.

## 6. Commit의 입력과 출력

중앙 적용기가 명령을 검증한 후에 핸들러의 Commit을 호출한다. 이제 도메인 객체를 사용할 수 있다.

```csharp
var result = mechanic.ApplyGrowthMarkGain(
    context.Target, context.Part, command.Operation.Amount,
    context.Runtime.Action(context.Frame.ActionId), command.Cause,
    new YujinMarkGainPolicy(
        preserveOverflow: true,
        repeatIgnition: false,
        reapplyAfterBurst: 0));
```

기존 메서드 이름에 Growth가 들어 있지만, 여기서는 그 서비스가 이미 제공하는 표식 정책을 재사용한다. 이 예제는 성장 단계에 따라 정책을 바꾸지 않는다. +44 계산기를 새로 작성하지 않는다.

Commit은 실제 적용 결과에 맞는 receipt를 반환한다. 적용됐으면 Applied와 실제 부여량·표식 전후 값을, 대상이 유효하지 않거나 적용하지 못했다면 Rejected를 반환한다. `AppliedAmount=8`인데 표식이 40→4일 수 있다. 8을 정상 부여한 뒤 44 발화가 표식을 소비했기 때문이다. 적용량과 최종 보유량 차이는 같은 개념이 아니다.

## 7. B: 직접 발화 보상은 별도 규칙이다

```csharp
new AbilityRuleData
{
    RuleId = "block_on_own_burst",
    Trigger = AbilityEventKind.MarkBurst,
    RequiresDuelGate = true,
    TargetSelectorId = "self",
    Policy = new AbilityRuleMetadata { CardAuthored = false },
    Condition = new AbilityConditionData
    {
        Predicate = new AbilityPredicateData
        {
            TypeId = "cause.direct",
            Key = source.SkillId
        }
    },
    Lifetime = new AbilityRuleLifetime { OncePerSkillExecution = true },
    Operations = new[]
    {
        new AbilityOperationData { TypeId = "resource.block", Amount = 3 }
    }
}
```

MarkBurst는 내부 사건이라 CardAuthored=false다. 그러나 정의의 SourceKind는 Skill이므로 결투 게이트는 그대로 적용된다. SourceKind=Passive로 바꾸지 않는다.

`cause.direct`는 발화와 그 부모 표식 부여의 직접 관계 및 source.SkillId를 확인한다. 같은 루트에서 발생한 덫 발화라는 이유로 통과하지 않는다. OncePerSkillExecution은 실제 보상 적용 후 그 사용을 기록한다. 다음 굴림의 두 번째 직접 발화에는 방어도가 추가되지 않는다.

## 8. C: 승리시 조건을 피해 후 읽는다

```csharp
var reserve = AbilityRuleAuthoringTemplates.AfterDamageEvaluation(
    "reserve_energy_on_part_transition",
    AbilityDamageConditionOrigin.ExchangeWon,
    new AbilityOperationData
    {
        TypeId = "schedule.next_turn",
        Settings = reservation
    });

reserve.Condition = new AbilityConditionData
{
    Kind = CombatConditionNodeKind.All,
    Children = new[]
    {
        new AbilityConditionData
        {
            Predicate = new AbilityPredicateData
            {
                TypeId = "damage.hp_at_least", Value = 6
            }
        },
        reserve.Condition // Any(damage.weakened, damage.broken)
    }
};
reserve.Lifetime.OncePerSkillExecution = true;
reserve.Lifetime.ConsumeOnSchedule = true;
```

템플릿은 내부 DamageSettled, 선행 ExchangeWon, AfterCommittedDamage, PostDamageResult, 결투 게이트를 구성한다. 검사하는 것은 적의 실제 피해 결과이고 보상 수령자는 self다.

6은 공격 위력이나 요청 피해가 아니라 실제 적용된 HP 피해다. 방어도가 모두 흡수했다면 HP 조건을 통과하지 않는다. `damage.weakened`는 대상이 지금 약화 상태라는 뜻이 아니라 이번 결과가 약화 전이를 일으켰다는 뜻이다.

예약이 큐에 들어갔다는 결과는 Scheduled이며 빛이 이미 적용됐다는 Applied가 아니다. 그래서 `ConsumeOnSchedule=true`가 중요하다. 다음 턴까지 수명 소비를 미루면 같은 스킬의 다른 굴림이 다시 예약할 수 있다. 예제 검증은 서로 다른 부위를 두 번 약화시켜도 예약이 하나인지 확인한다.

## 9. D: 예약 실행은 별도 사용 문맥이다

reservation은 같은 RuleSet의 `deliver_energy_next_turn`을 가리킨다. 이 규칙은 ScheduledOnly=true이고 `resource.energy +1`을 수행한다.

- 대상을 생성 시 self로 고정한다. 다음 턴에 조준 대상을 다시 고르지 않는다.
- DueTurn은 현재 턴+1이다. 같은 턴에 Drain을 여러 번 호출해도 조기 실행하지 않는다.
- 원래 스킬의 시작/종료와 별개인 새 실행 ID를 부여한다.
- RootActionId와 예약 원인은 이어 간다.
- 원래 액션의 스냅샷/스킬 메모리가 종료 때 해제돼도 예약 자체는 남는다.
- 예약 수령자가 죽으면 만료한다. 다른 캐릭터에게 보상을 옮기지 않는다.
- 자원 상한은 기존 AddEnergy가 적용한다.

## 10. 실제 호출 흐름

스킬 코드가 배틀 전체를 실행하지 않는다. 전투 시스템이 아래 순서에 따라 규칙을 평가한다.

```text
[전투 준비]
BattleContext.Abilities
  -> BattleAbilityRuntime 생성
  -> AbilityRuleModules.Configure
  -> EngravedContractModule.Register
     -> "example.engraved_contract.mark"와 MarkHandler 연결
  -> 활성 RuleSet 등록/컴파일

[행동 시작]
ClashManager.ResolvePair
  -> BattleEvent.RaiseActionStart
  -> BattleAbilityRuntime.ActionStarting
     -> 시작 표식/무기/상태 스냅샷 캡처

[각 실제 교환]
사용시 통지 -> 양측 굴림/승패 판정
  -> Skill.NotifyRollOutcome(won=true)
     -> OnExchangeWin
     -> SkillEffectDispatcher.Execute
     -> BattleAbilityRuntime.Execute
        -> 결투 게이트 -> 규칙 A -> 시작 스냅샷
        -> AbilityRunner.PlanWithFrame
           -> MarkHandler.Plan -> 명령만 생성
        -> PlannedCommandBridge.Materialize
        -> CombatCommandApplyService.Apply
           -> Backend.Apply -> MarkHandler.Commit
              -> YujinMechanic.ApplyGrowthMarkGain
                 -> 표식 증가/44 발화/초과분 보존
                 -> MarkBurst 확정 사건 -> 규칙 B 반응을 큐에 추가
        -> A의 적용 결과 확정 -> 반응 큐 처리
           -> B 직접성/수명 검사 -> resource.block Commit

  -> DamagePowerResolver / DamageManager.ApplyDamageContext
     -> HP/부위/흐트러짐 결과 확정
     -> DamageEventDispatcher
        -> Skill의 AfterDamage 통지 및 DamageCommitted 게시
        -> 규칙 C: 같은 굴림의 직접 승리 피해인지 검사
        -> HP>=6 AND (새 약화 OR 파괴)
        -> schedule.next_turn Commit -> Scheduled

  -> Skill.NotifyRollResolved
     (피해 후 호환 효과 처리. 공식 ExchangeWon은 앞의 NotifyRollOutcome)
  -> ExchangeResolved 관찰자
     (유진의 공통 낙점 등은 별도 처리)

[행동 종료]
스킬 실행 메모리/시작 스냅샷 정리, 예약은 유지

[다음 턴]
TurnStartEffectQueue.Drain
  -> 생존/예약 대상/도래 턴 확인
  -> 새 예약 실행 문맥
  -> 규칙 D -> 중앙 명령 적용기 -> Character.AddEnergy
```

발화가 Nakil HP 피해처럼 추가 피해를 만드는 경우, 그 파생 피해는 A의 도메인 처리 중에 먼저 확정될 수 있다. C는 원래 굴림의 직접 피해만 받아들이므로 이 피해가 원래 카드 보상을 자동 상속하지 않는다. 위의 기본 피해 경로와 구분해야 한다.

## 11. 실제 로그 읽기

검증에서 나온 첫 번째 교환의 명령 순서는 다음과 같다. 숫자 ID는 그 검증 실행의 예이며 하드코딩할 설정값이 아니다.

```text
A mark_on_win: Applied, 요청/적용 8, 표식 40->4, root=700001, event=5
B block_on_own_burst: Applied, 방어도 0->3, event=7, parent=6
C reserve_energy_on_part_transition: Scheduled, 적용량 0, event=9
...
D deliver_energy_next_turn: Applied, 빛 0->1,
  action=2000000000000001, root=700001, event=11, parent=9
```

원인 연결은 A 명령(5) → 표식 부여(6) → 발화(7)이다. B는 발화(7)와 부모 부여(6)를 검사한다. 예약 전달(11)은 원래 행동 root를 유지하면서 C(9)의 자식으로 실행된다.

실제 전체 교환에서는 낙점이 별도로 표식을 추가하므로 두 번째 A 명령의 시작 전 현재 표식이 4보다 많을 수 있다. 그래도 A가 선택한 부여량은 모든 굴림에서 8이다. 현재 표식과 시작 스냅샷을 구분한 결과다.

## 12. Unity에서 실행하는 법

먼저 `Tools > Project Abyss > Examples > Verify Engraved Contract`를 실행한다. 이 메뉴는 원본 캐릭터 데이터를 복제한 Preview Scene에서 검증하며 씬/콘텐츠 설정을 저장하지 않는다. 결과는 Logs/EngravedContractExampleVerification.txt, 명령 흐름은 Logs/EngravedContractExampleTrace.txt에 남는다.

실제 저작 자산은 `Tools > Project Abyss > Examples > Create Engraved Contract Assets`에서 저장 위치를 고르면 생성된다.

- SkillDefinition 자산 한 개.
- 표식 수치용 EngravedContractSettings 자산 한 개.
- 비활성 RuleSet 자산 한 개. 예약 설정은 그 자산의 subasset으로 저장된다.

`MarkWithoutStartMark/MarkWithStartMark`는 설정 SO에서 수정한다. 방어도, HP 조건, 다음 턴 빛은 생성된 RuleSet의 해당 Operations.Amount / Predicate.Value에서 수정한다. 팩토리 인수의 기본값은 새 자산 생성 때 쓰는 초기값이다. 팩토리를 매 공격마다 호출하지 않는다.

게임 화면에 연결하려면 테스트용 캐릭터 데이터/로드아웃 복사본을 사용하는 편이 좋다.

1. RuleSet의 원본 GUID·ID·이름과 Inspector 검증을 확인한다.
2. 검토한 RuleSet을 Enabled=true로 한다.
3. 그 RuleSet을 해당 캐릭터 데이터의 AbilityRules에 추가한다. 또는 자동 로드가 필요한 경우 Resources/ProjectAbyss/AbilityRules 아래에 저장한다. 어느 경로로 등록되는지 명확히 한다.
4. 생성된 SkillDefinition을 사용할 스킬 슬롯/로드아웃에 연결한다. 규칙 자산을 연결하는 것과 사용할 수 있는 스킬 목록에 넣는 것은 다른 작업이다.
5. SkillDefinition의 연출 자산도 연결한다. 이 예제는 전투 모델을 검증하며, 새 애니메이션/VFX 자산을 만들지 않는다.
6. 새 전투를 시작해 정상 계획 서비스로 빛 2 비용을 지불하고 결투를 배치한다.

핸들러의 Plan/Commit을 버튼이나 Update에서 직접 호출하지 않는다. UI는 행동 배치를 요청하고, 배틀 루프가 정의를 찾아 호출한다. 검증의 경계별 테스트는 일부 통지를 분리 호출하지만, 별도로 실제 ClashManager.ResolvePair 경유 사례도 포함한다.

## 13. 검증 범위

예제 32 PASS / 0 FAIL. 기존 확장 142, 상호작용 70, 확정 전투 77항목도 유지돼 총 321 PASS / 0 FAIL이다. Unity 컴파일 및 마지막 검증 콘솔 오류·경고는 0, 최종 씬 Dirty=false와 Play 종료 상태를 확인했다.

시작 조건 고정, 설정 컴파일 후 변이 차단, 같은 굴림 중복, 직접 발화/덫 발화 구분, 1회 보상, 실피해 임계치, 서로 다른 부위의 두 번 전이에도 1회 예약, 원래 행동 종료 후 예약 생존, 사망 취소, 실제 결투 대 결투/평타 게이트, 자산 저장·재로드·subasset 참조, 14단계 조건 트리를 검증했다.

이 결과는 실제 Editor의 전투 도메인과 저작 자산을 검사한 것이다. 새 연출, 실제 게임 화면 로드아웃 장착, 전체 빌드, IL2CPP 플랫폼 검증은 이번 예제 범위에 포함하지 않았다. 기존 미정 증강 Blocker 1과 경고 60은 임의 수치로 제거하지 않았다.

Unity의 참조형 직렬화 설명: https://docs.unity3d.com/kr/current/ScriptReference/SerializeReference.html

## 14. 다른 복잡한 스킬을 만드는 순서

1. 설명문에서 발동/조건/판정 시점/대상/변경/수명/직접성을 먼저 분리한다.
2. 기존 연산으로 표현되는 부분을 찾는다. 피해·상태·자원 계산을 복사하지 않는다.
3. 남은 고유 계산에만 불변 설정과 핸들러를 추가한다.
4. Plan에는 관찰/계획, Commit에는 도메인 서비스 호출과 실제 결과를 둔다.
5. 어떤 사건이 다음 규칙을 깨우는지, 어떤 파생 사건은 제외하는지 명시한다.
6. 성공 사례뿐 아니라 시작 상태 변경·같은 사건 재통지·대상 파괴·사망·예약·다른 원인·비활성 게이트를 검증한다.
7. 인메모리 검증 뒤 자산 저장/재로드도 확인한다. 전투 코드가 맞아도 저작 데이터가 보존되지 않으면 같은 스킬이 아니다.

이하 부록은 현재 예제의 전체 C# 코드다. `SkillRules` 및 공통 작성 헬퍼는 프로젝트의 Runtime/Abilities/Native에 있다.

### EngravedContractSettings.cs

```csharp
using System;
using UnityEngine;
using ProjectAbyss.Abilities;

namespace ProjectAbyss.Examples
{
    // These are educational example values, not canonical Project Abyss balance.
    [CreateAssetMenu(menuName="Project Abyss/Examples/Engraved Contract Settings")]
    public sealed class EngravedContractSettings : ScriptableObject,IAbilityRuleSettingsDefinition
    {
        [Min(1)] public int MarkWithoutStartMark=4;
        [Min(1)] public int MarkWithStartMark=8;
        public IAbilityRuleSettings CaptureSettings()=>new EngravedContractSpec(
            MarkWithoutStartMark,MarkWithStartMark);
    }

    public sealed class EngravedContractSpec : IAbilityRuleSettings
    {
        public readonly int Unmarked,Marked;
        public EngravedContractSpec(int unmarked,int marked)
        {
            if(unmarked<=0||marked<=0)
                throw new ArgumentException("All educational skill amounts must be explicitly positive.");
            Unmarked=unmarked;Marked=marked;
        }
    }
}
```

### EngravedContractModule.cs

```csharp
using System.Collections.Generic;
using ProjectAbyss.Abilities;

namespace ProjectAbyss.Examples
{
    [UnityEngine.Scripting.Preserve]
    [AbilityRuleModule("example.engraved_contract")]
    public sealed class EngravedContractModule : IAbilityRuleModule
    {
        public const string MarkOperation="example.engraved_contract.mark";
        public void Register(AbilityRuleRegistry registry)=>registry.RegisterOperation(MarkOperation,new MarkHandler());

        private sealed class MarkHandler : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec operation)=>
                operation.Settings is EngravedContractSpec?null:"ENGRAVED_CONTRACT_SETTINGS_REQUIRED";

            public override void Plan(AbilityRuleReadContext view,AbilityOperationSpec operation,
                IReadOnlyList<CombatTargetRef> targets,IAbilityRuleCommandSink sink)
            {
                if(view.Frame.SnapshotPolicy!=AbilitySnapshotPolicy.LockAtActionStart)
                    throw new System.InvalidOperationException("This operation requires an action-start snapshot.");

                var settings=(EngravedContractSpec)operation.Settings;
                var original=view.Snapshot.OriginalTarget;
                var scope=string.IsNullOrEmpty(original.PartId)?ConditionScope.Actor:ConditionScope.Part;
                bool marked=view.Snapshot.TryGet(original,scope,ConditionFact.KeywordStack,"yujin.mark",out int mark)&&mark>0;

                // Copy the immutable operation with the chosen amount. Planning never modifies a Character.
                var planned=operation.WithAmount(marked?settings.Marked:settings.Unmarked);
                base.Plan(view,planned,targets,sink);
            }

            public override CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command)
            {
                var mechanic=context.Source.GetMechanic<YujinMechanic>();
                if(mechanic==null)return CombatCommandReceipt.Reject("Yujin mark service unavailable",CombatFailureReason.UnsupportedOperation);

                // Reuse the existing mark service's supported overflow policy; do not implement another 44-burst calculator.
                var result=mechanic.ApplyGrowthMarkGain(context.Target,context.Part,command.Operation.Amount,
                    context.Runtime.Action(context.Frame.ActionId),command.Cause,
                    new YujinMarkGainPolicy(preserveOverflow:true,repeatIgnition:false,reapplyAfterBurst:0));

                return result.Applied?Applied(result.ActualGain,"Example skill mark committed",
                    CombatAppliedValues.FromResource(result.PreviousMark,result.MarkAfter)):
                    CombatCommandReceipt.Reject("Mark not applied",CombatFailureReason.TargetUnavailable);
            }
        }
    }
}
```

### EngravedContractRules.cs

```csharp
using System;
using UnityEngine;
using ProjectAbyss.Abilities;

namespace ProjectAbyss.Examples
{
    /// <summary>Creates a disabled sample definition. No canonical assets are replaced or automatically enabled.</summary>
    public static class EngravedContractRules
    {
        public const string StableId="example.yujin.engraved_contract";
        public const string MarkRule="mark_on_win";
        public const string BurstRule="block_on_own_burst";
        public const string ReserveRule="reserve_energy_on_part_transition";
        public const string DeliveryRule="deliver_energy_next_turn";

        public static AbilityRuleSetDefinition Create(SkillDefinition source,string sourceGuid,
            EngravedContractSettings settings,out AbilityScheduleDefinition reservation,
            int blockOnBurst=3,int minimumAppliedHp=6,int energyNextTurn=1)
        {
            if(source==null||settings==null||string.IsNullOrWhiteSpace(sourceGuid))throw new ArgumentException("Source, GUID and settings are required.");
            settings.CaptureSettings();
            if(blockOnBurst<=0||minimumAppliedHp<=0||energyNextTurn<=0)throw new ArgumentException("Positive example rule amounts required.");
            var definition=SkillRules.CreateDraft(source,sourceGuid);
            definition.name="EngravedContractRules";
            var nextTurn=SkillRules.NextTurnReward(definition,DeliveryRule,AbilityEffects.Energy(energyNextTurn));
            reservation=nextTurn.Reservation;
            reservation.name="EngravedContractReservation";

            definition.Rules=new[]{
                // A: 승리 시 실행하되, 표식 분기는 원래 대상의 행동 시작 상태로 고정한다.
                SkillRules.OnExchangeWin(MarkRule,
                    AbilityEffects.Custom(EngravedContractModule.MarkOperation,settings:settings))
                    .AtActionStart(),

                // B: 이 스킬이 직접 일으킨 발화만 보상한다. 내부 이벤트 설정은 헬퍼가 채운다.
                SkillRules.OnOwnDirectMarkBurst(BurstRule,source.SkillId,AbilityEffects.Block(blockOnBurst))
                    .OncePerSkillExecution(),

                // C: 발화·덫 피해를 제외한 원래 승리 타격의 확정 결과로 예약한다.
                SkillRules.AfterWinningHit(ReserveRule,
                    AbilityConditions.All(AbilityConditions.AppliedHpAtLeast(minimumAppliedHp),
                        AbilityConditions.PartWeakenedOrBroken()),
                    nextTurn.Schedule)
                    .OncePerSkillExecution(consumeOnSchedule:true),

                // D: C가 예약할 때 고정한 대상과 스냅샷으로 다음 턴에 별도 실행한다.
                nextTurn.Delivery
            };
            return definition;
        }
    }
}
```

### EngravedContractExample.cs

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectAbyss.Abilities;
using ProjectAbyss.Examples;

public static class EngravedContractExample
{
    public static string LastReport { get; private set; }
    public static string LastTrace { get; private set; }

    public static SkillDefinition CreateSource()
    {
        var source=ScriptableObject.CreateInstance<SkillDefinition>();
        source.name="EngravedContractSkill";source.SkillName="각인의 계약 (교육용)";
        source.ActionType=ActionType.Duel;source.Color=SkillColor.Red;source.ExchangeRollCount=3;
        source.OverrideEnergyCost=true;source.EnergyCost=2;source.BasePowerRule=SkillBasePowerRule.CanonicalCurve;
        var serialized=new SerializedObject(source);serialized.FindProperty("skillId").stringValue=EngravedContractRules.StableId;
        serialized.ApplyModifiedPropertiesWithoutUndo();return source;
    }

    [MenuItem("Tools/Project Abyss/Examples/Create Engraved Contract Assets")]
    public static void CreateAssets()
    {
        string path=EditorUtility.SaveFilePanelInProject("각인의 계약: 비활성 교육용 자산", "EngravedContractRules", "asset",
            "스킬·표식 설정·규칙과 예약 설정을 생성합니다. 규칙은 비활성 상태로 생성됩니다.");
        if(string.IsNullOrEmpty(path))return;
        var created=new List<string>();
        try
        {
            string folder=Path.GetDirectoryName(path).Replace('\\','/');
            var source=CreateSource();string skillPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/EngravedContractSkill.asset");
            AssetDatabase.CreateAsset(source,skillPath);created.Add(skillPath);
            var settings=ScriptableObject.CreateInstance<EngravedContractSettings>();
            string settingsPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/EngravedContractSettings.asset");
            AssetDatabase.CreateAsset(settings,settingsPath);created.Add(settingsPath);
            var rules=EngravedContractRules.Create(source,AssetDatabase.AssetPathToGUID(skillPath),settings,out var reservation);
            AbilityRuleSetEditor.SaveNewDraft(rules,path);created.Add(path);Selection.activeObject=rules;
            Debug.Log("[각인의 계약] 비활성 예시 자산 생성. 확정 수치가 아닌 교육용 값입니다: "+path);
        }
        catch
        {
            foreach(var item in created.AsEnumerable().Reverse())AssetDatabase.DeleteAsset(item);
            throw;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BattleContext Battle;
        public readonly List<UnityEngine.Object> Objects=new();
        public readonly Yujin Player,Enemy;
        public readonly SkillDefinition Source;
        public readonly EngravedContractSettings Settings;
        public AbilityRuleSetDefinition Rules;
        public BodyPart Part=>Enemy.GetBodyPart(PartType.LEFT_HAND);
        private readonly UnityEngine.SceneManagement.Scene scene;
        private readonly UnityEngine.Random.State random;
        private long nextId=700000;
        public Fixture()
        {
            random=UnityEngine.Random.state;UnityEngine.Random.InitState(20261117);scene=EditorSceneManager.NewPreviewScene();
            Battle=new BattleContext{SuppressPresentation=true,RunProgression=new RunProgressionState{Seed=20261117}};
            Battle.Services=new BattleRuntimeServices();Battle.EffectResolver=new BattleEffectResolver(Battle);
            Battle.Services.MomentumManager=new MomentumManager(Battle);Battle.Services.DamageManager=new DamageManager(Battle,Battle.Services.MomentumManager);
            Battle.Services.ActionManager=new ActionManager();Player=Spawn();Enemy=Spawn();Battle.Player=Player;Battle.Enemies.Add(Enemy);
            Source=CreateSource();Objects.Add(Source);Settings=ScriptableObject.CreateInstance<EngravedContractSettings>();Objects.Add(Settings);
        }
        private Yujin Spawn()
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2. Data/TestEncounters/Prefabs/Yujin_Test.prefab"));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);Objects.Add(go);
            var actor=go.GetComponent<Yujin>();var data=UnityEngine.Object.Instantiate(actor.Data);Objects.Add(data);
            actor.ConfigureAuthoringCore(data,actor.Data.CombatLoadout);actor.Initialize(Battle);return actor;
        }
        public void Register()
        {
            Rules=EngravedContractRules.Create(Source,"example-fixture-guid",Settings,out var reservation);
            Objects.Add(reservation);Objects.Add(Rules);
            // Explicit registration only in this isolated fixture. Auto-loading respects Enabled=false.
            Battle.Abilities.Register(Rules);
        }
        public BattleAction Action(bool enemy=false,bool opponentDuel=true)
        {
            Character owner=enemy?Enemy:Player,target=enemy?Player:Enemy;SkillDefinition definition=Source;
            if(enemy)
            {
                definition=ScriptableObject.CreateInstance<SkillDefinition>();Objects.Add(definition);
                definition.name="ExampleOpponent";definition.ActionType=opponentDuel?ActionType.Duel:ActionType.NormalAttack;
                definition.Color=SkillColor.Red;definition.ExchangeRollCount=3;definition.OverrideEnergyCost=true;definition.EnergyCost=0;
            }
            Skill skill=definition.ActionType==ActionType.Duel?(Skill)new DataDuelSkill(definition):new DataNormalSkill(definition);
            skill.Initialize(owner,Battle._battleEvent);
            var action=new BattleAction{Slot=new ActionSlot{ActionId=++nextId,Owner=owner,Part=owner.GetBodyPart(PartType.LEFT_HAND),Skill=skill,
                TargetCharacter=target,TargetPart=target.GetBodyPart(PartType.LEFT_HAND),SlotId="example-"+nextId}};
            action.SetDuelEffectsEligible(true);return action;
        }
        public void Win(BattleAction action,BattleAction opponent,int roll)
        {
            action.CurrentRollIndex=roll;action.Skill.NotifyRollUsing(action,opponent,roll,true,false);
            action.Skill.NotifyRollOutcome(action,opponent,roll,true,true,false);
        }
        public DamageContext Damage(BattleAction action,int power=10)
        {action.RolledPower=power;return Battle.Services.DamageManager.ApplyDamageContext(DamageRequest.FromAction(action,DamageType.SkillPart,false,true,true));}
        public int Committed(string id)=>Battle.Abilities.Trace.Count(t=>t.Request.CommandId.Contains(":"+id+":")&&t.Receipt.EffectCommitted);
        public int Scheduled=>Battle.Abilities.Trace.Count(t=>t.Receipt.Outcome==CombatCommandOutcome.Scheduled);
        public void Dispose()
        {
            Battle.DisposeAbilities();foreach(var item in Objects.AsEnumerable().Reverse())if(item!=null)UnityEngine.Object.DestroyImmediate(item);
            EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;
        }
    }

    [MenuItem("Tools/Project Abyss/Examples/Verify Engraved Contract")]
    public static void RunVerification()
    {
        int passed=0,failed=0;var lines=new List<string>();var trace=new List<string>();
        void Check(string id,bool ok){lines.Add((ok?"PASS ":"FAIL ")+id);if(ok)passed++;else failed++;}
        void Case(string name,Action body){try{body();}catch(Exception e){Check(name+": "+e.GetBaseException().Message,false);}}
        Case("authoring",()=>
        {
            using var f=new Fixture();f.Register();
            Check("rule graph validates",f.Battle.Abilities.Registry.Validate(f.Rules).Count==0);
            Check("three rolls red duel with example cost two",f.Source.ActionType==ActionType.Duel&&f.Source.Color==SkillColor.Red&&f.Source.ExchangeRollCount==3&&f.Source.EnergyCost==2);
            Check("assets are disabled by default",!f.Rules.Enabled);
            Check("custom operation automatically registered",f.Battle.Abilities.Registry.Operation(EngravedContractModule.MarkOperation)!=null);
        });
        Case("serialized authoring",()=>
        {
            using var f=new Fixture();string folder="Assets/__EngravedContractVerification_"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            try
            {
                var source=UnityEngine.Object.Instantiate(f.Source);AssetDatabase.CreateAsset(source,folder+"/Skill.asset");
                var settings=UnityEngine.Object.Instantiate(f.Settings);AssetDatabase.CreateAsset(settings,folder+"/Settings.asset");
                var rules=EngravedContractRules.Create(source,AssetDatabase.AssetPathToGUID(folder+"/Skill.asset"),settings,out var reservation);
                var nested=new AbilityConditionData();
                for(int i=0;i<14;i++)nested=new AbilityConditionData{Kind=CombatConditionNodeKind.Not,Children=new[]{nested}};
                rules.Rules[0].Condition=nested;
                AssetDatabase.CreateAsset(rules,folder+"/Rules.asset");AssetDatabase.AddObjectToAsset(reservation,rules);EditorUtility.SetDirty(reservation);AssetDatabase.SaveAssetIfDirty(reservation);EditorUtility.SetDirty(rules);AssetDatabase.SaveAssetIfDirty(rules);
                Check("settings have a Unity script binding",MonoScript.FromScriptableObject(settings)?.GetClass()==typeof(EngravedContractSettings));
                Resources.UnloadAsset(reservation);Resources.UnloadAsset(rules);AssetDatabase.ImportAsset(folder+"/Rules.asset",ImportAssetOptions.ForceUpdate);var loaded=AssetDatabase.LoadAssetAtPath<AbilityRuleSetDefinition>(folder+"/Rules.asset");
                var saved=(AbilityScheduleDefinition)loaded.Rules.Single(r=>r.RuleId==EngravedContractRules.ReserveRule).Operations[0].Settings;
                Check("reservation subasset survives reload and points at its graph",saved!=null&&saved.Definition==loaded&&saved.RuleId==EngravedContractRules.DeliveryRule);
                Check("deep condition tree survives serialization without flattening",f.Battle.Abilities.Registry.Compile(loaded.Rules[0].Condition).Evaluate(null,default));
                var issues=AbilityRuleSetEditor.ValidateAsset(loaded,f.Battle.Abilities.Registry);
                Check("serialized example passes source and schema validation / "+string.Join(";",issues),issues.Length==0);
            }
            finally{AssetDatabase.DeleteAsset(folder);}
        });
        Case("start snapshot",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();var opponent=f.Action(true);f.Battle._battleEvent.RaiseActionStart(action);
            f.Settings.MarkWithoutStartMark=30;
            for(int i=0;i<3;i++)f.Win(action,opponent,i);
            Check("unmarked start branch stays four on later rolls",f.Player.YujinMechanic.GetMark(f.Enemy,f.Part)==12);
            Check("settings mutation cannot change compiled plan",f.Battle.Abilities.Trace.Where(t=>t.Request.CommandId.Contains(":"+EngravedContractRules.MarkRule+":")).All(t=>t.Request.RequestedAmount==4));
            int before=f.Player.YujinMechanic.GetMark(f.Enemy,f.Part);f.Win(action,opponent,2);
            Check("same roll replay does not add marks",f.Player.YujinMechanic.GetMark(f.Enemy,f.Part)==before);
        });
        Case("own burst",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();var opponent=f.Action(true);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);f.Battle._battleEvent.RaiseActionStart(action);f.Win(action,opponent,0);
            Check("marked start grants eight and preserves overflow four",f.Player.YujinMechanic.GetMark(f.Enemy,f.Part)==4);
            Check("own direct burst grants block three",f.Player.RuntimeStatus.currentBlock==3&&f.Committed(EngravedContractRules.BurstRule)==1);
            f.Player.YujinMechanic.TryConsumeMark(f.Enemy,f.Part,4);f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);f.Win(action,opponent,1);
            Check("second own burst in same skill does not repeat reward",f.Player.RuntimeStatus.currentBlock==3&&f.Committed(EngravedContractRules.BurstRule)==1);
        });
        Case("derived burst",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);
            var cause=action.GetEffectCause(AbilityEventKind.RollUsing,0).Derive(f.Battle.EventSequence.Next(),EffectOriginKind.Trap);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,8,action,cause);f.Battle.Abilities.DrainEffects();
            Check("trap-derived burst is not this card's direct reward",f.Committed(EngravedContractRules.BurstRule)==0&&f.Player.RuntimeStatus.currentBlock==0);
        });
        Case("reservation",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);
            f.Player.TryConsumeEnergy(f.Player.CurrentEnergy);f.Part.SetDebugState(5,100,false,false);
            var damage=f.Damage(action);Check("real damage crosses the weakening transition",damage.AppliedHpDamage>=6&&damage.WasWeakened);
            Check("reservation queued but energy not delivered yet",f.Scheduled==1&&f.Battle.TurnStartEffects.Count==1&&f.Player.CurrentEnergy==0);
            action.CurrentRollIndex=1;var head=f.Enemy.GetBodyPart(PartType.HEAD);head.SetDebugState(5,100,false,false);action.Slot.TargetPart=head;f.Damage(action);
            Check("schedule consumes the skill-scoped use immediately",f.Scheduled==1&&f.Battle.TurnStartEffects.Count==1);
            f.Battle._battleEvent.RaiseActionEnd(action);f.Battle.Abilities.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
            Check("reservation survives action-end cleanup",f.Player.CurrentEnergy==1&&f.Committed(EngravedContractRules.DeliveryRule)==1);
            f.Battle.TurnStartEffects.Drain();Check("repeated turn-start drain cannot redeliver",f.Player.CurrentEnergy==1);
        });
        Case("failed reservation conditions",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);f.Part.SetDebugState(100,100,false,false);f.Damage(action);
            Check("HP damage without transition does not reserve",f.Battle.TurnStartEffects.Count==0);
            f.Part.SetDebugState(3,100,false,false);var low=f.Damage(action,2);
            Check("transition below HP threshold does not reserve",low.WasWeakened&&low.AppliedHpDamage<6&&f.Battle.TurnStartEffects.Count==0);
        });
        Case("dead recipient",()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();f.Battle._battleEvent.RaiseActionStart(action);f.Part.SetDebugState(5,100,false,false);f.Damage(action);
            f.Battle.Services.DamageManager.ApplyDamageContext(DamageRequest.Custom(DamageType.True,null,f.Player,null,99999,1f,false,false,false));
            f.Battle.Abilities.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
            Check("dead reserved recipient cancels delivery",f.Player.IsDead&&f.Committed(EngravedContractRules.DeliveryRule)==0&&f.Battle.TurnStartEffects.ExpiredCount==1);
        });
        foreach(bool opponentIsDuel in new[]{true,false})Case("production pair "+opponentIsDuel,()=>
        {
            using var f=new Fixture();f.Register();var action=f.Action();var opponent=f.Action(true,opponentIsDuel);
            f.Player.YujinMechanic.ApplyMarkGain(f.Enemy,f.Part,40);f.Part.SetDebugState(5,100,false,false);
            f.Battle.Abilities.Modifiers.Add(new AbilityScopeKey(AbilityOwnerScope.Actor,LiveSlotQuery.ActorRuntimeId(f.Player)),
                new CombatModifierSpec(CombatModifierStage.RollPower,CombatModifierKind.OverrideFinal,10,0,0),action.GetEffectCause(AbilityEventKind.RollUsing,0),1);
            f.Battle.Abilities.Modifiers.Add(new AbilityScopeKey(AbilityOwnerScope.Actor,LiveSlotQuery.ActorRuntimeId(f.Enemy)),
                new CombatModifierSpec(CombatModifierStage.RollPower,CombatModifierKind.OverrideFinal,1,0,0),opponent.GetEffectCause(AbilityEventKind.RollUsing,0),1);
            var manager=new ClashManager(f.Battle,f.Battle.Services.DamageManager,f.Battle.Services.MomentumManager);
            var result=manager.ResolvePair(new ClashPair(action.Slot,opponent.Slot));
            Check("production pair actually applies damage / "+opponentIsDuel,result.Exchanges.Any(e=>e.DamageContext?.AppliedHpDamage>0));
            Check("production mark rule obeys duel gate / "+opponentIsDuel,opponentIsDuel?f.Committed(EngravedContractRules.MarkRule)>0:f.Committed(EngravedContractRules.MarkRule)==0);
            Check("production burst reward obeys duel gate / "+opponentIsDuel,f.Committed(EngravedContractRules.BurstRule)==(opponentIsDuel?1:0));
            Check("production reservation obeys duel gate / "+opponentIsDuel,f.Scheduled==(opponentIsDuel?1:0));
            if(opponentIsDuel)
            {
                f.Player.TryConsumeEnergy(f.Player.CurrentEnergy);f.Battle.Abilities.BeginTurn(2);f.Battle.TurnStartEffects.Drain();
                var delivered=f.Battle.Abilities.Trace.Last(t=>t.Request.CommandId.Contains(":"+EngravedContractRules.DeliveryRule+":"));
                Check("scheduled delivery has a new execution and original causal root",delivered.Request.ActionId!=action.ActionId&&delivered.Request.SourceCause.RootActionId==action.ActionId&&f.Player.CurrentEnergy==1);
                foreach(var record in f.Battle.Abilities.Trace)
                    trace.Add(record.Request.CommandId+" | "+record.Receipt.Outcome+" | requested="+record.Request.RequestedAmount+" | applied="+record.Receipt.AppliedAmount+
                        " | resource="+record.Receipt.Values.ResourceBefore+"->"+record.Receipt.Values.ResourceAfter+
                        " | root="+record.Request.SourceCause.RootActionId+" | event="+record.Request.SourceCause.EventId+" | parent="+record.Request.SourceCause.ParentEventId);
            }
        });
        LastReport="PASS="+passed+" FAIL="+failed+"\n"+string.Join("\n",lines);LastTrace=string.Join("\n",trace);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/EngravedContractExampleVerification.txt",LastReport);
        File.WriteAllText("Logs/EngravedContractExampleTrace.txt",LastTrace);
        if(failed>0)Debug.LogError(LastReport);else Debug.Log(LastReport);
    }
}
#endif
```
