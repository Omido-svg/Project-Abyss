using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    /// <summary>Native R5 damage for the two approved mixed prestige cards.
    /// Builds the same damage request as ApplyRolledPartDamageEffect but
    /// never calls its legacy Apply method.</summary>
    public sealed class R7NativeRolledDamageExecutor
    {
        private sealed class Match : IAbilityTriggerMatcher
        {
            public bool Matches(in AbilityEventContext _) => true;
        }
        private sealed class Target : ICombatTargetSelector
        {
            private readonly CombatTargetRef target;
            public Target(Character character,BodyPart part)
            {
                target=new CombatTargetRef(R4LiveSlotQuery.ActorRuntimeId(character),
                    part?.PartId??string.Empty,"");
            }
            public IReadOnlyList<CombatTargetRef> Select(IReadOnlyAbilityContext _) =>
                new[]{target};
        }
        private sealed class DamageOperation : IEffectOperation
        {
            private readonly int damage;
            public DamageOperation(int value)=>damage=value;
            public void Plan(IReadOnlyAbilityContext _,
                IReadOnlyList<CombatTargetRef> targets,ICombatCommandSink sink)
            {
                foreach(var target in targets)
                    if(damage>0&&target.ActorRuntimeId>0)
                        sink.Enqueue(new CombatCommandIntent(
                            nameof(CombatCommandKind.DealDamage),target,damage));
            }
        }

        private readonly BattleContext battle;
        private readonly AbilityRunner runner=new AbilityRunner();
        private readonly CombatCommandApplyService apply=new CombatCommandApplyService();
        private readonly Match matcher=new Match();
        public int Attempts {get;private set;}
        public int Commits {get;private set;}
        public R7NativeRolledDamageExecutor(BattleContext ctx)=>
            battle=ctx??throw new ArgumentNullException(nameof(ctx));

        public bool Execute(AbilityInstance instance,AbilityEventContext cause,
            SkillEffectContext context,SkillEffectOverrides overrides,
            ApplyRolledPartDamageEffect source,int index)
        {
            if(instance==null||context?.Action==null||
                context.Owner==null||context.Target==null||
                cause.ActionId<=0||source==null)
                return false;

            DamageRequest? damage=source.CreateNativeDamageRequest(context,overrides);
            if(damage==null)return false;
            var plan=runner.Plan(instance,cause,matcher,null,
                new Target(context.Target,context.TargetPart),
                new IEffectOperation[]{new DamageOperation(damage.Value.Damage)});
            if(plan.Commands.Count!=1||plan.Result.AppliedCommands!=0)
                return false;
            string commandId="R7-MIXED-DAMAGE:"+instance.Definition.SourceAssetGuid+
                ":"+cause.ActionId+":"+index+":"+cause.Kind+":"+cause.RollUseIndex;
            var commands=R5PlannedCommandBridge.Materialize(
                instance,cause,plan,commandId,0);
            var backend=new R5CombatSpecialistBackend(battle);
            backend.RegisterDamage(commands[0],damage.Value);
            Attempts++;
            var outcome=apply.Apply(instance,cause,commands,backend);
            bool committed=outcome.CommittedCount==1&&!outcome.HasUnknownCommit;
            if(committed)Commits++;
            return committed;
        }
        public void Clear()=>apply.ResetForBattle();
    }
}