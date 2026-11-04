using System;
using System.Collections.Generic;
using UnityEngine;

/// R7-10 independently ported native mechanics. The 33+6 per-emotion
/// algorithms are copied to an R7 type and read ONLY R7 graph data.
/// The originals remain unchanged until R7-12 cleanup.

public enum NativeEmotion33Kind
{
    CompassionImmediateHeal = 0,
    CompassionExchangeWinRecovery = 1,
    CompassionLifesteal = 2,
    CompassionLastStandRecovery = 3,
    CompassionHealingConversion = 4,
    CompassionOverhealToBlock = 5,

    FaithTurnStartBlock = 10,
    FaithEnemyAttackSlotBlock = 11,
    FaithDamageReflection = 12,
    FaithEscalatingBlockGain = 13,
    FaithFirstClashDamageNullify = 14,
    FaithTurnEndBlockToHeal = 15,
    FaithBlockConversion = 16,

    DetachmentPartMaximumHp = 20,
    DetachmentStaggerMaximum = 21,
    DetachmentSingleWeakenedPenaltySuppression = 22,
    DetachmentKillMaximumHpGrowth = 23,
    DetachmentDamageDeferral = 24,

    AweFirstTurnStrength = 30,
    AweNextTurnSwift = 31,
    AweInfiniteHeadSpeed = 32,
    AweLastStandNextTurnStrength = 33,
    AweOverwhelmNextTurnStrength = 34,
    AweInfiniteArmsSpeed = 35,

    AdmirationHitPrestige = 40,
    AdmirationTakenPrestige = 41,
    AdmirationClashPrestige = 42,
    AdmirationElapsedClashPrestige = 43,
    AdmirationPrestigeStockpile = 44,
    AdmirationPrestigeFromStagger = 45,

