using UnityEngine;

public enum MomentumState
{
    Balance,       // 중앙
    Advantage,     // 우세
    Overwhelm      // 짓눌림
}


public class MomentumManager
{
    public BattleContext battleContext;
    public MomentumManager(BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }
    
    //--------------------------------
    // 설정값
    //--------------------------------

    // 최대 기세
    public float MaxMomentum = 50f;
    public float AdvantageThreshold = 30f;
    public float OverwhelmThreshold = 50f;
    
    // 발악
    public float LastStandThreshold = 30f;
    // 발악 보너스
    public int LastStandBonus = 5;

    // 턴 종료 시 복귀량
    public float Decay = 20f;

    // 현재 기세
    // +면 플레이어 우세
    // -면 적 우세
    public float CurrentMomentum { get; private set; }
    
    public MomentumState GetState(Character owner)
    {
        bool isPlayer = owner == battleContext.Player;

        float momentum =
            isPlayer ?
            CurrentMomentum :
            -CurrentMomentum;

        if (momentum >= OverwhelmThreshold)
            return MomentumState.Overwhelm;

        if (momentum >= AdvantageThreshold)
            return MomentumState.Advantage;

        return MomentumState.Balance;
    }

    //--------------------------------

    public void Reset()
    {
        CurrentMomentum = 0;
    }

    //--------------------------------
    // 플레이어가 합 승리
    //--------------------------------

    public void PushPlayer(float amount)
    {
        CurrentMomentum += amount;

        CurrentMomentum =
            Mathf.Clamp(
                CurrentMomentum,
                -MaxMomentum,
                MaxMomentum);
    }

    //--------------------------------
    // 적이 합 승리
    //--------------------------------

    public void PushEnemy(float amount)
    {
        CurrentMomentum -= amount;

        CurrentMomentum =
            Mathf.Clamp(
                CurrentMomentum,
                -MaxMomentum,
                MaxMomentum);
    }

    //--------------------------------
    // 턴 종료 감쇠
    //--------------------------------

    public void DecayMomentum()
    {
        if (CurrentMomentum > 0)
        {
            CurrentMomentum =
                Mathf.Max(
                    0,
                    CurrentMomentum - Decay);
        }
        else if (CurrentMomentum < 0)
        {
            CurrentMomentum =
                Mathf.Min(
                    0,
                    CurrentMomentum + Decay);
        }
    }

    //--------------------------------
    // 데미지 배율
    //--------------------------------

    public float GetDamageMultiplier(Character attacker)
    {
        switch (GetState(attacker))
        {
            case MomentumState.Balance:
                return 0.4f;

            case MomentumState.Advantage:
                return 1.0f;

            case MomentumState.Overwhelm:
                return 2.0f;
        }

        return 1f;
    }
    //--------------------------------

    public bool IsPlayerOverwhelm()
    {
        return CurrentMomentum >= MaxMomentum;
    }

    public bool IsEnemyOverwhelm()
    {
        return CurrentMomentum <= -MaxMomentum;
    }
    
    public bool ApplyClashResult(
        Character winner,
        Character loser,
        int clashGap)
    {
        MomentumState before = GetState(winner);

        // 기세 이동
        CurrentMomentum += clashGap;

        // 적이 이겼으면 반대 방향
        if (winner != battleContext.Player)
            CurrentMomentum -= clashGap;

        CurrentMomentum = Mathf.Clamp(
            CurrentMomentum,
            -MaxMomentum,
            MaxMomentum);

        MomentumState after = GetState(winner);

        // 이번 합으로 처음 Overwhelm에 진입했는가?
        return before != MomentumState.Overwhelm &&
            after == MomentumState.Overwhelm;
    }
    
    public bool IsOverwhelm(Character owner)
    {
        return GetState(owner) == MomentumState.Overwhelm;
    }
    
    public bool IsLastStand(Character owner)
    {
        bool isPlayer = owner == battleContext.Player;

        if (isPlayer)
            return CurrentMomentum <= -LastStandThreshold;

        return CurrentMomentum >= LastStandThreshold;
    }
    
    public int ApplyLastStand(Character owner, int power)
    {
        if (IsLastStand(owner))
            return power + LastStandBonus;

        return power;
    }
    
    public int CalculatePrestigeGain(int gap)
    {
        return Mathf.Clamp(gap, 0, 10); // 예: cap 필수
    }
}