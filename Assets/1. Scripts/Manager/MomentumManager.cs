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
    public float MaxMomentum = 100f;

    // 턴 종료 시 복귀량
    public float Decay = 20f;

    // 현재 기세
    // +면 플레이어 우세
    // -면 적 우세
    public float CurrentMomentum { get; private set; }
    public float OverwhelmThreshold = 30f;
    
    public MomentumState CurrentState
    {
        get
        {
            if(CurrentMomentum > OverwhelmThreshold)
                return MomentumState.Advantage;

            if(CurrentMomentum < -OverwhelmThreshold)
                return MomentumState.Overwhelm;

            return MomentumState.Balance;
        }
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
        bool isPlayer = attacker is not Enemy;

        if(isPlayer)
        {
            switch(CurrentState)
            {
                case MomentumState.Balance: return 0.4f;
                case MomentumState.Advantage: return 1.0f;
                case MomentumState.Overwhelm: return 2.0f;
            }
        }
        else
        {
            switch(CurrentState)
            {
                case MomentumState.Balance: return 0.4f;
                case MomentumState.Advantage: return 0.4f;
                case MomentumState.Overwhelm: return 2.0f;
            }
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
    
    public void ApplyClashResult(
        Character winner,
        Character loser,
        int clashGap)
    {
        // 이긴 만큼 기세 이동
        CurrentMomentum += clashGap;

        // 승자가 적이면 반대 방향
        if (winner != battleContext.Player)
            CurrentMomentum -= clashGap;

        CurrentMomentum = Mathf.Clamp(
            CurrentMomentum,
            -MaxMomentum,
            MaxMomentum);
    }
}