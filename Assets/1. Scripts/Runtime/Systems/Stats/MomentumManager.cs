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

    public MomentumShiftResult(
        int before,
        int after,
        int signedShift,
        MomentumShiftReason reason)
    {
        Before = before;
        After = after;
        SignedShift = signedShift;
        Reason = reason;
    }
}

public class MomentumManager
{
    private readonly BattleContext battleContext;
    private readonly MomentumRuleSettings settings;

    public const int MaxMomentum = 100;
    public const int MinMomentum = -100;

    public int CurrentMomentum { get; private set; }

    public MomentumRuleSettings Settings => settings;

    public MomentumManager(
        BattleContext battleContext)
    {
        this.battleContext = battleContext;
        settings = battleContext?.Rules?.Momentum ??
                   new MomentumRuleSettings();
        settings.Normalize();
    }

    public void Reset()
    {
        CurrentMomentum = 0;
    }

    public MomentumState GetState(
        Character owner)
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

    public int GetPerspectiveValue(
        Character owner)
    {
        if (owner == null)
            return 0;

        return IsPlayerSide(owner)
            ? CurrentMomentum
            : -CurrentMomentum;
    }

    public bool IsLastStand(Character character) =>
        GetState(character) == MomentumState.LastStand;

    public bool IsOverwhelm(Character character) =>
        GetState(character) == MomentumState.Overwhelm;

    public bool CanStandardBreakPart(
        Character attacker) =>
        IsOverwhelm(attacker);

    /// <summary>
    /// 새 설계에서 전투 피해에 허용되는 유일한 곱연산.
    /// 공격자 관점 기세 구간만 읽으며 대상 쪽 배율을 다시 곱하지 않는다.
    /// </summary>
    public float GetDamageMultiplier(
        Character attacker)
    {
        int perspective =
            GetPerspectiveValue(attacker);

        switch (GetState(attacker))
        {
            case MomentumState.LastStand:
                return settings.LastStandMultiplier;

            case MomentumState.Disadvantage:
                return settings.DisadvantageMultiplier;

            case MomentumState.Balance:
                return settings.BalanceMultiplier;

            case MomentumState.Advantage:
                return settings.AdvantageMultiplier;

            case MomentumState.Overwhelm:
            {
                float t = Mathf.InverseLerp(
                    settings.OverwhelmThreshold,
                    settings.Maximum,
                    perspective);

                return Mathf.Lerp(
                    settings.OverwhelmMultiplier,
                    settings.MaximumOverwhelmMultiplier,
                    t);
            }

            default:
                return 1f;
        }
    }

    // 기존 호출부 호환. 새 설계에서는 대상 측 배율을 별도로 곱하지 않는다.
    public float GetDamageTakenMultiplier(
        Character target) => 1f;

    /// <summary>
    /// 성공한 교환 또는 일방 공격 한 번의 히트 이동.
    /// 발악 구간에서는 히트 이동량만 배수 적용한다.
    /// </summary>
    public MomentumShiftResult ApplyHit(
        Character attacker)
    {
        int amount = settings.HitShift;

        if (IsLastStand(attacker))
        {
            amount *=
                settings.LastStandHitShiftMultiplier;
        }

        return ApplyShift(
            attacker,
            amount,
            MomentumShiftReason.Hit);
    }

    /// <summary>
    /// 결투 대 결투의 최종 다수결 승자에게 주는 추가 푸시.
    /// 교환별 히트 이동과 별개로 한 번만 적용한다.
    /// </summary>
    public MomentumShiftResult ApplyDuelVictory(
        Character winner,
        int skillBonus = 0)
    {
        int amount =
            settings.DuelVictoryShift +
            Mathf.Max(0, skillBonus);

        return ApplyShift(
            winner,
            amount,
            MomentumShiftReason.DuelVictory);
    }

    public MomentumShiftResult ApplySkillShift(
        Character pusher,
        int amount)
    {
        return ApplyShift(
            pusher,
            amount,
            MomentumShiftReason.Skill);
    }

    public MomentumShiftResult ApplyShift(
        Character pusher,
        int amount,
        MomentumShiftReason reason)
    {
        int before = CurrentMomentum;

        if (pusher == null || amount <= 0)
        {
            return new MomentumShiftResult(
                before,
                before,
                0,
                reason);
        }

        int signed = IsPlayerSide(pusher)
            ? amount
            : -amount;

        CurrentMomentum = Mathf.Clamp(
            CurrentMomentum + signed,
            settings.Minimum,
            settings.Maximum);

        int applied = CurrentMomentum - before;

        if (applied != 0)
        {
            Debug.Log(
                $"[Momentum] " +
                $"Reason={reason}, " +
                $"Pusher={pusher.Data?.CharacterName}, " +
                $"Before={before}, Shift={applied}, " +
                $"After={CurrentMomentum}");
        }

        return new MomentumShiftResult(
            before,
            CurrentMomentum,
            applied,
            reason);
    }

    private bool IsPlayerSide(
        Character character)
    {
        return character != null &&
               character == battleContext?.Player;
    }

    // -------------------------------------------------
    // Legacy compatibility APIs
    // -------------------------------------------------

    /// <summary>
    /// 기세는 스스로 중앙으로 돌아오지 않는다.
    /// 과거 호출부가 남아 있어도 아무 변화가 없도록 유지한다.
    /// </summary>
    public void DecayMomentum()
    {
    }

    /// <summary>
    /// 발악은 더 이상 합 위력 보너스가 아니다.
    /// 히트의 기세 이동량을 두 배로 만드는 규칙이다.
    /// </summary>
    public int ApplyLastStand(
        Character owner,
        int power) => power;

    public bool ApplyClashResult(
        Character winner,
        int clashGap,
        int bonusShift = 0)
    {
        MomentumState before = GetState(winner);

        MomentumShiftResult result =
            ApplySkillShift(
                winner,
                CalculateMomentumShift(clashGap) +
                Mathf.Max(0, bonusShift));

        return result.Changed &&
               before != MomentumState.Overwhelm &&
               GetState(winner) == MomentumState.Overwhelm;
    }

    public int CalculateMomentumShift(
        int gap)
    {
        return gap > 0
            ? settings.HitShift
            : 0;
    }

    public int CalculatePrestigeGain(
        int gap)
    {
        return gap > 0
            ? battleContext?.Rules?.Prestige
                ?.ClashVictoryCharge ?? 0
            : 0;
    }

    public void SetMomentumForDebug(
        float value)
    {
        CurrentMomentum = Mathf.Clamp(
            Mathf.RoundToInt(value),
            settings.Minimum,
            settings.Maximum);

        Debug.Log(
            $"[DEBUG TUNER] Momentum set : " +
            $"{CurrentMomentum}");
    }
}
