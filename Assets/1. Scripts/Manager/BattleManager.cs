using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private Character player;
    [SerializeField] private List<Character> enemies = new();

    [Header("Buttons")]
    [SerializeField] private List<BodyPartButton> playerButtons = new();
    [SerializeField] private List<BodyPartButton> enemyButtons = new();
    
    [Header("BattleUIManager")]
    [SerializeField] private BattleUIManager battleUIManager;

    //------------------------------------------

    public BattleContext BattleContext { get; private set; }

    public Character SelectedCharacter { get; set; }

    //------------------------------------------
    // Managers
    //------------------------------------------

    public TurnManager TurnManager { get; private set; }
    public ActionManager ActionManager { get; private set; }
    public MomentumManager MomentumManager { get; private set; }
    public BattleLogger BattleLogger { get; private set; }
    public SpeedManager SpeedManager { get; private set; }

    private DamageManager damageManager;
    private ClashManager clashManager;
    private ActionResolver actionResolver;
    private ClashBuilder clashBuilder;
    private AIManager aiManager;

    //------------------------------------------

    private void Awake()
    {
        InitializeContext();
        InitializeCharacters();
        CreateManagers();
        BindButtons();

        SelectedCharacter = player;
        
        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();

        Debug.Log("===== Battle Ready =====");
    }

    private IEnumerator Start()
    {
        // BattleUIManager.Start()가 먼저 이벤트 구독할 시간을 줌
        yield return null;

        Debug.Log("===== Battle Start =====");
        StartBattle();
    }

    //------------------------------------------------

    private void InitializeContext()
    {
        BattleContext = new BattleContext();

        BattleContext.battleManager = this;
        BattleContext.Player = player;
        BattleContext.Enemies = enemies;

        BattleContext.AllCharacters.Clear();

        if (player != null)
            BattleContext.AllCharacters.Add(player);

        if (enemies != null)
            BattleContext.AllCharacters.AddRange(enemies);
    }

    //------------------------------------------------

    private void InitializeCharacters()
    {
        if (player != null)
        {
            player.Initialize(BattleContext._battleEvent);
            player.ForceRecalculateHP();
        }

        foreach (Character enemy in enemies)
        {
            if (enemy == null)
                continue;

            enemy.Initialize(BattleContext._battleEvent);
            enemy.ForceRecalculateHP();
        }
    }

    //------------------------------------------------

    private void CreateManagers()
    {
        BattleLogger = new BattleLogger();

        MomentumManager = new MomentumManager(BattleContext);

        SpeedManager = new SpeedManager(BattleContext);

        damageManager = new DamageManager(
            BattleContext,
            MomentumManager);

        clashManager = new ClashManager(
            BattleContext,
            damageManager,
            MomentumManager);

        actionResolver = new ActionResolver(
            BattleContext,
            clashManager);

        ActionManager = new ActionManager();

        clashBuilder = new ClashBuilder();

        aiManager = new AIManager(
            BattleContext,
            ActionManager);

        TurnManager = new TurnManager(
            BattleContext,
            ActionManager,
            aiManager,
            SpeedManager,
            actionResolver,
            MomentumManager,
            clashBuilder);
    }

    //------------------------------------------------

    private void BindButtons()
    {
        BindPlayerButtons();
        BindEnemyButtons();
    }

    private void BindPlayerButtons()
    {
        if (player == null)
            return;

        if (player.BodyParts == null)
            return;

        int count =
            Mathf.Min(
                playerButtons.Count,
                player.BodyParts.Count);

        for (int i = 0; i < count; i++)
        {
            if (playerButtons[i] == null)
                continue;

            if (player.BodyParts[i] == null)
                continue;

            playerButtons[i].Bind(
                player,
                player.BodyParts[i]);
        }
    }

    private void BindEnemyButtons()
    {
        if (enemies == null)
            return;

        int count =
            Mathf.Min(
                enemyButtons.Count,
                enemies.Count);

        for (int i = 0; i < count; i++)
        {
            Character enemy = enemies[i];

            if (enemy == null)
                continue;

            if (enemy.BodyParts == null ||
                enemy.BodyParts.Count == 0)
                continue;

            if (enemyButtons[i] == null)
                continue;

            enemyButtons[i].Bind(
                enemy,
                enemy.BodyParts[0]);
        }
    }

    //------------------------------------------------

    public void StartBattle()
    {
        if (TurnManager == null)
        {
            Debug.LogWarning("BattleManager : TurnManager가 없습니다.");
            return;
        }

        TurnManager.StartBattle();
    }

    //------------------------------------------------

    public void NextTurn()
    {
        if (ActionManager == null ||
            BattleContext == null ||
            BattleContext.Player == null)
        {
            return;
        }

        int playerSlotCount =
            ActionManager.CountSlots(BattleContext.Player);

        if (playerSlotCount <= 0)
        {
            Debug.LogWarning(
                "플레이어 ActionSlot이 하나도 없습니다. " +
                "최소 하나 이상의 행동을 선택해야 턴을 진행할 수 있습니다.");

            ActionManager.PrintSlots("NEXT TURN BLOCKED - CURRENT SLOTS");

            return;
        }

        ActionManager.PrintSlots("BEFORE RESOLVE");

        TurnManager.ResolveTurn();

        if (battleUIManager != null)
            battleUIManager.RefreshAllBodyPartButtons();

        if (CheckBattleEnd())
        {
            EndBattle();
            return;
        }

        TurnManager.NextTurn();

        if (battleUIManager != null)
            battleUIManager.RefreshAllBodyPartButtons();

        if (CheckBattleEnd())
        {
            EndBattle();
            return;
        }

        TurnManager.NextTurn();
    }

    //------------------------------------------------

    public void ResetPlayerActions()
    {
        if (BattleContext == null)
            return;

        if (BattleContext.Player == null)
            return;

        if (ActionManager == null)
            return;

        ActionManager.RemoveSlotsByOwner(
            BattleContext.Player);

        Debug.Log("[BATTLE] Player actions reset.");
    }

    //------------------------------------------------

    private bool CheckBattleEnd()
    {
        if (BattleContext == null)
            return true;

        if (BattleContext.Player == null)
            return true;

        if (BattleContext.Player.IsDead)
            return true;

        foreach (Character enemy in BattleContext.Enemies)
        {
            if (enemy == null)
                continue;

            if (!enemy.IsDead)
                return false;
        }

        return true;
    }

    //------------------------------------------------

    private void EndBattle()
    {
        if (TurnManager != null)
            TurnManager.EndBattle();

        Debug.Log("===== Battle End =====");
    }
}