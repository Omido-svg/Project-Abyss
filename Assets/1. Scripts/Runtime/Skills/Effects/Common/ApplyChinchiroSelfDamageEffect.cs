using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Apply Chinchiro Self Damage",
    fileName = "ApplyChinchiroSelfDamageEffect")]
public sealed class ApplyChinchiroSelfDamageEffect :
    SkillEffectDefinition
{
    public override void Apply(
        SkillEffectContext context)
    {
        BattleAction action = context?.Action;
        RollResult roll = action?.LastRollResult;

        if (action?.Owner == null ||
            roll == null ||
            roll.ResolverType !=
                SkillResolverType.Chinchiro ||
            roll.ChinchiroCombination !=
                ChinchiroCombination.Hifumi ||
            roll.ChinchiroSelfDamage <= 0)
        {
            return;
        }

        context.ApplyDamage(
            DamageRequest.SelfCost(
                action.Owner,
                roll.ChinchiroSelfDamage));
    }
}
