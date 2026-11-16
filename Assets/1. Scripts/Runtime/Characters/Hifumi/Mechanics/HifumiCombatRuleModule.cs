using ProjectAbyss.Abilities;

[UnityEngine.Scripting.Preserve]
[AbilityRuleModule("character.hifumi")]
public sealed class HifumiCombatRuleModule : IAbilityRuleModule
{
    public void Register(AbilityRuleRegistry registry)=>registry.RegisterOperation("hifumi.bone.grant",new Bone());
    private sealed class Bone : TargetedAbilityRuleHandler
    {
        public override CombatCommandReceipt Commit(AbilityRuleCommitContext c,AbilityRuleCommand command)
        {
            var mechanic=c.Target.GetMechanic<HifumiMechanic>();
            if(mechanic==null)return CombatCommandReceipt.Reject("Bone mechanic unavailable",CombatFailureReason.UnsupportedOperation);
            var result=mechanic.GrantBone(command.Operation.Amount,BoneGrantReason.DirectCard,command.Cause);
            return result.Applied>0?Applied(result.Applied,"Direct bone grant",CombatAppliedValues.FromResource(result.Before,result.After)):
                CombatCommandReceipt.Reject("Bone unchanged",CombatFailureReason.GateNotMet);
        }
    }
}
