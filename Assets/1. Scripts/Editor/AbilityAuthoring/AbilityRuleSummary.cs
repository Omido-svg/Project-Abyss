#if UNITY_EDITOR
using System;
using System.Linq;
using ProjectAbyss.Abilities;

/// <summary>Read-only authoring display. Never changes a rule or its runtime ordering.</summary>
public static class AbilityRuleSummary
{
    public static string Describe(AbilityRuleData rule)
    {
        if(rule==null)return "비어 있는 규칙";
        string timing=rule.ScheduledOnly?"예약된 다음 턴 실행":rule.Policy?.DamageConditionOrigin switch
        {
            AbilityDamageConditionOrigin.ExchangeWon=>"승리한 원래 타격의 피해 확정 후",
            AbilityDamageConditionOrigin.RollUsing=>"사용한 원래 타격의 피해 확정 후",
            _=>rule.Trigger switch
            {
                AbilityEventKind.RollUsing=>"각 굴림 사용 시",AbilityEventKind.ExchangeWon=>"실제 교환 승리 시",
                AbilityEventKind.ExchangeLost=>"실제 교환 패배 시",AbilityEventKind.MarkBurst=>"표식 발화 시",
                AbilityEventKind.AttackResolutionStarting=>"공격 처리 시작 시",AbilityEventKind.ClashCompleted=>"합 종료 시",
                AbilityEventKind.PreparationActivated=>"준비 발동 시",_=>rule.Trigger.ToString()
            }
        };
        string target=rule.TargetSelectorId switch
        {
            AbilityTargets.Self=>"자신",AbilityTargets.Actual=>"실제 대상",AbilityTargets.Aimed=>"지정 대상",
            AbilityTargets.SourcePart=>"사용 부위",AbilityTargets.AllEnemies=>"모든 적",AbilityTargets.AllAllies=>"모든 아군",
            AbilityTargets.EnemyParts=>"적 부위들",_=>rule.TargetSelectorId
        };
        string effects=string.Join(", ",(rule.Operations??Array.Empty<AbilityOperationData>()).Select(DescribeEffect));
        string limit=rule.Lifetime?.OncePerSkillExecution==true?" · 스킬 실행당 1회":"";
        if(rule.Lifetime?.OncePerTurn==true)limit+=" · 턴당 1회";
        if(rule.Lifetime?.FullCooldownTurns>0)limit+=" · 쿨다운 "+rule.Lifetime.FullCooldownTurns+"턴";
        if(rule.Lifetime?.ConsumeOnSchedule==true)limit+=" · 예약 시 횟수 소비";
        return rule.RuleId+"\n"+timing+" → "+target+" : "+effects+"\n조건: "+DescribeCondition(rule.Condition,0)+
            "\n스냅샷: "+rule.Policy?.Snapshot+" · 결투 게이트: "+(rule.RequiresDuelGate?"적용":"해제")+limit;
    }

    private static string DescribeEffect(AbilityOperationData effect)
    {
        if(effect==null)return "효과 미설정";
        if(effect.TypeId=="schedule.next_turn"&&effect.Settings is AbilityScheduleDefinition schedule)
            return "다음 턴 예약 → "+schedule.RuleId;
        if(effect.Settings!=null&&effect.TypeId!="resource.energy"&&effect.TypeId!="resource.block"&&
            effect.TypeId!="damage.fixed"&&effect.TypeId!="status.add")
            return effect.TypeId+" (설정: "+effect.Settings.name+")";
        string name=effect.TypeId switch
        {
            "resource.energy"=>"빛", "resource.block"=>"방어도", "damage.fixed"=>"고정 피해",
            "status.add"=>"상태 "+effect.Key, _=>effect.TypeId
        };
        return name+" "+effect.Amount+(effect.Settings!=null?" ("+effect.Settings.name+")":"");
    }

    private static string DescribeCondition(AbilityConditionData condition,int depth)
    {
        if(condition==null)return "항상";
        if(depth>=6)return "… (상세 데이터에서 확인)";
        if(condition.Kind!=CombatConditionNodeKind.Predicate)
        {
            var children=condition.Children??Array.Empty<AbilityConditionData>();
            string join=condition.Kind==CombatConditionNodeKind.All?" 그리고 ":" 또는 ";
            return condition.Kind==CombatConditionNodeKind.Not?"아님("+string.Join(",",children.Select(c=>DescribeCondition(c,depth+1)))+")":
                "("+string.Join(join,children.Select(c=>DescribeCondition(c,depth+1)))+")";
        }
        var predicate=condition.Predicate;
        return predicate?.TypeId switch
        {
            "always"=>"항상", "cause.direct"=>"이 스킬의 직접 원인 ("+predicate.Key+")",
            "damage.hp_at_least"=>"실제 HP 피해 ≥ "+predicate.Value, "damage.weakened"=>"이번 타격으로 약화",
            "damage.broken"=>"이번 타격으로 파괴",_=>predicate?.TypeId??"조건 미설정"
        };
    }
}
#endif
