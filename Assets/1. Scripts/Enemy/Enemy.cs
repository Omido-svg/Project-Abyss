using UnityEngine;

public abstract class Enemy : Character
{
    public virtual BattleAction DecideAction(BattleContext context)
    {
        Character target = context.Player;

        // 사용할 부위
        BodyPart ownerPart = BodyParts[0];

        // 사용할 스킬
        Skill skill =
            SkillPool[Random.Range(0, SkillPool.Count)];
            
        ActionPhase phase = CalculateActionPhase(skill);

        // 가장 점수가 높은 부위 선택
        BodyPart bestPart = null;
        float bestScore = float.MinValue;

        foreach (BodyPart part in target.BodyParts)
        {
            float score = CalculateTargetScore(part);

            if (score > bestScore)
            {
                bestScore = score;
                bestPart = part;
            }
        }

        return new BattleAction()
        {
            Owner = this,
            Target = target,

            OwnerPart = ownerPart,
            TargetPart = bestPart,

            Skill = skill,
            Phase = phase
        };
    }
    
    protected float CalculateTargetScore(BodyPart part)
    {
        float score = 0f;

        // 이미 부숴진 부위는 대미지가 더 잘 들어간다면 높은 우선순위
        if (part.IsBroken)
            score += 100f;

        // HP가 적을수록 마무리하기 쉬움
        score += part.PartMaxHP - part.PartHP;

        // 속도가 빠른 부위를 우선 제거
        score += part.CurrentSpeed * 3f;

        return score;
    }
    
    protected ActionPhase CalculateActionPhase(Skill skill)
    {
        ActionPhase phase;
        switch(skill.ActionType)
        {
            case ActionType.Duel:
            case ActionType.NormalAttack:
                phase = ActionPhase.COMBAT;
                break;
            case ActionType.Preparation:
                phase = ActionPhase.FORESIGHT;
                break;
            case ActionType.Prestige:
                phase = ActionPhase.PRETURN;
                break;
            default:
                phase = ActionPhase.COMBAT;
                break;
        }
        return phase;
    }
}