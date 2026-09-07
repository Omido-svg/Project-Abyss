using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleResolutionUiController : MonoBehaviour
{
    private sealed class ActiveState
    {
        public GameObject Target;
        public bool WasActive;
    }

    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private GameObject abyssBattleUiRoot;
    [SerializeField] private GameObject persistentTopLayer;
    [SerializeField] private GameObject defaultBattleLayer;
    [SerializeField] private GameObject clashOverviewPanel;
    [SerializeField] private GameObject skillSelectionLayer;
    [SerializeField] private GameObject characterDetailLayer;
    [SerializeField] private GameObject clashPresentationLayer;
    [SerializeField] private BattleCharacterDetailPanelUI characterDetailPanel;
    [SerializeField] private BattleClashRollPresentationUI clashRollPresentation;
    [SerializeField] private BattleActionOrderRailUI actionOrderRail;
    [SerializeField] private BattleWorldCharacterPlateManager worldPlateManager;

    [Header("Behavior")]
    [SerializeField] private bool detectResolutionState = true;
    [SerializeField] private bool retireLegacyClashOverview = true;

    private readonly List<ActiveState> rootStates = new();
    private readonly List<ActiveState> defaultLayerStates = new();

    private CanvasGroup clashOverviewCanvasGroup;
    private bool overviewInteractable;
    private bool overviewBlocksRaycasts;
    private bool isResolutionPresentationActive;

    public static BattleResolutionUiController Instance
    {
        get;
        private set;
    }

    public bool IsResolutionPresentationActive =>
        isResolutionPresentationActive;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    public void Configure(
        BattleManager manager,
        GameObject uiRoot,
        GameObject topLayer,
        GameObject defaultLayer,
        GameObject overviewPanel,
        GameObject skillLayer,
        GameObject detailLayer,
        GameObject presentationLayer,
        BattleCharacterDetailPanelUI detailPanel,
        BattleClashRollPresentationUI rollPresentation)
    {
        battleManager = manager;
        abyssBattleUiRoot = uiRoot;
        persistentTopLayer = topLayer;
        defaultBattleLayer = defaultLayer;
        clashOverviewPanel = overviewPanel;
        skillSelectionLayer = skillLayer;
        characterDetailLayer = detailLayer;
        clashPresentationLayer = presentationLayer;
        characterDetailPanel = detailPanel;
        clashRollPresentation = rollPresentation;
        ResolveReferences();
        ApplyLegacyOverviewRetirement();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResolveReferences();
        ApplyLegacyOverviewRetirement();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        ResolveReferences();
        ApplyLegacyOverviewRetirement();
    }

    private void Update()
    {
        if (!detectResolutionState ||
            BattleSimulationRuntime.IsBatchSimulation)
        {
            return;
        }

        bool resolving =
            battleManager?.TurnManager?.IsResolving == true;

        if (resolving &&
            !isResolutionPresentationActive)
        {
            BeginResolutionPresentation();
        }
        else if (!resolving &&
                 isResolutionPresentationActive)
        {
            EndResolutionPresentation();
        }
    }

    private void OnDisable()
    {
        if (isResolutionPresentationActive)
            EndResolutionPresentation();

        if (Instance == this)
            Instance = null;
    }

    public static void BeginCurrentResolution()
    {
        Instance?.BeginResolutionPresentation();
    }

    public static void EndCurrentResolution()
    {
        Instance?.EndResolutionPresentation();
    }

    public void BeginResolutionPresentation()
    {
        if (isResolutionPresentationActive ||
            BattleSimulationRuntime.IsBatchSimulation)
        {
            return;
        }

        ResolveReferences();
        ApplyLegacyOverviewRetirement();
        CaptureStates();

        isResolutionPresentationActive = true;

        characterDetailPanel?.HideForResolution();
        BattlePresentationInteractionLock.SetLocked(true);

        // Resolution에서는 정적인 전투 UI를 모두 숨기고,
        // 캐릭터 월드 HUD(HP/흐트러짐/고유 게이지)와 행동 순서 레일만 유지한다.
        // 월드 플레이트가 Battle UI Root 안에 배치된 Scene이라도 부모 비활성화에
        // 휘말리지 않도록 먼저 독립 보존 상태로 전환한다.
        worldPlateManager?.PreserveForResolution(
            abyssBattleUiRoot != null
                ? abyssBattleUiRoot.transform
                : null);

        // 행동 순서 레일은 독립 Root Overlay Canvas이므로 별도 UI Layer가 필요 없다.
        actionOrderRail?.PreserveForResolution(null);

        HideStaticBattleUiForResolution();

        // RNG/합 굴림 연출은 Resolution의 핵심 피드백이므로 살아 있어야 한다.
        // 실제 표시/숨김 타이밍은 BattleAnimationDirector와
        // BattleClashRollPresentationUI가 교환 단위로 관리한다.
        if (clashPresentationLayer != null)
            clashPresentationLayer.SetActive(true);

        Debug.Log(
            "[BattleResolutionUI][BEGIN] " +
            "캐릭터 HP/흐트러짐/고유 게이지, 행동 순서, 합 RNG 연출을 유지하고 " +
            "나머지 전투 UI를 숨긴 채 Resolution을 시작합니다.",
            this);
    }

    public void EndResolutionPresentation()
    {
        if (!isResolutionPresentationActive)
            return;

        clashRollPresentation?.HideImmediate();

        RestoreOverviewInteraction();
        RestoreStates(defaultLayerStates);
        RestoreStates(rootStates);

        // UI Root가 원래 상태로 돌아온 뒤 World HUD와 레일을 Planning 상태로 복귀한다.
        worldPlateManager?.RestoreAfterResolution();
        actionOrderRail?.RestoreAfterResolution();

        defaultLayerStates.Clear();
        rootStates.Clear();

        isResolutionPresentationActive = false;
        BattlePresentationInteractionLock.SetLocked(false);

        Debug.Log(
            "[BattleResolutionUI][END] " +
            "기본 전투 UI와 입력을 복구합니다.",
            this);
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (abyssBattleUiRoot == null)
            abyssBattleUiRoot = gameObject;

        Transform root =
            abyssBattleUiRoot != null
                ? abyssBattleUiRoot.transform
                : transform;

        persistentTopLayer ??=
            FindDirectChild(root, "PersistentTopLayer")?.gameObject;

        defaultBattleLayer ??=
            FindDirectChild(root, "DefaultBattleLayer")?.gameObject;

        skillSelectionLayer ??=
            FindDirectChild(root, "SkillSelectionLayer")?.gameObject;

        characterDetailLayer ??=
            FindDirectChild(root, "CharacterDetailLayer")?.gameObject;

        clashPresentationLayer ??=
            FindDirectChild(root, "ClashRollPresentationLayer")?.gameObject;

        if (clashOverviewPanel == null &&
            defaultBattleLayer != null)
        {
            clashOverviewPanel =
                FindDirectChild(
                    defaultBattleLayer.transform,
                    "ClashOverviewPanel")?.gameObject;
        }

        if (characterDetailPanel == null)
        {
            characterDetailPanel =
                FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                    FindObjectsInactive.Include);
        }

        if (clashRollPresentation == null &&
            clashPresentationLayer != null)
        {
            clashRollPresentation =
                clashPresentationLayer.GetComponent<
                    BattleClashRollPresentationUI>();
        }

        if (actionOrderRail == null)
        {
            actionOrderRail =
                FindFirstObjectByType<BattleActionOrderRailUI>(
                    FindObjectsInactive.Include);
        }

        if (worldPlateManager == null)
        {
            worldPlateManager =
                FindFirstObjectByType<BattleWorldCharacterPlateManager>(
                    FindObjectsInactive.Include);
        }
    }

    private void ApplyLegacyOverviewRetirement()
    {
        if (!retireLegacyClashOverview || clashOverviewPanel == null)
            return;

        clashOverviewPanel.SetActive(false);
    }

    private void CaptureStates()
    {
        rootStates.Clear();
        defaultLayerStates.Clear();

        if (abyssBattleUiRoot != null)
        {
            Transform root = abyssBattleUiRoot.transform;

            for (int index = 0;
                 index < root.childCount;
                 index++)
            {
                GameObject child =
                    root.GetChild(index).gameObject;

                rootStates.Add(
                    new ActiveState
                    {
                        Target = child,
                        WasActive = child.activeSelf
                    });
            }
        }

        if (defaultBattleLayer != null)
        {
            Transform root = defaultBattleLayer.transform;

            for (int index = 0;
                 index < root.childCount;
                 index++)
            {
                GameObject child =
                    root.GetChild(index).gameObject;

                defaultLayerStates.Add(
                    new ActiveState
                    {
                        Target = child,
                        WasActive = child.activeSelf
                    });
            }
        }
    }

    private void HideStaticBattleUiForResolution()
    {
        // 캐릭터 머리 위 World HUD와 Damage/VFX 계층은 이 전용 Screen UI Layer들과
        // 분리되어 있으므로 건드리지 않는다.
        persistentTopLayer?.SetActive(false);
        defaultBattleLayer?.SetActive(false);
        skillSelectionLayer?.SetActive(false);
        characterDetailLayer?.SetActive(false);

        // ClashRollPresentationLayer는 숨기지 않는다.
        // 합 교환의 RNG 결과 UI가 Resolution 중 이 Layer에서 재생된다.

        if (clashOverviewPanel != null)
            clashOverviewPanel.SetActive(false);
    }

    private void DisableOverviewInteraction()
    {
        if (retireLegacyClashOverview || clashOverviewPanel == null)
            return;

        clashOverviewCanvasGroup =
            clashOverviewPanel.GetComponent<CanvasGroup>();

        if (clashOverviewCanvasGroup == null)
        {
            clashOverviewCanvasGroup =
                clashOverviewPanel.AddComponent<CanvasGroup>();
        }

        overviewInteractable =
            clashOverviewCanvasGroup.interactable;

        overviewBlocksRaycasts =
            clashOverviewCanvasGroup.blocksRaycasts;

        clashOverviewCanvasGroup.interactable = false;
        clashOverviewCanvasGroup.blocksRaycasts = false;
    }

    private void RestoreOverviewInteraction()
    {
        if (clashOverviewCanvasGroup == null)
            return;

        clashOverviewCanvasGroup.interactable =
            overviewInteractable;

        clashOverviewCanvasGroup.blocksRaycasts =
            overviewBlocksRaycasts;
    }

    private static void RestoreStates(
        List<ActiveState> states)
    {
        if (states == null)
            return;

        foreach (ActiveState state in states)
        {
            if (state?.Target == null)
                continue;

            state.Target.SetActive(
                state.WasActive);
        }
    }

    private static Transform FindDirectChild(
        Transform parent,
        string childName)
    {
        if (parent == null)
            return null;

        for (int index = 0;
             index < parent.childCount;
             index++)
        {
            Transform child =
                parent.GetChild(index);

            if (child.name == childName)
                return child;
        }

        return null;
    }
}