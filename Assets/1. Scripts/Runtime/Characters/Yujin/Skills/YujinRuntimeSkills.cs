using UnityEngine;

internal static class YujinSkillRollUtility
{
    public static int GetExchangeCount(
        Character owner)
    {
        YujinMechanic mechanic =
            owner?.GetMechanic<YujinMechanic>();

        return mechanic?.CurrentWeaponProfile.CoinCount ?? 1;
    }

    public static RollResult Roll(
        Character owner,
        SkillDefinition definition)
    {
        YujinMechanic mechanic =
            owner?.GetMechanic<YujinMechanic>();

        if (mechanic == null ||
            definition == null)
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
            Random.value < chance;

        int power =
            mechanic.GetSkillPower(
                definition.SkillId,
                front);

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
        YujinSkillRollUtility.GetExchangeCount(owner);


    public override int AttackWeight =>
        owner?.GetMechanic<YujinMechanic>()
            ?.CurrentWeapon == YujinWeaponType.Jeokseol
            ? 2
            : 1;

    public override float SecondaryTargetDamageMultiplier =>
        0.5f;

    public override AttackWeightSecondaryPartMode
        SecondaryAttackWeightPartMode =>
            AttackWeightSecondaryPartMode
                .AnotherPartOnPrimaryCharacter;

    public override bool AllowBrokenAttackWeightParts =>
        false;

    public override RollResult RollPowerResultForExchange(
        int exchangeIndex) =>
        YujinSkillRollUtility.Roll(
            owner,
            Definition);
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
        YujinSkillRollUtility.GetExchangeCount(owner);


    public override int AttackWeight =>
        owner?.GetMechanic<YujinMechanic>()
            ?.CurrentWeapon == YujinWeaponType.Jeokseol
            ? 2
            : 1;

    public override float SecondaryTargetDamageMultiplier =>
        0.5f;

    public override AttackWeightSecondaryPartMode
        SecondaryAttackWeightPartMode =>
            AttackWeightSecondaryPartMode
                .AnotherPartOnPrimaryCharacter;

    public override bool AllowBrokenAttackWeightParts =>
        false;

    public override RollResult RollPowerResultForExchange(
        int exchangeIndex) =>
        YujinSkillRollUtility.Roll(
            owner,
            Definition);

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
