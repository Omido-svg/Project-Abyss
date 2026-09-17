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

        // 0917 O/P are explicit one-roll cards even though ordinary Yujin
        // cards take their coin count from the current weapon.
        if (string.Equals(
                skillId,
                YujinSkillIds.AdvanceTiming,
                StringComparison.Ordinal) ||
            string.Equals(
                skillId,
                YujinSkillIds.FinishIt,
                StringComparison.Ordinal))
        {
            return 1;
        }

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

    public override bool CanUseByResource(
        Character character)
    {
        if (Definition != null &&
            string.Equals(
                Definition.SkillId,
                YujinSkillIds.AdvanceTiming,
                StringComparison.Ordinal))
        {
            YujinMechanic mechanic =
                character
                    ?.GetMechanic<YujinMechanic>();

            // 0917 문서의 "감 4 소모"를 기존 자원 문법에 맞춰 사용 게이트로 해석한다.
            if (mechanic == null ||
                mechanic.Sense < 4)
            {
                return false;
            }
        }

        return base.CanUseByResource(
            character);
    }

    public override bool TryConsumeResource(
        Character character,
        BattleAction sourceAction = null)
    {
        if (!base.TryConsumeResource(
                character,
                sourceAction))
        {
            return false;
        }

        if (Definition != null &&
            string.Equals(
                Definition.SkillId,
                YujinSkillIds.AdvanceTiming,
                StringComparison.Ordinal))
        {
            // O의 감 4는 빛과 같은 계획 commit 시점에 지불하여
            // 같은 턴 O 여러 장 예약으로 자원 초과예약하는 구멍을 막는다.
            YujinMechanic mechanic =
                character
                    ?.GetMechanic<YujinMechanic>();

            if (mechanic == null ||
                !mechanic.TrySpendSense(4))
            {
                return false;
            }
        }

        character
            ?.GetMechanic<Yujin0917Mechanic>()
            ?.RecordCommittedUse(
                Definition);

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
    public YujinPreparationRuntimeSkill(
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
