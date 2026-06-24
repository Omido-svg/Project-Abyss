using System;
using UnityEngine;

public class TurnManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;
    private readonly AIManager aiManager;
    private readonly ClashManager clashManager;
    private readonly SpeedManager speedManager;

    //--------------------------------

    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        ClashManager clashManager,
        SpeedManager speedManager)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
        this.aiManager = aiManager;
        this.clashManager = clashManager;
        this.speedManager = speedManager;
    }

    //--------------------------------

    public int CurrentTurn { get; private set; }

    public bool IsBattleRunning { get; private set; }

    //--------------------------------

    /// 전투 시작
    public void StartBattle()
    {
        CurrentTurn = 1;
        IsBattleRunning = true;

        StartTurn();
    }

    //--------------------------------

    /// 현재 턴 시작
    public void StartTurn()
    {
        battleContext._battleEvent.RaiseTurnStart(CurrentTurn);
        speedManager.RollAllSpeed();

        // 모든 캐릭터 턴 시작 처리
        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnStart();
            c.AssignSkills();
        }

        // 이전 턴 행동 제거
        actionManager.Clear();

        // AI 행동 생성
        aiManager.DecideEnemyActions();
        
        Debug.Log("AI 행동 생성 완료");
        Utils.PrintActions((System.Collections.Generic.List<BattleAction>)actionManager.Actions);

        // 플레이어는 UI에서 BattleAction을 생성
    }

    //--------------------------------

    /// 플레이어 입력 완료 후 호출
    public void ResolveTurn()
    {
        actionManager.BuildTurnActions();

        clashManager.Resolve(actionManager.BuildQueue());

        EndTurn();
    }

    //--------------------------------

    /// 현재 턴 종료
    public void EndTurn()
    {
        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnEnd();
        }

        battleContext._battleEvent.RaiseTurnEnd(CurrentTurn);

        CurrentTurn++;
    }

    //--------------------------------

    /// 다음 턴
    public void NextTurn()
    {
        StartTurn();
    }

    //--------------------------------

    /// 전투 종료
    public void EndBattle()
    {
        IsBattleRunning = false;
    }
}