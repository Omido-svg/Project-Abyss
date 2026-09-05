using UnityEngine;

public static class CombatRollResolver
{
    public static RollResult Roll(
        Skill skill,
        SkillRollData data,
        SkillResolverType fallbackType)
    {
        if (data == null)
            return skill?.RollPowerResult();

        SkillResolverType type = ResolveType(data.RngSource, fallbackType);
        return type switch
        {
            SkillResolverType.Coin => RollCoin(data),
            SkillResolverType.Chinchiro => RollChinchiro(skill, data),
            SkillResolverType.Slot => RollSlot(data),
            _ => RollDice(skill, data, SkillResolverType.Dice)
        };
    }

    private static SkillResolverType ResolveType(
        RollRngSource source,
        SkillResolverType fallback)
    {
        return source switch
        {
            RollRngSource.Dice => SkillResolverType.Dice,
            RollRngSource.Coin => SkillResolverType.Coin,
            RollRngSource.Chinchiro => SkillResolverType.Chinchiro,
            RollRngSource.Slot => SkillResolverType.Slot,
            _ => fallback
        };
    }

    private static RollResult RollDice(
        Skill skill,
        SkillRollData data,
        SkillResolverType resolverType)
    {
        int min = data.SafeMinPower;
        int max = data.SafeMaxPower;
        int value = Random.Range(min, max + 1);
        int basePower =
            data.ResolveDiceBasePower(
                skill?.BasePower ?? 0);

        RollResult result =
            Create(
                data,
                resolverType,
                value,
                basePower);
        result.DiceMin = min;
        result.DiceMax = max;
        result.DiceValues.Add(value);
        result.IsMax = value >= max;
        result.IsCritical = false;
        return result;
    }

    private static RollResult RollCoin(SkillRollData data)
    {
        bool front = Random.value < data.CoinFrontChance;
        int value = front ? data.CoinFrontPower : data.CoinBackPower;
        RollResult result = Create(data, SkillResolverType.Coin, value);
        result.CoinFaces.Add(front);
        result.CoinValues.Add(value);
        result.IsMax = value >= Mathf.Max(data.CoinBackPower, data.CoinFrontPower);
        result.IsCritical = front && data.CoinFrontIsCritical;
        return result;
    }

    private static RollResult RollSlot(
        SkillRollData data)
    {
        int minimum =
            Mathf.Clamp(
                data.SlotMinimum,
                1,
                9);

        int maximum =
            Mathf.Clamp(
                data.SlotMaximum,
                minimum,
                9);

        int a =
            Random.Range(
                minimum,
                maximum + 1);

        int b =
            Random.Range(
                minimum,
                maximum + 1);

        int value = a * b;

        RollResult result =
            Create(
                data,
                SkillResolverType.Slot,
                value);

        result.SlotA = a;
        result.SlotB = b;
        result.SlotValue = value;
        result.IsMax =
            value >=
            maximum * maximum;

        result.IsCritical = false;
        return result;
    }

    private static RollResult RollChinchiro(Skill skill, SkillRollData data)
    {
        int a;
        int b;
        int c;

        bool hasOverride =
            ChinchiroOutcomeOverrideResolver.TryResolve(
                skill?.Owner,
                out IChinchiroOutcomeOverride outcomeOverride);

        if (hasOverride &&
            outcomeOverride.TryGetForcedChinchiro(out ChinchiroCombination forced))
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
            a = Random.Range(1, 7);
            b = Random.Range(1, 7);
            c = Random.Range(1, 7);
        }
        int[] values = { a, b, c };
        System.Array.Sort(values);

        ChinchiroCombination combination;
        int power;

        if (a == b && b == c)
        {
            combination = ChinchiroCombination.Arashi;
            power = data.ChinchiroArashiPower;
        }
        else if (values[0] == 4 && values[1] == 5 && values[2] == 6)
        {
            combination = ChinchiroCombination.Shigoro;
            power = data.ChinchiroShigoroPower;
        }
        else if (values[0] == 1 && values[1] == 2 && values[2] == 3)
        {
            combination = ChinchiroCombination.Hifumi;
            power = data.ChinchiroHifumiPower;
        }
        else if (a == b || a == c || b == c)
        {
            combination = ChinchiroCombination.Moku;
            power = data.ChinchiroMokuPower;
        }
        else
        {
            combination = ChinchiroCombination.Blank;
            power = data.ChinchiroBlankPower;
        }

        RollResult result = Create(data, SkillResolverType.Chinchiro, power);
        result.DiceMin = 1;
        result.DiceMax = 6;
        result.DiceValues.Add(a);
        result.DiceValues.Add(b);
        result.DiceValues.Add(c);
        result.ChinchiroCombination = combination;
        result.ChinchiroBonus = power;
        result.IsCritical = false;
        return result;
    }

    private static RollResult Create(
        SkillRollData data,
        SkillResolverType resolverType,
        int power,
        int basePower = 0)
    {
        RollResult result = new RollResult
        {
            ResolverType = resolverType,
            RollIndex = data.Index,
            RollType = data.Type,
            JudgmentModifier = data.JudgmentModifier,
            BasePower = basePower,
            RawValue = power,
            ModifiedValue = power,
            ExternalModifier = 0
        };
        result.RecalculateFinalPower();
        return result;
    }
}