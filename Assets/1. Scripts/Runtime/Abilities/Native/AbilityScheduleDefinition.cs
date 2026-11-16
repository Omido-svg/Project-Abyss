using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.Abilities
{
    public sealed class AbilityScheduleSpec : IAbilityRuleSettings
    {
        public readonly AbilityRuleSetDefinition Definition;
        public readonly string RuleId;
        public readonly AbilitySnapshotPolicy Snapshot;
        public AbilityScheduleSpec(AbilityRuleSetDefinition definition,string ruleId)
        {if(definition==null||string.IsNullOrWhiteSpace(ruleId))throw new ArgumentException("Scheduled definition/rule required");Definition=definition;RuleId=ruleId;
            var rule=System.Linq.Enumerable.SingleOrDefault(definition.Rules??Array.Empty<AbilityRuleData>(),r=>r!=null&&r.RuleId==ruleId);
            if(rule?.Policy==null)throw new ArgumentException("Scheduled rule policy missing");Snapshot=rule.Policy.Snapshot;}
    }
    [CreateAssetMenu(menuName="Project Abyss/Abilities/Scheduled Rule",fileName="ScheduledRule")]
    public sealed class AbilityScheduleDefinition : ScriptableObject,IAbilityRuleSettingsDefinition
    {
        public AbilityRuleSetDefinition Definition;
        public string RuleId;
        public IAbilityRuleSettings CaptureSettings()=>new AbilityScheduleSpec(Definition,RuleId);
    }
    [UnityEngine.Scripting.Preserve]
[AbilityRuleModule("core.scheduling")]
    public sealed class ScheduledAbilityRules : TargetedAbilityRuleHandler,IAbilityRuleModule
    {
        public void Register(AbilityRuleRegistry registry)=>registry.RegisterOperation("schedule.next_turn",this);
        public override string Validate(AbilityOperationSpec op)
        {
            if(op.Settings is not AbilityScheduleSpec spec)return "SCHEDULE_SETTINGS_REQUIRED";
            var rules=spec.Definition.Rules??Array.Empty<AbilityRuleData>();
            var matches=System.Linq.Enumerable.Where(rules,r=>r!=null&&r.RuleId==spec.RuleId);
            if(System.Linq.Enumerable.Count(matches)!=1)return "SCHEDULE_RULE_MISSING_OR_DUPLICATE";
            return System.Linq.Enumerable.Single(matches).ScheduledOnly?null:"SCHEDULED_ONLY_REQUIRED";
        }
        public override void Plan(AbilityRuleReadContext view,AbilityOperationSpec op,IReadOnlyList<CombatTargetRef> targets,IAbilityRuleCommandSink sink)
        {
            var spec=(AbilityScheduleSpec)op.Settings;
            if(spec.Snapshot==AbilitySnapshotPolicy.AfterCommittedDamage&&view.Frame.DamageResolutionId<=0)
                    throw new InvalidOperationException("Historical damage scheduling requires committed damage evidence");
            base.Plan(view,op,targets,sink);
        }
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command)
        {
            var spec=(AbilityScheduleSpec)command.Operation.Settings;
            context.Runtime.Schedule(spec.Definition,spec.RuleId,context.Frame,command.Target);
            return new CombatCommandReceipt(CombatCommandOutcome.Scheduled,0,0,false,"Target-locked rule scheduled for next turn");
        }
    }
}
