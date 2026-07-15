using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Break Own Part",
    fileName = "OlafBreakOwnPartEffect")]
public class OlafBreakOwnPartEffect : SkillEffectDefinition
{
    [FormerlySerializedAs("PriorityParts")]
    [SerializeField]
    private PartType[] priorityParts;

    [Tooltip("true면 마지막 남은 부위도 파괴할 수 있습니다.")]
    [SerializeField]
    private bool allowBreakLastPart;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Owner is not Olaf olaf ||
            context.Resolver == null)
        {
            return;
        }

        BodyPart part =
            FindPartToBreak(olaf);

        if (part == null)
        {
            Debug.LogWarning(
                $"{olaf.Data.CharacterName} 도사림 실패 : " +
                "조건을 만족하는 파괴 가능 부위 없음");
            return;
        }

        Debug.Log(
            $"{olaf.Data.CharacterName} 도사림 : " +
            $"{part.Type} 부위 파괴 / " +
            $"ActionId={context.Action?.ActionId ?? 0}");

        EffectRequest request =
            EffectRequest.ForceBreak(
                olaf,
                olaf,
                part);

        request.SourceAction =
            context.Action;

        context.Resolver.ForceBreakPart(
            request);
    }

    private BodyPart FindPartToBreak(
        Olaf olaf)
    {
        if (olaf?.BodyParts == null)
            return null;

        List<BodyPart> candidates = new();

        foreach (BodyPart part in olaf.BodyParts)
        {
            if (part != null &&
                !part.IsBroken)
            {
                candidates.Add(part);
            }
        }

        if (!allowBreakLastPart &&
            candidates.Count <= 1)
        {
            return null;
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

        return candidates.Count > 0
            ? candidates[0]
            : null;
    }
}
