using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Break Own Part",
    fileName = "OlafBreakOwnPartEffect")]
public class OlafBreakOwnPartEffect : SkillEffectDefinition
{
    public PartType[] PriorityParts;

    public override void Apply(
        SkillEffectContext context)
    {
        if (context?.Owner == null ||
            context.Resolver == null)
        {
            return;
        }

        BodyPart part =
            FindPartToBreak(context.Owner);

        if (part == null)
        {
            Debug.LogWarning(
                $"{context.Owner.Data.CharacterName} 도사림 실패 : " +
                "파괴 가능한 부위 없음");
            return;
        }

        Debug.Log(
            $"{context.Owner.Data.CharacterName} 도사림 : " +
            $"{part.Type} 부위 파괴");

        EffectRequest request =
            EffectRequest.ForceBreak(
                context.Owner,
                context.Owner,
                part);

        request.SourceAction = context.Action;

        context.Resolver.ForceBreakPart(request);
    }

    private BodyPart FindPartToBreak(
        Character character)
    {
        if (character?.BodyParts == null)
            return null;

        if (PriorityParts != null)
        {
            foreach (PartType type in PriorityParts)
            {
                foreach (BodyPart part in character.BodyParts)
                {
                    if (part != null &&
                        !part.IsBroken &&
                        part.Type == type)
                    {
                        return part;
                    }
                }
            }
        }

        foreach (BodyPart part in character.BodyParts)
        {
            if (part != null && !part.IsBroken)
                return part;
        }

        return null;
    }
}
