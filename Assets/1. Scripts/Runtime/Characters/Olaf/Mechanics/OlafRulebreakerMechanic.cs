using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 올라프 특수 카드가 공통 전투 계약 위에서 동작하도록 하는 런타임 해석기.
/// 0922 Phase 7: 정면승부 10% 자해, 피의 맹세 3구간, 명예로운 전투 반복을 포함한다.
/// </summary>
public sealed class OlafRulebreakerMechanic :
    CombatMechanic,
    IExchangeRepeatRule
{
    public const int InfiniteAttackSpeed = 1_000_000;

    private readonly Dictionary<string, int> committedUses =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private readonly Dictionary<long, int> bloodOathTierByAction =
        new Dictionary<long, int>();

    private bool infiniteAttackSpeedThisTurn;
    private bool honorableFightPartlessPendingLogged;

    public override string MechanicName => "Olaf Rulebreakers";
    public bool HasInfiniteAttackSpeedThisTurn => infiniteAttackSpeedThisTurn;

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");

        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionEnd += OnActionEnd,
            () => battleEvent.OnActionEnd -= OnActionEnd,
            "OnActionEnd");
    }

    public override void OnUnregister()
    {
        committedUses.Clear();
        bloodOathTierByAction.Clear();
        infiniteAttackSpeedThisTurn = false;
        honorableFightPartlessPendingLogged = false;
        ClearSuppressedSlots();
    }

    public int ResolveEnergyCost(SkillDefinition definition, int baseCost)
    {
        SkillRulebreakerSettings rule = definition?.Rulebreaker;
        int safeBase = Mathf.Max(0, baseCost);

        if (rule?.HasDynamicCost != true)
            return safeBase;

        int used = GetCommittedUseCount(definition);
        int reduction = used * Mathf.Max(0, rule.EnergyCostReductionPerCommittedUse);
        return Mathf.Max(rule.ResolveMinimumEnergyCost(), safeBase - reduction);
    }

    public void RecordCommittedUse(SkillDefinition definition)
    {
        if (definition?.Rulebreaker?.HasDynamicCost != true)
            return;

        string key = GetSkillKey(definition);
        committedUses[key] = GetCommittedUseCount(definition) + 1;
    }

    public void RollbackCommittedUse(SkillDefinition definition)
    {
        if (definition?.Rulebreaker?.HasDynamicCost != true)
            return;

        string key = GetSkillKey(definition);
        int current = GetCommittedUseCount(definition);
        if (current <= 1)
            committedUses.Remove(key);
        else
            committedUses[key] = current - 1;
    }

    public int GetCommittedUseCount(SkillDefinition definition)
    {
        string key = GetSkillKey(definition);
        return !string.IsNullOrWhiteSpace(key) && committedUses.TryGetValue(key, out int count)
            ? Mathf.Max(0, count)
            : 0;
    }

    public bool CanUse(SkillDefinition definition)
    {
        SkillRulebreakerSettings rule = definition?.Rulebreaker;
        if (rule?.Enabled != true || !rule.RequirePreviousTurnLastStand)
            return true;

        MomentumManager momentum = battleContext?.Services?.MomentumManager;
        return momentum?.GetPreviousTurnFinalState(owner) == MomentumState.LastStand;
    }

    public void Execute(BattleAction action)
    {
        if (action?.Owner != owner || action.Skill?.Definition == null)
            return;

        SkillRulebreakerSettings rule = action.Skill.Definition.Rulebreaker;
        if (rule?.Enabled != true)
            return;

        if (rule.ConsumeAllOwnerBleedingOnExecute)
        {
            int consumed = ConsumeAllOwnerBleeding();
            if (consumed >= Mathf.Max(0, rule.IgnoreWeakenPrerequisiteBleedingThreshold) &&
                rule.IgnoreWeakenPrerequisiteBleedingThreshold > 0)
            {
                action.PartBreakModeOverride = PartBreakMode.IgnoreWeakenedPrerequisite;
            }
            else if (consumed >= Mathf.Max(0, rule.BreakAuthorityBleedingThreshold) &&
                     rule.BreakAuthorityBleedingThreshold > 0)
            {
                action.PartBreakModeOverride = PartBreakMode.WeakenedOnly;
            }
            else
            {
                action.PartBreakModeOverride = PartBreakMode.None;
            }
        }

        MomentumState? currentBand =
            battleContext?.Services?.MomentumManager
                ?.GetCurrentBand(owner);

        if (rule.ApplyCurrentLastStandPowerBonus &&
            currentBand == MomentumState.LastStand)
        {
            action.RulebreakerFlatPowerBonus +=
                rule.CurrentLastStandPowerBonus;
        }

        if (rule.ApplyCurrentDisadvantageOrWorsePowerBonus &&
            (currentBand == MomentumState.Disadvantage ||
             currentBand == MomentumState.LastStand))
        {
            action.RulebreakerFlatPowerBonus +=
                rule.CurrentDisadvantageOrWorsePowerBonus;
        }

        if (rule.GrantInfiniteAttackSlotSpeedThisTurn)
            ActivateInfiniteAttackSpeed();
    }

    public void NotifyDuelMatched(
        BattleAction action,
        BattleAction opponentAction)
    {
        if (action?.Owner != owner || opponentAction?.Owner == null)
            return;

        SkillDefinition definition = action.Skill?.Definition;
        SkillRulebreakerSettings rule = definition?.Rulebreaker;
        if (rule?.Enabled != true || !rule.MirrorThisSkillToOpponentOnDuelMatch)
            return;

        Skill mirrored = opponentAction.Owner.CreateRuntimeSkillForLoadout(definition);
        if (mirrored == null)
            return;

        mirrored.Initialize(opponentAction.Owner, opponentAction.Owner.BattleEvent);
        opponentAction.Slot.Skill = mirrored;
    }

    public bool ShouldForceInfiniteAttackSpeed(Skill skill)
    {
        return infiniteAttackSpeedThisTurn &&
               skill != null &&
               (skill.ActionType == ActionType.NormalAttack ||
                skill.ActionType == ActionType.Duel);
    }

    /// <summary>
    /// 0922 「피의 맹세」 사용시 자기 출혈을 전량 소비하고 그 직전 수치로 3구간을 확정한다.
    /// 0=0~4, 1=5~9, 2=10+.
    /// </summary>
    public int PrepareBloodOathDuel(BattleAction action)
    {
        if (action?.Owner != owner)
            return 0;

        int consumed = ConsumeAllOwnerBleeding();
        int tier = consumed >= 10 ? 2 : consumed >= 5 ? 1 : 0;

        if (action.ActionId > 0)
            bloodOathTierByAction[action.ActionId] = tier;

        switch (tier)
        {
            case 2:
                action.RulebreakerFlatPowerBonus += 8;
                action.PartBreakModeOverride = PartBreakMode.IgnoreWeakenedPrerequisite;
                break;

            case 1:
                action.RulebreakerFlatPowerBonus += 4;
                action.PartBreakModeOverride = PartBreakMode.WeakenedOnly;
                ApplyOwnerNumericStatus(StatusEffectId.Regeneration, 4, 3, action);
                break;

            default:
                action.PartBreakModeOverride = PartBreakMode.None;
                break;
        }

        return tier;
    }

    public void ResolveBloodOathDuelWin(
        BattleAction action,
        int rollNumber)
    {
        if (action?.Owner != owner || action.ActionId <= 0 || rollNumber <= 0)
            return;

        if (!bloodOathTierByAction.TryGetValue(action.ActionId, out int tier) || tier <= 0)
            return;

        if (rollNumber == 1)
            AddBleeding(action.Target, action.TargetPart, 3, action);

        if (tier >= 2 && rollNumber == 2)
        {
            AddBleeding(action.Target, action.TargetPart, 3, action);
            owner.AddEnergy(1, CombatResourceChangeReason.SkillEffect, action, action.Skill);
        }
    }

    public int GetOwnerBleedingTotal()
    {
        if (owner == null)
            return 0;

        int total = owner.GetStatus<Bleeding>()?.Stack ?? 0;
        if (owner.BodyParts == null)
            return Mathf.Max(0, total);

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            total += owner.GetPartStatus<Bleeding>(part)?.Stack ?? 0;
        }

        return Mathf.Max(0, total);
    }

    public int GetBleeding(Character target, BodyPart part)
    {
        if (target == null)
            return 0;

        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        if (!anchor.IsValid)
            return 0;

        Bleeding bleeding = anchor.IsPartAnchor
            ? target.GetPartStatus<Bleeding>(anchor.Part)
            : target.GetStatus<Bleeding>();

        return Mathf.Max(0, bleeding?.Stack ?? 0);
    }

    public bool TryConsumeBleeding(
        Character target,
        BodyPart part,
        int amount)
    {
        int safe = Mathf.Max(0, amount);
        if (safe <= 0 || target == null)
            return false;

        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        if (!anchor.IsValid)
            return false;

        Bleeding bleeding = anchor.IsPartAnchor
            ? target.GetPartStatus<Bleeding>(anchor.Part)
            : target.GetStatus<Bleeding>();

        if (bleeding == null || bleeding.Stack < safe)
            return false;

        int consumed = bleeding.ConsumeStacks(safe);
        if (consumed != safe)
            return false;

        if (bleeding.Stack <= 0)
        {
            if (anchor.IsPartAnchor)
                target.RemovePartStatus(anchor.Part, bleeding, StatusEffectRemoveReason.Manual);
            else
                target.RemoveStatus(bleeding, StatusEffectRemoveReason.Manual);
        }

        return true;
    }

    public void AddBleeding(
        Character target,
        BodyPart part,
        int amount,
        BattleAction sourceAction = null)
    {
        int safe = Mathf.Max(0, amount);
        if (target == null || safe <= 0)
            return;

        CombatStatusAnchor anchor = CombatStatusAnchor.Resolve(target, part);
        if (!anchor.IsValid)
            return;

        EffectRequest request = anchor.IsPartAnchor
            ? EffectRequest.BodyPartStatus(
                owner,
                target,
                anchor.Part,
                new Bleeding(safe),
                sourceAction,
                sourceAction?.CurrentRollIndex ?? -1)
            : EffectRequest.CharacterStatus(
                owner,
                target,
                new Bleeding(safe),
                sourceAction,
                sourceAction?.CurrentRollIndex ?? -1);

        if (anchor.IsPartAnchor)
            battleContext?.EffectResolver?.ApplyBodyPartStatus(request);
        else
            battleContext?.EffectResolver?.ApplyCharacterStatus(request);
    }

    public static int CalculateHeadOnSelfDamage(int dealtDamage)
    {
        return Mathf.Max(0, dealtDamage) / 10;
    }

    void IExchangeRepeatRule.ModifyRemainingRollCountsAfterExchange(
        ClashExchangeResult exchange,
        ref int firstRemaining,
        ref int secondRemaining)
    {
        if (exchange == null ||
            exchange.WasCancelled ||
            exchange.FirstAction == null ||
            exchange.SecondAction == null)
        {
            return;
        }

        BattleAction myAction =
            exchange.FirstAction.Owner == owner
                ? exchange.FirstAction
                : exchange.SecondAction.Owner == owner
                    ? exchange.SecondAction
                    : null;

        if (myAction?.Skill?.Definition?.SkillId != OlafSkillIds.HonorableFight)
            return;

        BattleAction opponent =
            myAction == exchange.FirstAction
                ? exchange.SecondAction
                : exchange.FirstAction;

        if (opponent?.Skill?.Definition?.SkillId != OlafSkillIds.HonorableFight)
            return;

        // 부위 없는 일반몹 종료조건은 0922 정본에서 미정이다. 임의 반복 규칙을 발명하지 않는다.
        if (myAction.TargetPart == null || opponent.TargetPart == null)
        {
            if (!honorableFightPartlessPendingLogged)
            {
                honorableFightPartlessPendingLogged = true;
                Debug.LogWarning(
                    "[PENDING_CANONICAL][0922][Olaf/HonorableFight] " +
                    "부위 없는 대상의 반복 종료조건이 미정이므로 일반 1교환으로 종료합니다.");
            }
            return;
        }

        bool eitherWeakened =
            myAction.TargetPart.IsWeakened ||
            myAction.TargetPart.IsBroken ||
            opponent.TargetPart.IsWeakened ||
            opponent.TargetPart.IsBroken;

        if (eitherWeakened)
            return;

        // 기술적 무한루프 안전망. gameplay 종료조건을 대체하지 않는다.
        if (exchange.ExchangeIndex >= 255)
        {
            Debug.LogError("[Olaf][HonorableFight] 256교환 안전망으로 반복을 중단합니다.");
            return;
        }

        firstRemaining = Mathf.Max(1, firstRemaining);
        secondRemaining = Mathf.Max(1, secondRemaining);
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (exchange == null || exchange.WasCancelled || exchange.IsTie)
            return;

        BattleAction myAction =
            exchange.FirstAction?.Owner == owner
                ? exchange.FirstAction
                : exchange.SecondAction?.Owner == owner
                    ? exchange.SecondAction
                    : null;

        if (myAction == null || exchange.WinnerAction != myAction)
            return;

        if (!exchange.IsOneSided &&
            exchange.IsDuelExchange &&
            myAction.Skill?.Definition?.SkillId == OlafSkillIds.HeadOn)
        {
            int selfDamage = CalculateHeadOnSelfDamage(exchange.TotalDamage);
            if (selfDamage > 0)
            {
                DamageRequest request = DamageRequest.SelfCost(owner, selfDamage);
                request.SourceAction = myAction;
                battleContext?.ResolveDamageManager()?.ApplyDamageContext(request);
            }
        }
    }

    private void OnActionEnd(BattleAction action)
    {
        if (action?.ActionId > 0)
            bloodOathTierByAction.Remove(action.ActionId);
    }

    private void ApplyOwnerNumericStatus(
        StatusEffectId id,
        int value,
        int duration,
        BattleAction sourceAction)
    {
        StatusEffect status =
            StatusEffectFactory.CreateCanonical0922(id, value, duration);

        if (status == null || owner == null)
            return;

        battleContext?.EffectResolver?.ApplyCharacterStatus(
            EffectRequest.CharacterStatus(
                owner,
                owner,
                status,
                sourceAction,
                sourceAction?.CurrentRollIndex ?? -1));
    }

    private void ActivateInfiniteAttackSpeed()
    {
        infiniteAttackSpeedThisTurn = true;

        ActionManager actionManager = battleContext?.Services?.ActionManager;
        if (actionManager?.Slots == null)
            return;

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner != owner)
                continue;

            bool attackCapableSlot =
                slot.Part == null
                    ? ShouldForceInfiniteAttackSpeed(slot.Skill)
                    : BodyPartSkillAccessPolicy.Allows(
                        slot.Part,
                        ActionType.NormalAttack);

            if (attackCapableSlot)
                slot.Speed = InfiniteAttackSpeed;
        }
    }

    private int ConsumeAllOwnerBleeding()
    {
        if (owner == null)
            return 0;

        int total = 0;
        Bleeding characterBleeding = owner.GetStatus<Bleeding>();
        if (characterBleeding != null)
        {
            total += characterBleeding.ConsumeAll();
            owner.RemoveStatus(characterBleeding, StatusEffectRemoveReason.Manual);
        }

        if (owner.BodyParts == null)
            return total;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            Bleeding bleeding = owner.GetPartStatus<Bleeding>(part);
            if (bleeding == null)
                continue;

            total += bleeding.ConsumeAll();
            owner.RemovePartStatus(part, bleeding, StatusEffectRemoveReason.Manual);
        }

        return total;
    }

    private void OnTurnEnd(int _)
    {
        infiniteAttackSpeedThisTurn = false;
        ClearSuppressedSlots();
    }

    private void ClearSuppressedSlots()
    {
        ActionManager actionManager = battleContext?.Services?.ActionManager;
        if (actionManager?.Slots == null)
            return;

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner == owner)
                slot.SuppressOneSidedResolution = false;
        }
    }

    private static string GetSkillKey(SkillDefinition definition) =>
        definition == null
            ? string.Empty
            : definition.SkillId ?? string.Empty;
}
