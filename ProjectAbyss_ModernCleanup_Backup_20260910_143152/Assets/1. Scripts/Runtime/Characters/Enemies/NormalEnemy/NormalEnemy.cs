using System.Collections.Generic;
using UnityEngine;

public class NormalEnemy : Enemy, ICharacterAuthoringTarget
{

    [Header("Single HP")]
    [SerializeField, Min(1)]
    private int singleMaxHP = 50;

    private readonly List<BodyPart> bodyParts = new();

    public override IReadOnlyList<BodyPart> BodyParts =>
        bodyParts;


    public override bool SupportsLastStand => false;

    public override int GetMaxCombatActionSlots() => 1;

    public bool ApplyCharacterAuthoring(
        CharacterAuthoringBundle bundle)
    {
        if (bundle == null ||
            bundle.Kind != CharacterAuthoringKind.NormalEnemy ||
            bundle.CombatLoadout == null)
        {
            return false;
        }

        if (bundle.OverrideNormalEnemySingleHp)
        {
            singleMaxHP =
                bundle.NormalEnemySingleMaxHp;
        }

        return true;
    }

    public override Skill CreateRuntimeSkillForLoadout(
        SkillDefinition definition)
    {
        return NormalEnemyRuntimeSkill.Create(
            definition);
    }

    protected override void BuildBodyParts()
    {
        // 단일 HP 캐릭터는 BodyPart를 만들지 않는다.
        bodyParts.Clear();
    }

    protected override ICombatTargetModel
        CreateCombatTargetModel()
    {
        return new SingleHpTargetModel(
            singleMaxHP);
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