using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Character
{
    public virtual List<BattleAction> DecideActions(BattleContext context)
    {
        List<BattleAction> actions = new();

        Character target = context.Player;

        // 1. 이번 턴 사용 가능한 스킬 리스트 생성
        List<Skill> usableSkills = BuildUsableSkillList();

        int skillIndex = 0;

        foreach (BodyPart ownerPart in BodyParts)
        {
            if (ownerPart.IsBroken)
                continue;

            // 스킬 부족하면 종료
            if (skillIndex >= usableSkills.Count)
                break;

            Skill skill = usableSkills[skillIndex++];
            BodyPart targetPart = SelectTargetPart(target);

            actions.Add(new BattleAction()
            {
                Owner = this,
                Target = target,

                OwnerPart = ownerPart,
                TargetPart = targetPart,

                Skill = skill,
                Phase = CalculateActionPhase(skill)
            });
        }

        return actions;
    }

    // 핵심: 스킬 풀 필터링
    protected List<Skill> BuildUsableSkillList()
    {
        List<Skill> result = new();

        foreach (Skill skill in SkillPool)
        {
            if (skill == null)
                continue;

            // 위세 스킬 제한
            if (!IsSkillUsable(skill))
                continue;

            result.Add(skill);
        }

        // 랜덤 섞기
        Utils.Shuffle(result);

        return result;
    }

    // 스킬 사용 조건
    protected bool IsSkillUsable(Skill skill)
    {
        if (skill.ActionType == ActionType.Prestige)
        {
            return RuntimeStatus.currentPrestige >= CurrentStatus.maxPrestige;
        }

        return true;
    }

    // 타겟 선택
    protected BodyPart SelectTargetPart(Character target)
    {
        List<BodyPart> broken = new();
        List<BodyPart> alive = new();

        foreach (BodyPart part in target.BodyParts)
        {
            if (part.IsBroken)
                broken.Add(part);
            else
                alive.Add(part);
        }

        if (broken.Count == 0)
            return alive[Random.Range(0, alive.Count)];

        if (Random.value < 0.7f && broken.Count > 0)
            return broken[Random.Range(0, broken.Count)];

        return alive.Count > 0
            ? alive[Random.Range(0, alive.Count)]
            : broken[Random.Range(0, broken.Count)];
    }

    // Phase
    protected ActionPhase CalculateActionPhase(Skill skill)
    {
        return skill.ActionType switch
        {
            ActionType.Prestige => ActionPhase.PRETURN,
            ActionType.Preparation => ActionPhase.FORESIGHT,
            ActionType.NormalAttack => ActionPhase.COMBAT,
            ActionType.Duel => ActionPhase.COMBAT,
            _ => ActionPhase.COMBAT
        };
    }
}