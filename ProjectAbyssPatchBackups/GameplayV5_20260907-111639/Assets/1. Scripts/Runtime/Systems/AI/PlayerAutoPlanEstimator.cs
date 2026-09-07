using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerAutoPlanService에서 확률 분포/합 승률/예상 피해 계산 책임을 분리한
/// 순수 계산 서비스. ActionManager와 실제 RNG 상태를 변경하지 않는다.
/// </summary>
public sealed class PlayerAutoPlanEstimator
{
    private readonly struct PowerOutcome
    {
        public PowerOutcome(
            int rawPower,
            int clashPower,
            float probability,
            CombatRollType rollType)
        {
            RawPower = rawPower;
            ClashPower = clashPower;
            Probability = probability;
            RollType = rollType;
        }

        public int RawPower { get; }
        public int ClashPower { get; }
        public float Probability { get; }
        public CombatRollType RollType { get; }
    }

    private readonly struct ExchangeEstimate
    {
        public ExchangeEstimate(
            float win,
            float loss,
            float draw,
            float damage)
        {
            Win = win;
            Loss = loss;
            Draw = draw;
            ExpectedDamage = damage;
        }

        public float Win { get; }
        public float Loss { get; }
        public float Draw { get; }
        public float ExpectedDamage { get; }
    }

    public readonly struct ClashEstimate
    {
        public ClashEstimate(
            float winRate,
            float expectedDamage)
        {
            WinRate = winRate;
            ExpectedDamage = expectedDamage;
        }

        public float WinRate { get; }
        public float ExpectedDamage { get; }
    }

    public bool TryEstimateClashWinRate(
        BattleContext context,
        Character playerOwner,
        BodyPart playerPart,
        int playerSpeed,
        Skill playerSkill,
        ActionSlot enemySlot,
        out float winRate)
    {
        winRate = 0f;

        if (playerOwner == null ||
            playerSkill == null ||
            enemySlot?.Owner == null ||
            enemySlot.Skill == null ||
            playerSkill.DefaultPhase != ActionPhase.COMBAT ||
            enemySlot.Phase != ActionPhase.COMBAT ||
            !playerSkill.CanClash ||
            !enemySlot.Skill.CanClash)
        {
            return false;
        }

        ClashEstimate estimate =
            EstimateClash(
                context,
                playerOwner,
                playerPart,
                playerSpeed,
                playerSkill,
                enemySlot.Owner,
                enemySlot.Part,
                enemySlot.Speed,
                enemySlot.Skill);

        winRate =
            Mathf.Clamp01(
                estimate.WinRate);

        return true;
    }

