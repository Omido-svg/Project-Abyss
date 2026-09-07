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
                enemySlot.Skill,
                playerTargetPart: null,
                enemyTargetPart: enemySlot.TargetPart);

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
        Skill enemySkill,
        BodyPart playerTargetPart = null,
        BodyPart enemyTargetPart = null)
    {
        if (playerSkill == null ||
            enemySkill == null)
        {
            return new ClashEstimate(
                0f,
                0f);
        }

        BattleAction playerPreview =
            CreatePreviewAction(
                playerOwner,
                playerPart,
                playerSpeed,
                playerSkill,
                enemyOwner,
                playerTargetPart,
                CombatRollType.Attack,
                0);

        BattleAction enemyPreview =
            CreatePreviewAction(
                enemyOwner,
                enemyPart,
                enemySpeed,
                enemySkill,
                playerOwner,
                enemyTargetPart,
                CombatRollType.Attack,
                0);

        int playerRollCount =
            playerPreview?.GetEffectiveExchangeRollCount() ??
            Mathf.Max(1, playerSkill.ExchangeRollCount);

        int enemyRollCount =
            enemyPreview?.GetEffectiveExchangeRollCount() ??
            Mathf.Max(1, enemySkill.ExchangeRollCount);

        int paired =
            Mathf.Min(
                playerRollCount,
                enemyRollCount);

        int playerExtra =
            Mathf.Max(
                0,
                playerRollCount - paired);

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
                    context,
                    playerOwner,
                    playerPart,
                    playerSpeed,
                    playerSkill,
                    enemyOwner,
                    enemyPart,
                    enemySpeed,
                    enemySkill,
                    playerTargetPart,
                    enemyTargetPart,
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
                    context,
                    playerSkill,
                    playerOwner,
                    playerPart,
                    playerSpeed,
                    enemyOwner,
                    playerTargetPart,
                    index);
        }

        float practicalWinRate =
            Mathf.Clamp01(
                finalWin +
                finalDraw * 0.5f);

        damage =
            ApplyAggregateDamageLimits(
                damage,
                enemyOwner,
                playerTargetPart);

        return new ClashEstimate(
            practicalWinRate,
            Mathf.Max(0f, damage));
    }

    private ExchangeEstimate EstimateExchange(
        BattleContext context,
        Character playerOwner,
        BodyPart playerPart,
        int playerSpeed,
        Skill playerSkill,
        Character enemyOwner,
        BodyPart enemyPart,
        int enemySpeed,
        Skill enemySkill,
        BodyPart playerTargetPart,
        BodyPart enemyTargetPart,
        int rollIndex,
        int maxTieRerolls,
        int speedWeight)
    {
        List<PowerOutcome> playerOutcomes =
            BuildPowerOutcomes(
                playerOwner,
                playerPart,
                playerSpeed,
                enemyOwner,
                playerTargetPart,
                enemySpeed,
                playerSkill,
                rollIndex,
                speedWeight);

        List<PowerOutcome> enemyOutcomes =
            BuildPowerOutcomes(
                enemyOwner,
                enemyPart,
                enemySpeed,
                playerOwner,
                enemyTargetPart,
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
                        float damage =
                            EstimateResolvedDamage(
                                context,
                                playerOwner,
                                playerPart,
                                playerSpeed,
                                playerSkill,
                                enemyOwner,
                                playerTargetPart,
                                player.RawPower,
                                rollIndex,
                                isClashDamage: true);

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
        BodyPart ownerPart,
        int selfSpeed,
        Character target,
        BodyPart targetPart,
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

        BattleAction preview =
            CreatePreviewAction(
                owner,
                ownerPart,
                selfSpeed,
                skill,
                target,
                targetPart,
                rollType,
                rollIndex);

        foreach (RawOutcome outcome in raw)
        {
            int judgedPower =
                owner != null
                    ? owner.ModifyRoll(
                        preview,
                        outcome.Power)
                    : outcome.Power;

            int runtimeJudgmentModifier =
                judgedPower -
                outcome.Power;

            int clash =
                outcome.Power +
                judgment +
                runtimeJudgmentModifier +
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

        BattleAction preview =
            CreatePreviewAction(
                owner,
                ownerPart,
                speed,
                skill,
                target,
                targetPart,
                CombatRollType.Attack,
                0);

        int count =
            preview?.GetEffectiveExchangeRollCount() ??
            Mathf.Max(
                1,
                skill.ExchangeRollCount);

        for (int index = 0;
             index < count;
             index++)
        {
            total +=
                EstimateOneSidedRollDamage(
                    context,
                    skill,
                    owner,
                    ownerPart,
                    speed,
                    target,
                    targetPart,
                    index);
        }

        return ApplyAggregateDamageLimits(
            total,
            target,
            targetPart);
    }

    private static float EstimateOneSidedRollDamage(
        BattleContext context,
        Skill skill,
        Character owner,
        BodyPart ownerPart,
        int speed,
        Character target,
        BodyPart targetPart,
        int rollIndex)
    {
        if (skill == null ||
            target == null ||
            skill.GetRollType(rollIndex) ==
            CombatRollType.Stagger)
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
            float damage =
                EstimateResolvedDamage(
                    context,
                    owner,
                    ownerPart,
                    speed,
                    skill,
                    target,
                    targetPart,
                    outcome.Power,
                    rollIndex,
                    isClashDamage: false);

            result +=
                damage *
                outcome.Probability;
        }

        return Mathf.Max(
            0f,
            result);
    }

    private static BattleAction CreatePreviewAction(
        Character owner,
        BodyPart ownerPart,
        int speed,
        Skill skill,
        Character target,
        BodyPart targetPart,
        CombatRollType rollType,
        int rollIndex)
    {
        if (owner == null ||
            skill == null)
        {
            return null;
        }

        ActionSlot slot =
            new ActionSlot
            {
                Owner = owner,
                Part = ownerPart,
                Skill = skill,
                Speed = speed,
                ActionIndex = 0,
                Phase = ActionPhase.COMBAT,
                TargetCharacter = target,
                TargetPart = targetPart
            };

        return new BattleAction
        {
            Slot = slot,
            CurrentRollType = rollType,
            CurrentRollIndex = Mathf.Max(0, rollIndex)
        };
    }

    /// <summary>
    /// 실제 DamagePipeline의 계산 단계만 재사용한다.
    /// HP/부위/가드 값을 읽기만 하고 Apply 단계는 호출하지 않으므로
    /// 자동계획 중 전투 상태를 변경하지 않는다.
    /// </summary>
    private static float EstimateResolvedDamage(
        BattleContext context,
        Character owner,
        BodyPart ownerPart,
        int speed,
        Skill skill,
        Character target,
        BodyPart targetPart,
        int rawPower,
        int rollIndex,
        bool isClashDamage)
    {
        if (owner == null ||
            skill == null ||
            target == null ||
            rawPower <= 0 ||
            skill.GetRollType(rollIndex) ==
            CombatRollType.Stagger)
        {
            return 0f;
        }

        BattleAction action =
            CreatePreviewAction(
                owner,
                ownerPart,
                speed,
                skill,
                target,
                targetPart,
                CombatRollType.Attack,
                rollIndex);

        if (action == null)
            return 0f;

        action.RolledPower =
            Mathf.Max(
                1,
                rawPower);

        action.finalPower =
            action.RolledPower;

        DamageType damageType =
            skill.ActionType == ActionType.Prestige
                ? DamageType.Prestige
                : targetPart == null
                    ? DamageType.Direct
                    : DamageType.SkillPart;

        bool canBreakPart =
            targetPart?.IsWeakened == true &&
            (owner.Data?.CombatantTier == CombatantTier.Boss ||
             (owner is Enemy
                 ? context?.Services?.MomentumManager?
                       .CanStandardBreakPart(owner) == true
                 : skill.CanBreakPart));

        DamageRequest request =
            DamageRequest.FromAction(
                action,
                damageType,
                canBreakPart,
                isClashDamage,
                targetLostClash: isClashDamage);

        request.Damage =
            action.RolledPower;

        request.RawPower =
            action.RolledPower;

        // 가드는 행동 전체에 걸쳐 소모되는 상태다.
        // 굴림별 기대값마다 현재 Guard를 반복 차감하지 않고
        // 행동 단위 기대 피해를 합친 뒤 한 번만 적용한다.
        request.ApplyGuard = false;

        DamageContext damageContext =
            new DamageContext(
                request);

        DamagePipeline pipeline =
            new DamagePipeline(
                context?.Services?.MomentumManager);

        pipeline.Calculate(
            damageContext);

        int calculated =
            Mathf.Max(
                0,
                damageContext.FinalDamage);

        if (calculated <= 0)
            return 0f;

        // CharacterDamageController의 실제 적용 계약을 값 변경 없이 미리 계산한다.
        if (targetPart == null ||
            targetPart.IsBroken)
        {
            return Mathf.Min(
                calculated,
                Mathf.Max(0, target.CurrentHP));
        }

        if (targetPart.IsWeakened)
        {
            // 약화 타격은 이 타격에서 HP/부위 HP를 감소시키지 않고,
            // 파괴 권한이 있으면 상태만 Broken으로 전환한다.
            return 0f;
        }

        int partBefore =
            Mathf.Max(
                0,
                Mathf.CeilToInt(
                    targetPart.PartHP));

        int maximumPartDamage =
            Mathf.Max(
                0,
                partBefore - 1);

        return Mathf.Min(
            calculated,
            maximumPartDamage);
    }

    private static float ApplyAggregateDamageLimits(
        float expectedDamage,
        Character target,
        BodyPart targetPart)
    {
        if (target == null ||
            expectedDamage <= 0f)
        {
            return 0f;
        }

        float damage =
            Mathf.Max(
                0f,
                expectedDamage);

        int guard =
            target.RuntimeStatus != null
                ? Mathf.Max(
                    0,
                    target.RuntimeStatus.currentBlock)
                : 0;

        damage =
            Mathf.Max(
                0f,
                damage - guard);

        if (targetPart == null ||
            targetPart.IsBroken)
        {
            return Mathf.Min(
                damage,
                Mathf.Max(0, target.CurrentHP));
        }

        if (targetPart.IsWeakened)
            return 0f;

        int partBefore =
            Mathf.Max(
                0,
                Mathf.CeilToInt(
                    targetPart.PartHP));

        int maximumPartDamage =
            Mathf.Max(
                0,
                partBefore - 1);

        return Mathf.Min(
            damage,
            maximumPartDamage);
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
