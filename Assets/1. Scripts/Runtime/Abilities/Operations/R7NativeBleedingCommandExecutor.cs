using System;
using System.Collections.Generic;

namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// First native Bleeding operation. Source/target/part are resolved by
    /// SkillEffectEntry before calling this command path.
    /// Apply uses the existing R5 specialist backend and BattleEffectResolver,
    /// never TempBalanceSkillProxyEffectDefinition.Apply.
    /// </summary>
    public sealed class R7NativeBleedingCommandExecutor
    {
        private sealed class FixedTarget : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public FixedTarget(Character owner,BodyPart part)
            {
                target=new CombatTargetRef(
                    R4LiveSlotQuery.ActorRuntimeId(owner),
                    part?.PartId??string.Empty,"");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext context)=>
                new[]{target};
        }
        private sealed class Match : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext c)=>true;
        }
        private sealed class StatusOperation : IEffectOperation
        {
            private readonly int stacks;
            private readonly bool part;
            public StatusOperation(int value,bool isPart)
            {
                stacks=value;part=isPart;
            }
            public void Plan(IReadOnlyAbilityContext context,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var target in targets)
                    if(target.ActorRuntimeId>0 && stacks>0)
                        sink.Enqueue(new CombatCommandIntent(
                            part?nameof(CombatCommandKind.ApplyPartStatus):
                                nameof(CombatCommandKind.ApplyCharacterStatus),
                            target,stacks));
            }
        }

        private readonly CombatCommandApplyService service=
            new CombatCommandApplyService();
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly Match matcher=new Match();
        private readonly BattleContext battle;
        public int Attempts {get;private set;}
        public int Committed {get;private set;}
        public R7NativeBleedingCommandExecutor(BattleContext battle)
        {
            this.battle=battle??throw new ArgumentNullException(nameof(battle));
        }

        public bool Apply(AbilityInstance ability,AbilityEventContext cause,
            SkillEffectContext context,SkillEffectOverrides overrides,
            TempBalanceSkillProxyEffectDefinition definition,int entryIndex)
        {
            if(ability==null||context?.Owner==null||context.Target==null||
                context.Action==null||context.Target.IsDead||
                cause.ActionId<=0||definition==null||
                definition.Operation!=TempBalanceSkillProxyOperation.TargetBleeding)
                return false;
            int stacks=Math.Max(0,overrides?.OverrideAmount==true
                ?overrides.Amount:definition.BaseAmount);
            if(stacks==0)return false;
            Character target=context.Target;
            BodyPart part=context.TargetPart;
            if(part!=null&&(part.Owner!=target||part.IsBroken))
                return false;
            var plan=runner.Plan(ability,cause,matcher,null,
                new FixedTarget(target,part),
                new IEffectOperation[]{new StatusOperation(stacks,part!=null)});
            if(plan.Commands.Count!=1||plan.Result.AppliedCommands!=0)return false;

            string commandId="R7-BLEED:"+ability.Definition.SourceAssetGuid+":"+
                cause.ActionId+":"+entryIndex+":"+cause.Kind+":"+
                cause.RollUseIndex+":"+cause.CauseId;
            var commands=R5PlannedCommandBridge.Materialize(
                ability,cause,plan,commandId,0);
            var command=commands[0];
            var backend=new R5CombatSpecialistBackend(battle);
            var effect=new Bleeding(stacks);
            if(part==null)
                backend.RegisterCharacterStatus(command,
                    EffectRequest.CharacterStatus(context.Owner,target,effect,
                        context.Action,context.RollIndex,context.Timing,true));
            else
                backend.RegisterPartStatus(command,
                    EffectRequest.BodyPartStatus(context.Owner,target,part,effect,
                        context.Action,context.RollIndex,context.Timing,true));

            Attempts++;
            var result=service.Apply(ability,cause,commands,backend);
            bool committed=result.CommittedCount==1 && !result.HasUnknownCommit;
            if(committed)Committed++;
            return committed;
        }
        public void Clear()=>service.ResetForBattle();
    }
}