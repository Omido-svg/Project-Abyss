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
            CombatRollType rollType,
            ForcedRollJudgmentDirective forcedJudgment =
                ForcedRollJudgmentDirective.None,
            bool forcedFailure = false)
        {
            RawPower = rawPower;
            ClashPower = clashPower;
            Probability = probability;
            RollType = rollType;
            ForcedJudgment = forcedJudgment;
            ForcedFailure = forcedFailure;
        }

        public int RawPower { get; }
        public int ClashPower { get; }
        public float Probability { get; }
        public CombatRollType RollType { get; }
        public ForcedRollJudgmentDirective ForcedJudgment { get; }
        public bool ForcedFailure { get; }
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

    private readonly struct RawOutcomeCacheKey :
        IEquatable<RawOutcomeCacheKey>
    {
        public RawOutcomeCacheKey(
            Character owner,
            Skill skill,
            int rollIndex)
        {
            Owner = owner;
            Skill = skill;
            RollIndex = rollIndex;
        }

        private Character Owner { get; }
        private Skill Skill { get; }
        private int RollIndex { get; }

        public bool Equals(
            RawOutcomeCacheKey other) =>
            ReferenceEquals(Owner, other.Owner) &&
            ReferenceEquals(Skill, other.Skill) &&
            RollIndex == other.RollIndex;

        public override bool Equals(object obj) =>
            obj is RawOutcomeCacheKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RefHash(Owner);
                hash = hash * 397 ^ RefHash(Skill);
                hash = hash * 397 ^ RollIndex;
                return hash;
            }
        }
    }

    private readonly struct PowerOutcomeCacheKey :
        IEquatable<PowerOutcomeCacheKey>
    {
        public PowerOutcomeCacheKey(
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
            Owner = owner;
            OwnerPart = ownerPart;
            SelfSpeed = selfSpeed;
            Target = target;
            TargetPart = targetPart;
            OpponentSpeed = opponentSpeed;
            Skill = skill;
            RollIndex = rollIndex;
            SpeedWeight = speedWeight;
        }

        private Character Owner { get; }
        private BodyPart OwnerPart { get; }
        private int SelfSpeed { get; }
        private Character Target { get; }
        private BodyPart TargetPart { get; }
        private int OpponentSpeed { get; }
        private Skill Skill { get; }
        private int RollIndex { get; }
        private int SpeedWeight { get; }

        public bool Equals(
            PowerOutcomeCacheKey other) =>
            ReferenceEquals(Owner, other.Owner) &&
            ReferenceEquals(OwnerPart, other.OwnerPart) &&
            SelfSpeed == other.SelfSpeed &&
            ReferenceEquals(Target, other.Target) &&
            ReferenceEquals(TargetPart, other.TargetPart) &&
            OpponentSpeed == other.OpponentSpeed &&
            ReferenceEquals(Skill, other.Skill) &&
            RollIndex == other.RollIndex &&
            SpeedWeight == other.SpeedWeight;

        public override bool Equals(object obj) =>
            obj is PowerOutcomeCacheKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RefHash(Owner);
                hash = hash * 397 ^ RefHash(OwnerPart);
                hash = hash * 397 ^ SelfSpeed;
                hash = hash * 397 ^ RefHash(Target);
                hash = hash * 397 ^ RefHash(TargetPart);
                hash = hash * 397 ^ OpponentSpeed;
                hash = hash * 397 ^ RefHash(Skill);
                hash = hash * 397 ^ RollIndex;
                hash = hash * 397 ^ SpeedWeight;
                return hash;
            }
        }
    }

    private readonly struct DamageEstimateCacheKey :
        IEquatable<DamageEstimateCacheKey>
    {
        public DamageEstimateCacheKey(
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
            Owner = owner;
            OwnerPart = ownerPart;
            Speed = speed;
            Skill = skill;
            Target = target;
            TargetPart = targetPart;
            RawPower = rawPower;
            RollIndex = rollIndex;
            IsClashDamage = isClashDamage;
        }

        private Character Owner { get; }
        private BodyPart OwnerPart { get; }
        private int Speed { get; }
        private Skill Skill { get; }
        private Character Target { get; }
        private BodyPart TargetPart { get; }
        private int RawPower { get; }
        private int RollIndex { get; }
        private bool IsClashDamage { get; }

        public bool Equals(
            DamageEstimateCacheKey other) =>
            ReferenceEquals(Owner, other.Owner) &&
            ReferenceEquals(OwnerPart, other.OwnerPart) &&
            Speed == other.Speed &&
            ReferenceEquals(Skill, other.Skill) &&
            ReferenceEquals(Target, other.Target) &&
            ReferenceEquals(TargetPart, other.TargetPart) &&
            RawPower == other.RawPower &&
            RollIndex == other.RollIndex &&
            IsClashDamage == other.IsClashDamage;

        public override bool Equals(object obj) =>
            obj is DamageEstimateCacheKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RefHash(Owner);
                hash = hash * 397 ^ RefHash(OwnerPart);
                hash = hash * 397 ^ Speed;
                hash = hash * 397 ^ RefHash(Skill);
                hash = hash * 397 ^ RefHash(Target);
                hash = hash * 397 ^ RefHash(TargetPart);
                hash = hash * 397 ^ RawPower;
                hash = hash * 397 ^ RollIndex;
                hash = hash * 397 ^ (IsClashDamage ? 1 : 0);
                return hash;
            }
        }
    }

    private readonly struct RollCountCacheKey :
        IEquatable<RollCountCacheKey>
    {
        public RollCountCacheKey(
            Character owner,
            BodyPart ownerPart,
            int speed,
            Skill skill,
            Character target,
            BodyPart targetPart)
        {
            Owner = owner;
            OwnerPart = ownerPart;
            Speed = speed;
            Skill = skill;
            Target = target;
            TargetPart = targetPart;
        }

        private Character Owner { get; }
        private BodyPart OwnerPart { get; }
        private int Speed { get; }
        private Skill Skill { get; }
        private Character Target { get; }
        private BodyPart TargetPart { get; }

        public bool Equals(
            RollCountCacheKey other) =>
            ReferenceEquals(Owner, other.Owner) &&
            ReferenceEquals(OwnerPart, other.OwnerPart) &&
            Speed == other.Speed &&
            ReferenceEquals(Skill, other.Skill) &&
            ReferenceEquals(Target, other.Target) &&
            ReferenceEquals(TargetPart, other.TargetPart);

        public override bool Equals(object obj) =>
            obj is RollCountCacheKey other &&
            Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RefHash(Owner);
                hash = hash * 397 ^ RefHash(OwnerPart);
                hash = hash * 397 ^ Speed;
                hash = hash * 397 ^ RefHash(Skill);
                hash = hash * 397 ^ RefHash(Target);
                hash = hash * 397 ^ RefHash(TargetPart);
                return hash;
            }
        }
    }

    private readonly Dictionary<RawOutcomeCacheKey, List<RawOutcome>>
        rawOutcomeCache =
            new Dictionary<RawOutcomeCacheKey, List<RawOutcome>>(128);

    private readonly Dictionary<PowerOutcomeCacheKey, List<PowerOutcome>>
        powerOutcomeCache =
            new Dictionary<PowerOutcomeCacheKey, List<PowerOutcome>>(256);

    private readonly Dictionary<DamageEstimateCacheKey, float>
        damageEstimateCache =
            new Dictionary<DamageEstimateCacheKey, float>(1024);

    private readonly Dictionary<RollCountCacheKey, int>
        rollCountCache =
            new Dictionary<RollCountCacheKey, int>(128);

    private float[] differenceDistributionA =
        Array.Empty<float>();

    private float[] differenceDistributionB =
        Array.Empty<float>();

    private bool evaluationSessionActive;
    private DamagePipeline previewDamagePipeline;

    public int RawOutcomeCacheHits { get; private set; }
    public int RawOutcomeCacheMisses { get; private set; }
    public int PowerOutcomeCacheHits { get; private set; }
    public int PowerOutcomeCacheMisses { get; private set; }
    public int DamageEstimateCacheHits { get; private set; }
    public int DamageEstimateCacheMisses { get; private set; }
    public int RollCountCacheHits { get; private set; }
    public int RollCountCacheMisses { get; private set; }

    // Prediction hooks only inspect the current roll synchronously.
    // Reuse one fully-reset RollResult instead of allocating RollResult +
    // three internal Lists for every probability outcome.
    private readonly RollResult predictionRollScratch =
        new RollResult();

    public void BeginEvaluationSession(
        BattleContext context)
    {
        rawOutcomeCache.Clear();
        powerOutcomeCache.Clear();
        damageEstimateCache.Clear();
        rollCountCache.Clear();

        RawOutcomeCacheHits = 0;
        RawOutcomeCacheMisses = 0;
        PowerOutcomeCacheHits = 0;
        PowerOutcomeCacheMisses = 0;
        DamageEstimateCacheHits = 0;
        DamageEstimateCacheMisses = 0;
        RollCountCacheHits = 0;
        RollCountCacheMisses = 0;

        previewDamagePipeline =
            new DamagePipeline(
                context?.Services?.MomentumManager);

        evaluationSessionActive = true;
    }

    public void EndEvaluationSession()
    {
        evaluationSessionActive = false;
        previewDamagePipeline = null;
    }

    private static int RefHash(
        object value) =>
        value == null
            ? 0
            : System.Runtime.CompilerServices
                .RuntimeHelpers.GetHashCode(value);

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

        int playerRollCount =
            GetEffectiveExchangeRollCount(
                playerOwner,
                playerPart,
                playerSpeed,
                playerSkill,
                enemyOwner,
                playerTargetPart);

        int enemyRollCount =
            GetEffectiveExchangeRollCount(
                enemyOwner,
                enemyPart,
                enemySpeed,
                enemySkill,
                playerOwner,
                enemyTargetPart);

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

        int distributionLength =
            Mathf.Max(
                1,
                paired * 2 + 1);

        EnsureDifferenceDistributionCapacity(
            distributionLength);

        Array.Clear(
            differenceDistributionA,
            0,
            distributionLength);

        Array.Clear(
            differenceDistributionB,
            0,
            distributionLength);

        int center =
            paired;

        differenceDistributionA[center] =
            1f;

        float[] currentDistribution =
            differenceDistributionA;

        float[] nextDistribution =
            differenceDistributionB;

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

            Array.Clear(
                nextDistribution,
                0,
                distributionLength);

            for (int difference = -rollIndex;
                 difference <= rollIndex;
                 difference++)
            {
                int sourceIndex =
                    center + difference;

                float probability =
                    currentDistribution[sourceIndex];

                if (probability <= 0f)
                    continue;

                nextDistribution[sourceIndex + 1] +=
                    probability * exchange.Win;

                nextDistribution[sourceIndex - 1] +=
                    probability * exchange.Loss;

                nextDistribution[sourceIndex] +=
                    probability * exchange.Draw;
            }

            float[] swap =
                currentDistribution;

            currentDistribution =
                nextDistribution;

            nextDistribution =
                swap;
        }

        float finalWin = 0f;

        for (int difference = 1;
             difference <= paired;
             difference++)
        {
            finalWin +=
                currentDistribution[
                    center + difference];
        }

        float finalDraw =
            currentDistribution[center];

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
            GetPowerOutcomesCached(
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
            GetPowerOutcomesCached(
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
            float enemyWinMass = 0f;
            float enemyLossMass = 0f;
            float enemyTieMass = 0f;

            foreach (PowerOutcome enemy
                     in enemyOutcomes)
            {
                int comparison =
                    ResolveForcedAwareComparison(
                        player,
                        enemy);

                if (comparison > 0)
                {
                    enemyWinMass +=
                        enemy.Probability;
                }
                else if (comparison < 0)
                {
                    enemyLossMass +=
                        enemy.Probability;
                }
                else
                {
                    enemyTieMass +=
                        enemy.Probability;
                }
            }

            float playerProbability =
                player.Probability;

            float playerWinMass =
                playerProbability *
                enemyWinMass;

            attemptWin +=
                playerWinMass;

            attemptLoss +=
                playerProbability *
                enemyLossMass;

            attemptTie +=
                playerProbability *
                enemyTieMass;

            // DamagePipeline 결과는 같은 player RawPower에 대해
            // 어떤 enemy outcome을 이겼는지와 무관하다.
            // 기존 중첩 loop는 동일 damage를 enemy outcome 수만큼 반복 계산했다.
            // 승리 확률 질량을 먼저 합친 뒤 정확히 한 번 계산한다.
            if (playerWinMass > 0f &&
                player.RollType ==
                    CombatRollType.Attack)
            {
                float resolvedDamage =
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
                    playerWinMass *
                    resolvedDamage;
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

    private List<PowerOutcome> GetPowerOutcomesCached(
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
        if (!evaluationSessionActive)
        {
            return BuildPowerOutcomesCore(
                owner,
                ownerPart,
                selfSpeed,
                target,
                targetPart,
                opponentSpeed,
                skill,
                rollIndex,
                speedWeight);
        }

        PowerOutcomeCacheKey key =
            new PowerOutcomeCacheKey(
                owner,
                ownerPart,
                selfSpeed,
                target,
                targetPart,
                opponentSpeed,
                skill,
                rollIndex,
                speedWeight);

        if (powerOutcomeCache.TryGetValue(
                key,
                out List<PowerOutcome> cached))
        {
            PowerOutcomeCacheHits++;
            return cached;
        }

        PowerOutcomeCacheMisses++;

        List<PowerOutcome> result =
            BuildPowerOutcomesCore(
                owner,
                ownerPart,
                selfSpeed,
                target,
                targetPart,
                opponentSpeed,
                skill,
                rollIndex,
                speedWeight);

        powerOutcomeCache[key] =
            result;

        return result;
    }

    private List<PowerOutcome> BuildPowerOutcomesCore(
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
            GetRawOutcomesCached(
                owner,
                skill,
                rollIndex);

        List<PowerOutcome> result =
            new List<PowerOutcome>(
                raw?.Count ?? 0);

        CombatRollType rollType =
            skill?.GetRollType(rollIndex) ??
            CombatRollType.Attack;

        SkillRollData rollData =
            skill?.GetRollData(rollIndex);

        int judgment =
            rollData?.JudgmentModifier ?? 0;

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

        int speedModifier =
            ResolveCanonicalSpeedModifier(
                selfSpeed,
                opponentSpeed,
                speedWeight);

        if (Canonical0922EmotionRuntimeHooks
                .TryResolveClashSpeedModifierOverride(
                    preview,
                    out int canonicalSpeedOverride))
        {
            speedModifier = canonicalSpeedOverride;
        }

        int preparationModifier =
            owner?.TurnClashPowerBonus ?? 0;

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

            RollResult previewRoll =
                predictionRollScratch;

            PopulatePredictionRollResult(
                previewRoll,
                outcome,
                rollType,
                rollIndex);

            ForcedRollJudgmentDirective forcedJudgment =
                ResolveForcedRollJudgment(
                    owner,
                    preview,
                    previewRoll);

            bool forcedFailure =
                ResolveForcedRollFailure(
                    owner,
                    preview,
                    previewRoll);

            result.Add(
                new PowerOutcome(
                    outcome.Power,
                    clash,
                    outcome.Probability,
                    rollType,
                    forcedJudgment,
                    forcedFailure));
        }

        NormalizePowerOutcomes(result);
        return result;
    }


    private readonly struct RawOutcome
    {
        public RawOutcome(
            int power,
            float probability,
            SkillResolverType resolverType =
                SkillResolverType.Dice,
            ChinchiroCombination chinchiroCombination =
                ChinchiroCombination.None)
        {
            Power = power;
            Probability = probability;
            ResolverType = resolverType;
            ChinchiroCombination = chinchiroCombination;
        }

        public int Power { get; }
        public float Probability { get; }
        public SkillResolverType ResolverType { get; }
        public ChinchiroCombination ChinchiroCombination { get; }
    }

    private List<RawOutcome> GetRawOutcomesCached(
        Character owner,
        Skill skill,
        int rollIndex)
    {
        if (!evaluationSessionActive)
        {
            return BuildRawOutcomes(
                owner,
                skill,
                rollIndex);
        }

        RawOutcomeCacheKey key =
            new RawOutcomeCacheKey(
                owner,
                skill,
                rollIndex);

        if (rawOutcomeCache.TryGetValue(
                key,
                out List<RawOutcome> cached))
        {
            RawOutcomeCacheHits++;
            return cached;
        }

        RawOutcomeCacheMisses++;

        List<RawOutcome> result =
            BuildRawOutcomes(
                owner,
                skill,
                rollIndex);

        rawOutcomeCache[key] =
            result;

        return result;
    }

    private static List<RawOutcome> BuildRawOutcomes(
        Character owner,
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

        SkillResolverType resolverType =
            data != null
                ? ResolveRollResolverType(
                    data,
                    definition)
                : definition?.ResolverType ??
                  SkillResolverType.Dice;

        if (resolverType ==
            SkillResolverType.Chinchiro)
        {
            // Hifumi's actual runtime does not use the generic
            // SkillRollData Chinchiro power fields. It resolves
            // BasePower + canonical Chinchiro combination values and
            // may apply a forced Arashi/Hifumi outcome for Trick.
            // Prediction uses the same pure Hifumi builder so both the
            // ordinary 216-outcome distribution and forced outcomes
            // stay aligned with live combat.
            HifumiMechanic hifumi =
                owner?.GetMechanic<HifumiMechanic>();

            if (hifumi != null)
            {
                return BuildHifumiChinchiroOutcomes(
                    owner,
                    skill,
                    rollIndex,
                    hifumi);
            }

            if (TryBuildForcedGenericChinchiroOutcome(
                    owner,
                    skill,
                    data,
                    definition,
                    out List<RawOutcome> forcedGeneric))
            {
                return forcedGeneric;
            }
        }

        if (data != null)
        {
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

    private static List<RawOutcome> BuildHifumiChinchiroOutcomes(
        Character owner,
        Skill skill,
        int rollIndex,
        HifumiMechanic mechanic)
    {
        int basePowerAdjustment =
            skill?.ActionType == ActionType.Duel
                ? mechanic?.ResolveDuelBasePowerAdjustment(
                    skill.Definition?.SkillId) ?? 0
                : 0;

        int basePower =
            Mathf.Max(
                0,
                (skill?.BasePower ?? 0) +
                basePowerAdjustment);

        if (TryGetForcedChinchiro(
                owner,
                out ChinchiroCombination forced))
        {
            int a =
                forced == ChinchiroCombination.Hifumi
                    ? 1
                    : 6;
            int b =
                forced == ChinchiroCombination.Hifumi
                    ? 2
                    : 6;
            int c =
                forced == ChinchiroCombination.Hifumi
                    ? 3
                    : 6;

            RollResult forcedResult =
                HifumiChinchiroRuntime
                    .BuildResultForVerification(
                        basePower,
                        a,
                        b,
                        c);

            int forcedPower =
                ResolveHifumiPredictionPower(
                    skill,
                    forcedResult);

            return new List<RawOutcome>
            {
                new RawOutcome(
                    forcedPower,
                    1f,
                    SkillResolverType.Chinchiro,
                    forcedResult?.ChinchiroCombination ??
                    ChinchiroCombination.None)
            };
        }

        List<RawOutcome> result =
            new List<RawOutcome>();

        const float probability =
            1f / 216f;

        for (int a = 1; a <= 6; a++)
        {
            for (int b = 1; b <= 6; b++)
            {
                for (int c = 1; c <= 6; c++)
                {
                    RollResult roll =
                        HifumiChinchiroRuntime
                            .BuildResultForVerification(
                                basePower,
                                a,
                                b,
                                c);

                    AddRawOutcome(
                        result,
                        ResolveHifumiPredictionPower(
                            skill,
                            roll),
                        probability,
                        SkillResolverType.Chinchiro,
                        roll?.ChinchiroCombination ??
                        ChinchiroCombination.None);
                }
            }
        }

        NormalizeRawOutcomes(result);
        return result;
    }

    private static int ResolveHifumiPredictionPower(
        Skill skill,
        RollResult roll)
    {
        if (roll == null)
            return 0;

        // 0922 H 운명을 흔들다 overrides the common catastrophe:
        // 1·2·3 becomes power 0 and is compared normally.
        if (skill?.Definition?.SkillId ==
                HifumiSkillIds.ShakeFate &&
            roll.ChinchiroCombination ==
                ChinchiroCombination.Hifumi)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            roll.FinalPower);
    }

    private static bool TryBuildForcedGenericChinchiroOutcome(
        Character owner,
        Skill skill,
        SkillRollData data,
        SkillDefinition definition,
        out List<RawOutcome> outcomes)
    {
        outcomes = null;

        if (!TryGetForcedChinchiro(
                owner ?? skill?.Owner,
                out ChinchiroCombination forced))
        {
            return false;
        }

        ChinchiroCombination actualCombination =
            forced == ChinchiroCombination.Hifumi
                ? ChinchiroCombination.Hifumi
                : ChinchiroCombination.Arashi;

        int power;

        if (data != null &&
            data.RngSource !=
                RollRngSource.CharacterDefault)
        {
            power =
                actualCombination ==
                    ChinchiroCombination.Hifumi
                    ? data.ChinchiroHifumiPower
                    : data.ChinchiroArashiPower;
        }
        else if (definition != null &&
                 skill != null)
        {
            power =
                actualCombination ==
                    ChinchiroCombination.Hifumi
                    ? skill.BasePower -
                      definition.ChinchiroHifumiPenalty
                    : skill.BasePower +
                      definition.ChinchiroArashiBonus;
        }
        else if (data != null)
        {
            power =
                actualCombination ==
                    ChinchiroCombination.Hifumi
                    ? data.ChinchiroHifumiPower
                    : data.ChinchiroArashiPower;
        }
        else
        {
            return false;
        }

        outcomes =
            new List<RawOutcome>
            {
                new RawOutcome(
                    power,
                    1f,
                    SkillResolverType.Chinchiro,
                    actualCombination)
            };

        return true;
    }

    private static bool TryGetForcedChinchiro(
        Character owner,
        out ChinchiroCombination forced)
    {
        forced =
            ChinchiroCombination.None;

        if (!ChinchiroOutcomeOverrideResolver.TryResolve(
                owner,
                out IChinchiroOutcomeOverride outcomeOverride) ||
            outcomeOverride == null)
        {
            return false;
        }

        return outcomeOverride.TryGetForcedChinchiro(
            out forced);
    }

    private static int ResolveCanonicalSpeedModifier(
        int selfSpeed,
        int opponentSpeed,
        int speedWeight)
    {
        if (speedWeight <= 0)
            return 0;

        int speedGap =
            Mathf.Max(
                0,
                selfSpeed -
                opponentSpeed);

        if (speedGap <= 0)
            return 0;

        return speedGap >= 6
            ? 2
            : 1;
    }

    private static void PopulatePredictionRollResult(
        RollResult result,
        RawOutcome outcome,
        CombatRollType rollType,
        int rollIndex)
    {
        if (result == null)
            return;

        result.ResolverType =
            outcome.ResolverType;

        result.RollIndex =
            Mathf.Max(
                0,
                rollIndex);

        result.RollType =
            rollType;

        result.JudgmentModifier = 0;
        result.BasePower = 0;
        result.RawValue = outcome.Power;
        result.ModifiedValue = outcome.Power;
        result.ExternalModifier = 0;
        result.FinalPower = outcome.Power;
        result.SpeedModifier = 0;
        result.MomentumModifier = 0;
        result.PreparationModifier = 0;

        result.IsMax = false;
        result.IsCritical = false;
        result.WasRerolled = false;
        result.WasReused = false;
        result.DebugOverrideApplied = false;
        result.DebugSource = null;

        result.DiceMin = 0;
        result.DiceMax = 0;
        result.DiceValues?.Clear();
        result.CoinFaces?.Clear();
        result.CoinValues?.Clear();

        result.SlotA = 0;
        result.SlotB = 0;
        result.SlotValue = 0;

        result.ChinchiroCombination =
            outcome.ChinchiroCombination;

        result.ChinchiroBonus =
            outcome.ResolverType ==
            SkillResolverType.Chinchiro
                ? outcome.Power
                : 0;

        result.ChinchiroSelfDamage = 0;

        result.RecalculateClashPower();
    }

    private static ForcedRollJudgmentDirective
        ResolveForcedRollJudgment(
            Character owner,
            BattleAction action,
            RollResult roll)
    {
        if (owner?.Mechanics == null ||
            action == null ||
            roll == null)
        {
            return ForcedRollJudgmentDirective.None;
        }

        foreach (CombatMechanic mechanic
                 in owner.Mechanics)
        {
            if (mechanic is
                    IForcedRollJudgmentRule rule &&
                rule.TryGetForcedRollJudgment(
                    action,
                    roll,
                    out ForcedRollJudgmentDirective directive,
                    out _) &&
                directive !=
                    ForcedRollJudgmentDirective.None)
            {
                return directive;
            }
        }

        return ForcedRollJudgmentDirective.None;
    }

    private static bool ResolveForcedRollFailure(
        Character owner,
        BattleAction action,
        RollResult roll)
    {
        if (owner?.Mechanics == null ||
            action == null ||
            roll == null)
        {
            return false;
        }

        foreach (CombatMechanic mechanic
                 in owner.Mechanics)
        {
            if (mechanic is
                    IForcedRollFailureRule rule &&
                rule.IsForcedRollFailure(
                    action,
                    roll,
                    out _))
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveForcedAwareComparison(
        PowerOutcome player,
        PowerOutcome enemy)
    {
        bool playerForcedWin =
            player.ForcedJudgment ==
                ForcedRollJudgmentDirective.ForceWin ||
            enemy.ForcedJudgment ==
                ForcedRollJudgmentDirective.ForceLoss;

        bool enemyForcedWin =
            enemy.ForcedJudgment ==
                ForcedRollJudgmentDirective.ForceWin ||
            player.ForcedJudgment ==
                ForcedRollJudgmentDirective.ForceLoss;

        // Same contract as ClashManager: one unambiguous forced
        // judgment wins. Conflicting directives fall back to normal
        // power comparison.
        if (playerForcedWin != enemyForcedWin)
            return playerForcedWin ? 1 : -1;

        // Same contract as ClashManager: exactly one forced failure
        // loses. If both sides fail (or neither does), compare power.
        if (player.ForcedFailure != enemy.ForcedFailure)
            return player.ForcedFailure ? -1 : 1;

        if (player.ClashPower >
            enemy.ClashPower)
        {
            return 1;
        }

        if (player.ClashPower <
            enemy.ClashPower)
        {
            return -1;
        }

        return 0;
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
            6f / denominator,
            SkillResolverType.Chinchiro,
            ChinchiroCombination.Arashi);

        AddRawOutcome(
            result,
            data.ChinchiroShigoroPower,
            6f / denominator,
            SkillResolverType.Chinchiro,
            ChinchiroCombination.Shigoro);

        AddRawOutcome(
            result,
            data.ChinchiroHifumiPower,
            6f / denominator,
            SkillResolverType.Chinchiro,
            ChinchiroCombination.Hifumi);

        AddRawOutcome(
            result,
            data.ChinchiroMokuPower,
            90f / denominator,
            SkillResolverType.Chinchiro,
            ChinchiroCombination.Moku);

        AddRawOutcome(
            result,
            data.ChinchiroBlankPower,
            108f / denominator,
            SkillResolverType.Chinchiro,
            ChinchiroCombination.Blank);

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
        List<RawOutcome> result =
            new List<RawOutcome>();

        const float probability =
            1f / 216f;

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

                    ChinchiroCombination combination;
                    int value;

                    if (a == b &&
                        b == c)
                    {
                        combination =
                            ChinchiroCombination.Arashi;
                        value =
                            definition
                                .ChinchiroArashiBonus;
                    }
                    else if (values[0] == 4 &&
                             values[1] == 5 &&
                             values[2] == 6)
                    {
                        combination =
                            ChinchiroCombination.Shigoro;
                        value =
                            definition
                                .ChinchiroShigoroBonus;
                    }
                    else if (values[0] == 1 &&
                             values[1] == 2 &&
                             values[2] == 3)
                    {
                        combination =
                            ChinchiroCombination.Hifumi;
                        value =
                            -definition
                                .ChinchiroHifumiPenalty;
                    }
                    else if (a == b ||
                             a == c ||
                             b == c)
                    {
                        combination =
                            ChinchiroCombination.Moku;

                        value =
                            a == b ||
                            a == c
                                ? a
                                : b;
                    }
                    else
                    {
                        combination =
                            ChinchiroCombination.Blank;
                        value =
                            values[0];
                    }

                    AddRawOutcome(
                        result,
                        basePower + value,
                        probability,
                        SkillResolverType.Chinchiro,
                        combination);
                }
            }
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
            GetEffectiveExchangeRollCount(
                owner,
                ownerPart,
                speed,
                skill,
                target,
                targetPart);

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

    private float EstimateOneSidedRollDamage(
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
            GetRawOutcomesCached(
                owner,
                skill,
                rollIndex);

        float result = 0f;

        BattleAction predictionAction =
            CreatePreviewAction(
                owner,
                ownerPart,
                speed,
                skill,
                target,
                targetPart,
                skill.GetRollType(rollIndex),
                rollIndex);

        foreach (RawOutcome outcome
                 in outcomes)
        {
            RollResult predictionRoll =
                predictionRollScratch;

            PopulatePredictionRollResult(
                predictionRoll,
                outcome,
                skill.GetRollType(rollIndex),
                rollIndex);

            if (ResolveForcedRollFailure(
                    owner,
                    predictionAction,
                    predictionRoll))
            {
                continue;
            }

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

    private int GetEffectiveExchangeRollCount(
        Character owner,
        BodyPart ownerPart,
        int speed,
        Skill skill,
        Character target,
        BodyPart targetPart)
    {
        if (skill == null)
            return 1;

        RollCountCacheKey key =
            new RollCountCacheKey(
                owner,
                ownerPart,
                speed,
                skill,
                target,
                targetPart);

        if (evaluationSessionActive &&
            rollCountCache.TryGetValue(
                key,
                out int cached))
        {
            RollCountCacheHits++;
            return cached;
        }

        if (evaluationSessionActive)
            RollCountCacheMisses++;

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

        int result =
            preview?.GetEffectiveExchangeRollCount() ??
            Mathf.Max(
                1,
                skill.ExchangeRollCount);

        result =
            Mathf.Max(
                1,
                result);

        if (evaluationSessionActive)
        {
            rollCountCache[key] =
                result;
        }

        return result;
    }

    private void EnsureDifferenceDistributionCapacity(
        int required)
    {
        required =
            Mathf.Max(
                1,
                required);

        if (differenceDistributionA.Length <
            required)
        {
            Array.Resize(
                ref differenceDistributionA,
                required);
        }

        if (differenceDistributionB.Length <
            required)
        {
            Array.Resize(
                ref differenceDistributionB,
                required);
        }
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
    private float EstimateResolvedDamage(
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

        DamageEstimateCacheKey key =
            new DamageEstimateCacheKey(
                owner,
                ownerPart,
                speed,
                skill,
                target,
                targetPart,
                rawPower,
                rollIndex,
                isClashDamage);

        if (evaluationSessionActive &&
            damageEstimateCache.TryGetValue(
                key,
                out float cachedDamage))
        {
            DamageEstimateCacheHits++;
            return cachedDamage;
        }

        if (evaluationSessionActive)
            DamageEstimateCacheMisses++;

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

        // P0 D-01: 파괴 권한은 기세/보스 등급에서 자동 획득하지 않는다.
        // 실제 스킬이 파괴 권한을 선언한 경우에만 약화 부위 파괴를 기대값에 반영한다.
        bool canBreakPart =
            targetPart?.IsWeakened == true &&
            skill.CanBreakPart;

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

        // AutoPlan은 최종 계산값만 필요하다.
        // Damage trace용 StageSnapshot 생성은 gameplay 결과에 관여하지 않으므로
        // preview에서는 끄고 수십만 개의 snapshot allocation을 제거한다.
        DamageContext damageContext =
            new DamageContext(
                request,
                captureStageSnapshots: false);

        DamagePipeline pipeline =
            evaluationSessionActive
                ? previewDamagePipeline
                : null;

        pipeline ??=
            new DamagePipeline(
                context?.Services?.MomentumManager);

        pipeline.Calculate(
            damageContext);

        int calculated =
            Mathf.Max(
                0,
                damageContext.FinalDamage);

        float result =
            calculated <= 0
                ? 0f
                : LimitWholeHpExpectedDamage(
                    calculated,
                    target.CurrentHP,
                    guard: 0);

        if (evaluationSessionActive)
        {
            damageEstimateCache[key] =
                result;
        }

        return result;
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

        int guard =
            target.RuntimeStatus != null
                ? Mathf.Max(
                    0,
                    target.RuntimeStatus.currentBlock)
                : 0;

        // targetPart is intentionally not used for the HP ledger.
        // A normal/weakened/broken part changes Part HP/break state,
        // not the amount charged to Whole HP.
        return LimitWholeHpExpectedDamage(
            expectedDamage,
            target.CurrentHP,
            guard);
    }

    private static float LimitWholeHpExpectedDamage(
        float expectedDamage,
        int currentHp,
        int guard)
    {
        float afterGuard =
            Mathf.Max(
                0f,
                expectedDamage -
                Mathf.Max(
                    0,
                    guard));

        return Mathf.Min(
            afterGuard,
            Mathf.Max(
                0,
                currentHp));
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
        float probability,
        SkillResolverType resolverType =
            SkillResolverType.Dice,
        ChinchiroCombination chinchiroCombination =
            ChinchiroCombination.None)
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

            if (existing.Power != power ||
                existing.ResolverType != resolverType ||
                existing.ChinchiroCombination !=
                    chinchiroCombination)
            {
                continue;
            }

            destination[index] =
                new RawOutcome(
                    power,
                    existing.Probability +
                    probability,
                    resolverType,
                    chinchiroCombination);

            return;
        }

        destination.Add(
            new RawOutcome(
                power,
                probability,
                resolverType,
                chinchiroCombination));
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
                    outcome.Probability / total,
                    outcome.ResolverType,
                    outcome.ChinchiroCombination);
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
                    outcome.RollType,
                    outcome.ForcedJudgment,
                    outcome.ForcedFailure);
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
