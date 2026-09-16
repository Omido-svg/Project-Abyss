using System;
using UnityEngine;

/// <summary>
/// 0916 Hifumi 친치로 Source of Truth.
/// 모든 히후미 공격 스킬은 BasePower + 친치로 값으로 순수 위력을 만든다.
/// 아라시/시고로=+18, 목=짝이 아닌 눈, 꽝=최저 눈, 1·2·3=대실패.
/// </summary>
public static class HifumiChinchiroRuntime
{
    public const int JackpotBonus = 18;
    public const int CatastropheSelfDamage = 60;

    public static RollResult Roll(
        Skill skill,
        int exchangeIndex,
        int basePowerAdjustment = 0)
    {
        if (skill == null)
            return null;

        SkillRollData data = skill.GetRollData(exchangeIndex);

        if (CharacterRandomDebugOverride.TryCreateRoll(
                skill.Owner,
                skill,
                data,
                SkillResolverType.Chinchiro,
                exchangeIndex,
                out RollResult debugResult))
        {
            ApplyBasePower(
                debugResult,
                skill.BasePower + basePowerAdjustment,
                data,
                exchangeIndex);
            return debugResult;
        }

        int a;
        int b;
        int c;

        bool hasOverride =
            ChinchiroOutcomeOverrideResolver.TryResolve(
                skill.Owner,
                out IChinchiroOutcomeOverride outcomeOverride);

        if (hasOverride &&
            outcomeOverride.TryGetForcedChinchiro(
                out ChinchiroCombination forced))
        {
            if (forced == ChinchiroCombination.Hifumi)
            {
                a = 1;
                b = 2;
                c = 3;
            }
            else
            {
                // 속임수의 성공 고정은 아라시.
                a = 6;
                b = 6;
                c = 6;
            }
        }
        else
        {
            a = UnityEngine.Random.Range(1, 7);
            b = UnityEngine.Random.Range(1, 7);
            c = UnityEngine.Random.Range(1, 7);
        }

        return BuildResult(
            skill,
            data,
            exchangeIndex,
            a,
            b,
            c,
            basePowerAdjustment);
    }


    public static RollResult RollStandalone(
        Character owner,
        int basePower)
    {
        int a;
        int b;
        int c;

        bool hasOverride =
            ChinchiroOutcomeOverrideResolver.TryResolve(
                owner,
                out IChinchiroOutcomeOverride outcomeOverride);

        if (hasOverride &&
            outcomeOverride.TryGetForcedChinchiro(
                out ChinchiroCombination forced))
        {
            if (forced == ChinchiroCombination.Hifumi)
            {
                a = 1;
                b = 2;
                c = 3;
            }
            else
            {
                a = 6;
                b = 6;
                c = 6;
            }
        }
        else
        {
            a = UnityEngine.Random.Range(1, 7);
            b = UnityEngine.Random.Range(1, 7);
            c = UnityEngine.Random.Range(1, 7);
        }

        return BuildResultCore(
            Mathf.Max(0, basePower),
            null,
            0,
            a,
            b,
            c);
    }

    public static RollResult BuildResultForVerification(
        int basePower,
        int a,
        int b,
        int c)
    {
        return BuildResultCore(
            basePower,
            null,
            0,
            a,
            b,
            c);
    }

    private static RollResult BuildResult(
        Skill skill,
        SkillRollData data,
        int exchangeIndex,
        int a,
        int b,
        int c,
        int basePowerAdjustment)
    {
        return BuildResultCore(
            Mathf.Max(0, skill.BasePower + basePowerAdjustment),
            data,
            exchangeIndex,
            a,
            b,
            c);
    }

