using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattleTestEncounterMode
{
    NormalBattle = 0,
    MixedBattle = 1,
    BossBattle = 2
}

public enum BattleTestPlayerMode
{
    Olaf = 0,
    Yujin = 1,
    Hifumi = 2
}

/// <summary>
/// CameraTest 한 Scene에서 테스트용 플레이어와 적 조합을 교체한다.
/// BattleManager보다 먼저 실행되어 Roster를 구성하고,
/// 이미 시작된 전투에서 선택을 바꾸면 Scene을 다시 불러 안전하게 재초기화한다.
/// </summary>
[DefaultExecutionOrder(-20000)]
[DisallowMultipleComponent]
public sealed class BattleTestScenarioSwitcher : MonoBehaviour
{
    private const string EncounterPreferenceKey =
        "ProjectAbyss.TestEncounter.Mode";

    private const string PlayerPreferenceKey =
        "ProjectAbyss.TestEncounter.Player";

    private const string EmotionPreferenceKey =
        "ProjectAbyss.TestEncounter.Emotion";

    private const string PanelCollapsedPreferenceKey =
        "ProjectAbyss.TestEncounter.PanelCollapsed";

    private static BattleTestScenarioSwitcher activeInstance;
    private static Rect activeRuntimePanelRect;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        activeInstance = null;
        activeRuntimePanelRect = default;
    }

    [Header("Battle Core")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleRosterController rosterController;

    [Header("Player Prefabs")]
    [SerializeField] private Character olafPrefab;
    [SerializeField] private Character yujinPrefab;
    [SerializeField] private Character hifumiPrefab;

    [Header("Enemy Prefabs")]
    [SerializeField] private Character normalEnemyPrefab;
    [SerializeField] private Character eliteEnemyPrefab;
    [SerializeField] private Character bossEnemyPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private List<Transform> enemySpawnPoints = new();
    [SerializeField] private Transform runtimeParticipantsRoot;

    [Header("Scene Prototypes To Hide")]
    [SerializeField] private List<Character> scenePrototypeCharacters = new();

    [Header("Selection")]
    [SerializeField] private BattleTestPlayerMode defaultPlayer =
        BattleTestPlayerMode.Yujin;

    [SerializeField] private BattleTestEncounterMode defaultEncounter =
        BattleTestEncounterMode.NormalBattle;

    [SerializeField] private bool rememberSelection = true;

    [Header("Gameplay v5 Emotion Test")]
    [SerializeField] private bool configureEmotionForTest = true;
    [SerializeField] private EmotionType defaultEmotion = EmotionType.Awe;
    [SerializeField] private EmotionAugmentCatalog emotionAugmentCatalog;

    [Header("Runtime Debug Panel")]
    [SerializeField] private bool showRuntimePanel = true;
    [SerializeField] private Rect runtimePanelRect =
        new Rect(16f, 16f, 300f, 250f);

    [SerializeField]
    private bool startRuntimePanelCollapsed;

    [SerializeField, Min(44f)]
    private float collapsedPanelHeight = 58f;

    [SerializeField, Min(180f)]
    private float expandedPanelHeight = 250f;

    private BattleTestPlayerMode selectedPlayer;
    private BattleTestEncounterMode selectedEncounter;
    private EmotionType selectedEmotion;
    private string lastApplyMessage = "아직 적용되지 않음";
    private bool runtimePanelCollapsed;

    public BattleTestPlayerMode SelectedPlayer => selectedPlayer;
    public BattleTestEncounterMode SelectedEncounter => selectedEncounter;
    public EmotionType SelectedEmotion => selectedEmotion;

    public Character OlafPrefab => olafPrefab;
    public Character YujinPrefab => yujinPrefab;
    public Character HifumiPrefab => hifumiPrefab;
    public Character NormalEnemyPrefab => normalEnemyPrefab;
    public Character EliteEnemyPrefab => eliteEnemyPrefab;
    public Character BossEnemyPrefab => bossEnemyPrefab;

    /// <summary>
    /// IMGUI 테스트 패널은 EventSystem Raycast 대상이 아니므로,
    /// 월드 클릭 라우터가 패널 영역을 별도로 차단할 때 사용한다.
    /// Input.mousePosition은 좌하단 원점이고 IMGUI는 좌상단 원점이므로
    /// Y축을 변환한 뒤 마지막으로 그려진 Window Rect와 비교한다.
    /// </summary>
    public static bool IsPointerBlockedByRuntimePanel(
        Vector2 screenPosition)
    {
        BattleTestScenarioSwitcher instance =
            activeInstance;

        if (instance == null ||
            !instance.isActiveAndEnabled ||
            !instance.showRuntimePanel ||
            !Application.isPlaying)
        {
            return false;
        }

        Vector2 guiPoint =
            new Vector2(
                screenPosition.x,
                Screen.height - screenPosition.y);

        return activeRuntimePanelRect.Contains(
            guiPoint);
    }

    private void Awake()
    {
        activeInstance = this;
        activeRuntimePanelRect = runtimePanelRect;

        ResolveReferences();
        LoadSelection();
        LoadRuntimePanelState();
        ApplyScenarioBeforeBattle();
    }

    private void OnEnable()
    {
        activeInstance = this;
        activeRuntimePanelRect = runtimePanelRect;
    }

    private void OnDisable()
    {
        if (activeInstance == this)
        {
            activeInstance = null;
            activeRuntimePanelRect = default;
        }
    }

    public void ConfigureAssets(
        Character newOlafPrefab,
        Character newYujinPrefab,
        Character newHifumiPrefab,
        Character newNormalEnemyPrefab,
        Character newEliteEnemyPrefab,
        Character newBossEnemyPrefab)
    {
        olafPrefab = newOlafPrefab;
        yujinPrefab = newYujinPrefab;
        hifumiPrefab = newHifumiPrefab;
        normalEnemyPrefab = newNormalEnemyPrefab;
        eliteEnemyPrefab = newEliteEnemyPrefab;
        bossEnemyPrefab = newBossEnemyPrefab;
    }

    public void ConfigureScene(
        BattleManager manager,
        BattleRosterController controller,
        Transform playerSpawn,
        IReadOnlyList<Transform> enemySpawns,
        Transform runtimeRoot,
        IReadOnlyList<Character> scenePrototypes)
    {
        battleManager = manager;
        rosterController = controller;
        playerSpawnPoint = playerSpawn;
        runtimeParticipantsRoot = runtimeRoot;

        enemySpawnPoints ??= new List<Transform>();
        enemySpawnPoints.Clear();

        if (enemySpawns != null)
        {
            for (int i = 0; i < enemySpawns.Count; i++)
            {
                Transform spawn = enemySpawns[i];
                if (spawn != null)
                    enemySpawnPoints.Add(spawn);
            }
        }

        scenePrototypeCharacters ??= new List<Character>();
        scenePrototypeCharacters.Clear();

        if (scenePrototypes == null)
            return;

        for (int i = 0; i < scenePrototypes.Count; i++)
        {
            Character character = scenePrototypes[i];

            if (character != null &&
                !scenePrototypeCharacters.Contains(character))
            {
                scenePrototypeCharacters.Add(character);
            }
        }
    }

    public bool ApplyScenarioBeforeBattle()
    {
        ResolveReferences();

        if (battleManager == null || rosterController == null)
        {
            lastApplyMessage = "BattleManager 또는 RosterController 없음";
            Debug.LogError("[BattleTestScenarioSwitcher] " + lastApplyMessage, this);
            return false;
        }

        if (battleManager.IsInitialized)
        {
            lastApplyMessage = "이미 초기화된 전투에는 직접 적용할 수 없음";
            return false;
        }

        Character playerPrefab = ResolvePlayerPrefab();
        List<Character> enemies = BuildEnemyPrefabList();

        if (playerPrefab == null)
        {
            lastApplyMessage = "선택한 플레이어 Prefab 없음";
            Debug.LogError("[BattleTestScenarioSwitcher] " + lastApplyMessage, this);
            return false;
        }

        if (enemies.Count == 0)
        {
            lastApplyMessage = "선택한 전투의 Enemy Prefab 없음";
            Debug.LogError("[BattleTestScenarioSwitcher] " + lastApplyMessage, this);
            return false;
        }

        EnsureEnemySpawnCapacity(enemies.Count);

        battleManager.ConfigureEmotionProgression(
            configureEmotionForTest
                ? selectedEmotion
                : (EmotionType?)null,
            emotionAugmentCatalog);

        rosterController.ConfigurePrefabRoster(
            playerPrefab,
            enemies,
            playerSpawnPoint,
            enemySpawnPoints,
            runtimeParticipantsRoot,
            scenePrototypeCharacters);

        battleManager.AssignRosterController(rosterController);

        lastApplyMessage =
            $"{GetPlayerLabel(selectedPlayer)} / " +
            $"{GetEncounterLabel(selectedEncounter)} / " +
            $"감정 {GameplayV5UiPresentation.GetEmotionTheme(selectedEmotion).DisplayName} / " +
            $"적 {enemies.Count}";

        Debug.Log(
            "[BattleTestScenarioSwitcher] Roster 적용 / " +
            lastApplyMessage,
            this);

        return true;
    }

    /// <summary>
    /// Character Verification 전용 단일 호출 시나리오 전환.
    /// rememberSelection 설정과 무관하게 검증 동안 필요한 Player/Encounter를
    /// PlayerPrefs에 저장해 Scene reload 뒤에도 동일한 Roster가 재구성되게 한다.
    /// 검증 종료 시 원래 선택값을 같은 API로 복원한다.
    /// </summary>
    public bool ApplyVerificationScenario(
        BattleTestPlayerMode player,
        BattleTestEncounterMode encounter,
        bool reloadScene = true)
    {
        selectedPlayer = player;
        selectedEncounter = encounter;

        PlayerPrefs.SetInt(
            PlayerPreferenceKey,
            (int)selectedPlayer);

        PlayerPrefs.SetInt(
            EncounterPreferenceKey,
            (int)selectedEncounter);

        PlayerPrefs.SetInt(
            EmotionPreferenceKey,
            (int)selectedEmotion);

        PlayerPrefs.Save();

        if (!reloadScene)
            return ApplyScenarioBeforeBattle();

        RequestCleanApply();
        return true;
    }

    public void SelectOlaf()
    {
        ChangeSelection(
            BattleTestPlayerMode.Olaf,
            selectedEncounter);
    }

    public void SelectYujin()
    {
        ChangeSelection(
            BattleTestPlayerMode.Yujin,
            selectedEncounter);
    }

    public void SelectHifumi()
    {
        ChangeSelection(
            BattleTestPlayerMode.Hifumi,
            selectedEncounter);
    }

    public void SelectNormalBattle()
    {
        ChangeSelection(
            selectedPlayer,
            BattleTestEncounterMode.NormalBattle);
    }

    public void SelectMixedBattle()
    {
        ChangeSelection(
            selectedPlayer,
            BattleTestEncounterMode.MixedBattle);
    }

    public void SelectBossBattle()
    {
        ChangeSelection(
            selectedPlayer,
            BattleTestEncounterMode.BossBattle);
    }

    public void SelectEmotion(EmotionType emotion)
    {
        selectedEmotion = emotion;
        SaveSelection();
        RequestCleanApply();
    }

    public void ResetSavedSelection()
    {
        selectedPlayer = defaultPlayer;
        selectedEncounter = defaultEncounter;
        selectedEmotion = defaultEmotion;

        PlayerPrefs.DeleteKey(PlayerPreferenceKey);
        PlayerPrefs.DeleteKey(EncounterPreferenceKey);
        PlayerPrefs.DeleteKey(EmotionPreferenceKey);
        PlayerPrefs.Save();

        RequestCleanApply();
    }

    public void ToggleRuntimePanelSize()
    {
        runtimePanelCollapsed =
            !runtimePanelCollapsed;

        PlayerPrefs.SetInt(
            PanelCollapsedPreferenceKey,
            runtimePanelCollapsed ? 1 : 0);

        PlayerPrefs.Save();
    }

    private void LoadRuntimePanelState()
    {
        runtimePanelCollapsed =
            PlayerPrefs.HasKey(
                PanelCollapsedPreferenceKey)
                ? PlayerPrefs.GetInt(
                    PanelCollapsedPreferenceKey,
                    startRuntimePanelCollapsed ? 1 : 0) != 0
                : startRuntimePanelCollapsed;
    }

    private void ChangeSelection(
        BattleTestPlayerMode player,
        BattleTestEncounterMode encounter)
    {
        selectedPlayer = player;
        selectedEncounter = encounter;
        SaveSelection();
        RequestCleanApply();
    }

    private void RequestCleanApply()
    {
        if (!Application.isPlaying)
        {
            ApplyScenarioBeforeBattle();
            return;
        }

        // 전투 Manager는 종료 후 동일 Instance 재초기화를 지원하지 않는다.
        // 현재 Scene을 다시 불러 Roster, UI, 이벤트 구독을 완전히 새로 만든다.
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid())
        {
            Debug.LogError(
                "[BattleTestScenarioSwitcher] 활성 Scene이 없어 다시 불러올 수 없습니다.",
                this);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
        {
            battleManager = GetComponent<BattleManager>();
            battleManager ??= FindFirstObjectByType<BattleManager>();
        }

        if (rosterController == null)
        {
            rosterController = GetComponent<BattleRosterController>();
            rosterController ??= battleManager?.RosterController;
        }

        if (runtimeParticipantsRoot == null)
        {
            GameObject runtimeRootObject =
                GameObject.Find("RuntimeParticipants");

            runtimeParticipantsRoot =
                runtimeRootObject != null
                    ? runtimeRootObject.transform
                    : null;
        }
    }

    private void LoadSelection()
    {
        selectedPlayer = defaultPlayer;
        selectedEncounter = defaultEncounter;
        selectedEmotion = defaultEmotion;

        if (!rememberSelection)
            return;

        if (PlayerPrefs.HasKey(PlayerPreferenceKey))
        {
            selectedPlayer =
                (BattleTestPlayerMode)Mathf.Clamp(
                    PlayerPrefs.GetInt(
                        PlayerPreferenceKey,
                        (int)defaultPlayer),
                    (int)BattleTestPlayerMode.Olaf,
                    (int)BattleTestPlayerMode.Hifumi);
        }

        if (PlayerPrefs.HasKey(EncounterPreferenceKey))
        {
            selectedEncounter =
                (BattleTestEncounterMode)Mathf.Clamp(
                    PlayerPrefs.GetInt(
                        EncounterPreferenceKey,
                        (int)defaultEncounter),
                    (int)BattleTestEncounterMode.NormalBattle,
                    (int)BattleTestEncounterMode.BossBattle);
        }

        if (PlayerPrefs.HasKey(EmotionPreferenceKey))
        {
            selectedEmotion =
                (EmotionType)Mathf.Clamp(
                    PlayerPrefs.GetInt(
                        EmotionPreferenceKey,
                        (int)defaultEmotion),
                    (int)EmotionType.Awe,
                    (int)EmotionType.Detachment);
        }
    }

    private void SaveSelection()
    {
        if (!rememberSelection)
            return;

        PlayerPrefs.SetInt(
            PlayerPreferenceKey,
            (int)selectedPlayer);

        PlayerPrefs.SetInt(
            EncounterPreferenceKey,
            (int)selectedEncounter);

        PlayerPrefs.SetInt(
            EmotionPreferenceKey,
            (int)selectedEmotion);

        PlayerPrefs.Save();
    }

    private Character ResolvePlayerPrefab()
    {
        Character selected = selectedPlayer switch
        {
            BattleTestPlayerMode.Yujin => yujinPrefab,
            BattleTestPlayerMode.Hifumi => hifumiPrefab,
            _ => olafPrefab
        };

        if (selected != null)
            return selected;

        if (olafPrefab != null)
            return olafPrefab;

        if (yujinPrefab != null)
            return yujinPrefab;

        return hifumiPrefab;
    }

    private List<Character> BuildEnemyPrefabList()
    {
        List<Character> result = new();

        switch (selectedEncounter)
        {
            case BattleTestEncounterMode.NormalBattle:
                AddRepeated(result, normalEnemyPrefab, 3);
                break;

            case BattleTestEncounterMode.MixedBattle:
                AddIfPresent(result, eliteEnemyPrefab);
                AddRepeated(result, normalEnemyPrefab, 2);
                break;

            case BattleTestEncounterMode.BossBattle:
                AddIfPresent(result, bossEnemyPrefab);
                break;
        }

        return result;
    }

    private void EnsureEnemySpawnCapacity(int count)
    {
        enemySpawnPoints ??= new List<Transform>();

        while (enemySpawnPoints.Count < count)
            enemySpawnPoints.Add(null);
    }

    private static void AddIfPresent(
        ICollection<Character> destination,
        Character value)
    {
        if (destination != null && value != null)
            destination.Add(value);
    }

    private static void AddRepeated(
        ICollection<Character> destination,
        Character value,
        int count)
    {
        if (destination == null || value == null)
            return;

        for (int i = 0; i < Mathf.Max(0, count); i++)
            destination.Add(value);
    }

    private void OnGUI()
    {
        if (!showRuntimePanel || !Application.isPlaying)
        {
            if (activeInstance == this)
                activeRuntimePanelRect = default;

            return;
        }

        runtimePanelRect.height =
            runtimePanelCollapsed
                ? Mathf.Max(
                    44f,
                    collapsedPanelHeight)
                : Mathf.Max(
                    360f,
                    expandedPanelHeight);

        // Update가 OnGUI보다 먼저 실행되는 Frame에서도 클릭을 막을 수 있도록
        // Window를 그리기 전후 모두 최신 Rect를 공개한다.
        activeRuntimePanelRect = runtimePanelRect;
        GUI.depth = -1000;

        runtimePanelRect = GUI.Window(
            GetInstanceID(),
            runtimePanelRect,
            DrawRuntimePanel,
            "Project Abyss 테스트 전투");

        activeRuntimePanelRect = runtimePanelRect;
    }

    private void DrawRuntimePanel(int windowId)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(
            $"{GetPlayerLabel(selectedPlayer)} / " +
            $"{GetEncounterLabel(selectedEncounter)} / " +
            $"{GameplayV5UiPresentation.GetEmotionTheme(selectedEmotion).DisplayName}",
            GUILayout.ExpandWidth(true));

        if (GUILayout.Button(
                runtimePanelCollapsed
                    ? "최대화"
                    : "최소화",
                GUILayout.Width(64f)))
        {
            ToggleRuntimePanelSize();
        }

        GUILayout.EndHorizontal();

        if (runtimePanelCollapsed)
        {
            GUI.DragWindow(
                new Rect(
                    0f,
                    0f,
                    runtimePanelRect.width,
                    24f));

            return;
        }

        GUILayout.Space(4f);
        GUILayout.Label(
            "플레이어: " + GetPlayerLabel(selectedPlayer));

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("올라프"))
            SelectOlaf();

        if (GUILayout.Button("유진"))
            SelectYujin();

        if (GUILayout.Button("히후미"))
            SelectHifumi();

        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        GUILayout.Label(
            "전투: " + GetEncounterLabel(selectedEncounter));

        if (GUILayout.Button("일반전투 · 일반몹 3"))
            SelectNormalBattle();

        if (GUILayout.Button("혼합전투 · 정예 1 + 일반 2"))
            SelectMixedBattle();

        if (GUILayout.Button("보스전투 · 보스 1"))
            SelectBossBattle();

        GUILayout.Space(6f);
        GUILayout.Label(
            "감정: " +
            GameplayV5UiPresentation.GetEmotionTheme(selectedEmotion).DisplayName);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("경외")) SelectEmotion(EmotionType.Awe);
        if (GUILayout.Button("신의")) SelectEmotion(EmotionType.Faith);
        if (GUILayout.Button("동경")) SelectEmotion(EmotionType.Admiration);
        if (GUILayout.Button("감복")) SelectEmotion(EmotionType.Impression);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("연민")) SelectEmotion(EmotionType.Compassion);
        if (GUILayout.Button("그리움")) SelectEmotion(EmotionType.Longing);
        if (GUILayout.Button("초연")) SelectEmotion(EmotionType.Detachment);
        GUILayout.EndHorizontal();

        if (emotionAugmentCatalog == null)
            GUILayout.Label("증강 Catalog: 미연결 (고조/열광 HUD는 정상 표시)");

        GUILayout.Space(6f);
        GUILayout.Label("현재 적용: " + lastApplyMessage);

        if (GUILayout.Button("저장된 선택 초기화"))
            ResetSavedSelection();

        GUI.DragWindow(
            new Rect(0f, 0f, runtimePanelRect.width, 24f));
    }

    private static string GetPlayerLabel(
        BattleTestPlayerMode mode)
    {
        return mode switch
        {
            BattleTestPlayerMode.Yujin => "유진",
            BattleTestPlayerMode.Hifumi => "히후미",
            _ => "올라프"
        };
    }

    private static string GetEncounterLabel(
        BattleTestEncounterMode mode)
    {
        return mode switch
        {
            BattleTestEncounterMode.NormalBattle =>
                "일반전투",

            BattleTestEncounterMode.MixedBattle =>
                "혼합전투",

            BattleTestEncounterMode.BossBattle =>
                "보스전투",

            _ => mode.ToString()
        };
    }
}