    public ClashEstimate EstimateClash(
        BattleContext context,
        Character playerOwner,
        BodyPart playerPart,
        int playerSpeed,
        Skill playerSkill,
        Character enemyOwner,
        BodyPart enemyPart,
        int enemySpeed,
        Skill enemySkill)
    {
        if (playerSkill == null ||
            enemySkill == null)
        {
            return new ClashEstimate(
                0f,
                0f);
        }

        int paired =
            Mathf.Min(
                Mathf.Max(1, playerSkill.ExchangeRollCount),
                Mathf.Max(1, enemySkill.ExchangeRollCount));

        int playerExtra =
            Mathf.Max(
                0,
                playerSkill.ExchangeRollCount - paired);

        ClashRuleSettings rules =
            context?.Rules?.Clash ??
            new ClashRuleSettings();

        int maxTieRerolls =
            Mathf.Max(
                1,
                rules.MaxTieRerolls);

        Dictionary<int, float> differenceDistribution =
            new Dictionary<int, float>
            {
                [0] = 1f
            };

        float damage = 0f;

        for (int rollIndex = 0;
             rollIndex < paired;
             rollIndex++)
        {
            ExchangeEstimate exchange =
                EstimateExchange(
                    playerOwner,
                    playerPart,
                    playerSpeed,
                    playerSkill,
                    enemyOwner,
                    enemyPart,
                    enemySpeed,
                    enemySkill,
                    rollIndex,
                    maxTieRerolls,
                    rules.SpeedWeight);

            damage +=
                exchange.ExpectedDamage;

            Dictionary<int, float> next =
                new Dictionary<int, float>();

            foreach (KeyValuePair<int, float> current
                     in differenceDistribution)
            {
                AddProbability(
                    next,
                    current.Key + 1,
                    current.Value * exchange.Win);

                AddProbability(
                    next,
                    current.Key - 1,
                    current.Value * exchange.Loss);

                AddProbability(
                    next,
                    current.Key,
                    current.Value * exchange.Draw);
            }

            differenceDistribution =
                next;
        }

        float finalWin = 0f;
        float finalDraw = 0f;

        foreach (KeyValuePair<int, float> pair
                 in differenceDistribution)
        {
            if (pair.Key > 0)
                finalWin += pair.Value;
            else if (pair.Key == 0)
                finalDraw += pair.Value;
        }

        for (int index = paired;
             index < paired + playerExtra;
             index++)
        {
            damage +=
                EstimateOneSidedRollDamage(
                    playerSkill,
                    playerOwner,
                    playerSpeed,
                    index);
        }

        float practicalWinRate =
            Mathf.Clamp01(
                finalWin +
                finalDraw * 0.5f);

        return new ClashEstimate(
            practicalWinRate,
            Mathf.Max(0f, damage));
    }

    private ExchangeEstimate EstimateExchange(
        Character playerOwner,
        BodyPart playerPart,
        int playerSpeed,
        Skill playerSkill,
        Character enemyOwner,
        BodyPart enemyPart,
        int enemySpeed,
        Skill enemySkill,
        int rollIndex,
        int maxTieRerolls,
        int speedWeight)
    {
        List<PowerOutcome> playerOutcomes =
            BuildPowerOutcomes(
                playerOwner,
                playerSpeed,
                enemySpeed,
                playerSkill,
                rollIndex,
                speedWeight);

        List<PowerOutcome> enemyOutcomes =
            BuildPowerOutcomes(
                enemyOwner,
                enemySpeed,
                playerSpeed,
                enemySkill,
                rollIndex,
                speedWeight);

        float attemptWin = 0f;
        float attemptLoss = 0f;
        float attemptTie = 0f;
        float winningDamageMass = 0f;

        foreach (PowerOutcome player
                 in playerOutcomes)
        {
            foreach (PowerOutcome enemy
                     in enemyOutcomes)
            {
                float probability =
                    player.Probability *
                    enemy.Probability;

                if (player.ClashPower >
                    enemy.ClashPower)
                {
                    attemptWin += probability;

                    if (player.RollType ==
                        CombatRollType.Attack)
                    {
                        int damage =
                            enemy.RollType ==
                            CombatRollType.Defense
                                ? Mathf.Max(
                                    1,
                                    player.ClashPower -
                                    enemy.ClashPower)
                                : Mathf.Max(
                                    1,
                                    player.RawPower);

                        winningDamageMass +=
                            probability * damage;
                    }
                }
                else if (player.ClashPower <
                         enemy.ClashPower)
                {
                    attemptLoss += probability;
                }
                else
                {
                    attemptTie += probability;
                }
            }
        }

        float nonTie =
            attemptWin +
            attemptLoss;

        if (nonTie <= 0.000001f)
        {
            return new ExchangeEstimate(
                0f,
                0f,
                1f,
                0f);
        }

        float terminalTie =
            Mathf.Pow(
                Mathf.Clamp01(attemptTie),
                maxTieRerolls);

        float resolvedMass =
            1f - terminalTie;

        float resolvedWin =
            resolvedMass *
            attemptWin /
            nonTie;

        float resolvedLoss =
            resolvedMass *
            attemptLoss /
            nonTie;

        float expectedDamage =
            winningDamageMass /
            nonTie *
            resolvedMass;

        return new ExchangeEstimate(
            Mathf.Clamp01(resolvedWin),
            Mathf.Clamp01(resolvedLoss),
            Mathf.Clamp01(terminalTie),
            Mathf.Max(0f, expectedDamage));
    }

