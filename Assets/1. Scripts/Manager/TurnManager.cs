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
    private readonly ClashBuilder clashBuilder;

    //--------------------------------

    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        ClashManager clashManager,
        SpeedManager speedManager,
        ActionResolver actionResolver,
        MomentumManager momentumManager,
        ClashBuilder clashBuilder)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
        this.aiManager = aiManager;
        this.clashManager = clashManager;
        this.speedManager = speedManager;
        this.actionResolver = actionResolver;
        this.momentumManager = momentumManager;
        this.clashBuilder = clashBuilder;
    }

    //--------------------------------

    public int CurrentTurn { get; private set; }

    public bool IsBattleRunning { get; private set; }

    //--------------------------------

    public void StartBattle()
    {
        CurrentTurn = 1;
        IsBattleRunning = true;

        StartTurn();
    }

    //--------------------------------

    public void StartTurn()
    {
        battleContext.battleManager.BattleLogger.Clear();

        battleContext._battleEvent.RaiseTurnStart(CurrentTurn);

        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnStart();
        }

        speedManager.RollAllSpeed();

        //--------------------------------
        // Slot 초기화
        //--------------------------------

        actionManager.Clear();

        //--------------------------------
        // AI 행동 생성
        //--------------------------------

        aiManager.DecideEnemyActions();

        //--------------------------------
        // 플레이어 입력 대기
        //--------------------------------
    }

    //--------------------------------

    public void ResolveTurn()
    {
        Debug.Log($"===== TURN {CurrentTurn} =====");

        //--------------------------------
        // Slot -> Queue
        //--------------------------------

        ActionExecutionQueue queue =
            clashBuilder.BuildQueue((System.Collections.Generic.List<ActionSlot>)actionManager.Slots);

        //--------------------------------
        // Queue 실행
        //--------------------------------

        actionResolver.Resolve(queue);

        //--------------------------------

        EndTurn();
    }

    //--------------------------------

    private void EndTurn()
    {
        battleContext.battleManager.BattleLogger.PrintTurn(CurrentTurn);

        foreach (Character c in battleContext.AllCharacters)
        {
            c.TurnEnd();
        }

        momentumManager.DecayMomentum();

        battleContext._battleEvent.RaiseTurnEnd(CurrentTurn);

        CurrentTurn++;
    }

    //--------------------------------

    public void NextTurn()
    {
        StartTurn();
    }

    //--------------------------------

    public void EndBattle()
    {
        IsBattleRunning = false;
    }
}