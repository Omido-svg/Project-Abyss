using System.Collections.Generic;
using ProjectAbyss.Abilities;

namespace ProjectAbyss.Examples
{
    [UnityEngine.Scripting.Preserve]
    [AbilityRuleModule("example.engraved_contract")]
    public sealed class EngravedContractModule : IAbilityRuleModule
    {
        public const string MarkOperation="example.engraved_contract.mark";
        public void Register(AbilityRuleRegistry registry)=>registry.RegisterOperation(MarkOperation,new MarkHandler());

        private sealed class MarkHandler : TargetedAbilityRuleHandler
        {
            public override string Validate(AbilityOperationSpec operation)=>
                operation.Settings is EngravedContractSpec?null:"ENGRAVED_CONTRACT_SETTINGS_REQUIRED";

            public override void Plan(AbilityRuleReadContext view,AbilityOperationSpec operation,
                IReadOnlyList<CombatTargetRef> targets,IAbilityRuleCommandSink sink)
            {
                if(view.Frame.SnapshotPolicy!=AbilitySnapshotPolicy.LockAtActionStart)
                    throw new System.InvalidOperationException("This operation requires an action-start snapshot.");

                var settings=(EngravedContractSpec)operation.Settings;
                var original=view.Snapshot.OriginalTarget;
                var scope=string.IsNullOrEmpty(original.PartId)?ConditionScope.Actor:ConditionScope.Part;
                bool marked=view.Snapshot.TryGet(original,scope,ConditionFact.KeywordStack,"yujin.mark",out int mark)&&mark>0;

                // Copy the immutable operation with the chosen amount. Planning never modifies a Character.
                var planned=operation.WithAmount(marked?settings.Marked:settings.Unmarked);
                base.Plan(view,planned,targets,sink);
            }

            public override CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command)
            {
                var mechanic=context.Source.GetMechanic<YujinMechanic>();
                if(mechanic==null)return CombatCommandReceipt.Reject("Yujin mark service unavailable",CombatFailureReason.UnsupportedOperation);

                // Reuse the existing mark service's supported overflow policy; do not implement another 44-burst calculator.
                var result=mechanic.ApplyGrowthMarkGain(context.Target,context.Part,command.Operation.Amount,
                    context.Runtime.Action(context.Frame.ActionId),command.Cause,
                    new YujinMarkGainPolicy(preserveOverflow:true,repeatIgnition:false,reapplyAfterBurst:0));

                return result.Applied?Applied(result.ActualGain,"Example skill mark committed",
                    CombatAppliedValues.FromResource(result.PreviousMark,result.MarkAfter)):
                    CombatCommandReceipt.Reject("Mark not applied",CombatFailureReason.TargetUnavailable);
            }
        }
    }
}
