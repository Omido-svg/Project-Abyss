using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>Native-only status ability execution. Takes no SkillDefinition,
    /// SkillEffectEntry, TempBalance definition or legacy overrides.</summary>
    public sealed class R7NativeStatusAbilityEngine : IDisposable
    {
        private sealed class Match : IAbilityTriggerMatcher
        {
            private readonly AbilityEventKind trigger;
            public Match(AbilityEventKind trigger)=>this.trigger=trigger;
            public bool Matches(in AbilityEventContext c)=>c.Kind==trigger;
        }
        private sealed class Target : ICombatTargetSelector
        {
            private readonly CombatTargetRef single;
            public Target(Character c,BodyPart p)=>single=new CombatTargetRef(
                R4LiveSlotQuery.ActorRuntimeId(c),p?.PartId??string.Empty,"");
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _)=>
                new[]{single};
        }
        private sealed class StatusOp : IEffectOperation
        {
            private readonly int amount;
            private readonly bool part;
            public StatusOp(int amount,bool part){this.amount=amount;this.part=part;}
            public void Plan(IReadOnlyAbilityContext _,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var t in targets)
                    if(amount>0&&t.ActorRuntimeId>0)
                        sink.Enqueue(new CombatCommandIntent(
                            part?nameof(CombatCommandKind.ApplyPartStatus):
                                nameof(CombatCommandKind.ApplyCharacterStatus),
                            t,amount));
            }
        }

        private readonly BattleContext battle;
        private readonly AbilityRunner planner=new AbilityRunner();
        private readonly CombatCommandApplyService apply=new CombatCommandApplyService();
        private readonly Dictionary<(long,string),AbilityInstance> instances=
            new Dictionary<(long,string),AbilityInstance>();
        private bool disposed;
        public int Attempts {get;private set;}
        public int Commits {get;private set;}
        public R7NativeStatusAbilityEngine(BattleContext ctx)=>
            battle=ctx??throw new ArgumentNullException(nameof(ctx));

        public bool Execute(R7NativeStatusAbilityDefinition graph,
            BattleAction action,Character owner,Character target,BodyPart part,
            AbilityEventContext evt,int level)
        {
            if(disposed||graph==null||!graph.IsValid||action==null||
                action.ActionId<=0||owner==null||target==null||
                target.IsDead||graph.Trigger!=evt.Kind||
                (graph.RestrictToRoll&&evt.RollUseIndex+1!=graph.RollNumber))
                return false;
            int stacks=graph.StacksForLevel(level);
            if(stacks<=0||part!=null&&(part.Owner!=target||part.IsBroken))
                return false;
            var key=(action.ActionId,graph.StableId);
            if(!instances.TryGetValue(key,out var instance))
            {
                instance=new AbilityInstance(
                    new AbilityDefinitionRef(graph.StableId,graph.SourceAssetGuid),
                    new AbilityIdentity(graph.StableId,
                        R4LiveSlotQuery.ActorRuntimeId(owner),
                        action.Slot?.SlotId??string.Empty,0));
                instances.Add(key,instance);
            }
            if(instance.Identity.OwnerActorRuntimeId!=
                R4LiveSlotQuery.ActorRuntimeId(owner))
                throw new InvalidOperationException("Native status identity conflict");
            var plan=planner.Plan(instance,evt,new Match(graph.Trigger),null,
                new Target(target,part),
                new IEffectOperation[]{new StatusOp(stacks,part!=null)});
            if(plan.Commands.Count!=1||plan.Result.AppliedCommands!=0)
                return false;
            string id="NativeStatus:"+graph.SourceAssetGuid+":"+
                evt.ActionId+":"+evt.Kind+":"+evt.RollUseIndex+":"+evt.CauseId;
            var commands=R5PlannedCommandBridge.Materialize(
                instance,evt,plan,id,0);
            var status=new Bleeding(stacks);
            var request=part==null
                ?EffectRequest.CharacterStatus(owner,target,status,action,
                    evt.RollUseIndex,ToTiming(evt.Kind),true)
                :EffectRequest.BodyPartStatus(owner,target,part,status,action,
                    evt.RollUseIndex,ToTiming(evt.Kind),true);
            var backend=new R5CombatSpecialistBackend(battle);
            if(part==null)backend.RegisterCharacterStatus(commands[0],request);
            else backend.RegisterPartStatus(commands[0],request);
            Attempts++;
            var receipt=apply.Apply(instance,evt,commands,backend);
            bool success=receipt.CommittedCount==1&&!receipt.HasUnknownCommit;
            if(success)Commits++;
            return success;
        }
        private static SkillEffectTiming ToTiming(AbilityEventKind e)=>
            e==AbilityEventKind.ExchangeWon?SkillEffectTiming.OnExchangeWin:
            e==AbilityEventKind.RollUsing?SkillEffectTiming.OnExecute:
            SkillEffectTiming.OnExecute;
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;instances.Clear();apply.ResetForBattle();
        }
    }
}