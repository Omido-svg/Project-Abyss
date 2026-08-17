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

        HideAllRootLayersExceptResolution();
        HideDefaultLayerChildrenExceptOverview();
        DisableOverviewInteraction();

        if (clashPresentationLayer != null)
            clashPresentationLayer.SetActive(true);

        Debug.Log(
            "[BattleResolutionUI][BEGIN] " +
            (retireLegacyClashOverview
                ? "TopStatusBar만 유지하고 구형 전체 결투 현황을 숨긴 채 전투 연출 입력을 잠급니다."
                : "TopStatusBar와 ClashOverviewPanel을 유지하고 전투 연출 입력을 잠급니다."),
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

    private void HideAllRootLayersExceptResolution()
    {
        if (abyssBattleUiRoot == null)
            return;

        Transform root = abyssBattleUiRoot.transform;

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            GameObject child =
                root.GetChild(index).gameObject;

            bool keep =
                child == persistentTopLayer ||
                child == defaultBattleLayer ||
                child == clashPresentationLayer;

            child.SetActive(keep);
        }

        // 턴/기세/빛/웨이브를 표시하는 TopStatusBar는
        // PersistentTopLayer 아래에 있으므로 합 연출 중에도 유지한다.
        persistentTopLayer?.SetActive(true);
        defaultBattleLayer?.SetActive(true);
    }

    private void HideDefaultLayerChildrenExceptOverview()
    {
        if (defaultBattleLayer == null)
            return;

        Transform root = defaultBattleLayer.transform;

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            GameObject child =
                root.GetChild(index).gameObject;

            child.SetActive(
                !retireLegacyClashOverview &&
                child == clashOverviewPanel);
        }

        if (!retireLegacyClashOverview)
            clashOverviewPanel?.SetActive(true);
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
