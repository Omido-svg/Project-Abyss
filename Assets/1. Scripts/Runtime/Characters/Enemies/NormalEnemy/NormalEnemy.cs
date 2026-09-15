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
    public const int FallbackDiceMin = 1;
    public const int FallbackDiceMax = 8;

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
        int minimum = definition?.DiceMin ?? FallbackDiceMin;
        int maximum = definition?.DiceMax ?? FallbackDiceMax;

        // 0915 C-43: 범위 미지정 일반 몹은 균등 D8(1~8) fallback.
        if (minimum <= 0 && maximum <= 0)
        {
            minimum = FallbackDiceMin;
            maximum = FallbackDiceMax;
        }

        if (maximum < minimum)
            (minimum, maximum) = (maximum, minimum);

        return new DiceResolver(
            minimum,
            maximum);
    }

}