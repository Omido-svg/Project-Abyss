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

    public ActionBuffer ActionBuffer { get; private set; }

    //------------------------------------------
    // Managers
    //------------------------------------------

    public TurnManager TurnManager { get; private set; }

    public ActionManager ActionManager { get; private set; }

    public MomentumManager MomentumManager { get; private set; }

    public BattleLogger BattleLogger { get; private set; }

    private SpeedManager speedManager;
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
        ActionBuffer = new ActionBuffer();

        BattleLogger = new BattleLogger();

        MomentumManager = new MomentumManager(BattleContext);

        speedManager = new SpeedManager(BattleContext);

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

        ActionManager = new ActionManager(BattleContext);

        clashBuilder = new ClashBuilder();

        aiManager = new AIManager(
            BattleContext,
            ActionBuffer);

        TurnManager = new TurnManager(
            BattleContext,
            ActionManager,
            aiManager,
            clashManager,
            speedManager,
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
        // UI Slot -> ActionManager
        ActionBuffer.Commit(ActionManager);

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