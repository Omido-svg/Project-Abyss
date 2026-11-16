using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public interface IAbilityRuleSettings { }
    public interface IAbilityRuleSettingsDefinition { IAbilityRuleSettings CaptureSettings(); }
    public enum CombatModifierStage { RollPower, HpDamageDealt, HpDamageTaken, StaggerDamageTaken, EnergyGain, Healing, Speed, StaggerDamageDealt }
    public enum CombatModifierKind { ReplaceBase, AddFlat, OverrideFinal }
    public sealed class CombatModifierSpec : IAbilityRuleSettings
    {
        public readonly CombatModifierStage Stage;
        public readonly CombatModifierKind Kind;
        public readonly int Amount,Priority,DurationTurns;
        public CombatModifierSpec(CombatModifierStage stage,CombatModifierKind kind,int amount,int priority,int durationTurns)
        {
            if(!Enum.IsDefined(typeof(CombatModifierStage),stage)||!Enum.IsDefined(typeof(CombatModifierKind),kind)||durationTurns<0)
                throw new ArgumentException("Invalid typed modifier policy");
            Stage=stage;Kind=kind;Amount=amount;Priority=priority;DurationTurns=durationTurns;
        }
    }
    public sealed class CombatModifierRegistry
    {
        private sealed class Entry
        {public AbilityScopeKey Scope;public CombatModifierSpec Spec;public EffectCause Cause;public long Order;public int Expires;}
        private readonly List<Entry> entries=new();private long nextOrder;
        public int Count=>entries.Count;
        public void Add(AbilityScopeKey scope,CombatModifierSpec spec,EffectCause cause,int turn)
        {
            if(spec==null||scope.ActorId<=0)throw new ArgumentException("Modifier scope/configuration required");
            entries.Add(new Entry{Scope=scope,Spec=spec,Cause=cause,Order=++nextOrder,
                Expires=spec.DurationTurns==0?int.MaxValue:checked(turn+spec.DurationTurns)});
        }
        public int Apply(CombatModifierStage stage,Character actor,int value,BattleAction action=null,BodyPart part=null)
        {
            if(actor==null)return value;long id=LiveSlotQuery.ActorRuntimeId(actor);
            string weapon=actor.Mechanics?.OfType<IAbilityExecutionScopeProvider>().FirstOrDefault()?.WeaponInstanceId??"";
            bool Matches(Entry e)=>e.Scope.ActorId==id && e.Spec.Stage==stage && (e.Scope.Scope switch
            {
                AbilityOwnerScope.WeaponInstance=>e.Scope.InstanceId==weapon,
                AbilityOwnerScope.BodyPart=>e.Scope.InstanceId==(part??(action?.Owner==actor?action.OwnerPart:null))?.PartId,
                AbilityOwnerScope.ClashExecution=>e.Scope.ExecutionId==action?.ClashExecutionId,
                AbilityOwnerScope.SkillExecution=>e.Scope.ExecutionId==action?.ActionId,
                AbilityOwnerScope.CounterExecution=>e.Scope.ExecutionId==action?.CounterExecution?.ExecutionId,
                _=>true
            });
            var active=entries.Where(Matches).OrderBy(e=>e.Spec.Priority).ThenBy(e=>e.Cause.SourceAbilityId,StringComparer.Ordinal)
                .ThenBy(e=>e.Order).ToArray();
            // Explicit policy: replacement base, additive modifiers, final override; priority then stable registration.
            foreach(var e in active.Where(e=>e.Spec.Kind==CombatModifierKind.ReplaceBase))value=e.Spec.Amount;
            foreach(var e in active.Where(e=>e.Spec.Kind==CombatModifierKind.AddFlat))value=checked(value+e.Spec.Amount);
            foreach(var e in active.Where(e=>e.Spec.Kind==CombatModifierKind.OverrideFinal))value=e.Spec.Amount;
            return value;
        }
        public void BeginTurn(int turn)=>entries.RemoveAll(e=>e.Expires<=turn || e.Scope.Scope==AbilityOwnerScope.Turn&&e.Scope.ExecutionId!=turn);
        public void ReleaseExecution(long id,AbilityOwnerScope? scope=null)=>entries.RemoveAll(e=>(!scope.HasValue||e.Scope.Scope==scope.Value)&&(e.Scope.Scope==AbilityOwnerScope.SkillExecution||
            e.Scope.Scope==AbilityOwnerScope.CounterExecution||e.Scope.Scope==AbilityOwnerScope.ClashExecution)&&e.Scope.ExecutionId==id);
        public void Clear(){entries.Clear();nextOrder=0;}
    }
}