    private static RollResult BuildResultCore(
        int basePower,
        SkillRollData data,
        int exchangeIndex,
        int a,
        int b,
        int c)
    {
        int[] sorted = { a, b, c };
        Array.Sort(sorted);

        ChinchiroCombination combination;
        int value;

        if (a == b && b == c)
        {
            combination = ChinchiroCombination.Arashi;
            value = JackpotBonus;
        }
        else if (sorted[0] == 4 &&
                 sorted[1] == 5 &&
                 sorted[2] == 6)
        {
            combination = ChinchiroCombination.Shigoro;
            value = JackpotBonus;
        }
        else if (sorted[0] == 1 &&
                 sorted[1] == 2 &&
                 sorted[2] == 3)
        {
            combination = ChinchiroCombination.Hifumi;
            // 판정값으로 이기지 못하게 하는 것은 IForcedRollFailureRule이 담당한다.
            // 수치 자체는 0으로 두어 미정 penalty를 하드코딩하지 않는다.
            value = 0;
        }
        else if (a == b || a == c || b == c)
        {
            combination = ChinchiroCombination.Moku;
            // 0916: 짝을 이루지 않은 눈 그대로.
            if (a == b)
                value = c;
            else if (a == c)
                value = b;
            else
                value = a;
        }
        else
        {
            combination = ChinchiroCombination.Blank;
            value = sorted[0];
        }

        RollResult result = new RollResult
        {
            ResolverType = SkillResolverType.Chinchiro,
            RollIndex = Mathf.Max(0, exchangeIndex),
            RollType = data?.Type ?? CombatRollType.Attack,
            JudgmentModifier = data?.JudgmentModifier ?? 0,
            BasePower = Mathf.Max(0, basePower),
            RawValue = value,
            ModifiedValue = value,
            ExternalModifier = 0,
            ChinchiroCombination = combination,
            ChinchiroBonus = value,
            ChinchiroSelfDamage =
                combination == ChinchiroCombination.Hifumi
                    ? CatastropheSelfDamage
                    : 0,
            DiceMin = 1,
            DiceMax = 6,
            IsCritical = false,
            IsMax =
                combination == ChinchiroCombination.Arashi ||
                combination == ChinchiroCombination.Shigoro
        };

        result.DiceValues.Add(a);
        result.DiceValues.Add(b);
        result.DiceValues.Add(c);
        result.RecalculateFinalPower();
        return result;
    }

    private static void ApplyBasePower(
        RollResult result,
        int basePower,
        SkillRollData data,
        int exchangeIndex)
    {
        if (result == null)
            return;

        result.ResolverType = SkillResolverType.Chinchiro;
        result.RollIndex = Mathf.Max(0, exchangeIndex);
        result.RollType = data?.Type ?? result.RollType;
        result.BasePower = Mathf.Max(0, basePower);
        result.RecalculateFinalPower();
    }
}

public sealed class HifumiNormalRuntimeSkill : DataNormalSkill
{
    public HifumiNormalRuntimeSkill(SkillDefinition definition) : base(definition) { }
    public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;

    public override RollResult RollPowerResultForExchange(int exchangeIndex)
    {
        return HifumiChinchiroRuntime.Roll(
            this,
            exchangeIndex);
    }
}

public sealed class HifumiDuelRuntimeSkill : DataDuelSkill
{
    public HifumiDuelRuntimeSkill(SkillDefinition definition) : base(definition) { }
    public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;

    public override RollResult RollPowerResultForExchange(int exchangeIndex)
    {
        HifumiMechanic mechanic =
            Owner?.GetMechanic<HifumiMechanic>();

        int adjustment =
            mechanic?.ResolveDuelBasePowerAdjustment(
                Definition?.SkillId) ?? 0;

        return HifumiChinchiroRuntime.Roll(
            this,
            exchangeIndex,
            adjustment);
    }
}

public sealed class HifumiPreparationRuntimeSkill : DataPreparationSkill
{
    public HifumiPreparationRuntimeSkill(SkillDefinition definition) : base(definition) { }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<HifumiMechanic>()?.ExecuteSkill(action);
    }
}

public sealed class HifumiPrestigeRuntimeSkill : DataPrestigeSkill
{
    public HifumiPrestigeRuntimeSkill(SkillDefinition definition) : base(definition) { }

    public override void Execute(BattleAction action)
    {
        base.Execute(action);
        action?.Owner?.GetMechanic<HifumiMechanic>()?.ExecuteSkill(action);
    }
}