    private static List<PowerOutcome> BuildPowerOutcomes(
        Character owner,
        int selfSpeed,
        int opponentSpeed,
        Skill skill,
        int rollIndex,
        int speedWeight)
    {
        List<RawOutcome> raw =
            BuildRawOutcomes(
                skill,
                rollIndex);

        List<PowerOutcome> result =
            new List<PowerOutcome>();

        CombatRollType rollType =
            skill?.GetRollType(rollIndex) ??
            CombatRollType.Attack;

        SkillRollData rollData =
            skill?.GetRollData(rollIndex);

        int judgment =
            rollData?.JudgmentModifier ?? 0;

        int speedModifier =
            selfSpeed - opponentSpeed >= 6 &&
            speedWeight > 0
                ? 1
                : 0;

        int preparationModifier =
            owner?.TurnClashPowerBonus ?? 0;

        foreach (RawOutcome outcome in raw)
        {
            int clash =
                outcome.Power +
                judgment +
                speedModifier +
                preparationModifier;

            result.Add(
                new PowerOutcome(
                    outcome.Power,
                    clash,
                    outcome.Probability,
                    rollType));
        }

        NormalizePowerOutcomes(result);
        return result;
    }

    private readonly struct RawOutcome
    {
        public RawOutcome(
            int power,
            float probability)
        {
            Power = power;
            Probability = probability;
        }

        public int Power { get; }
        public float Probability { get; }
    }

    private static List<RawOutcome> BuildRawOutcomes(
        Skill skill,
        int rollIndex)
    {
        if (skill == null)
        {
            return new List<RawOutcome>
            {
                new RawOutcome(0, 1f)
            };
        }

        SkillDefinition definition =
            skill.Definition;

        SkillRollData data =
            skill.GetRollData(
                rollIndex);

        if (data != null)
        {
            SkillResolverType resolverType =
                ResolveRollResolverType(
                    data,
                    definition);

            return resolverType switch
            {
                SkillResolverType.Coin =>
                    BuildExplicitCoinOutcomes(data),

                SkillResolverType.Chinchiro =>
                    BuildExplicitChinchiroOutcomes(data),

                SkillResolverType.Slot =>
                    BuildExplicitSlotOutcomes(data),

                _ =>
                    BuildUniformOutcomes(
                        data.GetDiceFinalMinPower(
                            skill.BasePower),
                        data.GetDiceFinalMaxPower(
                            skill.BasePower))
            };
        }

        if (definition == null)
        {
            return BuildUniformOutcomes(
                skill.MinPower,
                skill.MaxPower);
        }

        switch (definition.ResolverType)
        {
            case SkillResolverType.Coin:
                return BuildLegacyCoinOutcomes(
                    definition,
                    skill.BasePower);

            case SkillResolverType.Chinchiro:
                return BuildLegacyChinchiroOutcomes(
                    definition,
                    skill.BasePower);

            case SkillResolverType.Slot:
                return BuildLegacySlotOutcomes(
                    skill.BasePower);

            default:
                return BuildUniformOutcomes(
                    skill.MinPower,
                    skill.MaxPower);
        }
    }

    private static SkillResolverType ResolveRollResolverType(
        SkillRollData data,
        SkillDefinition definition)
    {
        if (data == null)
            return definition?.ResolverType ??
                   SkillResolverType.Dice;

        return data.RngSource switch
        {
            RollRngSource.Dice =>
                SkillResolverType.Dice,

            RollRngSource.Coin =>
                SkillResolverType.Coin,

            RollRngSource.Chinchiro =>
                SkillResolverType.Chinchiro,

            RollRngSource.Slot =>
                SkillResolverType.Slot,

            _ =>
                definition?.ResolverType ??
                SkillResolverType.Dice
        };
    }

