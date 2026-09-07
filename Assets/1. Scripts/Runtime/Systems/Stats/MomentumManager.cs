using UnityEngine;

public enum MomentumState
{
    LastStand,
    Disadvantage,
    Balance,
    Advantage,
    Overwhelm
}

public enum MomentumShiftReason
{
    Hit,
    DuelVictory,
    Skill,
    Debug
}

public readonly struct MomentumShiftResult
{
    public readonly int Before;
    public readonly int After;
    public readonly int SignedShift;
    public readonly MomentumShiftReason Reason;
    public bool Changed => Before != After;

    public MomentumShiftResult(int before, int after, int signedShift, MomentumShiftReason reason)
    {
        Before = before;
        After = after;
        SignedShift = signedShift;
        Reason = reason;
    }
}

/// <summary>
/// Gameplay v5 기세: 매 턴 0에서 시작하는 -100~100 줄다리기 바.
/// 피해 배율을 만들지 않고 턴 종료 위치는 FervorManager가 고조로 환산한다.
/// 발악은 직전 턴을 짓눌린 상태로 끝냈는지를 다음 턴 전체에 고정한다.
/// </summary>
public class MomentumManager
{
    private readonly BattleContext battleContext;
    private readonly MomentumRuleSettings settings;

    private bool playerLastStandThisTurn;
    private bool enemyLastStandThisTurn;
    private bool playerLastStandNextTurn;
    private bool enemyLastStandNextTurn;

    public const int MaxMomentum = 100;
    public const int MinMomentum = -100;

    public int CurrentMomentum { get; private set; }
    public MomentumRuleSettings Settings => settings;

    public MomentumManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
        settings = battleContext?.Rules?.Momentum ?? new MomentumRuleSettings();
        settings.Normalize();
    }

    public void Reset()
    {
        CurrentMomentum = 0;
        playerLastStandThisTurn = false;
        enemyLastStandThisTurn = false;
        playerLastStandNextTurn = false;
        enemyLastStandNextTurn = false;
    }

    public void BeginTurn()
    {
        playerLastStandThisTurn = playerLastStandNextTurn;
        enemyLastStandThisTurn = enemyLastStandNextTurn;
        playerLastStandNextTurn = false;
        enemyLastStandNextTurn = false;
        CurrentMomentum = 0;
    }

    public void FinalizeTurn()
    {
        playerLastStandNextTurn = CurrentMomentum <= settings.LastStandThreshold;
        enemyLastStandNextTurn = -CurrentMomentum <= settings.LastStandThreshold;
    }

    public MomentumState GetState(Character owner)
    {
        if (owner?.SupportsLastStand == true && IsLastStandActive(owner))
            return MomentumState.LastStand;

        int value = GetPerspectiveValue(owner);
        if (value < settings.DisadvantageThreshold)
            return MomentumState.Disadvantage;
        if (value <= settings.AdvantageThreshold)
            return MomentumState.Balance;
        if (value < settings.OverwhelmThreshold)
            return MomentumState.Advantage;
        return MomentumState.Overwhelm;
    }

    public MomentumState GetFinalTurnState(Character owner)
    {
        int value = GetPerspectiveValue(owner);
        if (value <= settings.LastStandThreshold)
            return MomentumState.LastStand;
        if (value < settings.DisadvantageThreshold)
            return MomentumState.Disadvantage;
        if (value <= settings.AdvantageThreshold)
            return MomentumState.Balance;
        if (value < settings.OverwhelmThreshold)
            return MomentumState.Advantage;
        return MomentumState.Overwhelm;
    }

    public int GetPerspectiveValue(Character owner)
    {
        if (owner == null) return 0;
        return IsPlayerSide(owner) ? CurrentMomentum : -CurrentMomentum;
    }

    public bool IsLastStand(Character character) =>
        character?.SupportsLastStand == true && IsLastStandActive(character);

    public bool IsOverwhelm(Character character) =>
        GetFinalTurnState(character) == MomentumState.Overwhelm;

    public bool CanStandardBreakPart(Character attacker) => IsOverwhelm(attacker);

    // Gameplay v5: 기세 구간은 피해량을 절대 곱하지 않는다.
    public float GetDamageMultiplier(Character attacker) => 1f;

    public MomentumShiftResult ApplyHit(Character attacker)
    {
        int amount = settings.HitShift;
        if (attacker?.SupportsLastStand == true && IsLastStand(attacker))
            amount *= settings.LastStandHitShiftMultiplier;
        return ApplyShift(attacker, amount, MomentumShiftReason.Hit);
    }

    /// <summary>
    /// Duel vs Duel의 개별 교환 승리가 만드는 총 이동량이다.
    /// HitShift에 추가하는 값이 아니므로 ClashManager는 Duel 교환에서 ApplyHit과 중복 호출하지 않는다.
    /// </summary>
    public MomentumShiftResult ApplyDuelExchangeVictory(Character winner, int skillBonus = 0)
    {
        int amount = settings.DuelExchangeTotalShift + Mathf.Max(0, skillBonus);
        if (winner?.SupportsLastStand == true && IsLastStand(winner))
            amount = Mathf.Max(amount, settings.HitShift * settings.LastStandHitShiftMultiplier);
        return ApplyShift(winner, amount, MomentumShiftReason.DuelVictory);
    }

    public MomentumShiftResult ApplySkillShift(Character pusher, int amount) =>
        ApplyShift(pusher, amount, MomentumShiftReason.Skill);

    public MomentumShiftResult ApplyShift(Character pusher, int amount, MomentumShiftReason reason)
    {
        int before = CurrentMomentum;
        if (pusher == null || amount <= 0)
            return new MomentumShiftResult(before, before, 0, reason);

        int signed = IsPlayerSide(pusher) ? amount : -amount;
        CurrentMomentum = Mathf.Clamp(CurrentMomentum + signed, settings.Minimum, settings.Maximum);
        int applied = CurrentMomentum - before;

        if (applied != 0)
        {
            Debug.Log($"[Momentum] Reason={reason}, Pusher={pusher.Data?.CharacterName}, Before={before}, Shift={applied}, After={CurrentMomentum}");
        }
        return new MomentumShiftResult(before, CurrentMomentum, applied, reason);
    }

    private bool IsLastStandActive(Character character) =>
        IsPlayerSide(character) ? playerLastStandThisTurn : enemyLastStandThisTurn;

    private bool IsPlayerSide(Character character) =>
        character != null && character == battleContext?.Player;

    public void SetMomentumForDebug(float value)
    {
        CurrentMomentum = Mathf.Clamp(Mathf.RoundToInt(value), settings.Minimum, settings.Maximum);
        Debug.Log($"[DEBUG TUNER] Momentum set : {CurrentMomentum}");
    }
}
