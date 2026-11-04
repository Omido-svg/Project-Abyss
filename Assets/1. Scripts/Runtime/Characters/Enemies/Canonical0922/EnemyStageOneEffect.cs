using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// R7-8 / 1029 §12.3: native, source-ID-specific stage-1 enemy effects.
/// No SkillEffectDefinition.Apply, no invented (미정) card values.
/// The producer IDs are reserved for future authored SkillDefinition assets.
/// </summary>
public static class EnemyStageOneEffect
{
    public const string Prefix="enemy1029.stage1.";
    public const string TankCoverHeavy=Prefix+"heavy_guard.cover";
    public const string TankCoverCounter=Prefix+"counter_guard.cover";
    public const string TankAttackHeavy=Prefix+"heavy_guard.attack_duel";
    public const string TankAttackCounter=Prefix+"counter_guard.attack_duel";
    public const string HealerHeal=Prefix+"healer.heal_preparation";
    public const string CommanderSupply=Prefix+"commander.supply_preparation";
    public const string HealerNormal=Prefix+"healer.normal";
    public const string HealerDuel=Prefix+"healer.basic_duel";
    public const string PressureStrong=Prefix+"pressure.strong_duel";
    public const string BindNormal=Prefix+"bind.normal";
    public const string BindBasic=Prefix+"bind.basic_duel";
    public const string BindStrong=Prefix+"bind.strong_duel";
    public const string CorrosionNormal=Prefix+"corrosion.normal";
    public const string CorrosionBasic=Prefix+"corrosion.basic_duel";
    public const string CorrosionStrong=Prefix+"corrosion.strong_duel";

    private sealed class NaturalPenalty
    {
        public int NextTurn=-1;
        public bool HealerMinusOne;
        public bool ShadeBlocksAll;
        public Character ShadeSource;
    }
    private static readonly ConditionalWeakTable<BattleContext,NaturalPenalty>
        Penalties=new();

    public static bool IsCoverDuelId(string id) =>
        id==TankCoverHeavy||id==TankCoverCounter;
    public static bool IsHealId(string id)=>id==HealerHeal;
    public static bool IsSupplyId(string id)=>id==CommanderSupply;
    public static bool IsHealerAttackId(string id)=>
        id==HealerNormal||id==HealerDuel;

    private static bool Eligible(BattleAction action,EnemySpecies1029 species)
    {
        return action?.Owner is NormalEnemy enemy &&
            enemy.GetMechanic<NormalEnemy0922RoleMechanic>()?.Species==species;
    }

    public static bool ApplyCoverProtection(Character owner,BattleAction action)
    {
        if(owner==null||owner.IsDead)return false;
        var status=StatusEffectFactory.CreateCanonical0922(
            StatusEffectId.Protection,2,3);
        return status!=null&&owner.BattleContext?.EffectResolver?.ApplyCharacterStatus(
            EffectRequest.CharacterStatus(owner,owner,status,action))==true;
    }

