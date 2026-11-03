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

    /// <summary>
    /// 1029 stage-1 species content is isolated from historical CharacterData
    /// loadouts. Only a genuinely authored, GUID-backed native card is added
    /// to this action. Other/(미정) choices retain the original AI fallback.
    /// No prefab, old loadout, or original SkillDefinition is mutated.
    /// </summary>
    public override List<ActionSlot> DecideSlots(BattleContext context)
    {
        var role=GetMechanic<NormalEnemy0922RoleMechanic>();
        if(role?.RoleAssigned!=true||
            role.Species==R7EnemySpecies1029.Unknown||
            context?.Services?.SpeedManager==null)
            return base.DecideSlots(context);
        var definition=R7EnemyStage1SkillCatalog20261103.Get(
            role.Species,role.PlannedAction);
        if(definition==null)
            return base.DecideSlots(context);
        var skill=NormalEnemyRuntimeSkill.Create(definition);
        if(skill==null)return base.DecideSlots(context);
        skill.Initialize(this,BattleEvent);
        if(!skill.CanUseByResource(this)||!IsSkillUsable(null,skill))
            return base.DecideSlots(context);
        var target=ChooseTarget(context);
        if(target==null||target.IsDead)return new List<ActionSlot>();
        var point=BattleTargetValidator.ChooseWeightedTargetPoint(
            target,TargetSelectionRule.StandardAttack,brokenPartWeight:.7f);
        if(!point.IsValid)return base.DecideSlots(context);
        return new List<ActionSlot>
        {
            new ActionSlot
            {
                Owner=this,
                Part=null,
                Skill=skill,
                TargetCharacter=point.Character,
                TargetPart=point.Part,
                Speed=context.Services.SpeedManager.GetSpeed(this,null),
                Phase=CalculateActionPhase(skill),
                ActionIndex=0
            }
        };
    }

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

    // Native 1029 support preparation is immediately committed by AIManager
    // during planning, never fought as a COMBAT clash.
    protected override bool AllowPreparationSkillAI
    {
        get
        {
            var p=GetMechanic<NormalEnemy0922RoleMechanic>()?.PlannedAction;
            return p==R7EnemyAction1029.HealPreparation||
                   p==R7EnemyAction1029.SupplyPreparation;
        }
    }

    // R7-8 / 1029 §12.3: AI chooses only an affordable card.
    // A 2-light Duel must not suppress a legal 0-light Normal attack.
    protected override bool IsSkillUsable(BodyPart part,Skill skill) =>
        skill!=null&&skill.CanUseByResource(this)&&
        base.IsSkillUsable(part,skill);

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

        public override int ExchangeRollCount =>
            RuntimeDefinition?.EffectiveRollCount ?? base.ExchangeRollCount;
        public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
    }

    private sealed class NormalEnemyDuelSkill : DataDuelSkill
    {
        public NormalEnemyDuelSkill(SkillDefinition definition)
            : base(definition)
        {
            Resolver = CreateUniformDice(definition);
        }

        public override int ExchangeRollCount =>
            RuntimeDefinition?.EffectiveRollCount ?? base.ExchangeRollCount;
        public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
    }

    private sealed class NormalEnemyPrestigeSkill : DataPrestigeSkill
    {
        public NormalEnemyPrestigeSkill(SkillDefinition definition)
            : base(definition)
        {
            Resolver = CreateUniformDice(definition);
        }

        public override int ExchangeRollCount =>
            RuntimeDefinition?.EffectiveRollCount ?? base.ExchangeRollCount;
        public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;
    }

    private static SkillResolver CreateUniformDice(
        SkillDefinition definition)
    {
        // 1029 counter soldier's 6-power retaliation is FIXED, not D8.
        // Never override its native authored zero-variance roll with 1..8.
        if(definition.SkillId==R7EnemyCounterReaction20261103.CounterSkillId)
            return definition.CreateResolver();

        // Normal enemy attacks otherwise still use the canonical D8.
        return new DiceResolver(FallbackDiceMin,FallbackDiceMax);
    }
}
