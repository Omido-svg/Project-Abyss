using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase D O-05. 올라프 특수 카드가 전용 하드코딩 분기를 늘리지 않고
/// 공통 전투 계약 위에서 동작하도록 하는 런타임 해석기.
/// </summary>
public sealed class OlafRulebreakerMechanic : CombatMechanic
{
    public const int InfiniteAttackSpeed = 1_000_000;

    private readonly Dictionary<string, int> committedUses =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private bool infiniteAttackSpeedThisTurn;

    public override string MechanicName => "Olaf Rulebreakers";
    public bool HasInfiniteAttackSpeedThisTurn => infiniteAttackSpeedThisTurn;

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");
    }

    public override void OnUnregister()
    {
        committedUses.Clear();
        infiniteAttackSpeedThisTurn = false;
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

        if (rule.ApplyCurrentLastStandPowerBonus &&
            battleContext?.Services?.MomentumManager?.GetCurrentBand(owner) == MomentumState.LastStand)
        {
            action.RulebreakerFlatPowerBonus += rule.CurrentLastStandPowerBonus;
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

            // 도사림이 속도 굴림 뒤, 공격 스킬 배치 전에 실행되어도
            // 팔/머리의 공격 슬롯은 즉시 무한속도가 되어야 한다.
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
