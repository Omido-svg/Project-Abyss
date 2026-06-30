using UnityEngine;

public class TurnManager
{
    private readonly BattleContext battleContext;
    private readonly ActionManager actionManager;
    private readonly AIManager aiManager;
    private readonly SpeedManager speedManager;
    private readonly ActionResolver actionResolver;
    private readonly MomentumManager momentumManager;
    private readonly ClashBuilder clashBuilder;

    //--------------------------------

    public TurnManager(
        BattleContext battleContext,
        ActionManager actionManager,
        AIManager aiManager,
        SpeedManager speedManager,
        ActionResolver actionResolver,
        MomentumManager momentumManager,
        ClashBuilder clashBuilder)
    {
        this.battleContext = battleContext;
        this.actionManager = actionManager;
        this.aiManager = aiManager;
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

        momentumManager.Reset();

        StartTurn();
    }

    //--------------------------------

    public void StartTurn()
    {
        if (!IsBattleRunning)
            return;

        Debug.Log($"===== TURN {CurrentTurn} START =====");

        //--------------------------------
        // 이전 턴 슬롯 제거
        //--------------------------------

        actionManager.Clear();

        //--------------------------------
        // 로그 초기화
        //--------------------------------

        battleContext.battleManager.BattleLogger.Clear();

        //--------------------------------
        // Turn Start 이벤트
        //--------------------------------

        battleContext._battleEvent.RaiseTurnStart(CurrentTurn);

        //--------------------------------
        // 캐릭터 턴 시작 처리
        //--------------------------------

        foreach (Character character in battleContext.AllCharacters)
        {
            if (character == null)
                continue;

            if (character.IsDead)
                continue;

            character.TurnStart();
        }

        //--------------------------------
        // 이번 턴 속도 굴림
        //--------------------------------

        speedManager.RollAllSpeed();

        speedManager.PrintSpeeds();

        //--------------------------------
        // AI 행동 슬롯 생성
        //--------------------------------

        aiManager.DecideEnemyActions();
        
        actionManager.PrintSlots("AFTER AI SLOT CREATE");

        //--------------------------------
        // 플레이어 입력 대기
        //--------------------------------
    }

    //--------------------------------

    public void ResolveTurn()
    {
        if (!IsBattleRunning)
            return;

        Debug.Log($"===== TURN {CurrentTurn} RESOLVE =====");

        //--------------------------------
        // Slot -> Queue
        //--------------------------------

        ActionExecutionQueue queue =
            clashBuilder.BuildQueue(actionManager.Slots);

        //--------------------------------
        // Queue 실행
        //--------------------------------

        actionResolver.Resolve(queue);

        //--------------------------------
        // 턴 종료
        //--------------------------------

        EndTurn();
    }

    //--------------------------------

    private void EndTurn()
    {
        Debug.Log($"===== TURN {CurrentTurn} END =====");

        foreach (Character character in battleContext.AllCharacters)
        {
            if (character == null)
                continue;

            if (character.IsDead)
                continue;

            character.TurnEnd();
        }

        momentumManager.DecayMomentum();

        battleContext._battleEvent.RaiseTurnEnd(CurrentTurn);

        battleContext.battleManager.BattleLogger.PrintTurn(CurrentTurn);

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