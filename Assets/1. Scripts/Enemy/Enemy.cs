using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Character
{
    //--------------------------------------------------
    // AI 행동 결정
    //--------------------------------------------------

    public List<ActionSlot> DecideSlots(BattleContext context)
    {
        List<ActionSlot> result = new();

        Character target = ChooseTarget(context);

        foreach (BodyPart part in BodyParts)
        {
            Skill skill = SelectSkillForPart(part);

            if (skill == null)
                continue;

            BodyPart targetPart = SelectTargetPart(target);

            ActionSlot slot = new ActionSlot
            {
                Owner = this,
                Part = part,

                Skill = skill,

                TargetCharacter = target,
                TargetPart = targetPart,

                Speed = part.CurrentSpeed,   // 또는 part.Speed

                Phase = CalculateActionPhase(skill)
            };

            result.Add(slot);
        }

        return result;
    }

    //--------------------------------------------------
    // 공격 대상 선택
    //--------------------------------------------------

    protected virtual Character ChooseTarget(BattleContext context)
    {
        return context.Player;
    }

    //--------------------------------------------------
    // 사용할 스킬 선택
    //--------------------------------------------------

    protected Skill SelectSkillForPart(BodyPart part)
    {
        List<Skill> candidates = new();

        foreach (Skill skill in part.AvailableSkills)
        {
            if (!IsSkillUsable(skill))
                continue;

            candidates.Add(skill);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    //--------------------------------------------------

    protected bool IsSkillUsable(Skill skill)
    {
        if (skill.ActionType == ActionType.Prestige)
        {
            return RuntimeStatus.currentPrestige >= CurrentStatus.maxPrestige;
        }

        return true;
    }

    //--------------------------------------------------
    // 공격 부위 선택
    //--------------------------------------------------

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

        if (Random.value < 0.7f)
            return broken[Random.Range(0, broken.Count)];

        if (alive.Count > 0)
            return alive[Random.Range(0, alive.Count)];

        return broken[Random.Range(0, broken.Count)];
    }

    //--------------------------------------------------
    // ActionPhase 계산
    //--------------------------------------------------

    protected ActionPhase CalculateActionPhase(Skill skill)
    {
        return skill.ActionType switch
        {
            ActionType.Prestige     => ActionPhase.PRETURN,
            ActionType.Preparation  => ActionPhase.FORESIGHT,
            ActionType.NormalAttack => ActionPhase.COMBAT,
            ActionType.Duel         => ActionPhase.COMBAT,
            _                       => ActionPhase.COMBAT
        };
    }
}