    LongingNeutralThreshold = 50,
    LongingNeutralTurnEndEnergy = 51
}
public enum NativeEmotionCommonKind
{
    PermanentSelfStatus = 0,
    TurnStartGuardStrength = 1,
    GoldThresholdStrength = 2,
    PrestigeTargetRuptureInfinite = 3,
    TimedHpRecovery = 4,
    TimedStaggerRecovery = 5
}
internal sealed class NativeEmotion33Mechanic : CombatMechanic,
    ICanonical0922HealingRule,
    ICanonical0922BlockGainRule,
    ICanonical0922PreGuardDamageRule,
    ICanonical0922PostGuardDamageRule,
    ICanonical0922ClashSpeedOverride,
    ICanonical0922WeakenedPenaltyRule,
    ICanonical0922MomentumBandRule,
    ICanonical0922PrestigeStockpileRule
{
    private readonly NativeEmotion33Graph definition;

    private int overhealBlockGranted;
    private int faithBlockGainBonus;
    private int faithDamageThisTurn;
    private int faithPendingReflectionBlock;
    private bool faithCompleteDefenseAvailable;
    private int faithBlockGainedThisTurn;

    private int deferredDamage;
    private int deferralDamageSeenThisTurn;
    private bool applyingDeferredDamage;

    private bool firstAweTurnPending;
    private bool firstAweSwiftPending;
    private bool pendingAweStrength;

    private int admirationElapsedTurns;
    private int originalPrestigeMaximum = -1;
    private int prestigeActivationThreshold;

    private int originalEnergyMaximum = -1;

    public override string MechanicName =>
        $"R7 Native Emotion33 · {definition?.Kind}";

    public NativeEmotion33Mechanic(
        NativeEmotion33Graph definition)
    {
        this.definition = definition;
    }

    public override void OnRegister()
    {
        if (owner == null || definition == null)
            return;

        SubscribeRelevantEvents();
        ApplyImmediateOrPersistentSetup();
    }

    public override void OnUnregister()
    {
        RestorePersistentCapacityOverrides();
        deferredDamage = 0;
        deferralDamageSeenThisTurn = 0;
        applyingDeferredDamage = false;
    }

    private void SubscribeRelevantEvents()
    {
        switch (definition.Kind)
        {
            case NativeEmotion33Kind.CompassionExchangeWinRecovery:
            case NativeEmotion33Kind.AdmirationHitPrestige:
            case NativeEmotion33Kind.AdmirationTakenPrestige:
                SubscribeToBattleEvent(
                    () => battleEvent.OnExchangeResolved += OnExchangeResolved,
                    () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
                    "OnExchangeResolved");
                break;

            case NativeEmotion33Kind.CompassionLifesteal:
            case NativeEmotion33Kind.FaithDamageReflection:
                SubscribeToBattleEvent(
                    () => battleEvent.OnDamageEventResolved += OnDamageEventResolved,
                    () => battleEvent.OnDamageEventResolved -= OnDamageEventResolved,
                    "OnDamageEventResolved");
                break;
        }

        if (NeedsTurnStart())
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnTurnStart += OnTurnStart,
                () => battleEvent.OnTurnStart -= OnTurnStart,
                "OnTurnStart");
        }

        if (NeedsTurnEnd())
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnTurnEnd += OnTurnEnd,
                () => battleEvent.OnTurnEnd -= OnTurnEnd,
                "OnTurnEnd");
        }

        if (definition.Kind ==
                NativeEmotion33Kind.DetachmentKillMaximumHpGrowth)
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnKillResolved += OnKillResolved,
                () => battleEvent.OnKillResolved -= OnKillResolved,
                "OnKillResolved");
        }

        if (definition.Kind ==
                NativeEmotion33Kind.DetachmentSingleWeakenedPenaltySuppression)
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnBodyPartWeakened += OnBodyPartStateChanged,
                () => battleEvent.OnBodyPartWeakened -= OnBodyPartStateChanged,
                "OnBodyPartWeakened");
            SubscribeToBattleEvent(
                () => battleEvent.OnBodyPartDestroyed += OnBodyPartStateChanged,
                () => battleEvent.OnBodyPartDestroyed -= OnBodyPartStateChanged,
                "OnBodyPartDestroyed");
            SubscribeToBattleEvent(
                () => battleEvent.OnBodyPartRecovered += OnBodyPartStateChanged,
                () => battleEvent.OnBodyPartRecovered -= OnBodyPartStateChanged,
                "OnBodyPartRecovered");
        }

        if (definition.Kind ==
                NativeEmotion33Kind.AdmirationClashPrestige ||
            definition.Kind ==
                NativeEmotion33Kind.AdmirationElapsedClashPrestige)
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnClashStart += OnClashStart,
                () => battleEvent.OnClashStart -= OnClashStart,
                "OnClashStart");
        }
    }

    private bool NeedsTurnStart()
    {
        return definition.Kind switch
        {
            NativeEmotion33Kind.FaithTurnStartBlock => true,
            NativeEmotion33Kind.FaithEnemyAttackSlotBlock => true,
            NativeEmotion33Kind.FaithDamageReflection => true,
            NativeEmotion33Kind.FaithFirstClashDamageNullify => true,
            NativeEmotion33Kind.DetachmentDamageDeferral => true,
            NativeEmotion33Kind.AweFirstTurnStrength => true,
            NativeEmotion33Kind.AweNextTurnSwift => true,
            NativeEmotion33Kind.AweLastStandNextTurnStrength => true,
            NativeEmotion33Kind.AweOverwhelmNextTurnStrength => true,
            NativeEmotion33Kind.AdmirationElapsedClashPrestige => true,
            NativeEmotion33Kind.AdmirationPrestigeFromStagger => true,
            _ => false
        };
    }

    private bool NeedsTurnEnd()
    {
        return definition.Kind switch
        {
            NativeEmotion33Kind.CompassionLastStandRecovery => true,
            NativeEmotion33Kind.FaithDamageReflection => true,
            NativeEmotion33Kind.FaithEscalatingBlockGain => true,
            NativeEmotion33Kind.FaithTurnEndBlockToHeal => true,
            NativeEmotion33Kind.AweLastStandNextTurnStrength => true,
            NativeEmotion33Kind.AweOverwhelmNextTurnStrength => true,
            NativeEmotion33Kind.LongingNeutralTurnEndEnergy => true,
            _ => false
        };
    }

    private void ApplyImmediateOrPersistentSetup()
    {
        switch (definition.Kind)
        {
            case NativeEmotion33Kind.CompassionImmediateHeal:
                owner.RestoreCurrentHP(Mathf.Max(0, definition.Amount));
                break;

            case NativeEmotion33Kind.FaithEscalatingBlockGain:
                // 획득은 이전 턴 종료 뒤에 일어나므로, 다음 실제 턴의 모든 방어도 획득에 +2.
                faithBlockGainBonus = Mathf.Max(0, definition.Amount);
                break;

            case NativeEmotion33Kind.FaithFirstClashDamageNullify:
                faithCompleteDefenseAvailable = true;
                break;

            case NativeEmotion33Kind.DetachmentPartMaximumHp:
                IncreasePartMaximumHp();
                break;

            case NativeEmotion33Kind.DetachmentStaggerMaximum:
                owner.GetMechanic<StaggerGaugeMechanic>()?
                    .AdjustMaximum(Mathf.Max(0, definition.Amount), true);
                break;

            case NativeEmotion33Kind.DetachmentSingleWeakenedPenaltySuppression:
                RefreshSingleWeakenedPenaltyState();
                break;

            case NativeEmotion33Kind.AweFirstTurnStrength:
                firstAweTurnPending = true;
                break;

            case NativeEmotion33Kind.AweNextTurnSwift:
                firstAweSwiftPending = true;
                break;

            case NativeEmotion33Kind.AweInfiniteHeadSpeed:
            case NativeEmotion33Kind.AweInfiniteArmsSpeed:
                originalEnergyMaximum = owner.MaxEnergy;
                owner.AdjustEnergyMaximum(-Mathf.Max(0, definition.Amount), false);
                break;

            case NativeEmotion33Kind.AdmirationPrestigeStockpile:
                ConfigurePrestigeStockpile();
                break;
        }
    }

    private void RestorePersistentCapacityOverrides()
    {
        if (originalEnergyMaximum >= 0 && owner != null)
        {
            int delta = originalEnergyMaximum - owner.MaxEnergy;
            if (delta != 0)
                owner.AdjustEnergyMaximum(delta, false);
            originalEnergyMaximum = -1;
        }

        if (originalPrestigeMaximum >= 0 &&
            owner?.CurrentStatus != null &&
            owner.RuntimeStatus != null)
        {
            owner.CurrentStatus.maxPrestige =
                Mathf.Max(0, originalPrestigeMaximum);
            owner.RuntimeStatus.currentPrestige =
                Mathf.Clamp(
                    owner.RuntimeStatus.currentPrestige,
                    0,
                    owner.CurrentStatus.maxPrestige);
            originalPrestigeMaximum = -1;
        }
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null || owner.IsDead)
            return;

        switch (definition.Kind)
        {
            case NativeEmotion33Kind.FaithTurnStartBlock:
                owner.AddBlock(Mathf.Max(0, definition.Amount));
                break;

            case NativeEmotion33Kind.FaithEnemyAttackSlotBlock:
                owner.AddBlock(
                    CountEnemyAttackSlots() *
                    Mathf.Max(0, definition.Amount));
                break;

            case NativeEmotion33Kind.FaithDamageReflection:
                if (faithPendingReflectionBlock > 0)
                {
                    owner.AddBlock(faithPendingReflectionBlock);
                    faithPendingReflectionBlock = 0;
                }
                break;

            case NativeEmotion33Kind.FaithFirstClashDamageNullify:
                faithCompleteDefenseAvailable = true;
                break;

            case NativeEmotion33Kind.DetachmentDamageDeferral:
                ApplyDeferredDamage();
                deferralDamageSeenThisTurn = 0;
                break;

            case NativeEmotion33Kind.AweFirstTurnStrength:
                if (firstAweTurnPending)
                {
                    AddTimedStatus(
                        StatusEffectId.Strength,
                        Mathf.Max(0, definition.Amount),
                        Mathf.Max(1, definition.Duration));
                    firstAweTurnPending = false;
                }
                break;

            case NativeEmotion33Kind.AweNextTurnSwift:
                if (firstAweSwiftPending)
                {
                    AddTimedStatus(
                        StatusEffectId.Swift,
                        Mathf.Max(0, definition.Amount),
                        Mathf.Max(1, definition.Duration));
                    firstAweSwiftPending = false;
                }
                break;

            case NativeEmotion33Kind.AweLastStandNextTurnStrength:
            case NativeEmotion33Kind.AweOverwhelmNextTurnStrength:
                if (pendingAweStrength)
                {
                    AddTimedStatus(
                        StatusEffectId.Strength,
                        Mathf.Max(0, definition.Amount),
                        Mathf.Max(1, definition.Duration));
                    pendingAweStrength = false;
                }
                break;

            case NativeEmotion33Kind.AdmirationElapsedClashPrestige:
                admirationElapsedTurns++;
                break;

            case NativeEmotion33Kind.AdmirationPrestigeFromStagger:
                PullPrestigeFromStagger();
                break;
        }
    }

    private void OnTurnEnd(int turn)
    {
        if (owner == null || owner.IsDead)
            return;

        MomentumState state =
            battleContext?.ResolveMomentumManager()?
                .GetFinalTurnState(owner) ??
            MomentumState.Balance;

        switch (definition.Kind)
        {
            case NativeEmotion33Kind.CompassionLastStandRecovery:
                if (state == MomentumState.LastStand)
                    owner.RestoreCurrentHP(Mathf.Max(0, definition.Amount));
                break;

            case NativeEmotion33Kind.FaithDamageReflection:
                faithPendingReflectionBlock =
                    ResolveRatioAmount(
                        faithDamageThisTurn,
                        definition.Ratio);
                faithDamageThisTurn = 0;
                break;

            case NativeEmotion33Kind.FaithEscalatingBlockGain:
                faithBlockGainBonus += Mathf.Max(0, definition.Amount);
                break;

            case NativeEmotion33Kind.FaithTurnEndBlockToHeal:
                if (faithBlockGainedThisTurn > 0)
                    owner.RestoreCurrentHP(faithBlockGainedThisTurn);
                faithBlockGainedThisTurn = 0;
                break;

            case NativeEmotion33Kind.AweLastStandNextTurnStrength:
                pendingAweStrength =
                    state == MomentumState.LastStand;
                break;

            case NativeEmotion33Kind.AweOverwhelmNextTurnStrength:
                pendingAweStrength =
                    state == MomentumState.Overwhelm;
                break;

            case NativeEmotion33Kind.LongingNeutralTurnEndEnergy:
                if (state == MomentumState.Balance)
                {
                    owner.AddEnergy(
                        Mathf.Max(0, definition.Amount),
                        CombatResourceChangeReason.SkillEffect);
                }
                break;
        }
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (exchange == null ||
            exchange.WasCancelled ||
            exchange.IsTie ||
            exchange.IsOneSided)
        {
            return;
        }

        switch (definition.Kind)
        {
            case NativeEmotion33Kind.CompassionExchangeWinRecovery:
                if (exchange.WinnerAction?.Owner == owner)
                {
                    owner.RestoreCurrentHP(Mathf.Max(0, definition.Amount));
                    owner.GetMechanic<StaggerGaugeMechanic>()?
                        .Recover(Mathf.Max(0, definition.SecondaryAmount));
                }
                break;

            case NativeEmotion33Kind.AdmirationHitPrestige:
                if (exchange.WinnerAction?.Owner == owner)
                    owner.AddPrestige(Mathf.Max(0, definition.Amount));
                break;

            case NativeEmotion33Kind.AdmirationTakenPrestige:
                if (exchange.LoserAction?.Owner == owner)
                    owner.AddPrestige(Mathf.Max(0, definition.Amount));
                break;
        }
    }

    private void OnDamageEventResolved(DamageEventResult result)
    {
        DamageContext context = result?.Context;
        if (context == null)
            return;

        switch (definition.Kind)
        {
            case NativeEmotion33Kind.CompassionLifesteal:
                if (context.Attacker == owner &&
                    context.Target != owner &&
                    context.AppliedHpDamage > 0)
                {
                    int heal =
                        ResolveRatioAmount(
                            context.AppliedHpDamage,
                            definition.Ratio);
                    if (heal > 0)
                        owner.RestoreCurrentHP(heal);
                }
                break;

            case NativeEmotion33Kind.FaithDamageReflection:
                if (context.Target == owner &&
                    context.TargetModifiedDamage > 0)
                {
                    faithDamageThisTurn +=
                        context.TargetModifiedDamage;
                }
                break;
        }
    }

    private void OnKillResolved(KillEventContext context)
    {
        if (context?.Killer != owner || context.Victim == owner)
            return;

        battleContext?.RunProgression?
            .AddMaximumHpBonus(Mathf.Max(0, definition.Amount));
    }

    private void OnBodyPartStateChanged(Character target, BodyPart part)
    {
        if (target == owner)
            RefreshSingleWeakenedPenaltyState();
    }

    private void OnClashStart(Character first, Character second)
    {
        if (owner == null || (first != owner && second != owner))
            return;

        switch (definition.Kind)
        {
            case NativeEmotion33Kind.AdmirationClashPrestige:
                owner.AddPrestige(Mathf.Max(0, definition.Amount));
                break;

            case NativeEmotion33Kind.AdmirationElapsedClashPrestige:
            {
                int gain =
                    Mathf.Max(0, admirationElapsedTurns) *
                    Mathf.Max(0, definition.Amount);
                if (gain > 0)
                    owner.AddPrestige(gain);
                break;
            }
        }
    }

    public int ModifyHealing(Character target, int amount)
    {
        if (target != owner || amount <= 0)
            return amount;

        if (definition.Kind ==
            NativeEmotion33Kind.CompassionHealingConversion)
        {
            DealRandomEnemyPartDamage(amount);
            return 0;
        }

        return amount;
    }

    public void OnHealingResolved(
        Character target,
        CanonicalHealingResult result)
    {
        if (target != owner || result == null)
            return;

        if (definition.Kind !=
            NativeEmotion33Kind.CompassionOverhealToBlock ||
            result.OverhealAmount <= 0)
        {
            return;
        }

        int cap = Mathf.Max(0, definition.MaximumTotal);
        int remaining = cap <= 0
            ? result.OverhealAmount
            : Mathf.Max(0, cap - overhealBlockGranted);

        int gain = Mathf.Min(result.OverhealAmount, remaining);
        if (gain <= 0)
            return;

        overhealBlockGranted += gain;
        owner.AddBlock(gain);
    }

    public int ModifyBlockGain(Character target, int amount)
    {
        if (target != owner || amount <= 0)
            return amount;

        switch (definition.Kind)
        {
            case NativeEmotion33Kind.FaithEscalatingBlockGain:
                return amount + Mathf.Max(0, faithBlockGainBonus);

            case NativeEmotion33Kind.FaithBlockConversion:
                DealRandomEnemyPartDamage(
                    Mathf.Max(
                        0,
                        Mathf.FloorToInt(
                            amount * Mathf.Max(0f, definition.Multiplier))));
                return 0;

            default:
                return amount;
        }
    }

    public void OnBlockGainResolved(
        Character target,
        int requestedAmount,
        int appliedAmount)
    {
        if (target != owner || appliedAmount <= 0)
            return;

        if (definition.Kind ==
            NativeEmotion33Kind.FaithTurnEndBlockToHeal)
        {
            faithBlockGainedThisTurn += appliedAmount;
        }
    }

    public int ModifyDamageBeforeGuard(DamageContext context, int damage)
    {
        if (definition.Kind !=
                NativeEmotion33Kind.FaithFirstClashDamageNullify ||
            !faithCompleteDefenseAvailable ||
            context?.Target != owner ||
            !context.IsClashDamage ||
            damage <= 0)
        {
            return damage;
        }

        faithCompleteDefenseAvailable = false;
        return 0;
    }

    public int ModifyDamageAfterGuard(DamageContext context, int damage)
    {
        if (definition.Kind !=
                NativeEmotion33Kind.DetachmentDamageDeferral ||
            applyingDeferredDamage ||
            context?.Target != owner ||
            damage <= 0)
        {
            return damage;
        }

        // "이번 턴 피해 절반"을 타격별 반올림이 아니라 턴 누계 기준으로 맞춘다.
        // 누적 실제 피해의 floor(1/2)만 다음 턴로 이동시켜 여러 작은 타격에서도
        // 총량이 정본의 절반에서 벗어나지 않게 한다.
        deferralDamageSeenThisTurn += damage;

        int targetDeferredTotal =
            Mathf.Max(
                0,
                deferralDamageSeenThisTurn / 2);

        int newlyDeferred =
            Mathf.Max(
                0,
                targetDeferredTotal - deferredDamage);

        newlyDeferred =
            Mathf.Min(
                damage,
                newlyDeferred);

        deferredDamage += newlyDeferred;
        return Mathf.Max(0, damage - newlyDeferred);
    }

    public bool TryGetClashSpeedModifier(
        BattleAction action,
        out int modifier)
    {
        modifier = 0;
        if (action?.Owner != owner || action.OwnerPart == null)
            return false;

        bool matches = definition.Kind switch
        {
            NativeEmotion33Kind.AweInfiniteHeadSpeed =>
                action.OwnerPart.Type == PartType.HEAD,

            NativeEmotion33Kind.AweInfiniteArmsSpeed =>
                action.OwnerPart.Type == PartType.LEFT_HAND ||
                action.OwnerPart.Type == PartType.RIGHT_HAND,

            _ => false
        };

        if (!matches)
            return false;

        // 0922 §14.3: 속도 무한은 처리 순서가 아니라 합 속도 보정만 항상 상한(+2).
        modifier = 2;
        return true;
    }

    public bool SuppressSingleWeakenedPenalty(Character target)
    {
        if (definition.Kind !=
                NativeEmotion33Kind.DetachmentSingleWeakenedPenaltySuppression ||
            target != owner)
        {
            return false;
        }

        return CountWeakenedParts() == 1;
    }

    public int ResolveAdvantageThreshold(
        Character target,
        int currentThreshold)
    {
        if (definition.Kind !=
                NativeEmotion33Kind.LongingNeutralThreshold ||
            target != owner)
        {
            return currentThreshold;
        }

        return Mathf.Max(
            currentThreshold,
            Mathf.Max(0, definition.Amount));
    }

    public int GetPrestigeActivationThreshold(Character target)
    {
        if (definition.Kind !=
                NativeEmotion33Kind.AdmirationPrestigeStockpile ||
            target != owner)
        {
            return 0;
        }

        return Mathf.Max(1, prestigeActivationThreshold);
    }

    private void IncreasePartMaximumHp()
    {
        if (owner.BodyParts == null)
            return;

        float ratio = Mathf.Max(0f, definition.Ratio);
        foreach (BodyPart part in owner.BodyParts)
        {
            part?.IncreaseMaxHPPercent(
                ratio,
                healByIncreaseAmount: true);
        }
    }

    private void RefreshSingleWeakenedPenaltyState()
    {
        if (owner?.BodyParts == null)
            return;

        int count = CountWeakenedParts();

        if (count == 1)
        {
            foreach (BodyPart part in owner.BodyParts)
            {
                if (part?.IsWeakened == true && !part.IsBroken)
                    owner.RemoveDisabledStatusForPart(part);
            }
            return;
        }

        if (count >= 2)
        {
            foreach (BodyPart part in owner.BodyParts)
            {
                if (part?.IsWeakened == true && !part.IsBroken)
                    owner.ApplyDisabledStatusForPart(part);
            }
        }
    }

    private int CountWeakenedParts()
    {
        if (owner?.BodyParts == null)
            return 0;

        int count = 0;
        foreach (BodyPart part in owner.BodyParts)
        {
            if (part?.IsWeakened == true && !part.IsBroken)
                count++;
        }
        return count;
    }

    private int CountEnemyAttackSlots()
    {
        if (battleContext?.Enemies == null)
            return 0;

        int count = 0;
        foreach (Character enemy in battleContext.Enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            int slots =
                enemy.CombatRulesRuntime?.GetActiveCombatSlotCount() ?? 0;

            if (slots <= 0)
                slots = Mathf.Max(0, enemy.GetMaxCombatActionSlots());

            count += slots;
        }

        return count;
    }

    private void AddTimedStatus(
        StatusEffectId id,
        int amount,
        int duration)
    {
        if (amount <= 0 || owner == null)
            return;

        StatusEffect effect =
            StatusEffectFactory.CreateCanonical0922(
                id,
                amount,
                duration);

        if (effect != null)
            owner.AddStatus(effect, owner);
    }

    private static int ResolveRatioAmount(int value, float ratio)
    {
        if (value <= 0 || ratio <= 0f)
            return 0;

        return Mathf.Max(
            1,
            Mathf.FloorToInt(value * ratio));
    }

    private void ConfigurePrestigeStockpile()
    {
        if (owner?.CurrentStatus == null)
            return;

        prestigeActivationThreshold =
            Mathf.Max(
                1,
                Canonical0922EmotionRuntimeHooks
                    .ResolveCanonicalPrestigeThreshold(owner));

        originalPrestigeMaximum =
            Mathf.Max(0, owner.CurrentStatus.maxPrestige);

        int multiplier = Mathf.Max(1, definition.Amount);
        owner.CurrentStatus.maxPrestige =
            Mathf.Max(
                originalPrestigeMaximum,
                prestigeActivationThreshold * multiplier);

        if (owner.RuntimeStatus != null)
        {
            owner.RuntimeStatus.currentPrestige =
                Mathf.Clamp(
                    owner.RuntimeStatus.currentPrestige,
                    0,
                    owner.CurrentStatus.maxPrestige);
        }
    }

    private void PullPrestigeFromStagger()
    {
        if (owner?.RuntimeStatus == null)
            return;

        int threshold =
            Canonical0922EmotionRuntimeHooks
                .ResolveCanonicalPrestigeThreshold(owner);

        int missing = Mathf.Max(
            0,
            threshold - owner.RuntimeStatus.currentPrestige);

        if (missing <= 0)
            return;

        StaggerGaugeMechanic stagger =
            owner.GetMechanic<StaggerGaugeMechanic>();

        int paid = stagger?.SacrificeGauge(missing) ?? 0;
        if (paid > 0)
            owner.AdjustPrestige(paid);
    }

    private void ApplyDeferredDamage()
    {
        if (deferredDamage <= 0 || owner == null || owner.IsDead)
            return;

        int amount = deferredDamage;
        deferredDamage = 0;

        BodyPart targetPart = GetHighestHpLivingPart();

        DamageManager manager = battleContext?.ResolveDamageManager();
        if (manager == null)
            return;

        DamageRequest request = DamageRequest.Custom(
            targetPart != null ? DamageType.SkillPart : DamageType.Direct,
            owner,
            owner,
            targetPart,
            amount,
            1f,
            canBreakPart: false,
            applyMomentum: false,
            applyGuard: false,
            sourceAction: null,
            sourceEffect: null);

        // 미뤄둔 이미 확정된 피해를 다시 내성/보호/감소에 태우지 않는다.
        request.ApplyPhysicalResistance = false;
        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = false;

        applyingDeferredDamage = true;
        try
        {
            manager.ApplyDamageContext(request);
        }
        finally
        {
            applyingDeferredDamage = false;
        }
    }

    private BodyPart GetHighestHpLivingPart()
    {
        if (owner?.BodyParts == null)
            return null;

        BodyPart selected = null;
        float highest = float.MinValue;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || part.IsBroken)
                continue;

            if (selected == null || part.PartHP > highest)
            {
                selected = part;
                highest = part.PartHP;
            }
        }

        return selected;
    }

    private void DealRandomEnemyPartDamage(int amount)
    {
        if (amount <= 0 || battleContext?.Enemies == null)
            return;

        List<Character> enemies = new List<Character>();
        foreach (Character enemy in battleContext.Enemies)
        {
            if (enemy != null && !enemy.IsDead)
                enemies.Add(enemy);
        }

        if (enemies.Count == 0)
            return;

        Character target = enemies[UnityEngine.Random.Range(0, enemies.Count)];
        BodyPart part = null;

        if (target.UsesBodyParts && target.BodyParts != null)
        {
            List<BodyPart> parts = new List<BodyPart>();
            foreach (BodyPart candidate in target.BodyParts)
            {
                if (candidate != null && !candidate.IsBroken)
                    parts.Add(candidate);
            }

            if (parts.Count > 0)
                part = parts[UnityEngine.Random.Range(0, parts.Count)];
        }

        DamageManager manager = battleContext.ResolveDamageManager();
        if (manager == null)
            return;

        DamageRequest request = DamageRequest.Custom(
            part != null ? DamageType.SkillPart : DamageType.Direct,
            owner,
            target,
            part,
            amount,
            1f,
            canBreakPart: false,
            applyMomentum: false,
            applyGuard: true,
            sourceAction: null,
            sourceEffect: null);

        // 카드에는 타격 타입이 없으므로 체력 내성 배율을 임의로 부여하지 않는다.
        request.ApplyPhysicalResistance = false;
        request.ApplyAttackerModifiers = false;
        request.ApplyTargetModifiers = true;

        manager.ApplyDamageContext(request);
    }
}

