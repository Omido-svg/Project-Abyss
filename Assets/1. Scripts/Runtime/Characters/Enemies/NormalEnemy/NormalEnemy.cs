using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy, ICharacterAuthoringTarget
{
    [Header("Skill Set")]
    [SerializeField]
    private NormalEnemySkillSet skillSet;

    [Header("Single HP")]
    [SerializeField, Min(1)]
    private int singleMaxHP = 50;

    private readonly List<BodyPart> bodyParts = new();
    private readonly List<Skill> characterSkills = new();

    public override IReadOnlyList<BodyPart> BodyParts =>
        bodyParts;

    public IReadOnlyList<Skill> CharacterSkills =>
        characterSkills;

    public override bool SupportsLastStand => false;

    public override int GetMaxCombatActionSlots() => 1;

    public bool ApplyCharacterAuthoring(
        CharacterAuthoringBundle bundle)
    {
        if (bundle?.SkillSet is not NormalEnemySkillSet configuredSkillSet)
            return false;

        skillSet = configuredSkillSet;

        if (bundle.OverrideNormalEnemySingleHp)
        {
            singleMaxHP =
                bundle.NormalEnemySingleMaxHp;
        }

        return true;
    }

    protected override void BuildBodyParts()
    {
        // 일반몹은 가짜 HEAD를 만들지 않는다.
        bodyParts.Clear();
        characterSkills.Clear();

        AddRuntimeSkill(
            skillSet != null
                ? skillSet.NormalAttack
                : null);

        AddRuntimeSkill(
            skillSet != null
                ? skillSet.DuelSkill
                : null);

        AddRuntimeSkill(
            skillSet != null
                ? skillSet.PrestigeSkill
                : null);
    }

    protected override IEnumerable<Skill>
        GetCharacterSkills()
    {
        return characterSkills;
    }

    protected override IReadOnlyList<Skill>
        GetAvailableSkills(BodyPart part)
    {
        return part == null
            ? characterSkills
            : null;
    }

    protected override ICombatTargetModel
        CreateCombatTargetModel()
    {
        return new SingleHpTargetModel(
            singleMaxHP);
    }

    private void AddRuntimeSkill(
        SkillDefinition definition)
    {
        if (definition == null)
            return;

        Skill skill =
            NormalEnemyRuntimeSkill.Create(definition);

        if (skill != null)
            characterSkills.Add(skill);
    }

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        AddMechanic(
            new NormalEnemyBloodScentMechanic());
    }

    protected override StatusEffect
        CreateDisabledDebuff(BodyPart part)
    {
        return null;
    }

    protected override StatusEffect
        CreateBrokenPartStatus(BodyPart part)
    {
        return null;
    }
}


internal static class NormalEnemyRuntimeSkill
{
    public static Skill Create(SkillDefinition definition)
    {
        if (definition == null)
            return null;

        return definition.ActionType switch
        {
            ActionType.NormalAttack => new NormalEnemyNormalSkill(definition),
            ActionType.Duel => new NormalEnemyDuelSkill(definition),
            ActionType.Prestige => new NormalEnemyPrestigeSkill(definition),
            _ => definition.CreateRuntimeSkill()
        };
    }

    private sealed class NormalEnemyNormalSkill : DataNormalSkill
    {
        public NormalEnemyNormalSkill(SkillDefinition definition)
            : base(definition)
        {
            Resolver = CreateUniformDice(definition);
        }

        public override int ExchangeRollCount => 3;
        public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
    }

    private sealed class NormalEnemyDuelSkill : DataDuelSkill
    {
        public NormalEnemyDuelSkill(SkillDefinition definition)
            : base(definition)
        {
            Resolver = CreateUniformDice(definition);
        }

        public override int ExchangeRollCount => 3;
        public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
    }

    private sealed class NormalEnemyPrestigeSkill : DataPrestigeSkill
    {
        public NormalEnemyPrestigeSkill(SkillDefinition definition)
            : base(definition)
        {
            Resolver = CreateUniformDice(definition);
        }

        public override int ExchangeRollCount => 3;
        public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
    }

    private static SkillResolver CreateUniformDice(
        SkillDefinition definition)
    {
        int minimum = definition?.DiceMin ?? 1;
        int maximum = definition?.DiceMax ?? 6;

        // 기존 비-Dice 에셋에서 주사위 범위가 비어 있으면
        // 일반 적 기준선인 균등 D6으로 안전하게 복구한다.
        if (minimum == 0 && maximum == 0)
        {
            minimum = 1;
            maximum = 6;
        }

        if (maximum < minimum)
            (minimum, maximum) = (maximum, minimum);

        return new DiceResolver(
            minimum,
            maximum);
    }

}