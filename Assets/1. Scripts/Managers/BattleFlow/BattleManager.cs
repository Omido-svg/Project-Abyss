using System;
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

    private readonly BattleLifecycleGuard lifecycleGuard = new();

    private bool charactersCleanedUp;
    private bool initializedSuccessfully;
    private bool endingOrEnded;
    private bool destroyed;

    private void Awake()
    {
        try
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

            if (!lifecycleGuard.MarkReady())
            {
                throw new InvalidOperationException(
                    "BattleLifecycleGuard를 Ready 상태로 전환하지 못했습니다.");
            }

            initializedSuccessfully = true;

            Debug.Log("===== Battle Ready =====");
        }
        catch (Exception exception)
        {
            HandleInitializationFailure(exception);
        }
    }

    private IEnumerator Start()
    {
        yield return null;

        if (!initializedSuccessfully ||
            endingOrEnded ||
            destroyed)
        {
            yield break;
        }

        Debug.Log("===== Battle Start =====");
        StartBattle();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying ||
            destroyed ||
            !initializedSuccessfully ||
            endingOrEnded ||
            TurnManager == null ||
            !TurnManager.IsBattleRunning)
        {
            return;
        }

        EndBattleInternal(
            "BattleManager disabled");
    }

    private void OnDestroy()
    {
        if (destroyed)
            return;

        destroyed = true;

        if (!endingOrEnded)
        {
            EndBattleInternal(
                "BattleManager destroyed");
        }

        ActionManager?.Dispose();
        lifecycleGuard.Dispose();
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
                ClashBuilder,
                lifecycleGuard,
                HandleTurnFatalError);
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
        if (!initializedSuccessfully ||
            endingOrEnded ||
            destroyed)
        {
            Debug.LogWarning(
                "[BattleManager] 초기화되지 않았거나 종료된 전투는 시작할 수 없습니다.");
            return;
        }

        if (TurnManager == null)
        {
            Debug.LogWarning(
                "BattleManager : TurnManager가 없습니다.");
            return;
        }

        BattleEvent battleEvent =
            BattleContext?._battleEvent;

        if (battleEvent == null ||
            battleEvent.IsDisposed)
        {
            Debug.LogWarning(
                "[BattleManager] BattleEvent가 없거나 이미 Dispose되었습니다.");
            return;
        }

        charactersCleanedUp = false;

        TurnManager.StartBattle();

        if (!TurnManager.IsBattleRunning)
            return;

        battleUIManager?.RefreshAllBodyPartButtons();
    }

    public void NextTurn()
    {
        if (!initializedSuccessfully ||
            endingOrEnded ||
            destroyed ||
            TurnManager == null ||
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
        if (endingOrEnded ||
            destroyed ||
            !initializedSuccessfully)
        {
            return;
        }

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

    public void EndBattle()
    {
        EndBattleInternal(
            "Battle completed");
    }

    private void EndBattleInternal(
        string reason)
    {
        if (endingOrEnded)
            return;

        endingOrEnded = true;

        BattleEvent battleEvent =
            BattleContext?._battleEvent;

        try
        {
            RunCleanupStep(
                "TurnManager.EndBattle",
                () => TurnManager?.EndBattle());

            // UI와 View Binder가 먼저 스스로 구독을 해제한다.
            RunCleanupStep(
                "BattleEvent.RaiseBattleEnded",
                () => battleEvent?.RaiseBattleEnded());

            RunCleanupStep(
                "Character Mechanics",
                CleanupCharacters);

            RunCleanupStep(
                "ActionManager.Clear",
                () => ActionManager?.Clear());

            RunCleanupStep(
                "Battle UI Refresh",
                () => battleUIManager?.RefreshAllBodyPartButtons());

            Debug.Log(
                "===== Battle End ===== " +
                $"Reason={reason}");
        }
        finally
        {
            // 알려지지 않은 구독자까지 참조를 남기지 않도록
            // 전투 단위 Event Bus를 마지막에 폐기한다.
            RunCleanupStep(
                "BattleEvent.Dispose",
                () => battleEvent?.Dispose());
        }
    }

    private void HandleTurnFatalError(
        Exception exception)
    {
        if (endingOrEnded)
            return;

        Debug.LogError(
            "[BattleManager] 턴 처리 실패로 전투를 안전 종료합니다.");

        if (exception != null)
            Debug.LogException(exception);

        EndBattleInternal(
            "Turn processing failure");
    }

    private void HandleInitializationFailure(
        Exception exception)
    {
        initializedSuccessfully = false;
        endingOrEnded = true;

        lifecycleGuard.Fault(exception);

        Debug.LogError(
            "[BattleManager] 전투 초기화에 실패했습니다.");

        if (exception != null)
            Debug.LogException(exception);

        try
        {
            CleanupCharacters();
        }
        finally
        {
            BattleContext?._battleEvent?.Dispose();
            ActionManager?.Dispose();
            enabled = false;
        }
    }

    private static void RunCleanupStep(
        string phase,
        Action cleanup)
    {
        if (cleanup == null)
            return;

        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[BattleManager] 전투 종료 정리 실패 / " +
                $"Phase={phase}");

            Debug.LogException(exception);
        }
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
            if (character == null)
                continue;

            try
            {
                character.UnregisterMechanics();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[BattleManager] 캐릭터 메커닉 해제 실패 / " +
                    $"Character={character.name}");

                Debug.LogException(exception);
            }
        }
    }
}