internal sealed class NativeEmotionCommonMechanic : CombatMechanic
{
    private readonly NativeEmotionCommonGraph definition;
    private readonly List<OwnedStatus> ownedStatuses = new();
    private bool guardLostThisTurn;
    private int timedTurnsRemaining;

    private sealed class OwnedStatus
    {
        public Character Target;
        public BodyPart Part;
        public StatusEffect Effect;
    }

    public override string MechanicName =>
        $"R7 Native Emotion · {definition?.Kind}";

    public NativeEmotionCommonMechanic(
        NativeEmotionCommonGraph definition)
    {
        this.definition = definition;
        timedTurnsRemaining = Mathf.Max(1, definition?.Turns ?? 1);
    }

    public override void OnRegister()
    {
        if (definition == null || owner == null)
            return;

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionStart += OnActionStart,
            () => battleEvent.OnActionStart -= OnActionStart,
            "OnActionStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnBattleEnded += OnBattleEnded,
            () => battleEvent.OnBattleEnded -= OnBattleEnded,
            "OnBattleEnded");

        switch (definition.Kind)
        {
            case NativeEmotionCommonKind.PermanentSelfStatus:
                EnsurePermanentSelfStatuses();
                break;

            case NativeEmotionCommonKind.GoldThresholdStrength:
                RefreshGoldThresholdStrength();
                break;
        }
    }

    public override void OnUnregister()
    {
        RemoveOwnedStatuses();
    }

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (definition?.BlockPrestige == true &&
            skill?.ActionType == ActionType.Prestige)
        {
            return false;
        }

        return true;
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null || owner.IsDead || definition == null)
            return;

        switch (definition.Kind)
        {
            case NativeEmotionCommonKind.TurnStartGuardStrength:
                guardLostThisTurn = false;
                RemoveOwnedStatusesFrom(owner);
                AddOwnedStatus(
                    owner,
                    null,
                    StatusEffectId.Strength,
                    1,
                    StatusEffect.InfiniteDuration);
                break;

            case NativeEmotionCommonKind.GoldThresholdStrength:
                RefreshGoldThresholdStrength();
                break;
        }
    }

    private void OnTurnEnd(int turn)
    {
        if (owner == null || owner.IsDead || definition == null)
            return;

        if (timedTurnsRemaining <= 0)
            return;

        switch (definition.Kind)
        {
            case NativeEmotionCommonKind.TimedHpRecovery:
                owner.RestoreCurrentHP(Mathf.Max(0, definition.Amount));
                timedTurnsRemaining--;
                break;

            case NativeEmotionCommonKind.TimedStaggerRecovery:
            {
                StaggerGaugeMechanic stagger =
                    owner.GetMechanic<StaggerGaugeMechanic>();

                stagger?.Recover(Mathf.Max(0, definition.Amount));
                timedTurnsRemaining--;
                break;
            }
        }
    }

    private void OnActionStart(BattleAction action)
    {
        if (definition?.Kind !=
                NativeEmotionCommonKind.PrestigeTargetRuptureInfinite ||
            action?.Owner != owner ||
            action.ActionType != ActionType.Prestige)
        {
            return;
        }

        Character target = action.Target;
        if (target == null || target.IsDead)
            return;

        AddOwnedStatus(
            target,
            action.TargetPart,
            StatusEffectId.Rupture,
            Mathf.Max(1, definition.PrimaryValue),
            StatusEffect.InfiniteDuration);
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (definition?.Kind !=
                NativeEmotionCommonKind.TurnStartGuardStrength ||
            guardLostThisTurn ||
            exchange == null ||
            exchange.WasCancelled ||
            exchange.IsTie)
        {
            return;
        }

        if (exchange.LoserAction?.Owner != owner)
            return;

        guardLostThisTurn = true;
        RemoveOwnedStatusesFrom(owner);
    }

    private void OnBattleEnded()
    {
        RemoveOwnedStatuses();
    }

    private void EnsurePermanentSelfStatuses()
    {
        AddOwnedStatus(
            owner,
            null,
            definition.PrimaryStatus,
            Mathf.Max(1, definition.PrimaryValue),
            definition.PrimaryDuration);

        if (definition.HasSecondaryStatus)
        {
            AddOwnedStatus(
                owner,
                null,
                definition.SecondaryStatus,
                Mathf.Max(1, definition.SecondaryValue),
                definition.SecondaryDuration);
        }
    }

    private void RefreshGoldThresholdStrength()
    {
        int gold = battleContext?.RunProgression?.Gold ?? 0;
        bool shouldBeActive = gold >= Mathf.Max(0, definition.GoldThreshold);
        bool isActive = HasOwnedStatusOn(owner, StatusEffectId.Strength);

        if (shouldBeActive && !isActive)
        {
            AddOwnedStatus(
                owner,
                null,
                StatusEffectId.Strength,
                Mathf.Max(1, definition.PrimaryValue),
                StatusEffect.InfiniteDuration);
        }
        else if (!shouldBeActive && isActive)
        {
            RemoveOwnedStatusesFrom(owner);
        }
    }

    private bool HasOwnedStatusOn(Character target, StatusEffectId id)
    {
        Type expectedType = GetStatusType(id);
        if (expectedType == null)
            return false;

        for (int i = 0; i < ownedStatuses.Count; i++)
        {
            OwnedStatus owned = ownedStatuses[i];
            if (owned?.Target == target &&
                owned.Effect != null &&
                owned.Effect.GetType() == expectedType)
            {
                return true;
            }
        }

        return false;
    }

    private void AddOwnedStatus(
        Character target,
        BodyPart part,
        StatusEffectId id,
        int value,
        int duration)
    {
        if (target == null)
            return;

        StatusEffect effect =
            StatusEffectFactory.CreateCanonical0922(
                id,
                value,
                duration);

        if (effect == null)
            return;

        if (part != null)
            target.AddPartStatus(part, effect, owner);
        else
            target.AddStatus(effect, owner);

        ownedStatuses.Add(
            new OwnedStatus
            {
                Target = target,
                Part = part,
                Effect = effect
            });
    }

    private void RemoveOwnedStatusesFrom(Character target)
    {
        if (target == null)
            return;

        for (int i = ownedStatuses.Count - 1; i >= 0; i--)
        {
            OwnedStatus owned = ownedStatuses[i];
            if (owned?.Target != target)
                continue;

            RemoveOne(owned);
            ownedStatuses.RemoveAt(i);
        }
    }

    private void RemoveOwnedStatuses()
    {
        for (int i = ownedStatuses.Count - 1; i >= 0; i--)
            RemoveOne(ownedStatuses[i]);

        ownedStatuses.Clear();
    }

    private static void RemoveOne(OwnedStatus owned)
    {
        if (owned?.Target == null || owned.Effect == null)
            return;

        if (owned.Part != null)
            owned.Target.RemovePartStatus(owned.Part, owned.Effect);
        else
            owned.Target.RemoveStatus(owned.Effect);
    }

    private static Type GetStatusType(StatusEffectId id)
    {
        return id switch
        {
            StatusEffectId.Strength => typeof(StrengthStatus),
            StatusEffectId.Swift => typeof(SwiftStatus),
            StatusEffectId.Rupture => typeof(RuptureStatus),
            _ => null
        };
    }
}

