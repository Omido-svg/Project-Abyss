using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class BattleManager : MonoBehaviour
{
    [Header("Battle Roster")]
    [SerializeField] private BattleRosterController rosterController;
    [SerializeField] private bool autoInitializeOnAwake = true;
    [SerializeField] private bool autoStartBattle = true;

    [Header("Legacy Scene Roster Fallback")]
    [FormerlySerializedAs("player")]
    [SerializeField, HideInInspector]
    private Character legacyPlayer;

    [FormerlySerializedAs("enemies")]
    [SerializeField, HideInInspector]
    private List<Character> legacyEnemies = new();

    [Header("Legacy Button Migration")]
    [FormerlySerializedAs("playerButtons")]
    [SerializeField, HideInInspector]
    private List<BodyPartButton> legacyPlayerButtons = new();

    [FormerlySerializedAs("enemyButtons")]
    [SerializeField, HideInInspector]
    private List<BodyPartButton> legacyEnemyButtons = new();

    [Header("Battle Rules")]
    [SerializeField]
    private BattleRuleSettings battleRuleSettings = new();

    [Header("Battle UI")]
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
    public DamageManager DamageManager { get; private set; }
    public ClashManager ClashManager { get; private set; }
    public ActionResolver ActionResolver { get; private set; }
    public ClashBuilder ClashBuilder { get; private set; }
    public AIManager AIManager { get; private set; }

    public BattleRosterController RosterController => rosterController;
    public BattleUIManager BattleUIManager => battleUIManager;
    public BattleRuleSettings BattleRules => battleRuleSettings;

    public Character LegacyPlayerForMigration => legacyPlayer;
    public IReadOnlyList<Character> LegacyEnemiesForMigration => legacyEnemies;
    public IReadOnlyList<BodyPartButton> LegacyPlayerButtonsForMigration =>
        legacyPlayerButtons;
    public IReadOnlyList<BodyPartButton> LegacyEnemyButtonsForMigration =>
        legacyEnemyButtons;

    public bool IsInitialized => initializedSuccessfully;
    public bool IsEndingOrEnded => endingOrEnded;

    public event Action<BattleContext> BattlePrepared;

    private readonly BattleLifecycleGuard lifecycleGuard = new();

    private bool initializationStarted;
    private bool charactersCleanedUp;
    private bool initializedSuccessfully;
    private bool endingOrEnded;
    private bool destroyed;

    private void Awake()
    {
        ResolveSceneReferences();

        if (autoInitializeOnAwake)
            InitializeBattle();
    }

    private IEnumerator Start()
    {
        yield return null;

        if (!autoStartBattle ||
            !initializedSuccessfully ||
            endingOrEnded ||
            destroyed)
        {
            yield break;
        }

        Debug.Log("===== Battle Start =====");
        StartBattle();
    }

    public bool InitializeBattle()
    {
        if (initializedSuccessfully)
            return true;

        if (initializationStarted ||
            endingOrEnded ||
            destroyed)
        {
            return false;
        }

        initializationStarted = true;

        try
        {
            ResolveSceneReferences();

            if (!TryResolveRoster(
                    out Character player,
                    out List<Character> enemies))
            {
                throw new InvalidOperationException(
                    "Battle Roster를 구성하지 못했습니다.");
            }

            ValidateRoster(player, enemies);
            InitializeContext(player, enemies);
            CreateManagers();
            InitializeCharacters();

            battleUIManager?.BuildParticipantButtons(
                BattleContext,
                legacyPlayerButtons,
                legacyEnemyButtons);

            SelectedCharacter = BattleContext.Player;

            if (!lifecycleGuard.MarkReady())
            {
                throw new InvalidOperationException(
                    "BattleLifecycleGuard를 Ready 상태로 전환하지 못했습니다.");
            }

            initializedSuccessfully = true;

            BattlePrepared?.Invoke(BattleContext);

            Debug.Log("===== Battle Ready =====");
            return true;
        }
        catch (Exception exception)
        {
            HandleInitializationFailure(exception);
            return false;
        }
    }

    public bool ConfigureRoster(
        Character playerSource,
        IReadOnlyList<Character> enemySources,
        bool instantiateCopies = true)
    {
        if (initializationStarted || initializedSuccessfully)
        {
            Debug.LogWarning(
                "[BattleManager] Roster는 InitializeBattle 이전에만 변경할 수 있습니다.");

            return false;
        }

        if (rosterController == null)
        {
            rosterController =
                GetComponent<BattleRosterController>();

            if (rosterController == null)
                rosterController = gameObject.AddComponent<BattleRosterController>();
        }

        rosterController.ConfigureSources(
            playerSource,
            enemySources,
            instantiateCopies);

        return true;
    }

    public void AssignRosterController(
        BattleRosterController controller)
    {
        if (initializationStarted)
            return;

        rosterController = controller;
    }

    public void ClearLegacyMigrationData()
    {
        if (initializationStarted)
            return;

        legacyPlayer = null;
        legacyEnemies?.Clear();
        legacyPlayerButtons?.Clear();
        legacyEnemyButtons?.Clear();
    }

    public void SetAutoLifecycle(
        bool initializeOnAwake,
        bool startAutomatically)
    {
        if (initializationStarted)
            return;

        autoInitializeOnAwake = initializeOnAwake;
        autoStartBattle = startAutomatically;
    }

    private bool TryResolveRoster(
        out Character player,
        out List<Character> enemies)
    {
        player = null;
        enemies = new List<Character>();

        if (rosterController != null &&
            rosterController.HasConfiguredPlayer)
        {
            if (!rosterController.TryBuildRoster(
                    out BattleRosterSnapshot snapshot))
            {
                return false;
            }

            player = snapshot.Player;
            enemies.AddRange(snapshot.Enemies);
            return true;
        }

        player = legacyPlayer;

        if (legacyEnemies != null)
        {
            foreach (Character enemy in legacyEnemies)
            {
                if (enemy != null)
                    enemies.Add(enemy);
            }
        }

        return player != null;
    }

    private static void ValidateRoster(
        Character player,
        IReadOnlyList<Character> enemies)
    {
        if (player == null)
            throw new InvalidOperationException("Player가 없습니다.");

        if (enemies == null || enemies.Count == 0)
            throw new InvalidOperationException("Enemy가 하나도 없습니다.");

        HashSet<Character> unique = new() { player };

        foreach (Character enemy in enemies)
        {
            if (enemy == null)
            {
                throw new InvalidOperationException(
                    "Enemy 목록에 null이 포함되어 있습니다.");
            }

            if (!unique.Add(enemy))
            {
                throw new InvalidOperationException(
                    "같은 Character Instance가 여러 전투 슬롯에 중복 등록되었습니다.");
            }
        }
    }

    private void ResolveSceneReferences()
    {
        if (rosterController == null)
            rosterController = GetComponent<BattleRosterController>();

        if (battleUIManager == null)
            battleUIManager = FindFirstObjectByType<BattleUIManager>();
    }

    private void InitializeContext(
        Character player,
        List<Character> enemies)
    {
        battleRuleSettings ??=
            new BattleRuleSettings();
        battleRuleSettings.Normalize();

        BattleContext =
            new BattleContext
            {
                battleManager = this,
                Player = player,
                Enemies = enemies,
                Rules = battleRuleSettings
            };

        BattleContext.EffectResolver =
            new BattleEffectResolver(BattleContext);
    }

    private void CreateManagers()
    {
        BattleLogger = new BattleLogger();
        ActionManager = new ActionManager();
        MomentumManager = new MomentumManager(BattleContext);
        SpeedManager = new SpeedManager(BattleContext);
        DamageManager = new DamageManager(BattleContext, MomentumManager);
        ClashManager = new ClashManager(
            BattleContext,
            DamageManager,
            MomentumManager);

        ActionResolver = new ActionResolver(
            BattleContext,
            ClashManager,
            battleAnimationDirector,
            defaultVisualProfile);

        ClashBuilder = new ClashBuilder();
        AIManager = new AIManager(BattleContext, ActionManager);

        TurnManager = new TurnManager(
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
        Character player = BattleContext?.Player;

        if (player != null)
        {
            player.Initialize(BattleContext);
            player.ForceRecalculateHP();
        }

        if (BattleContext?.Enemies == null)
            return;

        foreach (Character enemy in BattleContext.Enemies)
        {
            if (enemy == null)
                continue;

            enemy.Initialize(BattleContext);
            enemy.ForceRecalculateHP();
        }
    }

    public void StartBattle()
    {
        if (!initializedSuccessfully &&
            !InitializeBattle())
        {
            return;
        }

        if (endingOrEnded || destroyed)
        {
            Debug.LogWarning(
                "[BattleManager] 종료되었거나 파괴된 전투는 시작할 수 없습니다.");

            return;
        }

        if (TurnManager == null)
        {
            Debug.LogWarning(
                "BattleManager : TurnManager가 없습니다.");

            return;
        }

        BattleEvent battleEvent = BattleContext?._battleEvent;

        if (battleEvent == null || battleEvent.IsDisposed)
        {
            Debug.LogWarning(
                "[BattleManager] BattleEvent가 없거나 이미 Dispose되었습니다.");

            return;
        }

        charactersCleanedUp = false;

        TurnManager.StartBattle();

        if (TurnManager.IsBattleRunning)
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
            ActionManager.CountSlots(BattleContext.Player);

        if (playerSlotCount <= 0)
        {
            Debug.LogWarning(
                "플레이어 ActionSlot이 하나도 없습니다. " +
                "최소 하나 이상의 행동을 선택해야 턴을 진행할 수 있습니다.");

            ActionManager.PrintSlots(
                "NEXT TURN BLOCKED - CURRENT SLOTS");

            return;
        }

        ActionManager.PrintSlots("BEFORE RESOLVE");

        BattleResolutionUiController.BeginCurrentResolution();
        TurnManager.ResolveTurn(OnTurnResolved);

        if (!TurnManager.IsResolving)
            BattleResolutionUiController.EndCurrentResolution();
    }

    private void OnTurnResolved()
    {
        BattleResolutionUiController.EndCurrentResolution();

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

        ActionManager.RemoveSlotsByOwner(BattleContext.Player);

        Debug.Log("[BATTLE] Player actions reset.");
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

        foreach (Character enemy in BattleContext.Enemies)
        {
            if (enemy != null && !enemy.IsDead)
                return false;
        }

        return true;
    }

    public void EndBattle()
    {
        EndBattleInternal("Battle completed");
    }

    private void EndBattleInternal(string reason)
    {
        BattleResolutionUiController.EndCurrentResolution();

        if (endingOrEnded)
            return;

        endingOrEnded = true;
        BattleEvent battleEvent = BattleContext?._battleEvent;

        try
        {
            RunCleanupStep(
                "TurnManager.EndBattle",
                () => TurnManager?.EndBattle());

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
            RunCleanupStep(
                "BattleEvent.Dispose",
                () => battleEvent?.Dispose());
        }
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

        EndBattleInternal("BattleManager disabled");
    }

    private void OnDestroy()
    {
        if (destroyed)
            return;

        destroyed = true;

        if (!endingOrEnded)
            EndBattleInternal("BattleManager destroyed");

        ActionManager?.Dispose();
        lifecycleGuard.Dispose();
        rosterController?.ReleaseSpawnedRoster();
    }

    private void HandleTurnFatalError(Exception exception)
    {
        if (endingOrEnded)
            return;

        Debug.LogError(
            "[BattleManager] 턴 처리 실패로 전투를 안전 종료합니다.");

        if (exception != null)
            Debug.LogException(exception);

        EndBattleInternal("Turn processing failure");
    }

    private void HandleInitializationFailure(Exception exception)
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
            battleUIManager?.ClearParticipantButtons();
            rosterController?.ReleaseSpawnedRoster();
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

        foreach (Character character in BattleContext.AllCharacters)
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