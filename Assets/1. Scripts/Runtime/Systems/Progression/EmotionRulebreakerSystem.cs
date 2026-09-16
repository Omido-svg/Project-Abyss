using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase C (C-35) battle-scoped rulebreaker infrastructure.
///
/// Tier-3 emotion augments are intentionally authored as data and call this service instead of
/// adding one-off branches to Character/Turn/Damage code.  The service owns only temporary
/// battle state; the actual 63 augment assets are Phase E data work.
/// </summary>
public sealed class EmotionRulebreakerService
{
    private sealed class CharacterState
    {
        public int PreserveBrokenPartSlotCharges;
        public readonly HashSet<string> PreservedBrokenPartSlots =
            new(StringComparer.Ordinal);
        public readonly Dictionary<string, int> AdditionalSlots =
            new(StringComparer.Ordinal);
        public readonly HashSet<string> SuppressedParts =
            new(StringComparer.Ordinal);

        public bool HasHpResistanceOverride;
        public float HpResistanceOverride = 1f;
        public bool StaggerGaugeSuppressed;
        public bool DeathResistanceDisabled;

        public int OverwhelmStreak;
        public int OverwhelmStreakRequired;
        public bool OverwhelmAscensionTriggered;
        public float OverwhelmEnemyHpResistanceOverride = 2f;

        public int ReviveCharges;
        public float ReviveHpRatio = 0.30f;
        public bool ReviveWeakensAllParts = true;

        public int LastStandStreak;
        public int LastStandStreakRequired;
        public int LastStandReviveCharges;
        public float LastStandReviveHpRatio = 0.30f;
        public bool LastStandReviveTriggered;

        public int BalanceStreak;
        public int BalanceStreakRequired;
        public bool BalanceRepeatPaysEnergyAgain = true;
    }

    private readonly BattleContext context;
    private readonly Dictionary<Character, CharacterState> states = new();

    private bool carryPositiveMomentum;
    private bool carryNegativeMomentum;
    private int carriedMomentum;

    private int pendingPlayerResolutionRepeats;
    private bool pendingRepeatPaysEnergyAgain = true;

    public EmotionRulebreakerService(BattleContext context)
    {
        this.context = context;
    }

    public void ResetForBattle()
    {
        states.Clear();
        carryPositiveMomentum = false;
        carryNegativeMomentum = false;
        carriedMomentum = 0;
        pendingPlayerResolutionRepeats = 0;
        pendingRepeatPaysEnergyAgain = true;
    }

    private CharacterState State(Character owner, bool create = true)
    {
        if (owner == null)
            return null;

        if (states.TryGetValue(owner, out CharacterState state))
            return state;

        if (!create)
            return null;

        state = new CharacterState();
        states[owner] = state;
        return state;
    }

    private static string PartKey(BodyPart part) =>
        part == null
            ? string.Empty
            : (!string.IsNullOrWhiteSpace(part.PartId)
                ? part.PartId
                : part.Type.ToString());

    public void GrantBrokenPartSlotPreservation(Character owner, int charges = 1)
    {
        CharacterState state = State(owner);
        if (state == null)
            return;

        state.PreserveBrokenPartSlotCharges += Mathf.Max(0, charges);
    }

    public bool TryPreserveSlotsOnBreak(Character owner, BodyPart part)
    {
        CharacterState state = State(owner, false);
        if (state == null || part == null || state.PreserveBrokenPartSlotCharges <= 0)
            return false;

        string key = PartKey(part);
        if (state.PreservedBrokenPartSlots.Contains(key))
            return true;

        state.PreserveBrokenPartSlotCharges--;
        state.PreservedBrokenPartSlots.Add(key);
        return true;
    }

    public bool IsBrokenPartSlotPreserved(Character owner, BodyPart part)
    {
        CharacterState state = State(owner, false);
        return state != null &&
               part != null &&
               state.PreservedBrokenPartSlots.Contains(PartKey(part));
    }

    public void AddPartSlots(Character owner, PartType partType, int amount)
    {
        if (owner?.BodyParts == null || amount == 0)
            return;

        CharacterState state = State(owner);
        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || part.Type != partType)
                continue;

