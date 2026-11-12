using System;
using UnityEngine;

internal static class YujinSkillRollUtility
{
    public static int GetExchangeCount(
        Character owner,
        Skill skill = null)
    {
        string skillId =
            skill?.Definition?.SkillId;

        YujinMechanic mechanic =
            owner?.GetMechanic<YujinMechanic>();

        return mechanic?.CurrentWeaponProfile.CoinCount ?? 1;
    }

    public static RollResult Roll(
        Character owner,
        Skill skill)
    {
        YujinMechanic mechanic =
            owner?.GetMechanic<YujinMechanic>();

        if (mechanic == null ||
            skill == null ||
            skill.Definition == null)
        {
            return new RollResult
            {
                ResolverType = SkillResolverType.Coin,
                BasePower = 0,
                RawValue = 0,
                ModifiedValue = 0,
                FinalPower = 0
            };
        }

        bool forcedFront =
            mechanic.TryConsumeForcedFront();

        float chance =
            Mathf.Clamp01(
                mechanic.CurrentFrontChance);

        bool front =
            forcedFront ||
            UnityEngine.Random.value < chance;

        int power =
            mechanic.GetSkillPower(
                skill,
                front);

        if (string.Equals(
                skill.Definition.SkillId,
                YujinSkillIds.FinishIt,
                StringComparison.Ordinal))
        {
            // 0917 P: 실제 지불 비용은 4→3→2→1이지만
            // 코인 위력 산정은 언제나 "빛 2" 기준으로 고정한다.
            // 공통 유진 공식의 빛 1 = +2를 역보정하여 기존 무기/환형 보너스는 보존한다.
            power +=
                (2 - Mathf.Max(0, skill.EnergyCost)) * 2;
        }

        RollResult result =
            new RollResult
            {
                ResolverType = SkillResolverType.Coin,
                BasePower = 0,
                RawValue = power,
                ModifiedValue = power,
                FinalPower = power,
                IsMax = front,
                IsCritical = front
            };

        result.CoinFaces.Add(front);
        result.CoinValues.Add(power);
        result.RecalculateClashPower();
        return result;
    }
}

public sealed class YujinNormalRuntimeSkill :
    DataNormalSkill
{
    public YujinNormalRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override int ExchangeRollCount =>
        YujinSkillRollUtility.GetExchangeCount(
            owner,
            this);

    public override RollResult RollPowerResultForExchange(
        int exchangeIndex) =>
        YujinSkillRollUtility.Roll(
            owner,
            this);
}

public sealed class YujinDuelRuntimeSkill :
    DataDuelSkill
{
    public YujinDuelRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override int ExchangeRollCount =>
        YujinSkillRollUtility.GetExchangeCount(
            owner,
            this);

    public override int EnergyCost
    {
        get
        {
            int baseCost = base.EnergyCost;

            return owner
                ?.GetMechanic<Yujin0917Mechanic>()
                ?.ResolveEnergyCost(
                    Definition,
                    baseCost)
                ?? baseCost;
        }
    }

    public override bool CanUseByResource(Character character)
    {
        // Current-turn Sense is checked by the roll's OnUse effect, not planning.
        return base.CanUseByResource(character);
    }

    public override bool TryConsumeResource(Character character, BattleAction sourceAction = null)
    {
        if (!base.TryConsumeResource(character, sourceAction)) return false;
        character?.GetMechanic<Yujin0917Mechanic>()?.RecordCommittedUse(Definition);
        return true;
    }

    protected override void OnPlanningUseRolledBack()
    {
        owner
            ?.GetMechanic<Yujin0917Mechanic>()
            ?.RollbackCommittedUse(
                Definition);
    }

    public override RollResult RollPowerResultForExchange(
        int exchangeIndex) =>
        YujinSkillRollUtility.Roll(
            owner,
            this);

    public override int GetMomentumPushBonus(
        BattleAction action) => 0;
}

public sealed class YujinPreparationRuntimeSkill :
    DataPreparationSkill
{
    public override int EnergyCost => YujinMechanic.IsHwanhyeongSkill(Definition?.SkillId)
        ? 0 : base.EnergyCost;
    public YujinPreparationRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
        if (YujinMechanic.IsHwanhyeongSkill(definition?.SkillId)) SkillName = "환형";
    }

    public override void Execute(
        BattleAction action)
    {
        if (YujinMechanic.IsHwanhyeongSkill(Definition?.SkillId)) return;
        base.Execute(action);

        action?.Owner
            ?.GetMechanic<YujinMechanic>()
            ?.ExecuteSkill(action);
    }
}

public sealed class YujinPrestigeRuntimeSkill :
    DataPrestigeSkill
{
    public YujinPrestigeRuntimeSkill(
        SkillDefinition definition)
        : base(definition)
    {
    }

    public override void Execute(
        BattleAction action)
    {
        base.Execute(action);

        action?.Owner
            ?.GetMechanic<YujinMechanic>()
            ?.ExecuteSkill(action);
    }
}
