using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Character Skill Set/Olaf Skill Set",
    fileName = "OlafSkillSet")]
public class OlafSkillSet : ScriptableObject
{
    [Header("Legacy fallback definitions")]
    public SkillDefinition NormalAttack;
    public SkillDefinition DuelSkill;
    public SkillDefinition PreparationSkill;
    public SkillDefinition PrestigeSkill;

    public Skill CreateNormalAttack() =>
        IsExpectedType(NormalAttack, ActionType.NormalAttack)
            ? new OlafNormalRuntimeSkill(NormalAttack)
            : null;

    public Skill CreateDuelSkill() =>
        IsExpectedType(DuelSkill, ActionType.Duel)
            ? new OlafDuelRuntimeSkill(DuelSkill)
            : null;

    public Skill CreatePreparationSkill() =>
        IsExpectedType(PreparationSkill, ActionType.Preparation)
            ? new OlafPreparationRuntimeSkill(PreparationSkill)
            : null;

    public Skill CreatePrestigeSkill() =>
        IsExpectedType(PrestigeSkill, ActionType.Prestige)
            ? new OlafPrestigeRuntimeSkill(PrestigeSkill)
            : null;

    private bool IsExpectedType(
        SkillDefinition definition,
        ActionType expectedType)
    {
        if (definition == null)
            return false;

        if (definition.ActionType == expectedType)
            return true;

        Debug.LogWarning(
            $"{name}: {definition.name}의 ActionType이 " +
            $"{definition.ActionType}입니다. Expected={expectedType}",
            this);

        return false;
    }
}

public sealed class OlafNormalRuntimeSkill : DataNormalSkill
{
    public OlafNormalRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }
}

public sealed class OlafDuelRuntimeSkill : DataDuelSkill
{
    public OlafDuelRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override int GetMomentumPushBonus(
        BattleAction action) => 0;
}

public sealed class OlafPreparationRuntimeSkill :
    DataPreparationSkill
{
    public OlafPreparationRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override void Execute(
        BattleAction action)
    {
        base.Execute(action);

        action?.Owner
            ?.GetMechanic<OlafMadnessMechanic>()
            ?.ExecuteSkill(action);
    }
}

public sealed class OlafPrestigeRuntimeSkill :
    DataPrestigeSkill
{
    public OlafPrestigeRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override void Execute(
        BattleAction action)
    {
        base.Execute(action);

        action?.Owner
            ?.GetMechanic<OlafMadnessMechanic>()
            ?.ExecuteSkill(action);
    }
}
