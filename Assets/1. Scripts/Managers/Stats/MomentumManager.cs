using UnityEngine;

public enum MomentumState
{
    LastStand,      // -100 ~ -70
    Disadvantage,   // -69 ~ -31
    Balance,        // -30 ~ +30
    Advantage,      // +31 ~ +69
    Overwhelm       // +70 ~ +100
}

public class MomentumManager
{
    private readonly BattleContext battleContext;

    public MomentumManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    //------------------------------------------------
    // 설정
    //------------------------------------------------

    public const int MaxMomentum = 100;
    public const int MinMomentum = -100;

    public const int LastStandBonus = 5;

    private const int LowDecayAmount = 5;
    private const int HighDecayAmount = 12;

    //------------------------------------------------

    public int CurrentMomentum { get; private set; }

    //------------------------------------------------

    public void Reset()
    {
        CurrentMomentum = 0;
    }

    //------------------------------------------------
    // 상태
    //------------------------------------------------

    public MomentumState GetState(Character owner)
    {
        if (owner == null)
            return MomentumState.Balance;

        int value =
            IsPlayerSide(owner)
                ? CurrentMomentum
                : -CurrentMomentum;

        if (value <= -70)
            return MomentumState.LastStand;

        if (value < -30)
            return MomentumState.Disadvantage;

        if (value <= 30)
            return MomentumState.Balance;

        if (value < 70)
            return MomentumState.Advantage;

        return MomentumState.Overwhelm;
    }

    //------------------------------------------------

    private bool IsPlayerSide(Character character)
    {
        return character == battleContext.Player;
    }

    //------------------------------------------------

    public bool IsLastStand(Character character)
    {
        return GetState(character) == MomentumState.LastStand;
    }

    public bool IsOverwhelm(Character character)
    {
        return GetState(character) == MomentumState.Overwhelm;
    }

    //------------------------------------------------
    // 데미지
    //------------------------------------------------

    public float GetDamageMultiplier(Character attacker)
    {
        switch (GetState(attacker))
        {
            case MomentumState.LastStand:
                return 0.4f;

            case MomentumState.Disadvantage:
                return 0.4f;

            case MomentumState.Balance:
                return 0.4f;

            case MomentumState.Advantage:
                return 1.0f;

            case MomentumState.Overwhelm:
                return 2.0f;

            default:
                return 1.0f;
        }
    }

    //------------------------------------------------

    public float GetDamageTakenMultiplier(Character target)
    {
        switch (GetState(target))
        {
            case MomentumState.LastStand:
                return 2.0f;

            case MomentumState.Disadvantage:
                return 1.0f;

            case MomentumState.Balance:
                return 0.4f;

            case MomentumState.Advantage:
                return 0.4f;

            case MomentumState.Overwhelm:
                return 0.4f;

            default:
                return 1.0f;
        }
    }

    //------------------------------------------------
    // 턴 종료 감쇄
    //------------------------------------------------

    public void DecayMomentum()
    {
        int decayAmount = GetDecayAmount();

        if (CurrentMomentum > 0)
        {
            CurrentMomentum =
                Mathf.Max(
                    0,
                    CurrentMomentum - decayAmount);
        }
        else if (CurrentMomentum < 0)
        {
            CurrentMomentum =
                Mathf.Min(
                    0,
                    CurrentMomentum + decayAmount);
        }
    }

    //------------------------------------------------

    private int GetDecayAmount()
    {
        int abs = Mathf.Abs(CurrentMomentum);

        if (abs >= 30)
            return HighDecayAmount;

        return LowDecayAmount;
    }

    //------------------------------------------------
    // 발악
    //------------------------------------------------

    public int ApplyLastStand(Character owner, int power)
    {
        if (IsLastStand(owner))
            return power + LastStandBonus;

        return power;
    }

    //------------------------------------------------
    // 기세 이동
    //------------------------------------------------

    public bool ApplyClashResult(
        Character winner,
        int clashGap,
        int bonusShift = 0)
    {
        if (winner == null)
            return false;

        MomentumState before =
            GetState(winner);

        int shift =
            CalculateMomentumShift(clashGap);

        shift += Mathf.Max(0, bonusShift);

        if (shift <= 0)
            return false;

        if (IsPlayerSide(winner))
        {
            CurrentMomentum += shift;

            Debug.Log(
                $"{shift} 만큼 플레이어가 기세를 밀어냄");
        }
        else
        {
            CurrentMomentum -= shift;

            Debug.Log(
                $"{shift} 만큼 적들이 기세를 밀어냄");
        }

        CurrentMomentum =
            Mathf.Clamp(
                CurrentMomentum,
                MinMomentum,
                MaxMomentum);

        MomentumState after =
            GetState(winner);

        return before != MomentumState.Overwhelm &&
            after == MomentumState.Overwhelm;
    }

    //------------------------------------------------
    // 합 차이에 따른 기세 이동량
    //------------------------------------------------

    public int CalculateMomentumShift(int gap)
    {
        if (gap >= 20)
            return 40;

        if (gap >= 10)
            return 25;

        if (gap >= 1)
            return 10;

        return 0;
    }

    //------------------------------------------------
    // 합 차이에 따른 위세 획득량
    //------------------------------------------------

    public int CalculatePrestigeGain(int gap)
    {
        if (gap >= 20)
            return 40;

        if (gap >= 10)
            return 25;

        if (gap >= 1)
            return 10;

        return 0;
    }
    
    public void SetMomentumForDebug(float value)
    {
        CurrentMomentum = (int)Mathf.Clamp(value, -100f, 100f);

        Debug.Log($"[DEBUG TUNER] Momentum set : {CurrentMomentum}");
    }
}