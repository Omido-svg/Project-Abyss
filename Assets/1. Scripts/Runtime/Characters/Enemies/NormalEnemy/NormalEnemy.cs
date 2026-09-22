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
        // 0922 §12.3: 일반전투는 총 HP pool을 거의 동일하게 유지한다.
        // 같은 NormalEnemy prefab을 사용하더라도 3인 조우는 135, 4인 조우는 101.
        // 혼합/비정본 테스트 조우는 기존 serialized 값을 보존한다.
        int resolvedMaxHp =
            Canonical0922EnemyEncounterRules.ResolveNormalEnemyHp(
                BattleContext,
                singleMaxHP);

        return new SingleHpTargetModel(
            resolvedMaxHp);
    }

    protected override void BuildMechanics()
    {
        base.BuildMechanics();

        // 0922 §12 normal encounter role/quota/cover contract.
        // Role is assigned only when the roster is a canonical 3/4-normal encounter;
        // mixed/legacy fixtures retain their old AI behavior.
        AddMechanic(
            new NormalEnemy0922RoleMechanic());

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
        // 0922 §12.1 confirmed invariant: normal-enemy RNG is uniform D8.
        // Per-roll serialized data is normalized by the Phase 9 migration too;
        // this resolver remains the canonical fallback for data without rolls.
        return new DiceResolver(
            FallbackDiceMin,
            FallbackDiceMax);
    }
}