    private static List<RawOutcome> BuildExplicitSlotOutcomes(
        SkillRollData data)
    {
        if (data == null)
        {
            return new List<RawOutcome>
            {
                new RawOutcome(1, 1f)
            };
        }

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

        Dictionary<int, int> counts =
            new Dictionary<int, int>();

        int total = 0;

        for (int a = minimum;
             a <= maximum;
             a++)
        {
            for (int b = minimum;
                 b <= maximum;
                 b++)
            {
                int value = a * b;

                if (!counts.ContainsKey(value))
                    counts[value] = 0;

                counts[value]++;
                total++;
            }
        }

        List<RawOutcome> result =
            new List<RawOutcome>();

        foreach (KeyValuePair<int, int> pair
                 in counts)
        {
            result.Add(
                new RawOutcome(
                    pair.Key,
                    total > 0
                        ? (float)pair.Value / total
                        : 0f));
        }

        return result;
    }

    private static List<RawOutcome> BuildLegacySlotOutcomes(
        int basePower)
    {
        Dictionary<int, int> counts =
            new Dictionary<int, int>();

        for (int a = 1; a <= 9; a++)
        {
            for (int b = 1; b <= 9; b++)
            {
                int value =
                    basePower +
                    a * b;

                if (!counts.ContainsKey(value))
                    counts[value] = 0;

                counts[value]++;
            }
        }

        List<RawOutcome> result =
            new List<RawOutcome>();

        foreach (KeyValuePair<int, int> pair
                 in counts)
        {
            result.Add(
                new RawOutcome(
                    pair.Key,
                    pair.Value / 81f));
        }

        return result;
    }

    private static List<RawOutcome> BuildUniformOutcomes(
        int minimum,
        int maximum)
    {
        int min =
            Mathf.Min(
                minimum,
                maximum);

        int max =
            Mathf.Max(
                minimum,
                maximum);

        int count =
            Mathf.Max(
                1,
                max - min + 1);

        float probability =
            1f / count;

        List<RawOutcome> result =
            new List<RawOutcome>(count);

        for (int value = min;
             value <= max;
             value++)
        {
            result.Add(
                new RawOutcome(
                    value,
                    probability));
        }

        return result;
    }

    private static List<RawOutcome> BuildExplicitCoinOutcomes(
        SkillRollData data)
    {
        float front =
            Mathf.Clamp01(
                data.CoinFrontChance);

        List<RawOutcome> result =
            new List<RawOutcome>();

        AddRawOutcome(
            result,
            data.CoinBackPower,
            1f - front);

        AddRawOutcome(
            result,
            data.CoinFrontPower,
            front);

        NormalizeRawOutcomes(result);
        return result;
    }

    private static List<RawOutcome> BuildExplicitChinchiroOutcomes(
        SkillRollData data)
    {
        const float denominator = 216f;

        List<RawOutcome> result =
            new List<RawOutcome>();

        AddRawOutcome(
            result,
            data.ChinchiroArashiPower,
            6f / denominator);

        AddRawOutcome(
            result,
            data.ChinchiroShigoroPower,
            6f / denominator);

        AddRawOutcome(
            result,
            data.ChinchiroHifumiPower,
            6f / denominator);

        AddRawOutcome(
            result,
            data.ChinchiroMokuPower,
            90f / denominator);

        AddRawOutcome(
            result,
            data.ChinchiroBlankPower,
            108f / denominator);

        NormalizeRawOutcomes(result);
        return result;
    }

    private static List<RawOutcome> BuildLegacyCoinOutcomes(
        SkillDefinition definition,
        int basePower)
    {
        int count =
            Mathf.Max(
                0,
                definition.CoinCount);

        float frontChance =
            Mathf.Clamp01(
                definition.CoinFrontChance);

        List<RawOutcome> result =
            new List<RawOutcome>();

        if (count <= 0)
        {
            result.Add(
                new RawOutcome(
                    basePower,
                    1f));

            return result;
        }

        for (int fronts = 0;
             fronts <= count;
             fronts++)
        {
            float probability =
                Combination(
                    count,
                    fronts) *
                Mathf.Pow(
                    frontChance,
                    fronts) *
                Mathf.Pow(
                    1f - frontChance,
                    count - fronts);

            int value =
                basePower +
                fronts *
                definition.CoinFrontValue +
                (count - fronts) *
                definition.CoinBackValue;

            AddRawOutcome(
                result,
                value,
                probability);
        }

        NormalizeRawOutcomes(result);
        return result;
    }