            string key = PartKey(part);
            state.AdditionalSlots.TryGetValue(key, out int current);
            state.AdditionalSlots[key] = Mathf.Max(0, current + amount);
        }
    }

    public int GetAdditionalSlotCount(Character owner, BodyPart part)
    {
        CharacterState state = State(owner, false);
        if (state == null || part == null)
            return 0;

        return state.AdditionalSlots.TryGetValue(PartKey(part), out int count)
            ? Mathf.Max(0, count)
            : 0;
    }

    public void SetPartSlotsSuppressed(Character owner, PartType partType, bool suppressed)
    {
        if (owner?.BodyParts == null)
            return;

        CharacterState state = State(owner);
        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || part.Type != partType)
                continue;

            string key = PartKey(part);
            if (suppressed)
                state.SuppressedParts.Add(key);
            else
                state.SuppressedParts.Remove(key);
        }
    }

    public bool ArePartSlotsSuppressed(Character owner, BodyPart part)
    {
        CharacterState state = State(owner, false);
        return state != null &&
               part != null &&
               state.SuppressedParts.Contains(PartKey(part));
    }

    public bool RegenerateOneBrokenPartAsWeakened(
        Character owner,
        PartType? preferredPartType = null)
    {
        if (owner?.BodyParts == null)
            return false;

        BodyPart selected = null;
        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || !part.IsBroken)
                continue;

            if (preferredPartType.HasValue && part.Type != preferredPartType.Value)
                continue;

            selected = part;
            break;
        }

        if (selected == null && preferredPartType.HasValue)
        {
            foreach (BodyPart part in owner.BodyParts)
            {
                if (part != null && part.IsBroken)
                {
                    selected = part;
                    break;
                }
            }
        }

        return selected != null &&
               owner.RegenerateBrokenPartAsWeakened(selected);
    }

    public void SetHpResistanceOverride(Character owner, float multiplier)
    {
        CharacterState state = State(owner);
        if (state == null)
            return;

        state.HasHpResistanceOverride = true;
        state.HpResistanceOverride = Mathf.Max(0f, multiplier);
    }

    public void ClearHpResistanceOverride(Character owner)
    {
        CharacterState state = State(owner, false);
        if (state != null)
            state.HasHpResistanceOverride = false;
    }

    public bool TryGetHpResistanceOverride(Character owner, out float multiplier)
    {
        CharacterState state = State(owner, false);
        if (state?.HasHpResistanceOverride == true)
        {
            multiplier = state.HpResistanceOverride;
            return true;
        }

        multiplier = 1f;
        return false;
    }

    public void SetStaggerGaugeSuppressed(Character owner, bool suppressed)
    {
        CharacterState state = State(owner);
        if (state != null)
            state.StaggerGaugeSuppressed = suppressed;
    }

    public bool IsStaggerGaugeSuppressed(Character owner) =>
        State(owner, false)?.StaggerGaugeSuppressed == true;

    public void SetDeathResistanceDisabled(Character owner, bool disabled)
    {
        CharacterState state = State(owner);
        if (state != null)
            state.DeathResistanceDisabled = disabled;
    }

    public bool IsDeathResistanceDisabled(Character owner) =>
        State(owner, false)?.DeathResistanceDisabled == true;

    public void ConfigureOverwhelmAscension(
        Character owner,
        int requiredConsecutiveTurns,
        float enemyHpResistanceOverride = 2f)
    {
        CharacterState state = State(owner);
        if (state == null)
            return;

        state.OverwhelmStreak = 0;
        state.OverwhelmStreakRequired = Mathf.Max(1, requiredConsecutiveTurns);
        state.OverwhelmAscensionTriggered = false;
        state.OverwhelmEnemyHpResistanceOverride = Mathf.Max(0f, enemyHpResistanceOverride);
    }

    public void ConfigureMomentumCarry(bool carryPositive, bool carryNegative)
    {
        carryPositiveMomentum = carryPositive;
        carryNegativeMomentum = carryNegative;
        if (!carryPositive && !carryNegative)
            carriedMomentum = 0;
    }

    public void CaptureMomentumForNextTurn(int playerPerspectiveMomentum)
    {
        bool carry =
            (playerPerspectiveMomentum > 0 && carryPositiveMomentum) ||
            (playerPerspectiveMomentum < 0 && carryNegativeMomentum);

        carriedMomentum = carry
            ? Mathf.Clamp(playerPerspectiveMomentum, MomentumManager.MinMomentum, MomentumManager.MaxMomentum)
            : 0;
    }

    public int ConsumeCarriedMomentum()
    {
        int result = carriedMomentum;
        carriedMomentum = 0;
        return result;
    }

    public void ConfigureRevive(
        Character owner,
        int charges,
        float hpRatio,
        bool weakenAllParts = true)
    {
        CharacterState state = State(owner);
        if (state == null)
            return;

        state.ReviveCharges = Mathf.Max(0, charges);
        state.ReviveHpRatio = Mathf.Clamp01(hpRatio);
        state.ReviveWeakensAllParts = weakenAllParts;
    }

    public void ConfigureLastStandStreakRevive(
        Character owner,
        int requiredConsecutiveTurns,
        int charges,
        float hpRatio)
    {
        CharacterState state = State(owner);
        if (state == null)
            return;

        state.LastStandStreak = 0;
        state.LastStandStreakRequired = Mathf.Max(1, requiredConsecutiveTurns);
        state.LastStandReviveTriggered = false;
        state.LastStandReviveCharges = Mathf.Max(0, charges);
        state.LastStandReviveHpRatio = Mathf.Clamp01(hpRatio);
    }

    public void ConfigureBalanceResolutionRepeat(
        Character owner,
        int requiredConsecutiveTurns,
        bool payEnergyAgain = true)
    {
        CharacterState state = State(owner);
        if (state == null)
            return;

        state.BalanceStreak = 0;
        state.BalanceStreakRequired = Mathf.Max(1, requiredConsecutiveTurns);
        state.BalanceRepeatPaysEnergyAgain = payEnergyAgain;
    }

    public void ObserveTurnFinalState(Character owner, MomentumState finalState)
    {
        CharacterState state = State(owner, false);
        if (state == null)
            return;

        if (state.OverwhelmStreakRequired > 0 && !state.OverwhelmAscensionTriggered)
        {
            state.OverwhelmStreak =
                finalState == MomentumState.Overwhelm
                    ? state.OverwhelmStreak + 1
                    : 0;

            if (state.OverwhelmStreak >= state.OverwhelmStreakRequired)
            {
                state.OverwhelmAscensionTriggered = true;
                state.OverwhelmStreak = 0;

                if (context?.Enemies != null)
                {
                    foreach (Character enemy in context.Enemies)
                    {
                        if (enemy == null || enemy.IsDead)
                            continue;

                        SetDeathResistanceDisabled(enemy, true);
                        SetHpResistanceOverride(
                            enemy,
                            state.OverwhelmEnemyHpResistanceOverride);
                    }
                }
            }
        }

        if (state.LastStandStreakRequired > 0 && !state.LastStandReviveTriggered)
        {
            state.LastStandStreak =
                finalState == MomentumState.LastStand
                    ? state.LastStandStreak + 1
                    : 0;

            if (state.LastStandStreak >= state.LastStandStreakRequired)
            {
                state.LastStandStreak = 0;
                state.LastStandReviveTriggered = true;
                state.ReviveCharges += state.LastStandReviveCharges;
                state.ReviveHpRatio = state.LastStandReviveHpRatio;
                state.ReviveWeakensAllParts = true;
            }
        }
    }

    public void ObservePrimaryResolutionState(Character owner, MomentumState stateAfterPrimaryResolution)
    {
        CharacterState state = State(owner, false);
        if (state == null || state.BalanceStreakRequired <= 0)
            return;

        state.BalanceStreak =
            stateAfterPrimaryResolution == MomentumState.Balance
                ? state.BalanceStreak + 1
                : 0;

        if (state.BalanceStreak < state.BalanceStreakRequired)
            return;

        state.BalanceStreak = 0;
        RequestPlayerResolutionRepeat(state.BalanceRepeatPaysEnergyAgain);
    }

    public bool TryConsumeRevive(Character owner)
    {
        CharacterState state = State(owner, false);
        if (state == null || state.ReviveCharges <= 0)
            return false;

        state.ReviveCharges--;
        return owner != null &&
               owner.RestoreFromRulebreakerRevive(
                   state.ReviveHpRatio,
                   state.ReviveWeakensAllParts);
    }

    public void RequestPlayerResolutionRepeat(bool payEnergyAgain = true)
    {
        pendingPlayerResolutionRepeats++;
        pendingRepeatPaysEnergyAgain &= payEnergyAgain;
    }

    public bool TryConsumePlayerResolutionRepeat(out bool payEnergyAgain)
    {
        if (pendingPlayerResolutionRepeats <= 0)
        {
            payEnergyAgain = false;
            return false;
        }

        pendingPlayerResolutionRepeats--;
        payEnergyAgain = pendingRepeatPaysEnergyAgain;
        if (pendingPlayerResolutionRepeats <= 0)
            pendingRepeatPaysEnergyAgain = true;
        return true;
    }
}

public enum EmotionRulebreakerOperation
{
    PreserveNextBrokenPartSlots = 0,
    AddPartSlots = 1,
    SuppressPartSlots = 2,
    RegenerateBrokenPartAsWeakened = 3,
    OverrideHpResistance = 4,
    DisableStaggerGauge = 5,
    CarryPositiveMomentum = 6,
    CarryNegativeMomentum = 7,
    ConfigureLastStandRevive = 8,
    ConfigureBalanceResolutionRepeat = 9,
    ConfigureOverwhelmAscension = 10
}

/// <summary>
/// C-35 authoring bridge. Phase E emotion assets can use this definition without adding
/// character-specific runtime branches.
/// </summary>
