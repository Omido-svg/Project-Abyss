using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Native R7 implementation for the seven mixed/bleeding cards.
    /// Never evaluates SkillEffectEntry, SkillEffectDefinition or overrides.
    /// Old SkillEffectContext is an outer call boundary only.
    /// </summary>
    public sealed class R7NativeMixedAbilityEngine : IDisposable
    {
        private sealed class TriggerMatch : IAbilityTriggerMatcher
        {
            private readonly AbilityEventKind trigger;
            public TriggerMatch(AbilityEventKind trigger) { this.trigger=trigger; }
            public bool Matches(in AbilityEventContext e) => e.Kind==trigger;
        }
        private sealed class OneTarget : ICombatTargetSelector
        {
            private readonly CombatTargetRef reference;
            public OneTarget(Character target,BodyPart part)
            {
                reference=new CombatTargetRef(
                    R4LiveSlotQuery.ActorRuntimeId(target),
                    part?.PartId??string.Empty, "");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                new[]{reference};
        }
        private sealed class OneCommand : IEffectOperation
        {
            private readonly string kind;
            private readonly int amount;
            public OneCommand(string kind,int amount)
            {
                this.kind=kind;this.amount=amount;
            }
            public void Plan(IReadOnlyAbilityContext _,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var t in targets)
                    if(amount>0 && t.ActorRuntimeId>0)
                        sink.Enqueue(new CombatCommandIntent(kind,t,amount));
            }
        }
        private readonly BattleContext battle;
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly CombatCommandApplyService apply=new CombatCommandApplyService();
        private readonly Dictionary<(long,string,int),AbilityInstance> instances=
            new Dictionary<(long,string,int),AbilityInstance>();
        private bool disposed;
        public int StatusAttempts {get;private set;}
        public int StatusCommits {get;private set;}
        public int DamageAttempts {get;private set;}
        public int DamageCommits {get;private set;}

        public R7NativeMixedAbilityEngine(BattleContext battle)
        {
            this.battle=battle??throw new ArgumentNullException(nameof(battle));
        }

        public bool Execute(R7NativeMixedAbilityDefinition graph,int index,
            SkillEffectContext context)
        {
            if(disposed||graph==null||!graph.IsValid||
                graph.SchemaVersion!=2 || index<0 || index>=graph.Operations.Length ||
                context?.Action==null||context.Owner==null||context.Target==null||
                context.Resolver==null||context.Action.ActionId<=0)
                return false;
            var op=graph.Operations[index];
            if(op==null||op.Trigger!=AbilityEventKind.RollUsing||
                context.Timing!=SkillEffectTiming.OnExecute)
                return false;
            long actionId=context.Action.ActionId;
            long owner=R4LiveSlotQuery.ActorRuntimeId(context.Owner);
            var key=(actionId,graph.StableId,index);
            if(!instances.TryGetValue(key,out var instance))
            {
                instance=new AbilityInstance(
                    new AbilityDefinitionRef(graph.StableId,graph.SourceAssetGuid),
                    new AbilityIdentity(graph.StableId,owner,
                        context.Action.Slot?.SlotId??string.Empty,index));
                instances.Add(key,instance);
            }
            if(instance.Identity.OwnerActorRuntimeId!=owner)
                throw new InvalidOperationException("Native mixed action/actor identity conflict");
            var evt=new AbilityEventContext(op.Trigger,actionId,actionId,owner,
                context.RollIndex,context.Action.DuelEffectsEligible,context.IsOneSided);
            var backend=new R5CombatSpecialistBackend(battle);
            string kind;
            int amount;
            Character target=context.Target;
            BodyPart part=context.TargetPart;
            if(op.Kind==R7MixedOperationKind.ApplyBleeding)
            {
                if(target.IsDead)return false;
                var anchor=CombatStatusAnchor.Resolve(target,part);
                if(!anchor.IsValid || (anchor.IsPartAnchor && anchor.Part.IsBroken))
                    return false;
                target=anchor.Character;
                part=anchor.IsPartAnchor?anchor.Part:null;
                amount=op.StackAmount;
                kind=part==null
                    ?nameof(CombatCommandKind.ApplyCharacterStatus)
                    :nameof(CombatCommandKind.ApplyPartStatus);
            }
            else if(op.Kind==R7MixedOperationKind.RolledPartDamage)
            {
                if(!context.Action.HasRolled)context.Action.RollPower();
                int power=context.Action.GetDamagePower();
                amount=Mathf.Max(0,Mathf.RoundToInt(power*op.DamageMultiplier)+
                    op.DamageFlatBonus);
                if(amount<=0)return false;
                kind=nameof(CombatCommandKind.DealDamage);
            }
            else return false;

            var plan=runner.Plan(instance,evt,new TriggerMatch(op.Trigger),null,
                new OneTarget(target,part),
                new IEffectOperation[]{new OneCommand(kind,amount)});
            if(plan.Commands.Count!=1 || plan.Result.AppliedCommands!=0)
                return false;
            string id="R7-NATIVE-MIXED:"+graph.SourceAssetGuid+":"+actionId+
                ":"+index+":"+op.Trigger+":"+evt.RollUseIndex+":"+evt.CauseId;
            var commands=R5PlannedCommandBridge.Materialize(instance,evt,plan,id,0);
            if(op.Kind==R7MixedOperationKind.ApplyBleeding)
            {
                var status=new Bleeding(amount,op.Duration);
                var request=part==null
                    ?EffectRequest.CharacterStatus(context.Owner,target,status,
                        context.Action,context.RollIndex,context.Timing,true)
                    :EffectRequest.BodyPartStatus(context.Owner,target,part,status,
                        context.Action,context.RollIndex,context.Timing,true);
                if(part==null)backend.RegisterCharacterStatus(commands[0],request);
                else backend.RegisterPartStatus(commands[0],request);
                StatusAttempts++;
            }
            else
            {
                var type=part==null && op.DamageType==DamageType.SkillPart
                    ?DamageType.Direct:op.DamageType;
                var request=DamageRequest.Custom(type,context.Owner,target,part,
                    amount,1f,
                    op.UseSkillCanBreakPart?graph.SourceCanBreakPart:op.CanBreakPart,
                    op.ApplyMomentum,op.ApplyGuard,context.Action);
                backend.RegisterDamage(commands[0],request);
                DamageAttempts++;
            }
            var result=apply.Apply(instance,evt,commands,backend);
            bool success=result.CommittedCount==1 && !result.HasUnknownCommit;
            if(success)
            {
                if(op.Kind==R7MixedOperationKind.ApplyBleeding)StatusCommits++;
                else DamageCommits++;
            }
            return success;
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;instances.Clear();apply.ResetForBattle();
        }
    }
}