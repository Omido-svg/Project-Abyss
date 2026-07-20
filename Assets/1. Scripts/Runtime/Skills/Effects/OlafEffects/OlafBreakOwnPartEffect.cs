
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Weaken Own Part And Gain Madness",
    fileName = "OlafWeakenOwnPartEffect")]
public class OlafBreakOwnPartEffect : SkillEffectDefinition
{
    [FormerlySerializedAs("PriorityParts")]
    [SerializeField]
    private PartType[] priorityParts;

    [Tooltip("강한 도사림으로 얻는 광기. 부위 파괴가 아니라 약화이므로 별도 지급합니다.")]
    [SerializeField, Min(0)]
    private int madnessGain = 1;

    [Tooltip("약화된 다리 슬롯에서 사용했을 때 광기 획득량 감소")]
    [SerializeField, Min(0)]
    private int weakenedLegMadnessPenalty = 1;

    public override void Apply(SkillEffectContext context)
    {
        if (context?.Owner is not Olaf olaf)
            return;

        BodyPart part = FindPartToWeaken(olaf);
        if (part == null)
        {
            Debug.LogWarning(
                $"{olaf.Data?.CharacterName} 강한 도사림 실패 : " +
                "정상 상태의 약화 가능 부위가 없습니다.");
            return;
        }

        olaf.WeakenPart(
            part,
            olaf,
            context.Action);

        int effectiveMadness =
            PreparationEffectUtility.ApplyPositiveFlatPenalty(
                context,
                madnessGain,
                weakenedLegMadnessPenalty);

        if (effectiveMadness > 0)
            olaf.MadnessMechanic?.AddMadness(effectiveMadness);

        Debug.Log(
            $"{olaf.Data?.CharacterName} 강한 도사림 / " +
            $"{part.Type} 약화, 광기 +{effectiveMadness}, " +
            $"WeakenedLegSource=" +
            $"{PreparationEffectUtility.IsWeakenedLegSource(context)}");
    }

    private BodyPart FindPartToWeaken(Olaf olaf)
    {
        if (olaf?.BodyParts == null)
            return null;

        List<BodyPart> candidates = new();
        foreach (BodyPart part in olaf.BodyParts)
        {
            if (part != null &&
                !part.IsBroken &&
                !part.IsWeakened)
            {
                candidates.Add(part);
            }
        }

        if (priorityParts != null)
        {
            foreach (PartType type in priorityParts)
            {
                foreach (BodyPart part in candidates)
                {
                    if (part.Type == type)
                        return part;
                }
            }
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }
}

// 새 코드에서 의미를 명확히 사용할 수 있는 별칭.
public sealed class OlafWeakenOwnPartEffect : OlafBreakOwnPartEffect
{
}
