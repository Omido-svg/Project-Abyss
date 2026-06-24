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

    //--------------------------------

    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        ClashManager clashManager,
        SpeedManager speedManager,
        ActionResolver actionResolver)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
        this.aiManager = aiManager;
        this.clashManager = clashManager;
        this.speedManager = speedManager;
        this.actionResolver = actionResolver;
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

    /// 현재 턴 시작
    public void StartTurn()
    {
        battleContext._battleEvent.RaiseTurnStart(CurrentTurn);

        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnStart();
        }

        speedManager.RollAllSpeed();

        actionManager.Clear();

        aiManager.DecideEnemyActions();

        // 플레이어 UI 입력 대기
    }


    // 플레이어 입력 완료 후 호출
    public void ResolveTurn()
    {
        ActionExecutionQueue executionQueue = actionManager.BuildExecutionQueue();
        actionResolver.Resolve(executionQueue);
        EndTurn();
    }

    // 현재 턴 종료
    public void EndTurn()
    {
        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnEnd();
        }

        battleContext._battleEvent.RaiseTurnEnd(CurrentTurn);

        CurrentTurn++;
    }

    /// 다음 턴
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