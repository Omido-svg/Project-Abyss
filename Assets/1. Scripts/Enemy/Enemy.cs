using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy : Character
{
    public virtual List<BattleAction> DecideActions(BattleContext context)
    {
        List<BattleAction> actions = new();

        Character target = context.Player;

        foreach (BodyPart ownerPart in BodyParts)
        {
            if (ownerPart.IsBroken)
                continue;

            Skill skill = SelectSkillForPart(ownerPart);

            if (skill == null)
                continue;

            ownerPart.CurrentSkill = skill;

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

    //--------------------------------------------------


    // 해당 부위가 사용할 수 있는 스킬 선택
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

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    //--------------------------------------------------
    // 위세 사용 가능 여부
    protected bool IsSkillUsable(Skill skill)
    {
        if (skill.ActionType == ActionType.Prestige)
        {
            return RuntimeStatus.currentPrestige >= CurrentStatus.maxPrestige;
        }

        return true;
    }

    //--------------------------------------------------
    // 공격 대상 부위 선택
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
            return alive[UnityEngine.Random.Range(0, alive.Count)];

        if (UnityEngine.Random.value < 0.7f && broken.Count > 0)
            return broken[UnityEngine.Random.Range(0, broken.Count)];

        if (alive.Count > 0)
            return alive[UnityEngine.Random.Range(0, alive.Count)];

        return broken[UnityEngine.Random.Range(0, broken.Count)];
    }

    //--------------------------------------------------
    // ActionPhase 결정
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