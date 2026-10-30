using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Typed native command for legacy-authored AddBodyPartStatusEffect
    /// with Immediate/Legacy/Bleeding only. It NEVER calls
    /// AddBodyPartStatusEffect.Apply; original Entry/Definition predicates
    /// run first, and status mutation is delegated to the R5 backend.
    /// </summary>
    public sealed class R7NativeLegacyStatusExecutor
    {
        private sealed class Match : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext c) => true;
        }
        private sealed class SingleTarget : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public SingleTarget(Character owner,BodyPart part)
            {
                target=new CombatTargetRef(
                    R4LiveSlotQuery.ActorRuntimeId(owner),
                    part?.PartId??string.Empty,"");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                new[]{target};
        }
        private sealed class NativeStatusOperation : IEffectOperation
        {
            private readonly int stacks;
            private readonly bool part;
            public NativeStatusOperation(int stacks,bool part)
            {
                this.stacks=stacks;this.part=part;
            }
            public void Plan(IReadOnlyAbilityContext _,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var target in targets)
                    if(stacks>0 && target.ActorRuntimeId>0)
                        sink.Enqueue(new CombatCommandIntent(
                            part?nameof(CombatCommandKind.ApplyPartStatus):
                            nameof(CombatCommandKind.ApplyCharacterStatus),
                            target,stacks));
            }
        }
        private readonly BattleContext battle;
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly Match matcher=new Match();
        private readonly CombatCommandApplyService apply=new CombatCommandApplyService();
        public int Attempts {get;private set;}
        public int Commits {get;private set;}

        public R7NativeLegacyStatusExecutor(BattleContext battle)
        {
            this.battle=battle??throw new ArgumentNullException(nameof(battle));
        }
        public static bool CanConvert(AddBodyPartStatusEffect source) =>
            source!=null && source.StatusEffectId==StatusEffectId.Bleeding &&
            source.AuthoringSchema==StatusEffectAuthoringSchema.Legacy &&
            source.ApplicationTiming==StatusApplicationTiming.Immediate;

        public bool Execute(AbilityInstance ability,AbilityEventContext cause,
            SkillEffectContext context,SkillEffectOverrides overrides,
            AddBodyPartStatusEffect authoring,int index)
        {
            if(!CanConvert(authoring)||ability==null||context?.Resolver==null||
                context.Action==null||cause.ActionId<=0||
                context.Owner==null||context.Target==null||
                context.Target.IsDead)
                return false;

            var anchor=CombatStatusAnchor.Resolve(context.Target,context.TargetPart);
            if(!anchor.IsValid || anchor.IsPartAnchor && anchor.Part.IsBroken)
                return false;
            int count=authoring.ResolveAuthoredValue(overrides);
            int duration=authoring.ResolveAuthoredDuration(overrides);
            var status=StatusEffectFactory.CreateForAuthoring(
                authoring.AuthoringSchema,authoring.StatusEffectId,
                count,duration,authoring.RegenerationHealAmount,
                authoring.RegenerationChannel);
            if(status==null)return false;
            Character target=anchor.Character;
            BodyPart part=anchor.IsPartAnchor?anchor.Part:null;

            var planned=runner.Plan(ability,cause,matcher,null,
                new SingleTarget(target,part),
                new IEffectOperation[]{new NativeStatusOperation(count,part!=null)});
            if(planned.Commands.Count!=1 ||
                planned.Result.AppliedCommands!=0)return false;
            string commandId="R7-LEGACY-STATUS:"+
                ability.Definition.SourceAssetGuid+":"+
                cause.ActionId+":"+index+":"+cause.Kind+":"+
                cause.RollUseIndex+":"+cause.CauseId;
            var cmds=R5PlannedCommandBridge.Materialize(
                ability,cause,planned,commandId,0);
            var request=EffectRequest.BodyPartStatus(context.Owner,target,part,
                status,context.Action,context.RollIndex,context.Timing,true);
            var backend=new R5CombatSpecialistBackend(battle);
            if(part==null)
                backend.RegisterCharacterStatus(cmds[0],request);
            else
                backend.RegisterPartStatus(cmds[0],request);
            Attempts++;
            var outcome=apply.Apply(ability,cause,cmds,backend);
            bool committed=outcome.CommittedCount==1&&!outcome.HasUnknownCommit;
            if(committed)Commits++;
            return committed;
        }
        public void Clear()=>apply.ResetForBattle();
    }
}