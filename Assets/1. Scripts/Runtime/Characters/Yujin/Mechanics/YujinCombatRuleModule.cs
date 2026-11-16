using System;
using System.Collections.Generic;
using ProjectAbyss.Abilities;

[UnityEngine.Scripting.Preserve]
[AbilityRuleModule("character.yujin")]
public sealed class YujinCombatRuleModule : IAbilityRuleModule
{
    public void Register(AbilityRuleRegistry registry)
    {
        registry.RegisterOperation("yujin.mark",new Mark());
        registry.RegisterOperation("yujin.contagion_trap",new Contagion());
        registry.RegisterOperation("yujin.weapon.sharpen",new Sharpen());
        registry.RegisterOperation("yujin.growth",new Growth());
    }
    private sealed class Mark : TargetedAbilityRuleHandler
    {
        public override string Validate(AbilityOperationSpec op)=>op.Amount<=0?"MARK_AMOUNT_UNSET":null;
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
        {
            var mechanic=c.Source.GetMechanic<YujinMechanic>();
            if(mechanic==null)return CombatCommandReceipt.Reject("Mark mechanic unavailable",CombatFailureReason.UnsupportedOperation);
            var result=mechanic.ApplyMarkGain(c.Target,c.Part,command.Operation.Amount,c.Runtime.Action(c.Frame.ActionId),command.Cause);
            return result.Applied?Applied(result.ActualGain,"Mark applied"):CombatCommandReceipt.Reject("Mark target unavailable",CombatFailureReason.TargetUnavailable);
        }
    }
    private sealed class Contagion : TargetedAbilityRuleHandler
    {
        public override string Validate(AbilityOperationSpec op)=>op.Amount<=0?"CONTAGION_AMOUNT_UNSET":null;
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
        {
            var mechanic=c.Source.GetMechanic<YujinMechanic>();
            return mechanic?.RegisterContagionTrap(c.Target,c.Part,command.Operation.Amount,c.Runtime.Action(c.Frame.ActionId),command.Cause)>0?
                Applied(1,"Contagion trap installed"):CombatCommandReceipt.Reject("Contagion trap rejected",CombatFailureReason.TargetUnavailable);
        }
    }
    private sealed class Sharpen : TargetedAbilityRuleHandler
    {
        public override string Validate(AbilityOperationSpec op)=>op.Amount<=0?"SHARPEN_AMOUNT_UNSET":null;
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
        {
            var mechanic=c.Source.GetMechanic<YujinMechanic>();
            return mechanic?.SharpenWeapon(command.Operation.Amount,c.Frame.WeaponInstanceId)==true?
                Applied(command.Operation.Amount,"Weapon-owned next-turn mark bonus"):
                CombatCommandReceipt.Reject("Weapon unavailable",CombatFailureReason.GateNotMet);
        }
    }
    private sealed class Growth : TargetedAbilityRuleHandler
    {
        public override string Validate(AbilityOperationSpec op)=>op.Settings is YujinGrowthRuleSpec?null:"GROWTH_SETTINGS_REQUIRED";
        public override void Plan(AbilityRuleReadContext view,AbilityOperationSpec op,IReadOnlyList<CombatTargetRef> targets,IAbilityRuleCommandSink sink)
        {
            if(view.Frame.SnapshotPolicy!=AbilitySnapshotPolicy.LockAtActionStart)throw new InvalidOperationException("Growth rules require action-start snapshots");
            var config=(YujinGrowthRuleSpec)op.Settings;
            var original=view.Snapshot.OriginalTarget;
            var scope=string.IsNullOrEmpty(original.PartId)?ConditionScope.Actor:ConditionScope.Part;
            bool marked=view.Snapshot.TryGet(original,scope,ConditionFact.KeywordStack,"yujin.mark",out int amount)&&amount>0;
            var compiled=op.WithAmount(marked?config.MarkedAmount:config.UnmarkedAmount);
            base.Plan(view,compiled,targets,sink);
        }
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
        {
            var config=(YujinGrowthRuleSpec)command.Operation.Settings;
            var mechanic=c.Source.GetMechanic<YujinMechanic>();
            if(mechanic==null)return CombatCommandReceipt.Reject("Growth mechanic unavailable",CombatFailureReason.UnsupportedOperation);
            int stage=config.Stage>0?config.Stage:mechanic.Growth.Stage(config.Axis);
            var policy=config.Axis==YujinGrowthAxis.Execution?new YujinMarkGainPolicy(stage>=3,stage>=4,stage>=2?config.ReapplyAfterBurst:0):default;
            var gain=mechanic.ApplyGrowthMarkGain(c.Target,c.Part,command.Operation.Amount,c.Runtime.Action(c.Frame.ActionId),command.Cause,policy);
            return gain.Applied?Applied(gain.ActualGain,"Growth mark committed from locked start facts"):
                CombatCommandReceipt.Reject("Growth mark rejected",CombatFailureReason.TargetUnavailable);
        }
    }
}
