using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectAbyss.Abilities
{
    public abstract class TargetedAbilityRuleHandler : IAbilityRuleHandler
    {
        public virtual void Plan(AbilityRuleReadContext context,AbilityOperationSpec operation,
            IReadOnlyList<CombatTargetRef> targets,IAbilityRuleCommandSink sink)
        {foreach(var target in targets)if(target.ActorRuntimeId>0)sink.Enqueue(new AbilityRuleCommand(operation,target,context.Frame.Cause));}
        public abstract CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command);
        public virtual string Validate(AbilityOperationSpec op)=>null;
        protected static CombatCommandReceipt Applied(int amount,string detail,CombatAppliedValues values=default)=>
            new(CombatCommandOutcome.Applied,Math.Max(0,amount),0,true,detail,values);
    }
    [UnityEngine.Scripting.Preserve]
[AbilityRuleModule("core.rules")]
    public sealed class BuiltinAbilityRules : IAbilityRuleModule
    {
        private sealed class BooleanCondition : ICombatCondition
        {
            private readonly bool value;public BooleanCondition(bool value){this.value=value;}
            public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef target)=>value;
        }
        private sealed class StateCondition : ICombatCondition
        {
            private readonly string key;private readonly long value;private readonly AbilityOwnerScope scope;private readonly FactComparison comparison;
            public StateCondition(AbilityPredicateData data){key=data.Key;value=data.Value;scope=data.StateScope;comparison=data.Comparison;}
            public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef target)=>
                context is IReadOnlyScopedAbilityContext view && view.State!=null && Compare(view.State.Get(scope,key));
            private bool Compare(long current)=>comparison switch{FactComparison.Present=>current!=0,FactComparison.AtMost=>current<=value,
                FactComparison.Equals=>current==value,FactComparison.AtLeast=>current>=value,_=>false};
        }
        private sealed class DirectCauseCondition : ICombatCondition
        {
            private readonly string ability;public DirectCauseCondition(string ability){this.ability=ability;}
            public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef target)=>
                context is IReadOnlyAbilityExecutionContext view && view.Frame.ObservedCause.IsDirectResultOf(view.Frame.ObservedParent) &&
                view.Frame.ObservedCause.SourceAbilityId==(string.IsNullOrEmpty(ability)?view.Ability.DefinitionId:ability);
        }
        public void Register(AbilityRuleRegistry registry)
        {
            registry.RegisterPredicate("always",d=>new BooleanCondition(true));
            registry.RegisterPredicate("fact",d=>new FactCondition(d.Subject,d.Scope,d.Fact,d.Key,d.Comparison,d.Value));
            registry.RegisterPredicate("state.at_least",d=>new StateCondition(d));
            registry.RegisterPredicate("cause.direct",d=>new DirectCauseCondition(d.Key));
            registry.RegisterPredicate("roll.succeeded",d=>new CombatEventFactCondition(CombatEventFact.RollSucceeded));
            registry.RegisterPredicate("roll.failed",d=>new CombatEventFactCondition(CombatEventFact.RollFailed));
            registry.RegisterPredicate("roll.critical",d=>new CombatEventFactCondition(CombatEventFact.CriticalRoll));
            registry.RegisterPredicate("damage.hp_at_least",d=>new CombatDamageResultCondition(DamageEvidenceTest.AppliedHpAtLeast,d.Value));
            registry.RegisterPredicate("damage.hp_zero",d=>new CombatDamageResultCondition(DamageEvidenceTest.HpZero));
            registry.RegisterPredicate("damage.stagger_at_least",d=>new CombatDamageResultCondition(DamageEvidenceTest.StaggerAtLeast,d.Value));
            registry.RegisterPredicate("damage.opened_window",d=>new CombatDamageResultCondition(DamageEvidenceTest.OpenedWindow));
            registry.RegisterPredicate("damage.weakened",d=>new CombatDamageResultCondition(DamageEvidenceTest.WeakenedPart));
            registry.RegisterPredicate("damage.broken",d=>new CombatDamageResultCondition(DamageEvidenceTest.BrokePart));
            registry.RegisterSelector("self",v=>new[]{new CombatTargetRef(v.Frame.Source.ActorRuntimeId,"","")});
            registry.RegisterSelector("source_part",v=>new[]{v.Frame.Source});
            registry.RegisterSelector("aimed",v=>new[]{v.Frame.AimedTarget});
            registry.RegisterSelector("actual",v=>new[]{v.Frame.ActualDamageTarget});
            registry.RegisterSelector("all_enemies",v=>v.Enemies);
            registry.RegisterSelector("all_allies",v=>v.Allies);
            registry.RegisterSelector("enemy_parts",v=>v.EnemyParts);
            registry.RegisterOperation("resource.energy",new Energy());
            registry.RegisterOperation("resource.block",new Block());
            registry.RegisterOperation("resource.custom",new CustomResource());
            registry.RegisterOperation("status.add",new Status());
            registry.RegisterOperation("damage.fixed",new Damage());
            registry.RegisterOperation("state.add",new StateAdd());
            registry.RegisterOperation("state.set",new StateSet());
            registry.RegisterOperation("modifier.add",new AddModifier());
        }
        private sealed class Energy : TargetedAbilityRuleHandler
        {
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                int before=c.Target.CurrentEnergy;c.Target.AddEnergy(command.Operation.Amount,CombatResourceChangeReason.SkillEffect);
                int after=c.Target.CurrentEnergy;return after==before?CombatCommandReceipt.Reject("Energy unchanged",CombatFailureReason.GateNotMet):
                    Applied(Math.Abs(after-before),"Energy",CombatAppliedValues.FromResource(before,after));
            }
        }
        private sealed class Block : TargetedAbilityRuleHandler
        {
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                int before=c.Target.RuntimeStatus.currentBlock;c.Target.AddBlock(command.Operation.Amount);
                int after=c.Target.RuntimeStatus.currentBlock;return after==before?CombatCommandReceipt.Reject("Block unchanged",CombatFailureReason.GateNotMet):
                    Applied(after-before,"Block",CombatAppliedValues.FromResource(before,after));
            }
        }
        private sealed class CustomResource : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec op)=>string.IsNullOrWhiteSpace(op.Key)?"RESOURCE_KEY_REQUIRED":null;
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                int before=SkillResourceAccess.Get(c.Target,command.Operation.Key);
                int after=SkillResourceAccess.Modify(c.Target,command.Operation.Key,command.Operation.Amount);
                return before==after?CombatCommandReceipt.Reject("Resource unchanged",CombatFailureReason.GateNotMet):
                    Applied(Math.Abs(after-before),"Resource",CombatAppliedValues.FromResource(before,after));
            }
        }
        private sealed class Status : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec op)=>!Enum.TryParse<StatusEffectId>(op.Key,out _)||op.Amount<=0||op.Duration==0?"INVALID_STATUS_SPEC":null;
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                var op=command.Operation;var status=StatusEffectFactory.CreateCanonical0922(Enum.Parse<StatusEffectId>(op.Key),op.Amount,op.Duration);
                var request=c.Part==null?EffectRequest.CharacterStatus(c.Source,c.Target,status,null,-1,SkillEffectTiming.OnExecute,true):
                    EffectRequest.BodyPartStatus(c.Source,c.Target,c.Part,status,null,-1,SkillEffectTiming.OnExecute,true);
                bool applied=c.Part==null?c.Battle.EffectResolver.ApplyCharacterStatus(request):c.Battle.EffectResolver.ApplyBodyPartStatus(request);
                return applied?Applied(op.Amount,"Status"):CombatCommandReceipt.Reject("Status rejected",CombatFailureReason.GateNotMet);
            }
        }
        private sealed class Damage : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec op)=>op.Amount<=0?"DAMAGE_AMOUNT_UNSET":null;
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                var request=DamageRequest.Custom(c.Part==null?DamageType.Direct:DamageType.SkillPart,c.Source,c.Target,c.Part,
                    command.Operation.Amount,1f,canBreakPart:false,applyMomentum:false,applyGuard:true);
                request.SourceAction=c.Runtime.Action(c.Frame.ActionId);
                request.SourceCause=command.Cause;
                var result=c.Battle.ResolveDamageManager()?.ApplyDamageContext(request);
                if(result?.Result==null)return CombatCommandReceipt.Reject("Damage rejected",CombatFailureReason.TargetUnavailable);
                var values=CombatAppliedValues.FromDamage(result);
                return new CombatCommandReceipt(values.HitLanded?result.AppliedHpDamage==0&&result.GuardAbsorbed>0?
                    CombatCommandOutcome.Absorbed:CombatCommandOutcome.Applied:CombatCommandOutcome.Rejected,
                    result.AppliedDamage,result.GuardAbsorbed,values.HitLanded,"Damage",values);
            }
        }
        private sealed class StateAdd : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec op)=>string.IsNullOrWhiteSpace(op.Key)?"STATE_KEY_REQUIRED":null;
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                var op=command.Operation;long before=c.Runtime.GetState(c.Frame,op.StateScope,op.Key);
                long after=checked(before+op.Amount);c.Runtime.SetState(c.Frame,op.StateScope,op.Key,after);
                return Applied((int)Math.Min(int.MaxValue,Math.Abs((long)op.Amount)),"State "+before+" -> "+after);
            }
        }
        private sealed class StateSet : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec op)=>string.IsNullOrWhiteSpace(op.Key)?"STATE_KEY_REQUIRED":null;
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {c.Runtime.SetState(c.Frame,command.Operation.StateScope,command.Operation.Key,command.Operation.Amount);return Applied(0,"State set");}
        }
        private sealed class AddModifier : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec op)=>op.Settings is not CombatModifierSpec?"MODIFIER_SETTINGS_REQUIRED":null;
            public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
            {
                var spec=(CombatModifierSpec)command.Operation.Settings;
                c.Runtime.Modifiers.Add(c.Runtime.ScopeForTarget(c.Frame,command.Operation.StateScope,command.Target),spec,c.Frame.Cause,c.Runtime.CurrentTurn);
                return Applied(Math.Abs(spec.Amount),"Modifier registered");
            }
        }
    }
}
