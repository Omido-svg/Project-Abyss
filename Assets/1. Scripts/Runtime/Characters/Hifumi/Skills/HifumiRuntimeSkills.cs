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
    // §4.4 · §19.3 대실패의 친치로 값(대박 +18과 대칭, 11-19). 합 · 일방 · 반격 굴림 모두
    // 이 값으로 정상 판정 · 타격한다 — 합에서는 웬만하면 지고, 피해는 최소 1(피해 처리에서).
    public const int CatastropheValue = -18;
    // §19.3 반격 굴림의 아라시 · 시고로 값(합 굴림은 JackpotBonus 18).
    public const int DuelCounterJackpot = 18;
    public const int NormalCounterJackpot = 10;

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
                // §19.8 속임수 — 시고로(4·5·6) 고정(11-19, 구 아라시).
                a = 4;
                b = 5;
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
        int basePower,
        int jackpotBonus = JackpotBonus)
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
                // §19.8 속임수 — 시고로(4·5·6) 고정(11-19, 구 아라시).
                a = 4;
                b = 5;
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
            c,
            jackpotBonus);
    }

    /// <summary>
    /// §19.3 강화 반격 굴림 — 친치로를 굴리지 않고 아라시로 확정. 눈은 1~6 무작위.
    /// 속임수 등 결과 고정(ChinchiroOutcomeOverride)은 걸리지 않는다.
    /// </summary>
    public static RollResult RollEnhancedCounter(
        int basePower,
        int jackpotBonus)
    {
        int eye = UnityEngine.Random.Range(1, 7);
        return BuildResultCore(
            Mathf.Max(0, basePower),
            null,
            0,
            eye,
            eye,
            eye,
            jackpotBonus);
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
        int c,
        int jackpotBonus = JackpotBonus)
    {
        int[] sorted = { a, b, c };
        Array.Sort(sorted);

        ChinchiroCombination combination;
        int value;

        if (a == b && b == c)
        {
            combination = ChinchiroCombination.Arashi;
            value = jackpotBonus;
        }
        else if (sorted[0] == 4 &&
                 sorted[1] == 5 &&
                 sorted[2] == 6)
        {
            combination = ChinchiroCombination.Shigoro;
            value = jackpotBonus;
        }
        else if (sorted[0] == 1 &&
                 sorted[1] == 2 &&
                 sorted[2] == 3)
        {
            combination = ChinchiroCombination.Hifumi;
            // §19.3(11-19): 대실패는 어디서든 친치로 값 −18로 정상 판정한다(강제 패배 없음).
            value = CatastropheValue;
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

/// <summary>
/// §19.6 평타③ 동귀어진 — 굴림 없음 · 합 없음 · 태그 없음.
/// 사용시 조준한 상대 부위와 자신(이 카드를 쓴 슬롯 부위)에 같은 값 N의 고정 피해(§10.2).
/// 자기 피해는 실제 피해라 피격 적립 · 핏값이 그대로 붙는다. 파괴 권한 없음(약화까지만).
/// </summary>
public sealed class HifumiMutualDestructionRuntimeSkill : DataNormalSkill, IRollessResolutionSkill
{
    public HifumiMutualDestructionRuntimeSkill(SkillDefinition definition) : base(definition) { }

    // 이 슬롯을 노린 적 공격은 받아 주지 못하고 맞는다(회피와 같은 처리).
    public override bool CanClash => false;

    public void ResolveWithoutRolls(BattleAction action, BattleContext battleContext)
    {
        Character self = action?.Owner;
        DamageManager damage = battleContext?.Services?.DamageManager;
        if (self == null || self.IsDead || damage == null)
            return;

        int amount = self is Hifumi hifumi
            ? hifumi.HifumiMechanic?.CardRules.MutualDestructionFixedDamage ?? 0
            : 0;
        if (amount <= 0)
            return;

        Character target = action.Target;
        BodyPart targetPart = action.TargetPart;
        bool hitTarget = target != null && !target.IsDead && target != self;
        if (targetPart != null && (targetPart.IsBroken || targetPart.Owner != target))
            targetPart = null;

        BodyPart ownPart = action.OwnerPart;
        if (ownPart != null && ownPart.IsBroken)
            ownPart = null;

        // 상대가 먼저 맞고 그다음 히후미가 맞는다(11-18 사용자).
        if (hitTarget)
        {
            damage.ApplyDamageContext(
                DamageRequest.Fixed(self, target, targetPart, amount, action));
        }

        // 자기 피해는 전체 체력 1 아래로 내리지 않는다 — 막힌 만큼 부위 · 전체 피해 모두 줄고,
        // 뼈는 실제 받은 피해 + 핏값 등 피격 적립 보너스. 0이 되면 핏값만 받는다(11-18 사용자).
        int selfAmount = HifumiNativeRules.SelfDamageAboveFloor(self, amount);
        if (selfAmount > 0)
        {
            damage.ApplyDamageContext(
                DamageRequest.Fixed(self, self, ownPart, selfAmount, action));
        }
        else if (self is Hifumi owner)
        {
            owner.HifumiMechanic?.GrantBloodPriceOnlyReceivedHit(
                action.GetEffectCause(ProjectAbyss.Abilities.AbilityEventKind.RollUsing, action.CurrentRollIndex));
        }
    }
}

public sealed class HifumiDuelRuntimeSkill : DataDuelSkill
{
    public HifumiDuelRuntimeSkill(SkillDefinition definition) : base(definition) { }
    public override SkillRollReusePolicy RollReusePolicy => SkillRollReusePolicy.RollEachExchange;

    public override int ExchangeRollCount
    {
        get
        {
            if (Definition?.SkillId == HifumiSkillIds.EngraveBody)
            {
                return Owner?.GetMechanic<HifumiMechanic>()?.EngraveRollCount ??
                       base.ExchangeRollCount;
            }

            return base.ExchangeRollCount;
        }
    }

    public override int EnergyCost
    {
        get
        {
            HifumiMechanic mechanic =
                Owner?.GetMechanic<HifumiMechanic>();

            if (Definition?.SkillId == HifumiSkillIds.EngraveBody)
                return mechanic?.EngraveEnergyCost ?? base.EnergyCost;

            if (Definition?.SkillId == HifumiSkillIds.RaiseStake)
                return mechanic?.ResolveRaiseStakeEnergyCost(base.EnergyCost) ?? base.EnergyCost;

            return base.EnergyCost;
        }
    }

    public override bool CanUseByResource(Character character)
    {
        HifumiMechanic mechanic =
            Owner?.GetMechanic<HifumiMechanic>();

        if (Definition?.SkillId == HifumiSkillIds.RaiseStake &&
            mechanic?.RaiseStakeLocked == true)
        {
            return false;
        }

        if (HifumiSkillIds.HasPendingCanonicalRuntimeNumbers(Definition?.SkillId))
            return false;

        return base.CanUseByResource(character);
    }

    public override RollResult RollPowerResultForExchange(int exchangeIndex)
    {
        HifumiMechanic mechanic =
            Owner?.GetMechanic<HifumiMechanic>();

        int adjustment =
            mechanic?.ResolveDuelBasePowerAdjustment(
                Definition?.SkillId) ?? 0;

        RollResult result = HifumiChinchiroRuntime.Roll(
            this,
            exchangeIndex,
            adjustment);

        // 0922 H 운명을 흔들다: 1·2·3은 공통 대실패가 아니라
        // 최종 판정값 0으로 정상 비교한다.
        if (Definition?.SkillId == HifumiSkillIds.ShakeFate &&
            result?.ChinchiroCombination == ChinchiroCombination.Hifumi)
        {
            result.BasePower = 0;
            result.RawValue = 0;
            result.ModifiedValue = 0;
            result.ExternalModifier = 0;
            result.JudgmentModifier = 0;
            result.RecalculateFinalPower();
        }

        return result;
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
