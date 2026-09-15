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

    // 직전 턴 최종 band와 그 결과로 예약된 다음 턴 판정 보너스는
    // 현재 턴의 실시간 Momentum band와 절대 섞지 않는다.
    private MomentumState playerPreviousFinalState = MomentumState.Balance;
    private MomentumState enemyPreviousFinalState = MomentumState.Balance;
    private bool playerLastStandJudgmentThisTurn;
    private bool enemyLastStandJudgmentThisTurn;
    private bool playerLastStandJudgmentNextTurn;
    private bool enemyLastStandJudgmentNextTurn;

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
        playerPreviousFinalState = MomentumState.Balance;
        enemyPreviousFinalState = MomentumState.Balance;
        playerLastStandJudgmentThisTurn = false;
        enemyLastStandJudgmentThisTurn = false;
        playerLastStandJudgmentNextTurn = false;
        enemyLastStandJudgmentNextTurn = false;
    }

    public void BeginTurn()
    {
        playerLastStandJudgmentThisTurn = playerLastStandJudgmentNextTurn;
        enemyLastStandJudgmentThisTurn = enemyLastStandJudgmentNextTurn;
        playerLastStandJudgmentNextTurn = false;
        enemyLastStandJudgmentNextTurn = false;
        CurrentMomentum = 0;
    }

    public void FinalizeTurn()
    {
        playerPreviousFinalState = EvaluateBand(CurrentMomentum);
        enemyPreviousFinalState = EvaluateBand(-CurrentMomentum);
        playerLastStandJudgmentNextTurn = playerPreviousFinalState == MomentumState.LastStand;
        enemyLastStandJudgmentNextTurn = enemyPreviousFinalState == MomentumState.LastStand;
    }

    // 현재 B만 본다. 직전 턴 발악 예약은 이 API의 결과를 덮어쓰지 않는다.
    public MomentumState GetState(Character owner) =>
        EvaluateBand(GetPerspectiveValue(owner));

    public MomentumState GetCurrentBand(Character owner) =>
        GetState(owner);

    public MomentumState GetPreviousTurnFinalState(Character owner)
    {
        if (owner == null) return MomentumState.Balance;
        return IsPlayerSide(owner)
            ? playerPreviousFinalState
            : enemyPreviousFinalState;
    }

    public bool HasLastStandJudgmentBonus(Character owner) =>
        owner?.SupportsLastStand == true &&
        (IsPlayerSide(owner)
            ? playerLastStandJudgmentThisTurn
            : enemyLastStandJudgmentThisTurn);

    public MomentumState GetFinalTurnState(Character owner) =>
        EvaluateBand(GetPerspectiveValue(owner));

    public int GetPerspectiveValue(Character owner)
    {
        if (owner == null) return 0;
        return IsPlayerSide(owner) ? CurrentMomentum : -CurrentMomentum;
    }

    // Legacy 이름은 "직전 턴 짓눌림으로 얻은 이번 턴 판정 보너스"를 뜻한다.
    public bool IsLastStand(Character character) =>
        HasLastStandJudgmentBonus(character);

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

    private MomentumState EvaluateBand(int value)
    {
        if (value <= settings.LastStandThreshold)
            return MomentumState.LastStand;
        if (value <= settings.DisadvantageThreshold)
            return MomentumState.Disadvantage;
        if (value < settings.AdvantageThreshold)
            return MomentumState.Balance;
        if (value < settings.OverwhelmThreshold)
            return MomentumState.Advantage;
        return MomentumState.Overwhelm;
    }

    private bool IsPlayerSide(Character character) =>
        character != null && character == battleContext?.Player;

    public void SetMomentumForDebug(float value)
    {
        CurrentMomentum = Mathf.Clamp(Mathf.RoundToInt(value), settings.Minimum, settings.Maximum);
        Debug.Log($"[DEBUG TUNER] Momentum set : {CurrentMomentum}");
    }
}