    private static List<RawOutcome> BuildLegacyChinchiroOutcomes(
        SkillDefinition definition,
        int basePower)
    {
        Dictionary<int, int> counts =
            new Dictionary<int, int>();

        for (int a = 1; a <= 6; a++)
        {
            for (int b = 1; b <= 6; b++)
            {
                for (int c = 1; c <= 6; c++)
                {
                    int[] values =
                    {
                        a,
                        b,
                        c
                    };

                    Array.Sort(values);

                    int value;

                    if (a == b &&
                        b == c)
                    {
                        value =
                            definition
                                .ChinchiroArashiBonus;
                    }
                    else if (values[0] == 4 &&
                             values[1] == 5 &&
                             values[2] == 6)
                    {
                        value =
                            definition
                                .ChinchiroShigoroBonus;
                    }
                    else if (values[0] == 1 &&
                             values[1] == 2 &&
                             values[2] == 3)
                    {
                        value =
                            -definition
                                .ChinchiroHifumiPenalty;
                    }
                    else if (a == b ||
                             a == c ||
                             b == c)
                    {
                        value =
                            a == b ||
                            a == c
                                ? a
                                : b;
                    }
                    else
                    {
                        value =
                            values[0];
                    }

                    int total =
                        basePower +
                        value;

                    if (!counts.ContainsKey(total))
                        counts[total] = 0;

                    counts[total]++;
                }
            }
        }

        List<RawOutcome> result =
            new List<RawOutcome>();

        foreach (KeyValuePair<int, int> pair
                 in counts)
        {
            result.Add(
                new RawOutcome(
                    pair.Key,
                    pair.Value / 216f));
        }

        NormalizeRawOutcomes(result);
        return result;
    }

    public float EstimateOneSidedDamage(
        BattleContext context,
        Character owner,
        BodyPart ownerPart,
        int speed,
        Skill skill,
        Character target,
        BodyPart targetPart)
    {
        if (skill == null ||
            target == null)
        {
            return 0f;
        }

        float total = 0f;

        int count =
            Mathf.Max(
                1,
                skill.ExchangeRollCount);

        for (int index = 0;
             index < count;
             index++)
        {
            total +=
                EstimateOneSidedRollDamage(
                    skill,
                    owner,
                    speed,
                    index);
        }

        return total *
               GetDamageVulnerabilityMultiplier(
                   target,
                   targetPart,
                   skill);
    }

    private static float EstimateOneSidedRollDamage(
        Skill skill,
        Character owner,
        int speed,
        int rollIndex)
    {
        if (skill == null ||
            skill.GetRollType(rollIndex) ==
            CombatRollType.Defense)
        {
            return 0f;
        }

        List<RawOutcome> outcomes =
            BuildRawOutcomes(
                skill,
                rollIndex);

        float result = 0f;

        foreach (RawOutcome outcome
                 in outcomes)
        {
            result +=
                Mathf.Max(
                    1,
                    outcome.Power) *
                outcome.Probability;
        }

        return result;
    }

