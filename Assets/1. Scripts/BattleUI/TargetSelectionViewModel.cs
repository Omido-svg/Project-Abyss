using System;

[Serializable]
public sealed class TargetSelectionViewModel
{
    public BattleInputMode Mode { get; private set; } =
        BattleInputMode.SelectOwner;

    public Character Owner { get; private set; }
    public BodyPart OwnerPart { get; private set; }

    public Character Target { get; private set; }
    public BodyPart TargetPart { get; private set; }

    public Skill Skill { get; private set; }

    public TargetSelectionRule TargetRule { get; private set; } =
        TargetSelectionRule.StandardAttack;

    public int ActionIndex { get; private set; }
    public int MaxActionSlots { get; private set; } = 1;

    public bool HasOwner => Owner != null && OwnerPart != null;
    public bool HasTarget => Target != null;
    public bool HasSkill => Skill != null;

    public bool IsSelfTarget =>
        Owner != null &&
        Owner == Target;

    public void SetMode(BattleInputMode mode)
    {
        Mode = mode;
    }

    public void SelectOwner(
        Character owner,
        BodyPart ownerPart,
        int actionIndex,
        int maxActionSlots)
    {
        Owner = owner;
        OwnerPart = ownerPart;
        ActionIndex = Math.Max(0, actionIndex);
        MaxActionSlots = Math.Max(1, maxActionSlots);

        Target = null;
        TargetPart = null;
        Skill = null;
        TargetRule = TargetSelectionRule.StandardAttack;
    }

    public void SelectTarget(
        Character target,
        BodyPart targetPart)
    {
        Target = target;
        TargetPart = targetPart;
    }

    public void SelectSkill(Skill skill)
    {
        Skill = skill;

        TargetRule =
            skill != null &&
            skill.ActionType == ActionType.Preparation
                ? TargetSelectionRule.LivingPartOnly
                : TargetSelectionRule.StandardAttack;
    }

    public void SetActionIndex(
        int actionIndex,
        int maxActionSlots)
    {
        MaxActionSlots = Math.Max(1, maxActionSlots);
        ActionIndex = Math.Max(
            0,
            Math.Min(
                actionIndex,
                MaxActionSlots - 1));
    }

    public void ResolveTargetForSkill(
        Skill skill,
        out Character resolvedTarget,
        out BodyPart resolvedTargetPart)
    {
        if (skill != null &&
            skill.ActionType == ActionType.Preparation)
        {
            resolvedTarget = Owner;
            resolvedTargetPart = OwnerPart;
            return;
        }

        resolvedTarget = Target;
        resolvedTargetPart = TargetPart;
    }

    public void ClearTarget()
    {
        Target = null;
        TargetPart = null;
    }

    public void Reset()
    {
        Mode = BattleInputMode.SelectOwner;

        Owner = null;
        OwnerPart = null;

        Target = null;
        TargetPart = null;

        Skill = null;
        TargetRule = TargetSelectionRule.StandardAttack;

        ActionIndex = 0;
        MaxActionSlots = 1;
    }
}