    private static bool ApplyCanonical(BattleAction action,StatusEffectId id,
        int value,int duration,bool deferred)
    {
        if(action?.Owner==null||action.Target==null||action.Target.IsDead)
            return false;
        StatusEffect status=deferred
            ?new DeferredStatusEffect(id,value,duration)
            :StatusEffectFactory.CreateCanonical0922(id,value,duration);
        if(status==null)return false;
        var target=action.Target;
        var part=action.TargetPart;
        var resolver=action.Owner.BattleContext?.EffectResolver;
        return resolver!=null&&(part!=null
            ?resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(action.Owner,target,part,status,
                    action,action.CurrentRollIndex))
            :resolver.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(action.Owner,target,status,
                    action,action.CurrentRollIndex)));
    }

    /// <summary>
    /// Exact first roll consumed (not tie rerolls or plan-time).
    /// Called after the Duel gate by Skill.NotifyRollUsing.
    /// </summary>
    public static void OnRollUsing(BattleAction action,int rollIndex)
    {
        if(rollIndex==0&&Eligible(action,EnemySpecies1029.Bind)&&
           action.Skill?.Definition?.SkillId==BindNormal)
            ApplyCanonical(action,StatusEffectId.Bind,1,1,true);
    }

    public static void OnExchangeWon(ClashExchangeResult exchange,
        EnemySpecies1029 species)
    {
        var action=exchange?.WinnerAction;
        if(action==null||action.Target==null||action.Owner==null)return;
        string id=action.Skill?.Definition?.SkillId;
        if(string.IsNullOrEmpty(id))return;
        // Duel-v-Duel gates are strict even if an attack won one-sided.
        if(action.Skill.ActionType==ActionType.Duel &&
           !exchange.IsDuelExchange)return;
        switch(species)
        {
            case EnemySpecies1029.Pressure when id==PressureStrong:
                ApplyCanonical(action,StatusEffectId.Weakness,1,1,true);break;
            case EnemySpecies1029.Bind when id==BindBasic:
                ApplyCanonical(action,StatusEffectId.Disarm,1,1,true);break;
            case EnemySpecies1029.Bind when id==BindStrong:
                ApplyCanonical(action,StatusEffectId.Weakness,1,1,true);break;
            case EnemySpecies1029.Corrosion when id==CorrosionNormal:
                ApplyCanonical(action,StatusEffectId.Rupture,1,1,false);break;
            case EnemySpecies1029.Corrosion when id==CorrosionBasic:
                ApplyCanonical(action,StatusEffectId.Stagnation,2,1,false);break;
            case EnemySpecies1029.Corrosion when id==CorrosionStrong:
                ScheduleShade(action.Owner,action.Target);break;
            case EnemySpecies1029.HeavyGuard when id==TankAttackHeavy:
            case EnemySpecies1029.CounterGuard when id==TankAttackCounter:
                var mechanic=action.Owner.GetMechanic<NormalEnemy0922RoleMechanic>();
                if(mechanic?.HasActiveCover==true)
                    ApplyCoverProtection(action.Owner,action);
                break;
        }
    }

    public static Character SelectLowestHpRatio(
        Character owner,BattleContext battle,bool includeSelf)
    {
        if(owner==null||battle?.Enemies==null)return null;
        var candidates=battle.Enemies.Where(x=>x!=null&&!x.IsDead&&
            (includeSelf||x!=owner)).ToArray();
        if(candidates.Length==0)return null;
        float min=candidates.Min(x=>x.MaxCombatHP<=0
            ?1f:(float)x.CurrentHP/x.MaxCombatHP);
        var tied=candidates.Where(x=>Mathf.Abs(
            (x.MaxCombatHP<=0?1f:(float)x.CurrentHP/x.MaxCombatHP)-min)<.00001f)
            .ToArray();
        return tied[UnityEngine.Random.Range(0,tied.Length)];
    }
    public static bool HasHealerTarget(Character owner,BattleContext battle)
    {
        if(owner==null||battle?.Enemies==null)return false;
        return battle.Enemies.Any(x=>x!=null&&!x.IsDead&&x.MaxCombatHP>0&&
            x.CurrentHP*2<=x.MaxCombatHP);
    }
    public static bool HasSupplyTarget(Character owner,BattleContext battle) =>
        battle?.Enemies!=null&&battle.Enemies.Any(x=>x!=null&&
            x!=owner&&!x.IsDead);

    public static int ApplyHealerPreparation(BattleAction action)
    {
        if(!Eligible(action,EnemySpecies1029.Healer)||
           action.Skill?.Definition?.SkillId!=HealerHeal)return 0;
        var target=SelectLowestHpRatio(action.Owner,
            action.Owner.BattleContext,true);
        if(target==null||target.CurrentHP*2>target.MaxCombatHP)return 0;
        int hp=target.CurrentHP;
        target.RestoreCurrentHP(20);
        return Mathf.Max(0,target.CurrentHP-hp);
    }

    /// <summary>Supply +1 energy or +1 turn Duel power to lowest-light 2 allies.</summary>
    public static int ApplyCommanderSupply(BattleAction action)
    {
        if(!Eligible(action,EnemySpecies1029.Commander)||
           action.Skill?.Definition?.SkillId!=CommanderSupply)return 0;
        var list=action.Owner.BattleContext?.Enemies?
            .Where(x=>x!=null&&x!=action.Owner&&!x.IsDead)
            .OrderBy(x=>x.CurrentEnergy)
            .ThenBy(_=>UnityEngine.Random.value)
            .Take(2).ToArray();
        if(list==null)return 0;
        foreach(var target in list)
            if(target.CurrentEnergy<target.MaxEnergy)
                target.AddEnergy(1,CombatResourceChangeReason.SkillEffect,
                    action,action.Skill);
            else
                target.AddTurnClashPowerBonus(1);
        return list.Length;
    }

    public static void ScheduleHealerLightPenalty(Character source,Character target)
    {
        if(source?.BattleContext==null||target==null||
           target!=source.BattleContext.Player)return;
        var state=Penalties.GetOrCreateValue(source.BattleContext);
        int next=(source.BattleContext.Services?.TurnManager?.CurrentTurn??1)+1;
        if(state.NextTurn!=next)
        {
            state.NextTurn=next;state.HealerMinusOne=false;
            state.ShadeBlocksAll=false;state.ShadeSource=null;
        }
        state.HealerMinusOne=true; // shared global max 1, nonstacking
    }
    public static void ScheduleShade(Character source,Character target)
    {
        if(source?.BattleContext==null||target==null||
           target!=source.BattleContext.Player)return;
        var state=Penalties.GetOrCreateValue(source.BattleContext);
        int next=(source.BattleContext.Services?.TurnManager?.CurrentTurn??1)+1;
        if(state.NextTurn!=next)
        {
            state.NextTurn=next;state.HealerMinusOne=false;
            state.ShadeBlocksAll=false;
        }
        state.ShadeBlocksAll=true;state.ShadeSource=source;
    }

    /// <summary>
    /// Called ONCE per player TurnStart, BEFORE normal energy refill.
    /// Shade blocks all natural gain, Healer reduces it by one, neither
    /// drains current energy or suppresses other skill-generated energy.
    /// </summary>
    public static int AdjustNaturalTurnRefill(Character character,int baseGain)
    {
        int amount=Mathf.Max(0,baseGain);
        if(character==null||character.BattleContext?.Player!=character)
            return amount;
        if(!Penalties.TryGetValue(character.BattleContext,out var state))
            return amount;
        int turn=character.BattleContext.Services?.TurnManager?.CurrentTurn??1;
        if(state.NextTurn!=turn)return amount;
        if(state.ShadeBlocksAll)
        {
            var shade=new EnemyShadeStatus();
            character.BattleContext.EffectResolver?.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(
                    state.ShadeSource??character,character,shade));
            amount=0;
        }
        else if(state.HealerMinusOne)
            amount=Mathf.Max(0,amount-1);
        state.NextTurn=-1;state.HealerMinusOne=false;
        state.ShadeBlocksAll=false;state.ShadeSource=null;
        return amount;
    }

    public static (int turn,bool healer,bool shade) PendingForVerification(
        BattleContext battle)
    {
        if(battle==null||!Penalties.TryGetValue(battle,out var pending))
            return (-1,false,false);
        return (pending.NextTurn,pending.HealerMinusOne,pending.ShadeBlocksAll);
    }
}

