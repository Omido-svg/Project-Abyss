using System;

public class BattleEvent
{
    //-----------------------------------
    // 턴
    //-----------------------------------
    public event Action<int> OnTurnStart;
    public event Action<int> OnTurnEnd;
    
    public void RaiseTurnStarted(int turn)
    {
        OnTurnStart?.Invoke(turn);
    }

    public void RaiseTurnEnded(int turn)
    {
        OnTurnEnd?.Invoke(turn);
    }
    

    //-----------------------------------
    // 행동
    //-----------------------------------
    public event Action<BattleAction> OnActionStart;
    public event Action<BattleAction> OnActionEnd;
    

    //-----------------------------------
    // 합
    //-----------------------------------
    public event Action<BattleAction, BattleAction> OnClashStart;
    public event Action<BattleAction> OnClashWin;
    public event Action<BattleAction> OnClashLose;
    

    //-----------------------------------
    // 데미지
    //-----------------------------------
    public event Action<Character,float> OnDamageTaken;
    public event Action<Character,float> OnDamageDeal;

    //-----------------------------------
    // 상태이상
    //-----------------------------------
    public event Action<Character,StatusEffect> OnStatusApplied;
    public event Action<Character,StatusEffect> OnStatusRemoved;
    

    //-----------------------------------
    // 사망
    //-----------------------------------
    public event Action<Character> OnCharacterDeath;
}