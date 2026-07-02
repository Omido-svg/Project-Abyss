using UnityEngine;

public class OlafPreparationSkill : PreparationSkill
{
    public OlafPreparationSkill()
    {
        SkillName = "광기의 난도질(도사림)";
        BasePower = 0;
        Resolver = new DiceResolver(0, 0);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Owner == null)
            return;

        BodyPart sacrificePart =
            FindSacrificePart(action.Owner);

        if (sacrificePart == null)
        {
            Debug.LogWarning(
                $"{action.Owner.Data.CharacterName} 도사림 실패 : 파괴할 수 있는 부위가 없음");

            return;
        }

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 도사림 : {sacrificePart.Type} 부위 파괴");

        action.Owner.ForceBreakPart(sacrificePart);
    }

    private BodyPart FindSacrificePart(Character owner)
    {
        if (owner == null || owner.BodyParts == null)
            return null;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            if (part.Type == PartType.LEFT_HAND)
                return part;
        }

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            if (part.Type == PartType.RIGHT_HAND)
                return part;
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