using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Break Own Part",
    fileName = "OlafBreakOwnPartEffect")]
public class OlafBreakOwnPartEffect : SkillEffectDefinition
{
    public PartType[] PriorityParts;

    public override void Apply(SkillEffectContext context)
    {
        if (context == null)
            return;

        if (context.Owner == null)
            return;

        if (context.Resolver == null)
            return;

        BodyPart part =
            FindPartToBreak(context.Owner);

        if (part == null)
        {
            Debug.LogWarning(
                $"{context.Owner.Data.CharacterName} 도사림 실패 : 파괴 가능한 부위 없음");

            return;
        }

        Debug.Log(
            $"{context.Owner.Data.CharacterName} 도사림 : {part.Type} 부위 파괴");

        context.Resolver.ForceBreakPart(
            EffectRequest.ForceBreak(
                context.Owner,
                context.Owner,
                part));
    }

    private BodyPart FindPartToBreak(Character owner)
    {
        if (owner == null)
            return null;

        if (owner.BodyParts == null)
            return null;

        if (PriorityParts != null)
        {
            foreach (PartType type in PriorityParts)
            {
                foreach (BodyPart part in owner.BodyParts)
                {
                    if (part == null)
                        continue;

                    if (part.IsBroken)
                        continue;

                    if (part.Type == type)
                        return part;
                }
            }
        }

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (!part.IsBroken)
                return part;
        }

        return null;
    }
}