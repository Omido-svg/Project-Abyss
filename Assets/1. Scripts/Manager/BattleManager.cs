using UnityEngine;
using System.Collections.Generic;

// 매니저들의 총괄 매니저
// Battle -> Turn -> Action -> Clash -> Damage -> Status
public class BattleManager : MonoBehaviour
{
    // 디버깅용 플레이어 및 적 놓기
    [SerializeField] Character _player;
    [SerializeField] List<Character> _enemies;
    public Character SelectedCharacter { get; set; }
    
    
    
    // Managers
    public TurnManager turnManager;
    
    private ActionManager actionManager;
    public ActionManager ActionManager => actionManager;
    
    private ClashManager clashManager;
    
    private DamageManager damageManager;


    // Battle Context
    private BattleContext _battleContext;
    public BattleContext BattleContext => _battleContext;

    //----------------------------

    private void Awake()
    {
        _battleContext = new BattleContext();

        turnManager = new TurnManager(_battleContext);
        damageManager = new DamageManager(_battleContext);
        clashManager = new ClashManager(_battleContext, damageManager);
        actionManager = new ActionManager(_battleContext);
        StartBattle(_player, _enemies);
    }

    //----------------------------------------------------

    public void StartBattle(
        Character player,
        List<Character> enemies)
    {
        _battleContext.Player = player;
        _battleContext.Enemies = enemies;
        
        player.Initialize(_battleContext._battleEvent);

        foreach (Character enemy in enemies)
        {
            enemy.Initialize(_battleContext._battleEvent);
        }
        
        _battleContext.AllCharacters.Clear();

        _battleContext.AllCharacters.Add(player);

        _battleContext.AllCharacters.AddRange(enemies);

        turnManager.StartBattle();
        
        // BattleLoop(); 차후 시작 
    }

    //----------------------------------------------------

    private void BattleLoop() // 차후 수정
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
        turnManager.StartTurn();

        foreach (Character c in _battleContext.AllCharacters)
            c.TurnStart();

        actionManager.Clear();
        actionManager.CreateActions();

        clashManager.Resolve(actionManager.BuildQueue());

        foreach (Character c in _battleContext.AllCharacters)
            c.TurnEnd();

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
