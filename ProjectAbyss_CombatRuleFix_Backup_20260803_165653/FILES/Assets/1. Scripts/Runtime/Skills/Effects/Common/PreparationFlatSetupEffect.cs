
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Preparation Flat Setup",
    fileName = "PreparationFlatSetupEffect")]
public sealed class PreparationFlatSetupEffect : SkillEffectDefinition
{
    [Header("Weak Preparation Common Effect")]
    [Min(0)] public int BlockGain = 3;
    [Min(0)] public int TurnClashPowerBonus = 1;

    [Tooltip("약화된 다리 슬롯에서 사용했을 때 각 positive flat 효과에서 차감할 값")]
    [Min(0)]
    public int WeakenedLegPenalty = 1;

    public bool GiveToSelectedTarget;

    public override void Apply(SkillEffectContext context)
    {
        Character target = GiveToSelectedTarget
            ? context?.Target
            : context?.Owner;

        if (target == null)
            return;

        int effectiveBlock =
            PreparationEffectUtility.ApplyPositiveFlatPenalty(
                context,
                BlockGain,
                WeakenedLegPenalty);

        int effectiveClashBonus =
            PreparationEffectUtility.ApplyPositiveFlatPenalty(
                context,
                TurnClashPowerBonus,
                WeakenedLegPenalty);

        if (effectiveBlock > 0)
            target.AddBlock(effectiveBlock);

        if (effectiveClashBonus > 0)
            target.AddTurnClashPowerBonus(effectiveClashBonus);

        Debug.Log(
            $"{target.Data?.CharacterName ?? target.name} 도사림 셋업 / " +
            $"방어도 +{effectiveBlock}, " +
            $"이번 턴 합 +{effectiveClashBonus}, " +
            $"WeakenedLegSource=" +
            $"{PreparationEffectUtility.IsWeakenedLegSource(context)}");
    }
}