    private static float GetDamageVulnerabilityMultiplier(
        Character target,
        BodyPart part,
        Skill skill)
    {
        float multiplier = 1f;

        if (part != null)
        {
            if (part.IsBroken)
            {
                multiplier += 0.30f;
            }
            else if (part.IsWeakened)
            {
                // 약화 부위 타격은 이번 공격에서 HP 피해를 만들지 않는다.
                return 0f;
            }
        }

        float hpRate;

        if (part != null &&
            part.MaxPartHP > 0f)
        {
            hpRate =
                Mathf.Clamp01(
                    part.PartHP /
                    part.MaxPartHP);
        }
        else
        {
            hpRate =
                target.MaxCombatHP > 0
                    ? Mathf.Clamp01(
                        (float)target.CurrentHP /
                        target.MaxCombatHP)
                    : 1f;
        }

        multiplier +=
            (1f - hpRate) *
            0.15f;

        return multiplier;
    }

    public float ScoreTargetVulnerability(
        Character target,
        BodyPart targetPart,
        Skill skill)
    {
        if (target == null)
            return 0f;

        float score = 0f;

        if (targetPart != null)
        {
            if (targetPart.IsBroken)
                score += 140f;
            else if (targetPart.IsWeakened)
                score += skill?.CanBreakPart == true
                    ? 260f
                    : -200f;

            if (targetPart.MaxPartHP > 0f)
            {
                float hpRate =
                    Mathf.Clamp01(
                        targetPart.PartHP /
                        targetPart.MaxPartHP);

                score +=
                    (1f - hpRate) *
                    90f;
            }
        }
        else if (target.MaxCombatHP > 0)
        {
            float hpRate =
                Mathf.Clamp01(
                    (float)target.CurrentHP /
                    target.MaxCombatHP);

            score +=
                (1f - hpRate) *
                110f;
        }

        return score;
    }

    private static void AddProbability(
        Dictionary<int, float> destination,
        int key,
        float probability)
    {
        if (destination == null ||
            probability <= 0f)
        {
            return;
        }

        if (!destination.ContainsKey(key))
            destination[key] = 0f;

        destination[key] +=
            probability;
    }

    private static void AddRawOutcome(
        List<RawOutcome> destination,
        int power,
        float probability)
    {
        if (destination == null ||
            probability <= 0f)
        {
            return;
        }

        for (int index = 0;
             index < destination.Count;
             index++)
        {
            RawOutcome existing =
                destination[index];

            if (existing.Power != power)
                continue;

            destination[index] =
                new RawOutcome(
                    power,
                    existing.Probability +
                    probability);

            return;
        }

        destination.Add(
            new RawOutcome(
                power,
                probability));
    }

    private static void NormalizeRawOutcomes(
        List<RawOutcome> outcomes)
    {
        if (outcomes == null ||
            outcomes.Count == 0)
        {
            return;
        }

        float total = 0f;

        foreach (RawOutcome outcome
                 in outcomes)
        {
            total +=
                Mathf.Max(
                    0f,
                    outcome.Probability);
        }

        if (total <= 0f)
            return;

        for (int index = 0;
             index < outcomes.Count;
             index++)
        {
            RawOutcome outcome =
                outcomes[index];

            outcomes[index] =
                new RawOutcome(
                    outcome.Power,
                    outcome.Probability / total);
        }
    }

    private static void NormalizePowerOutcomes(
        List<PowerOutcome> outcomes)
    {
        if (outcomes == null ||
            outcomes.Count == 0)
        {
            return;
        }

        float total = 0f;

        foreach (PowerOutcome outcome
                 in outcomes)
        {
            total +=
                Mathf.Max(
                    0f,
                    outcome.Probability);
        }

        if (total <= 0f)
            return;

        for (int index = 0;
             index < outcomes.Count;
             index++)
        {
            PowerOutcome outcome =
                outcomes[index];

            outcomes[index] =
                new PowerOutcome(
                    outcome.RawPower,
                    outcome.ClashPower,
                    outcome.Probability / total,
                    outcome.RollType);
        }
    }

    private static float Combination(
        int n,
        int k)
    {
        if (k < 0 ||
            k > n)
        {
            return 0f;
        }

        k =
            Mathf.Min(
                k,
                n - k);

        double value = 1d;

        for (int index = 1;
             index <= k;
             index++)
        {
            value *=
                (double)(n - k + index) /
                index;
        }

        return (float)value;
    }
}