/// <summary>Common 1029 속박 N/T: speed maximum -N, separate from Swift.</summary>
public sealed class EnemyBindStatus:
    NumericTimedStatus,ICommonSpeedMaximumStatus
{
    public EnemyBindStatus(int value=1,int duration=1)
        :base("속박",value,duration){}
    public int GetSpeedMaximumIncrease(BodyPart part)=>-Stack;
}

/// <summary>Presence status for this turn's no-natural-light effect.</summary>
public sealed class EnemyShadeStatus:StatusEffect
{
    public EnemyShadeStatus()
    {
        Name="차광";Stack=1;Duration=1;
    }
    public override StatusEffectStorageKind StorageKind=>
        StatusEffectStorageKind.PresenceTimed;
}

/// <summary>Commander aura kept as a distinct source-owned status instance.</summary>
public sealed class EnemyCommanderStrengthAura:
    NumericTimedStatus,ICommonRollShiftStatus
{
    private readonly int token;
    public EnemyCommanderStrengthAura(int ownerToken)
        :base("힘(지휘관)",1,StatusEffect.InfiniteDuration){token=ownerToken;}
    protected override string GetMergeKey()=>base.GetMergeKey()+":"+token;
    public int GetRollShift(BattleAction action)=>action!=null?Stack:0;
}
public sealed class EnemyCommanderSwiftAura:
    NumericTimedStatus,ICommonSpeedMaximumStatus
{
    private readonly int token;
    public EnemyCommanderSwiftAura(int ownerToken)
        :base("신속(지휘관)",1,StatusEffect.InfiniteDuration){token=ownerToken;}
    protected override string GetMergeKey()=>base.GetMergeKey()+":"+token;
    public int GetSpeedMaximumIncrease(BodyPart part)=>Stack;
}