using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;
    private readonly AIManager aiManager;
    private readonly ClashManager clashManager;
    private readonly SpeedManager speedManager;
    private readonly ActionResolver actionResolver;
    private readonly MomentumManager momentumManager;

    //--------------------------------

    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        ClashManager clashManager,
        SpeedManager speedManager,
        ActionResolver actionResolver,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
        this.aiManager = aiManager;
        this.clashManager = clashManager;
        this.speedManager = speedManager;
        this.actionResolver = actionResolver;
        this.momentumManager = momentumManager;
    }

    //--------------------------------

    public int CurrentTurn { get; private set; }

    public bool IsBattleRunning { get; private set; }

    //--------------------------------

    // 전투 시작
    public void StartBattle()
    {
        CurrentTurn = 1;
        IsBattleRunning = true;

        StartTurn();
    }

    //--------------------------------

    // 현재 턴 시작
    public void StartTurn()
    {
        // 이전 턴 로그 제거
        battleContext.battleManager.BattleLogger.Clear();

        battleContext._battleEvent.RaiseTurnStart(CurrentTurn);

        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnStart();
        }

        speedManager.RollAllSpeed();

        actionManager.Clear();

        aiManager.DecideEnemyActions();

        // 플레이어 입력 대기
    }


    // 플레이어 입력 완료 후 호출
    public void ResolveTurn()
    {
        Debug.Log("턴을 시작합니다.");
        ActionExecutionQueue executionQueue = actionManager.BuildExecutionQueue();
        actionResolver.Resolve(executionQueue);
        EndTurn();
    }

    // 현재 턴 종료
    public void EndTurn()
    {
        battleContext.battleManager.BattleLogger.PrintTurn();
        
        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnEnd();
        }

        momentumManager.DecayMomentum();

        // 모든 처리가 끝난 뒤 출력
        battleContext.battleManager.BattleLogger.PrintTurn();

        battleContext._battleEvent.RaiseTurnEnd(CurrentTurn);

        CurrentTurn++;
    }

    // 다음 턴
    public void NextTurn()
    {
        StartTurn();
    }

    // 전투 종료
    public void EndBattle()
    {
        IsBattleRunning = false;
    }
}