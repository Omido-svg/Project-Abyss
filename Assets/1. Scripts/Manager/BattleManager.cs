using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.WindowsMR.Input;

// Battle -> Turn -> Action -> Speed -> Clash -> Moment -> Damage 순으로 처리
public class BattleManager : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private Character player;
    [SerializeField] private List<Character> enemies = new();
    [Header("Player Button")]
    [SerializeField] private List<BodyPartButton> Playerparts = new();
    [Header("Enemies Button")]
    [SerializeField] private List<BodyPartButton> Enemiesparts = new();
    

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
    public MomentumManager MomentumManager { get; private set; }
    private SpeedManager SpeedManager;
    private ActionResolver ActionResolver;
    
    public BattleLogger BattleLogger { get; private set; }

    //----------------------------------------------------
    // Battle Context
    //----------------------------------------------------

    private BattleContext battleContext;
    public BattleContext BattleContext => battleContext;

    //----------------------------------------------------

    private void Awake()
    {
        battleContext = new BattleContext();
        battleContext.battleManager = this;

        battleContext.Player = player;
        battleContext.Enemies = enemies;

        battleContext.AllCharacters.Clear();
        battleContext.AllCharacters.Add(player);
        battleContext.AllCharacters.AddRange(enemies);
        
        // BattleEvent 연결
        player.Initialize(battleContext._battleEvent);
        player.ForceRecalculateHP();
        
        // 플레이어의 스킬을 간단하게 임의로 지정해주는 메서드 (디버깅용)
        AssignSkills(player);

        foreach (Character enemy in enemies)
        {
            enemy.Initialize(battleContext._battleEvent);
        }
        
        // ------------------------------------------
        for (int i = 0; i < Playerparts.Count; i++)
            Playerparts[i].Bind(player, player.BodyParts[i]);
        for (int i = 0; i < Enemiesparts.Count; i++)
            Enemiesparts[i].Bind(enemies[i], enemies[i].BodyParts[0]);
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
            
        BattleLogger = new BattleLogger();

        SelectedCharacter = player;
        
        // 디버깅용
        Utils.PrintList(BattleContext.AllCharacters);
        Debug.Log("Start Battle");
        
        StartBattle();
    }

    //----------------------------------------------------

    public void StartBattle()
    {
        TurnManager.StartBattle();
        
        // 디버깅용 출력
        Utils.PrintActions((List<BattleAction>)ActionManager.Actions);
    }

    //----------------------------------------------------

    public void NextTurn()
    {
        TurnManager.ResolveTurn();
        
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
    
    // 디버깅용 스킬 할당 메서드
    private void AssignSkills(Character character)
    {
        foreach (BodyPart part in character.BodyParts)
        {
            if (part.AvailableSkills.Count == 0)
            {
                Debug.LogWarning($"{character.Data.CharacterName} : {part.Type}에 등록된 스킬이 없습니다.");
                continue;
            }

            part.CurrentSkill =
                part.AvailableSkills[Random.Range(0, part.AvailableSkills.Count)];
        }
    }
}