internal sealed class NativeEmotionPrototypeMechanic : CombatMechanic
{
    private readonly string label;
    private readonly int rollBonus;
    private readonly float damageDealtMultiplier;
    private readonly float damageTakenMultiplier;
    private readonly int positiveMomentumBonus;
    private readonly float prestigeGainMultiplier;

    public override string MechanicName => $"Emotion Augment · {label}";

    public NativeEmotionPrototypeMechanic(
        string label,
        int rollBonus,
        float damageDealtMultiplier,
        float damageTakenMultiplier,
        int positiveMomentumBonus,
        float prestigeGainMultiplier)
    {
        this.label = string.IsNullOrWhiteSpace(label) ? "Prototype" : label;
        this.rollBonus = rollBonus;
        this.damageDealtMultiplier = Mathf.Max(0f, damageDealtMultiplier);
        this.damageTakenMultiplier = Mathf.Max(0f, damageTakenMultiplier);
        this.positiveMomentumBonus = Mathf.Max(0, positiveMomentumBonus);
        this.prestigeGainMultiplier = Mathf.Max(0f, prestigeGainMultiplier);
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner)
            return roll;
        return Mathf.Max(0, roll + rollBonus);
    }

    public override int ModifyDamageDealt(DamageContext context, int damage)
    {
        if (context?.Attacker != owner || damage <= 0)
            return damage;
        return Mathf.Max(0, Mathf.RoundToInt(damage * damageDealtMultiplier));
    }

    public override int ModifyDamageTaken(DamageContext context, int damage)
    {
        if (context?.Target != owner || damage <= 0)
            return damage;
        return Mathf.Max(0, Mathf.RoundToInt(damage * damageTakenMultiplier));
    }

    public override int ModifyMomentumShift(ClashResultContext context, int shift)
    {
        if (shift <= 0 || positiveMomentumBonus <= 0)
            return shift;
        return shift + positiveMomentumBonus;
    }

    public override int ModifyPrestigeGain(ClashResultContext context, int prestigeGain)
    {
        if (prestigeGain <= 0)
            return prestigeGain;
        return Mathf.Max(0, Mathf.RoundToInt(prestigeGain * prestigeGainMultiplier));
    }
}
