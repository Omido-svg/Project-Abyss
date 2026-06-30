using UnityEngine;
using System.Collections.Generic;

// Battle
//  └ Turn
//      └ Slot
//          └ Queue
//              └ Resolver
//                  └ Clash
//                      └ Damage

public class BattleManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private Character player;

    [SerializeField] private List<Character> enemies = new();

    [Header("Buttons")]
    [SerializeField] private List<BodyPartButton> playerButtons = new();

    [SerializeField] private List<BodyPartButton> enemyButtons = new();

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
        BindButtons();
        CreateManagers();
        SelectedCharacter = player;
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

        BattleContext.AllCharacters.Add(player);

        BattleContext.AllCharacters.AddRange(enemies);
    }

    //------------------------------------------------

    private void InitializeCharacters()
    {
        player.Initialize(BattleContext._battleEvent);

        player.ForceRecalculateHP();

        AssignSkills(player);

        foreach (Character enemy in enemies)
        {
            enemy.Initialize(BattleContext._battleEvent);

            AssignSkills(enemy);
        }
    }

    //------------------------------------------------

    private void BindButtons()
    {
        for (int i = 0; i < playerButtons.Count; i++)
        {
            playerButtons[i].Bind(
                player,
                player.BodyParts[i]);
        }

        for (int i = 0; i < enemyButtons.Count; i++)
        {
            enemyButtons[i].Bind(
                enemies[i],
                enemies[i].BodyParts[0]);
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

    public void StartBattle()
    {
        TurnManager.StartBattle();
    }

    //------------------------------------------------

    public void NextTurn()
    {
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

        if (CheckBattleEnd())
        {
            EndBattle();
            return;
        }

        TurnManager.NextTurn();
    }

    //------------------------------------------------

    private bool CheckBattleEnd()
    {
        if (BattleContext.Player.IsDead)
            return true;

        foreach (Character enemy in BattleContext.Enemies)
        {
            if (!enemy.IsDead)
                return false;
        }

        return true;
    }

    //------------------------------------------------

    private void EndBattle()
    {
        TurnManager.EndBattle();

        Debug.Log("===== Battle End =====");
    }

    //------------------------------------------------

    private void AssignSkills(Character character)
    {
        foreach (BodyPart part in character.BodyParts)
        {
            if (part.AvailableSkills.Count == 0)
                continue;

            part.CurrentSkill =
                part.AvailableSkills[
                    Random.Range(0, part.AvailableSkills.Count)];
        }
    }
}