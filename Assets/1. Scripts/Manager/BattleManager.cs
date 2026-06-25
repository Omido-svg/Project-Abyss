using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.WindowsMR.Input;

// Battle -> Turn -> Action -> Speed -> Clash -> Moment -> Damage 순으로 처리
public class BattleManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private Character player;
    [SerializeField] private List<Character> enemies = new();
    [Header("Button")]
    [SerializeField] private List<BodyPartButton> parts = new();
    

    // UI에서 현재 선택된 캐릭터
    public Character SelectedCharacter { get; set; }
    
    

    //----------------------------------------------------
    // Managers
    //----------------------------------------------------

    public TurnManager TurnManager { get; private set; }
    public ActionManager ActionManager { get; private set; }
    private ClashManager clashManager;
    private DamageManager damageManager;
    private AIManager AIManager;
    private MomentumManager MomentumManager;
    private SpeedManager SpeedManager;
    private ActionResolver ActionResolver;

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
        
        // ------------------------------------------
        for (int i = 0; i < parts.Count; i++)
            parts[i].Bind(player, player.BodyParts[i]);
            
        Debug.Log($"Buttons : {parts.Count}");
        Debug.Log($"BodyParts : {player.BodyParts.Count}");

        for (int i = 0; i < player.BodyParts.Count; i++)
        {
            Debug.Log(player.BodyParts[i].Type);
        }    
        // --------------------------------------------
        
        MomentumManager = new MomentumManager(battleContext);
        SpeedManager = new SpeedManager(battleContext);
        damageManager = new DamageManager(battleContext, MomentumManager);
        clashManager = new ClashManager(battleContext, damageManager, MomentumManager);
        ActionResolver = new ActionResolver(battleContext, clashManager);
        ActionManager = new ActionManager(battleContext);
        AIManager = new AIManager(battleContext, ActionManager);
        TurnManager = new TurnManager(
            battleContext,
            ActionManager,
            AIManager,
            clashManager,
            SpeedManager,
            ActionResolver,
            MomentumManager);

        SelectedCharacter = player;
        
        // 디버깅용
        Utils.PrintList(BattleContext.AllCharacters);
    }

    //----------------------------------------------------

    public void StartBattle()
    {
        TurnManager.StartBattle();
        
        // 디버깅용 출력
        Utils.PrintList(BattleContext.AllCharacters);
        Utils.PrintActions((List<BattleAction>)ActionManager.Actions);
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