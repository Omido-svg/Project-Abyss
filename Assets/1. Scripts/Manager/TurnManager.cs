using System;

public class TurnManager
{
    BattleContext _battleContext;
    public TurnManager(BattleContext _battleContext)
    {
        this._battleContext = _battleContext;
    }
    
    /// 현재 턴 번호
    public int CurrentTurn { get; private set; }

    /// 전투가 진행 중인지
    public bool IsBattleRunning { get; private set; }
    
    /// 전투 시작
    public void StartBattle()
    {
        CurrentTurn = 1;
        IsBattleRunning = true;

        StartTurn();
    }

    /// 현재 턴 시작
    public void StartTurn()
    {
        _battleContext._battleEvent.RaiseTurnStarted(CurrentTurn);
    }

    /// 현재 턴 종료
    public void EndTurn()
    {
        _battleContext._battleEvent.RaiseTurnEnded(CurrentTurn);
        CurrentTurn++;
    }

    /// 다음 턴 시작
    public void NextTurn()
    {
        StartTurn();
    }

    /// 전투 종료
    public void EndBattle()
    {
        IsBattleRunning = false;
    }
}