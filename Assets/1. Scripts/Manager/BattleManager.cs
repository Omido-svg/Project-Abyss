using UnityEngine;
using System.Collections.Generic;

// 매니저들의 총괄 매니저
// Battle -> Turn -> Action -> Clash -> Damage -> Status
public class BattleManager : MonoBehaviour
{
    // Managers
    private TurnManager turnManager;
    private ActionManager actionManager;
    private ClashManager clashManager;
    private DamageManager damageManager;
    private StatusManager statusManager;


    // Battle Context
    BattleContext _battleContext;
    
    // Battel Event
    BattleEvent _battleEvent;

    //----------------------------

    private void Awake()
    {
        _battleContext = new BattleContext();
        _battleEvent = new BattleEvent();

        _battleContext._battleEvent = _battleEvent;

        turnManager = new TurnManager(_battleContext);
        damageManager = new DamageManager(_battleContext);
        statusManager = new StatusManager(_battleContext);
        clashManager = new ClashManager(_battleContext, damageManager);
        actionManager = new ActionManager(_battleContext);
    }

    //----------------------------------------------------

    public void StartBattle(
        Character player,
        List<Character> enemies)
    {
        _battleContext.Player = player;
        _battleContext.Enemies = enemies;

        turnManager.StartBattle();

        BattleLoop();
    }

    //----------------------------------------------------

    private void BattleLoop()
    {
        while (true)
        {
            RunTurn();
            
            if (CheckBattleEnd())
                break;
        }   

        EndBattle();
    }

    //----------------------------------------------------

    private void RunTurn()
    {
        // Turn Start
        turnManager.StartTurn();

        // Status
        statusManager.ProcessTurnStart();

        // Action
        actionManager.Clear();
        actionManager.CreateActions();

        // Clash
        clashManager.Resolve(actionManager.BuildQueue());

        // Status End
        statusManager.ProcessTurnEnd();

        // Turn End
        turnManager.EndTurn();
    }

    //----------------------------------------------------

    private bool CheckBattleEnd()
    {
        bool playerDead = true;

        if (!_battleContext.Player.IsDead)
        {
            playerDead = false;
        }

        bool enemyDead = true;

        foreach (Character c in _battleContext.Enemies)
        {
            if (!c.IsDead)
            {
                enemyDead = false;
                break;
            }
        }

        return playerDead || enemyDead;
    }

    //----------------------------------------------------

    private void EndBattle()
    {
        turnManager.EndBattle();
        Debug.Log("Battle End");
    }
}
