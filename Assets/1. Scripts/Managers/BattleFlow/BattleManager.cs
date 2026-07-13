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

    [Header("Animation")]
    [SerializeField] private BattleAnimationDirector battleAnimationDirector;
    [SerializeField] private SkillVisualProfile defaultVisualProfile;

    public BattleContext BattleContext { get; private set; }

    public Character SelectedCharacter { get; set; }

    public TurnManager TurnManager { get; private set; }
    public ActionManager ActionManager { get; private set; }
    public MomentumManager MomentumManager { get; private set; }
    public BattleLogger BattleLogger { get; private set; }
    public SpeedManager SpeedManager { get; private set; }

    // UI 미리보기와 테스트가 실제 전투와 같은 계산기를 사용하도록
    // 읽기 전용으로 공개한다.
    public DamageManager DamageManager { get; private set; }
    public ClashManager ClashManager { get; private set; }
    public ActionResolver ActionResolver { get; private set; }
    public ClashBuilder ClashBuilder { get; private set; }
    public AIManager AIManager { get; private set; }

    private bool charactersCleanedUp;

    private void Awake()
    {
        InitializeContext();

        if (battleUIManager == null)
        {
            battleUIManager =
                FindFirstObjectByType<BattleUIManager>();
        }

        CreateManagers();
        InitializeCharacters();
        BindButtons();

        SelectedCharacter = player;

        Debug.Log("===== Battle Ready =====");
    }

    private IEnumerator Start()
    {
        yield return null;

        Debug.Log("===== Battle Start =====");
        StartBattle();
    }

    private void OnDestroy()
    {
        CleanupCharacters();
    }

    private void InitializeContext()
    {
        BattleContext =
            new BattleContext
            {
                battleManager = this,
                Player = player,
                Enemies = enemies
            };

        BattleContext.EffectResolver =
            new BattleEffectResolver(
                BattleContext);
    }

    private void CreateManagers()
    {
        BattleLogger = new BattleLogger();
        ActionManager = new ActionManager();

        MomentumManager =
            new MomentumManager(
                BattleContext);

        SpeedManager =
            new SpeedManager(
                BattleContext);

        DamageManager =
            new DamageManager(
                BattleContext,
                MomentumManager);

        ClashManager =
            new ClashManager(
                BattleContext,
                DamageManager,
                MomentumManager);

        ActionResolver =
            new ActionResolver(
                BattleContext,
                ClashManager,
                battleAnimationDirector,
                defaultVisualProfile);

        ClashBuilder =
            new ClashBuilder();

        AIManager =
            new AIManager(
                BattleContext,
                ActionManager);

        TurnManager =
            new TurnManager(
                BattleContext,
                ActionManager,
                AIManager,
                SpeedManager,
                ActionResolver,
                MomentumManager,
                ClashBuilder);
    }

    private void InitializeCharacters()
    {
        if (player != null)
        {
            player.Initialize(BattleContext);
            player.ForceRecalculateHP();
        }

        if (enemies == null)
            return;

        foreach (Character enemy in enemies)
        {
            if (enemy == null)
                continue;

            enemy.Initialize(BattleContext);
            enemy.ForceRecalculateHP();
        }
    }

    private void BindButtons()
    {
        BindPlayerButtons();
        BindEnemyButtons();
    }

    private void BindPlayerButtons()
    {
        if (playerButtons == null)
            return;

        int count = 0;

        if (player != null &&
            player.BodyParts != null)
        {
            count =
                Mathf.Min(
                    playerButtons.Count,
                    player.BodyParts.Count);

            for (int i = 0; i < count; i++)
            {
                if (playerButtons[i] == null ||
                    player.BodyParts[i] == null)
                {
                    continue;
                }

                playerButtons[i]
                    .gameObject
                    .SetActive(true);

                playerButtons[i].Bind(
                    player,
                    player.BodyParts[i]);
            }
        }

        for (int i = count;
             i < playerButtons.Count;
             i++)
        {
            if (playerButtons[i] == null)
                continue;

            playerButtons[i].Bind(
                null,
                null);

            playerButtons[i]
                .gameObject
                .SetActive(false);
        }
    }

    private void BindEnemyButtons()
    {
        if (enemyButtons == null)
            return;

        int buttonIndex = 0;

        if (enemies != null)
        {
            foreach (Character enemy in enemies)
            {
                if (enemy == null)
                    continue;

                if (enemy.IsSingleHpTarget)
                {
                    if (!TryBindEnemyButton(
                            ref buttonIndex,
                            enemy,
                            null))
                    {
                        return;
                    }

                    continue;
                }

                if (enemy.BodyParts == null)
                    continue;

                foreach (BodyPart part
                         in enemy.BodyParts)
                {
                    if (part == null)
                        continue;

                    if (!TryBindEnemyButton(
                            ref buttonIndex,
                            enemy,
                            part))
                    {
                        return;
                    }
                }
            }
        }

        for (int i = buttonIndex;
             i < enemyButtons.Count;
             i++)
        {
            BodyPartButton button =
                enemyButtons[i];

            if (button == null)
                continue;

            button.Bind(
                null,
                null);

            button.gameObject.SetActive(
                false);
        }
    }

    private bool TryBindEnemyButton(
        ref int buttonIndex,
        Character enemy,
        BodyPart part)
    {
        if (buttonIndex >=
            enemyButtons.Count)
        {
            Debug.LogWarning(
                "Enemy BodyPartButton 수가 부족합니다. " +
                "일반몹은 캐릭터당 1개, " +
                "부위형 적은 부위당 1개의 버튼이 필요합니다.");

            return false;
        }

        BodyPartButton button =
            enemyButtons[buttonIndex];

        if (button != null)
        {
            button.gameObject.SetActive(
                true);

            button.Bind(
                enemy,
                part);
        }

        buttonIndex++;
        return true;
    }

    public void StartBattle()
    {
        if (TurnManager == null)
        {
            Debug.LogWarning(
                "BattleManager : TurnManager가 없습니다.");
            return;
        }

        charactersCleanedUp = false;

        TurnManager.StartBattle();

        battleUIManager?.RefreshAllBodyPartButtons();
    }

    public void NextTurn()
    {
        if (TurnManager == null ||
            TurnManager.IsResolving)
        {
            return;
        }

        if (ActionManager == null ||
            BattleContext == null ||
            BattleContext.Player == null)
        {
            return;
        }

        int playerSlotCount =
            ActionManager.CountSlots(
                BattleContext.Player);

        if (playerSlotCount <= 0)
        {
            Debug.LogWarning(
                "플레이어 ActionSlot이 하나도 없습니다. " +
                "최소 하나 이상의 행동을 선택해야 턴을 진행할 수 있습니다.");

            ActionManager.PrintSlots(
                "NEXT TURN BLOCKED - CURRENT SLOTS");

            return;
        }

        ActionManager.PrintSlots(
            "BEFORE RESOLVE");

        TurnManager.ResolveTurn(
            OnTurnResolved);
    }

    private void OnTurnResolved()
    {
        battleUIManager?.RefreshAllBodyPartButtons();

        if (CheckBattleEnd())
        {
            EndBattle();
            return;
        }

        TurnManager.NextTurn();

        battleUIManager?.RefreshAllBodyPartButtons();

        if (CheckBattleEnd())
            EndBattle();
    }

    public void ResetPlayerActions()
    {
        if (BattleContext?.Player == null ||
            ActionManager == null)
        {
            return;
        }

        ActionManager.RemoveSlotsByOwner(
            BattleContext.Player);

        Debug.Log(
            "[BATTLE] Player actions reset.");

        battleUIManager?.RefreshAllBodyPartButtons();
    }

    private bool CheckBattleEnd()
    {
        if (BattleContext?.Player == null)
            return true;

        if (BattleContext.Player.IsDead)
            return true;

        if (BattleContext.Enemies == null ||
            BattleContext.Enemies.Count == 0)
        {
            return true;
        }

        foreach (Character enemy
                 in BattleContext.Enemies)
        {
            if (enemy != null &&
                !enemy.IsDead)
            {
                return false;
            }
        }

        return true;
    }

    private void EndBattle()
    {
        TurnManager?.EndBattle();

        CleanupCharacters();

        battleUIManager?.RefreshAllBodyPartButtons();

        Debug.Log("===== Battle End =====");
    }

    private void CleanupCharacters()
    {
        if (charactersCleanedUp)
            return;

        if (BattleContext?.AllCharacters == null)
            return;

        charactersCleanedUp = true;

        foreach (Character character
                 in BattleContext.AllCharacters)
        {
            character?.UnregisterMechanics();
        }
    }
}
