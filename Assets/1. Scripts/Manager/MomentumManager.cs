using UnityEngine;

public enum MomentumState
{
    LastStand,      // -100 ~ -70
    Disadvantage,   // -70 ~ -30
    Balance,        // -30 ~ +30
    Advantage,      // +30 ~ +70
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
        int value =
            owner == battleContext.Player
                ? CurrentMomentum
                : -CurrentMomentum;

        if (value <= -70)
            return MomentumState.LastStand;

        if (value <= -30)
            return MomentumState.Disadvantage;

        if (value < 30)
            return MomentumState.Balance;

        if (value < 70)
            return MomentumState.Advantage;

        return MomentumState.Overwhelm;
    }

    //------------------------------------------------

    public bool IsLastStand(Character c)
        => GetState(c) == MomentumState.LastStand;

    public bool IsOverwhelm(Character c)
        => GetState(c) == MomentumState.Overwhelm;

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
        }

        return 1f;
    }

    //------------------------------------------------

    public float GetDamageTakenMultiplier(Character target)
    {
        switch (GetState(target))
        {
            case MomentumState.LastStand:
                return 2f;

            case MomentumState.Disadvantage:
                return 1f;

            case MomentumState.Balance:
                return 0.4f;

            case MomentumState.Advantage:
                return 0.4f;

            case MomentumState.Overwhelm:
                return 0.4f;
        }

        return 1f;
    }

    //------------------------------------------------
    // 턴 종료 감쇄
    //------------------------------------------------

    public void DecayMomentum()
    {
        int decay = GetDecay();

        if (CurrentMomentum > 0)
            CurrentMomentum = Mathf.Max(0, CurrentMomentum + decay);

        else if (CurrentMomentum < 0)
            CurrentMomentum = Mathf.Min(0, CurrentMomentum - decay);
    }

    private int GetDecay()
    {
        int abs = Mathf.Abs(CurrentMomentum);

        if (abs >= 70)
            return -12;

        if (abs >= 30)
            return -12;

        return -5;
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
        Character loser,
        int clashGap)
    {
        MomentumState before = GetState(winner);

        int shift = CalculateMomentumShift(clashGap);

        if (winner == battleContext.Player)
            CurrentMomentum += shift;
        else
            CurrentMomentum -= shift;

        CurrentMomentum =
            Mathf.Clamp(
                CurrentMomentum,
                MinMomentum,
                MaxMomentum);

        MomentumState after = GetState(winner);

        return before != MomentumState.Overwhelm &&
               after == MomentumState.Overwhelm;
    }

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
    // 위세 획득
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
}