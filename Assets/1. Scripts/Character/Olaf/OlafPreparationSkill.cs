using UnityEngine;

public class OlafPreparationSkill : PreparationSkill
{
    public OlafPreparationSkill()
    {
        SkillName = "광기의 난도질(도사림)";

        BasePower = 0;

        Resolver = new CoinResolver(0);
    }

    public override void Execute(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Owner == null)
            return;

        BodyPart lowestPart =
            FindLowestHpUsablePart(
                action.Owner);

        if (lowestPart == null)
        {
            Debug.Log("도사림 실패 : 파괴할 수 있는 부위가 없습니다.");
            return;
        }

        Debug.Log(
            $"{action.Owner.Data.CharacterName} 도사림 : " +
            $"{lowestPart.Type} 부위 파괴");

        action.Owner.ForceBreakPart(
            lowestPart);
    }

    private BodyPart FindLowestHpUsablePart(
        Character owner)
    {
        if (owner == null)
            return null;

        BodyPart result = null;

        int aliveCount = 0;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            aliveCount++;
        }

        // 마지막 1부위만 남았을 때는 도사림으로 자살하지 않게 막음
        if (aliveCount <= 1)
            return null;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (part.IsBroken)
                continue;

            if (result == null)
            {
                result = part;
                continue;
            }

            if (part.PartHP < result.PartHP)
                result = part;
        }

        return result;
    }
}