using System;

namespace ProjectAbyss.Abilities
{
    internal sealed class SpecialistRuleSettings : IAbilityRuleSettings
    {
        private readonly Func<AbilityRuleCommitContext,bool> apply;
        internal SpecialistRuleSettings(Func<AbilityRuleCommitContext,bool> apply){this.apply=apply;}
        internal bool Commit(AbilityRuleCommitContext context)=>apply(context);
    }
    [UnityEngine.Scripting.Preserve]
[AbilityRuleModule("core.specialist-adapter")]
    public sealed class SpecialistRuleAdapter : TargetedAbilityRuleHandler,IAbilityRuleModule
    {
        public void Register(AbilityRuleRegistry registry)=>registry.RegisterOperation("compat.specialist",this);
        public override string Validate(AbilityOperationSpec operation)=>operation.Settings is SpecialistRuleSettings?null:"Internal compatibility adapter requires a bound specialist";
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext context,AbilityRuleCommand command)=>
            ((SpecialistRuleSettings)command.Operation.Settings).Commit(context)?Applied(0,"Specialist committed through canonical domain services"):
                CombatCommandReceipt.Reject("Specialist gate did not apply",CombatFailureReason.GateNotMet);
    }
    internal sealed class CapturedGateCondition : ICombatCondition
    {
        private readonly bool result;
        internal CapturedGateCondition(bool result){this.result=result;}
        public bool Evaluate(IReadOnlyAbilityContext context,CombatTargetRef candidate)=>result;
    }
}
