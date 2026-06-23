using UnityEngine;
using System.Collections.Generic;

// Battle -> Turn -> Action -> Clash -> Damage
public class BattleManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private Character player;
    [SerializeField] private List<Character> enemies = new();

    // UI에서 현재 선택된 캐릭터
    public Character SelectedCharacter { get; set; }
    
    

    //----------------------------------------------------
    // Managers
    //----------------------------------------------------

    public TurnManager TurnManager { get; private set; }
    public ActionManager ActionManager { get; private set; }

    private ClashManager clashManager;
    private DamageManager damageManager;
    private AIManager aIManager;

    //----------------------------------------------------
    // Battle Context
    //----------------------------------------------------

    private BattleContext battleContext;
    public BattleContext BattleContext => battleContext;

    //----------------------------------------------------

    private void Awake()
    {
        battleContext = new BattleContext();

        battleContext.Player = player;
        battleContext.Enemies = enemies;

        battleContext.AllCharacters.Clear();
        battleContext.AllCharacters.Add(player);
        battleContext.AllCharacters.AddRange(enemies);
        
        // BattleEvent 연결
        player.Initialize(battleContext._battleEvent);

        foreach (Character enemy in enemies)
        {
            enemy.Initialize(battleContext._battleEvent);
        }

        damageManager = new DamageManager(battleContext);
        clashManager = new ClashManager(battleContext, damageManager);
        ActionManager = new ActionManager(battleContext);
        aIManager = new AIManager(battleContext, ActionManager);
        TurnManager = new TurnManager(
            battleContext,
            ActionManager,
            aIManager,
            clashManager);

        SelectedCharacter = player;
        
        Utils.PrintList(BattleContext.AllCharacters);
    }

    //----------------------------------------------------

    public void StartBattle()
    {
        TurnManager.StartBattle();
    }

    //----------------------------------------------------

    public void NextTurn()
    {
        if (CheckBattleEnd())
        {
            EndBattle();
            return;
        }

        TurnManager.NextTurn();
    }

    //----------------------------------------------------

    private bool CheckBattleEnd()
    {
        if (battleContext.Player.IsDead)
            return true;

        foreach (Character enemy in battleContext.Enemies)
        {
            if (!enemy.IsDead)
                return false;
        }

        return true;
    }

    //----------------------------------------------------

    private void EndBattle()
    {
        TurnManager.EndBattle();
        Debug.Log("Battle End");
    }
}