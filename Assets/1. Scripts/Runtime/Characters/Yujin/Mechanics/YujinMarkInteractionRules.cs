using System;
using System.Collections.Generic;
using System.Linq;
using ProjectAbyss.Abilities;

public sealed partial class YujinMechanic
{
    public bool SharpenWeapon(int amount,string weaponInstanceId=null)
    {
        if(amount<=0||owner==null)return false;
        var key=new AbilityScopeKey(AbilityOwnerScope.WeaponInstance,LiveSlotQuery.ActorRuntimeId(owner),weaponInstanceId??WeaponInstanceId);
        battleContext.Abilities.State.Set(key,"yujin.weapon/sharpen.amount",amount);
        battleContext.Abilities.State.Set(key,"yujin.weapon/sharpen.turn",battleContext.Abilities.CurrentTurn+1);
        return true;
    }
    public int SharpenBonus
    {
        get
        {
            var key=new AbilityScopeKey(AbilityOwnerScope.WeaponInstance,LiveSlotQuery.ActorRuntimeId(owner),WeaponInstanceId);
            return battleContext.Abilities.State.Get(key,"yujin.weapon/sharpen.turn")==battleContext.Abilities.CurrentTurn
                ?(int)battleContext.Abilities.State.Get(key,"yujin.weapon/sharpen.amount"):0;
        }
    }
    private EffectCause WeaponMarkCause(BattleAction action)
    {
        var parent=action.GetEffectCause(AbilityEventKind.ExchangeWon,action.CurrentRollIndex);
        var cause=new EffectCause(parent.RootActionId,parent.EventId,battleContext.EventSequence.Next(),"yujin.weapon.nakjeom",
            LiveSlotQuery.ActorRuntimeId(owner),action.CurrentRollIndex,EffectOriginKind.Reaction);
        battleContext.EventSequence.Record(cause);return cause;
    }
    public YujinMarkGainResult ApplyGrowthMarkGain(Character target,BodyPart part,int amount,BattleAction action,
        EffectCause cause,YujinMarkGainPolicy policy)
    {
        if(!cause.IsKnown||cause.SourceActorId!=LiveSlotQuery.ActorRuntimeId(owner))return default;
        if(action?.Skill?.Definition?.SkillId!=cause.SourceAbilityId || (cause.OriginKind!=EffectOriginKind.DirectRoll && cause.OriginKind!=EffectOriginKind.Reaction))policy=default;
        return ApplyMarkGain(CombatStatusAnchor.Resolve(target,part),amount,action,cause,policy);
    }
    public long RegisterContagionTrap(Character target,BodyPart part,int amount,BattleAction action=null,
        EffectCause cause=default,bool derived=false)
    {
        if(amount<=0)return 0;
        var anchor=CombatStatusAnchor.Resolve(target,part);
        if(!anchor.IsValid||anchor.IsBroken||target.IsDead)return 0;
        return delayedTriggers.Schedule(anchor,YujinDelayedTriggerKeys.MarkIgnited,derived?1:2,0,
            new YujinMarkRiderPayload(YujinMarkRiderType.Contagion,amount),action,cause);
    }
    public int ContagionTrapCount(Character target,BodyPart part)=>delayedTriggers.CountFor(
        CombatStatusAnchor.Resolve(target,part),p=>p is YujinMarkRiderPayload rider && rider.Type==YujinMarkRiderType.Contagion);
    private void SpreadContagion(CombatStatusAnchor source,DelayedEffectTriggerEntry trap,BattleAction action,EffectCause ignition)
    {
        if(trap?.Payload is not YujinMarkRiderPayload payload||payload.Value<=0)return;
        var candidates=new List<CombatStatusAnchor>();
        var opponents=owner==battleContext.Player?battleContext.Enemies:
            battleContext.Player==null?new List<Character>():new List<Character>{battleContext.Player};
        foreach(var enemy in opponents)
        {
            if(enemy==null||enemy==source.Character||enemy.IsDead)continue;
            if(enemy.IsSingleHpTarget)candidates.Add(CombatStatusAnchor.Resolve(enemy,null));
            else foreach(var part in enemy.BodyParts.Where(p=>p!=null&&!p.IsBroken))candidates.Add(CombatStatusAnchor.Resolve(enemy,part));
        }
        var selected=new List<CombatStatusAnchor>();
        while(candidates.Count>0&&selected.Count<2)
        {
            int highest=candidates.Max(c=>GetMark(c.Character,c.Part));
            var tied=candidates.Where(c=>GetMark(c.Character,c.Part)==highest).ToArray();
            var chosen=tied[battleContext.Abilities.Random.Next(tied.Length)];selected.Add(chosen);candidates.Remove(chosen);
        }
        foreach(var target in selected)
        {
            var cause=new EffectCause(ignition.RootActionId,ignition.EventId,battleContext.EventSequence.Next(),"yujin.trap.contagion",
                LiveSlotQuery.ActorRuntimeId(owner),trap.SourceCause.SourceRollIndex,EffectOriginKind.Trap);
            battleContext.EventSequence.Record(cause);
            // Install first, so an immediately igniting infection can legally continue or reinfect previous targets.
            RegisterContagionTrap(target.Character,target.Part,payload.Value,action,cause,true);
            ApplyMarkGain(target,payload.Value,action,cause);
        }
    }
}
