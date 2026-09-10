using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 월드 ActionSlot 전용 Screen Space 화살표 오버레이.
/// Focused Encounter식으로 "플레이어가 지정한 타깃"과 "실제로 성립하는 합"을 분리해서 표시한다.
/// - TargetSlot 지정은 속도/가로채기 성공 여부와 무관하게 그대로 보존한다.
/// - 합이 성립하지 않으면 플레이어 -> 적, 적 -> 원래 대상의 두 일방 화살표를 유지한다.
/// - 합이 성립하면 해당 두 행동의 일반 화살표 대신 플레이어/적 양쪽 화살표가 중앙에서 충돌한다.
/// - 합 표시가 생겨도 플레이어가 지정한 적 ActionSlot 자체를 다른 슬롯으로 자동 재배선하지 않는다.
/// - 타깃 선택 중 적 슬롯에 Hover하면 Mouse Arrow가 해당 슬롯 중심으로 Snap된다.
/// - Hover 정보는 가로채기 성공/실패 원인을 노출하지 않고 합 우세도 또는 "일방공격"만 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleWorldSlotArrowOverlayUI : MonoBehaviour
{
    private sealed class ArrowVisual
    {
        public RectTransform Root;
        public Image Body;
        public Image HeadLeft;
        public Image HeadRight;

        public void SetActive(bool active)
        {
            if (Root != null && Root.gameObject.activeSelf != active)
                Root.gameObject.SetActive(active);
        }

        public void SetColor(Color color)
        {
            if (Body != null)
                Body.color = color;
            if (HeadLeft != null)
                HeadLeft.color = color;
            if (HeadRight != null)
                HeadRight.color = color;
        }
    }

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private BattleScreenModeController screenModeController;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SkillSelectPanelUI skillSelectPanel;


    [Header("TargetArrowUI Parity")]
    [SerializeField] private bool showPlayerIntentArrows = true;
    [SerializeField] private bool showEnemyIntentArrows = true;

    [Header("Arrow Color")]
    // Focused Encounter 화살표 색 정책
    // - 플레이어 일방공격: 파랑
    // - 실제/예정 합: 양쪽 모두 노랑
    // - 적 일방공격: 빨강
    [SerializeField] private Color playerArrowColor =
        new(0.18f, 0.52f, 1f, 0.96f);

    [SerializeField] private Color enemyArrowColor =
        new(1f, 0.12f, 0.12f, 0.96f);

    [SerializeField] private Color clashArrowColor =
        new(1f, 0.82f, 0.12f, 0.98f);

    // 아직 적 슬롯을 Hover하지 않은 플레이어의 지정 화살표도
    // 일방공격 의도와 동일하게 파란색으로 보여준다.
    [SerializeField] private Color pendingColor =
        new(0.18f, 0.52f, 1f, 0.96f);

    [Header("Arrow Style")]
    [SerializeField, Min(1f)] private float normalThickness = 5f;
    [SerializeField, Min(1f)] private float pendingThickness = 7f;
    [SerializeField, Min(4f)] private float arrowHeadLength = 22f;
    [SerializeField, Range(10f, 80f)] private float arrowHeadAngle = 34f;
    [SerializeField, Min(0f)] private float endpointPadding = 7f;
    [SerializeField, Min(0f)] private float clashCenterGap = 18f;
    [SerializeField, Min(0f)] private float actionLaneSpacing = 8f;

    [Header("Planning Readability")]
    [Tooltip("아무 슬롯도 선택하지 않았을 때는 전체 관계를 읽을 수 있을 정도로만 화살표를 희미하게 유지합니다.")]
    [SerializeField] private bool deEmphasizeIdleArrows = true;
    [SerializeField, Range(0.05f, 1f)] private float idlePlayerArrowAlpha = 0.34f;
    [SerializeField, Range(0.05f, 1f)] private float idleEnemyArrowAlpha = 0.24f;
    [SerializeField, Range(0.05f, 1f)] private float idleClashArrowAlpha = 0.48f;
    [Tooltip("타깃 선택/호버 중 현재 관계와 무관한 화살표의 Alpha입니다.")]
    [SerializeField, Range(0f, 0.5f)] private float unrelatedFocusAlpha = 0.08f;

    [Header("Hover Clash Preview")]
    [SerializeField] private Color veryUnfavorableColor = new(1f, 0.30f, 0.24f, 1f);
    [SerializeField] private Color unfavorableColor = new(1f, 0.62f, 0.22f, 1f);
    [SerializeField] private Color balancedColor = new(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color favorableColor = new(0.38f, 0.82f, 1f, 1f);
    [SerializeField] private Color veryFavorableColor = new(0.34f, 1f, 0.68f, 1f);
    [SerializeField] private Vector2 hoverPreviewSize = new(196f, 66f);
    [SerializeField] private Vector2 hoverPreviewMouseOffset = new(20f, -18f);

    private Canvas overlayCanvas;
    private RectTransform arrowRoot;
    private RectTransform hoverPreviewRoot;
    private Image hoverPreviewBackground;
    private TMP_Text hoverPreviewText;
    private readonly List<ArrowVisual> arrows = new();
    private readonly HashSet<ActionSlot> resolvedPlayerClashSlots = new();
    private readonly HashSet<ActionSlot> resolvedEnemyClashSlots = new();
    private bool hiddenForResolution;
    private bool missingSceneOverlayLogged;

    // 클릭 전 Hover에서만 사용하는 임시 합 시각화 상태.
    // 실제 ActionSlot.TargetSlot / ClashBuilder 결과는 변경하지 않는다.
    private ActionSlot hoverClashEnemySlot;
    private ActionSlot hoverClashPlayerSlot;

    private readonly PlayerAutoPlanService clashEstimator = new();

    public void Configure(
        BattleManager manager,
        BattleUIManager managerUi,
        Camera camera)
    {
        battleManager = manager;
        uiManager = managerUi;
        targetCamera = camera;
        EnsureOverlay();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureOverlay();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        EnsureOverlay();

        if (hiddenForResolution)
        {
            SetPlanningOverlayVisible(false);
            return;
        }

        RefreshArrows();
    }

    public void SetResolutionHidden(
        bool hidden)
    {
        hiddenForResolution = hidden;
        EnsureOverlay();

        if (hidden)
            SetPlanningOverlayVisible(false);
    }

    private void ResolveReferences()
    {
        battleManager ??= FindFirstObjectByType<BattleManager>();
        uiManager ??= FindFirstObjectByType<BattleUIManager>(FindObjectsInactive.Include);
        screenModeController ??=
            FindFirstObjectByType<BattleScreenModeController>(
                FindObjectsInactive.Include);
        targetCamera ??= Camera.main;

        if (skillSelectPanel == null)
        {
            skillSelectPanel =
                FindFirstObjectByType<SkillSelectPanelUI>(
                    FindObjectsInactive.Include);
        }
    }

    private void SetPlanningOverlayVisible(bool visible)
    {
        if (!visible)
        {
            HideUnused(0);
            SetHoverPreviewVisible(false);
        }

        if (arrowRoot != null &&
            arrowRoot.gameObject.activeSelf != visible)
        {
            arrowRoot.gameObject.SetActive(visible);
        }

        if (!visible &&
            hoverPreviewRoot != null &&
            hoverPreviewRoot.gameObject.activeSelf)
        {
            hoverPreviewRoot.gameObject.SetActive(false);
        }
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas != null &&
            arrowRoot != null &&
            hoverPreviewRoot != null &&
            hoverPreviewBackground != null &&
            hoverPreviewText != null)
        {
            return;
        }

        if (TryResolveSceneOverlay())
        {
            missingSceneOverlayLogged = false;
            return;
        }

        // Play Mode에서는 고정 Overlay Canvas/Hover Preview를 만들거나 배치하지 않는다.
        if (Application.isPlaying)
        {
            if (!missingSceneOverlayLogged)
            {
                missingSceneOverlayLogged = true;
                Debug.LogWarning(
                    "[BattleWorldSlotArrowOverlayUI] Scene-authored WorldSlotArrowOverlay가 없습니다. " +
                    "Editor 변환 도구를 실행하세요.",
                    this);
            }
            return;
        }

        if (overlayCanvas == null || arrowRoot == null)
        {
            Transform existing = transform.Find("WorldSlotArrowOverlay");
            GameObject canvasGo;

            if (existing != null)
            {
                canvasGo = existing.gameObject;
            }
            else
            {
                canvasGo = new GameObject(
                    "WorldSlotArrowOverlay",
                    typeof(RectTransform),
                    typeof(Canvas));
                canvasGo.transform.SetParent(transform, false);
            }

            overlayCanvas = canvasGo.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 20;

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            Stretch(canvasRect);

            Transform rootExisting = canvasGo.transform.Find("Arrows");
            GameObject rootGo;

            if (rootExisting != null)
            {
                rootGo = rootExisting.gameObject;
            }
            else
            {
                rootGo = new GameObject("Arrows", typeof(RectTransform));
                rootGo.transform.SetParent(canvasGo.transform, false);
            }

            arrowRoot = rootGo.GetComponent<RectTransform>();
            Stretch(arrowRoot);
        }

        EnsureHoverPreview();
    }

    private bool TryResolveSceneOverlay()
    {
        Transform existing = transform.Find("WorldSlotArrowOverlay");
        if (existing == null)
            return false;

        Canvas canvas = existing.GetComponent<Canvas>();
        RectTransform arrowsRoot =
            existing.Find("Arrows") as RectTransform;
        Transform preview = existing.Find("HoverClashPreview");
        Image previewBackground = preview?.GetComponent<Image>();
        TMP_Text previewText =
            preview?.Find("Text")?.GetComponent<TMP_Text>();

        if (canvas == null ||
            arrowsRoot == null ||
            preview == null ||
            previewBackground == null ||
            previewText == null)
        {
            return false;
        }

        overlayCanvas = canvas;
        arrowRoot = arrowsRoot;
        hoverPreviewRoot = preview as RectTransform;
        hoverPreviewBackground = previewBackground;
        hoverPreviewText = previewText;
        return hoverPreviewRoot != null;
    }

    private void EnsureHoverPreview()
    {
        if (overlayCanvas == null)
            return;

        if (hoverPreviewRoot != null &&
            hoverPreviewBackground != null &&
            hoverPreviewText != null)
        {
            return;
        }

        Transform existing =
            overlayCanvas.transform.Find(
                "HoverClashPreview");

        if (existing != null)
        {
            hoverPreviewRoot = existing as RectTransform;
            hoverPreviewBackground = existing.GetComponent<Image>();
            hoverPreviewText =
                existing.Find("Text")?.GetComponent<TMP_Text>();

            if (hoverPreviewRoot != null &&
                hoverPreviewBackground != null &&
                hoverPreviewText != null)
            {
                return;
            }
        }

        if (Application.isPlaying)
            return;

        GameObject rootGo = new GameObject(
            "HoverClashPreview",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));

        rootGo.transform.SetParent(
            overlayCanvas.transform,
            false);

        hoverPreviewRoot =
            rootGo.GetComponent<RectTransform>();

        hoverPreviewRoot.anchorMin = Vector2.zero;
        hoverPreviewRoot.anchorMax = Vector2.zero;
        hoverPreviewRoot.pivot = new Vector2(0f, 1f);
        hoverPreviewRoot.sizeDelta = hoverPreviewSize;

        hoverPreviewBackground =
            rootGo.GetComponent<Image>();

        hoverPreviewBackground.color =
            new Color(
                0.025f,
                0.035f,
                0.055f,
                0.96f);

        hoverPreviewBackground.raycastTarget = false;

        Outline outline =
            rootGo.GetComponent<Outline>();

        outline.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.92f);

        outline.effectDistance =
            new Vector2(
                2f,
                -2f);

        GameObject textGo = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        textGo.transform.SetParent(
            rootGo.transform,
            false);

        RectTransform textRect =
            textGo.GetComponent<RectTransform>();

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 5f);
        textRect.offsetMax = new Vector2(-8f, -5f);

        hoverPreviewText =
            textGo.GetComponent<TMP_Text>();

        hoverPreviewText.alignment =
            TextAlignmentOptions.Center;
        hoverPreviewText.fontStyle = FontStyles.Bold;
        hoverPreviewText.fontSize = 19f;
        hoverPreviewText.enableAutoSizing = true;
        hoverPreviewText.fontSizeMin = 13f;
        hoverPreviewText.fontSizeMax = 19f;
        hoverPreviewText.textWrappingMode =
            TextWrappingModes.NoWrap;
        hoverPreviewText.raycastTarget = false;

        hoverPreviewRoot.SetAsLastSibling();
        SetHoverPreviewVisible(false);
    }

#if UNITY_EDITOR
    public void EditorAuthorSceneView()
    {
        EnsureOverlay();
    }
#endif

    private void RefreshArrows()
    {
        // 감정 증강 3택 Overlay가 화면을 덮는 동안에는 별도 Screen Space
        // 월드 슬롯 화살표 계층도 함께 꺼서 카드 위로 화살표가 관통하지 않게 한다.
        if (EmotionAugmentChoiceUI.IsAnyChoiceOverlayBlockingBattleArrows)
        {
            SetPlanningOverlayVisible(false);
            return;
        }

        // 이 클래스는 TargetArrowUI와 별개의 월드 슬롯 화살표 계층이다.
        // 캐릭터 상세 화면에서는 계획/합 화살표를 전부 강제로 숨긴다.
        if (screenModeController?.CurrentMode ==
            BattleUiScreenMode.CharacterDetails)
        {
            SetPlanningOverlayVisible(false);
            return;
        }

        SetPlanningOverlayVisible(true);

        if (arrowRoot == null ||
            uiManager == null ||
            battleManager?.ActionManager == null ||
            battleManager.BattleContext == null)
        {
            HideUnused(0);
            SetHoverPreviewVisible(false);
            return;
        }

        // 스킬 선택 Drawer가 화면을 덮고 있는 동안에는 World 화살표/프리뷰를 전부 숨긴다.
        // 닫힘 Tween 중에도 alpha가 남아 있으면 숨김 상태를 유지한다.
        if (skillSelectPanel != null &&
            skillSelectPanel.BlocksWorldPlanningOverlay)
        {
            HideUnused(0);
            SetHoverPreviewVisible(false);
            return;
        }

        Character player = battleManager.BattleContext.Player;
        IReadOnlyList<ActionSlot> slots = battleManager.ActionManager.Slots;

        if (player == null || slots == null)
        {
            HideUnused(0);
            SetHoverPreviewVisible(false);
            return;
        }

        int used = 0;

        bool hasPendingPlan =
            TryResolvePendingPlan(
                out BattleWorldActionSlotCellUI pendingSource,
                out Character pendingOwner,
                out BodyPart pendingPart,
                out Skill pendingSkill,
                out int pendingSpeed);

        BattleWorldActionSlotCellUI hoveredTarget =
            BattleWorldActionSlotCellUI.HoveredTargetCell;

        BattleWorldActionSlotCellUI hoveredCell =
            BattleWorldActionSlotCellUI.HoveredCell;

        ActionSlot focusedEnemySlot =
            hoveredTarget?.TargetSlot;

        bool hoverRelationFocus =
            hoveredCell != null;

        bool focusMode =
            hasPendingPlan ||
            focusedEnemySlot != null ||
            BattleSkillDragContext.HasPayload ||
            hoverRelationFocus;

        ActionSlot pendingHoverEnemy = null;

        bool hoverWouldClash =
            hasPendingPlan &&
            TryBuildPendingHoverClash(
                pendingSource,
                pendingOwner,
                pendingPart,
                pendingSpeed,
                pendingSkill,
                hoveredTarget,
                out _,
                out pendingHoverEnemy);

        hoverClashPlayerSlot =
            hoverWouldClash
                ? pendingSource?.PlannedSlot
                : null;

        hoverClashEnemySlot =
            hoverWouldClash
                ? pendingHoverEnemy
                : null;

        IReadOnlyList<ClashPair> previewPairs = BuildPreviewPairs(slots);
        BuildResolvedClashSlotSets(previewPairs, player);

        used = DrawNormalPlayerIntents(
            slots,
            player,
            used,
            focusMode,
            focusedEnemySlot,
            hoveredCell);

        used = DrawNormalEnemyIntents(
            slots,
            player,
            used,
            focusMode,
            focusedEnemySlot,
            hoveredCell);

        used = DrawPreviewClashes(
            previewPairs,
            player,
            used,
            focusMode,
            focusedEnemySlot,
            hoveredCell);

        // 클릭 전 Hover도 실제 TargetSlot을 놓았을 때와 같은 합/일방공격 규칙으로 보여준다.
        if (hasPendingPlan)
        {
            Vector2 from =
                pendingSource.GetScreenCenter(
                    targetCamera);

            Vector2 to =
                Input.mousePosition;

            if (hoveredTarget?.TargetSlot != null &&
                hoveredTarget.TargetSlot.Owner != player)
            {
                to =
                    hoveredTarget.GetScreenCenter(
                        targetCamera);
            }
            else
            {
                to.x =
                    Mathf.Clamp(
                        to.x,
                        0f,
                        Screen.width);

                to.y =
                    Mathf.Clamp(
                        to.y,
                        0f,
                        Screen.height);
            }

            float pulse =
                0.72f +
                0.28f *
                (0.5f +
                 0.5f *
                 Mathf.Sin(
                     Time.unscaledTime * 3.2f));

            Color color =
                pendingColor;

            color.a *= pulse;

            if (hoverWouldClash &&
                hoveredTarget != null)
            {
                // 합 가능: 클릭하기 전부터 양쪽 화살표가 중앙에서 충돌한다.
                Color clashColor =
                    clashArrowColor;

                clashColor.a *= pulse;

                used =
                    DrawTwoArrowsToCenter(
                        used,
                        from,
                        to,
                        clashColor,
                        clashColor);
            }
            else
            {
                // 합 불가: TargetSlot 지정은 가능하며 플레이어 쪽은 일방공격 화살표다.
                used =
                    DrawArrow(
                        used,
                        from,
                        to,
                        color,
                        pendingThickness);
            }

            RefreshHoverClashPreview(
                pendingOwner,
                pendingPart,
                pendingSpeed,
                pendingSkill);
        }
        else
        {
            hoverClashPlayerSlot = null;
            hoverClashEnemySlot = null;
            SetHoverPreviewVisible(false);
        }

        HideUnused(used);
    }

    private IReadOnlyList<ClashPair> BuildPreviewPairs(
        IReadOnlyList<ActionSlot> slots)
    {
        if (battleManager?.ClashBuilder == null || slots == null)
            return null;

        return battleManager.ClashBuilder.BuildClashPreview(slots);
    }

    private void BuildResolvedClashSlotSets(
        IReadOnlyList<ClashPair> pairs,
        Character player)
    {
        resolvedPlayerClashSlots.Clear();
        resolvedEnemyClashSlots.Clear();

        if (pairs == null || player == null)
            return;

        foreach (ClashPair pair in pairs)
        {
            if (pair == null ||
                !pair.IsClash ||
                pair.First == null ||
                pair.Second == null)
            {
                continue;
            }

            bool firstIsPlayer =
                pair.First.Owner == player;

            bool secondIsPlayer =
                pair.Second.Owner == player;

            if (firstIsPlayer == secondIsPlayer)
                continue;

            ActionSlot playerSlot =
                firstIsPlayer
                    ? pair.First
                    : pair.Second;

            ActionSlot enemySlot =
                firstIsPlayer
                    ? pair.Second
                    : pair.First;

            resolvedPlayerClashSlots.Add(playerSlot);
            resolvedEnemyClashSlots.Add(enemySlot);
        }
    }

    private int DrawNormalPlayerIntents(
        IReadOnlyList<ActionSlot> slots,
        Character player,
        int used,
        bool focusMode,
        ActionSlot focusedEnemySlot,
        BattleWorldActionSlotCellUI hoveredCell)
    {
        if (!showPlayerIntentArrows || slots == null)
            return used;

        foreach (ActionSlot source in slots)
        {
            if (source == null ||
                source.Owner != player ||
                source.Owner == null ||
                source.TargetCharacter == null ||
                source.TargetCharacter == player ||
                source.Phase != ActionPhase.COMBAT ||
                resolvedPlayerClashSlots.Contains(source) ||
                source == hoverClashPlayerSlot)
            {
                continue;
            }

            if (!BattleWorldActionSlotCellUI.TryGetPlanningCell(
                    source,
                    out BattleWorldActionSlotCellUI fromCell))
            {
                continue;
            }

            BattleWorldActionSlotCellUI toCell = null;

            // 사용자가 정확한 적 ActionSlot을 지정했다면 그 슬롯을 최우선으로 사용한다.
            if (source.TargetSlot != null)
            {
                BattleWorldActionSlotCellUI.TryGetTargetCell(
                    source.TargetSlot,
                    out toCell);
            }

            // AI/기존 계획처럼 TargetSlot이 비어 있는 경우 기존 TargetArrowUI처럼 TargetPart를 사용한다.
            if (toCell == null)
            {
                BattleWorldActionSlotCellUI.TryGetTargetCell(
                    source.TargetCharacter,
                    source.TargetPart,
                    out toCell);
            }

            if (toCell == null)
                continue;

            Vector2 from = fromCell.GetScreenCenter(targetCamera);
            Vector2 to = toCell.GetScreenCenter(targetCamera);
            Vector2 lane = GetActionIndexOffset(
                from,
                to,
                source.ActionIndex);

            bool hoverRelated =
                hoveredCell != null &&
                hoveredCell.IsTargetedBy(
                    source);

            float alphaMultiplier =
                ResolveArrowAlpha(
                    focusMode,
                    hoverRelated ||
                    source.TargetSlot == focusedEnemySlot,
                    idlePlayerArrowAlpha);

            used = DrawArrow(
                used,
                from + lane,
                to + lane,
                WithAlpha(
                    playerArrowColor,
                    alphaMultiplier),
                normalThickness);
        }

        return used;
    }

    private int DrawNormalEnemyIntents(
        IReadOnlyList<ActionSlot> slots,
        Character player,
        int used,
        bool focusMode,
        ActionSlot focusedEnemySlot,
        BattleWorldActionSlotCellUI hoveredCell)
    {
        if (!showEnemyIntentArrows || slots == null)
            return used;

        foreach (ActionSlot source in slots)
        {
            if (source == null ||
                source.Owner == null ||
                source.Owner == player ||
                source.TargetCharacter != player ||
                source.Phase != ActionPhase.COMBAT ||
                resolvedEnemyClashSlots.Contains(source) ||
                source == hoverClashEnemySlot)
            {
                continue;
            }

            if (!BattleWorldActionSlotCellUI.TryGetTargetCell(
                    source,
                    out BattleWorldActionSlotCellUI fromCell))
            {
                continue;
            }

            BattleWorldActionSlotCellUI toCell = null;

            if (source.TargetSlot != null &&
                source.TargetSlot.Owner == player)
            {
                BattleWorldActionSlotCellUI.TryGetPlanningCell(
                    source.TargetSlot,
                    out toCell);
            }

            if (toCell == null)
            {
                BattleWorldActionSlotCellUI.TryGetPlanningCell(
                    player,
                    source.TargetPart,
                    out toCell);
            }

            if (toCell == null)
                continue;

            Vector2 from = fromCell.GetScreenCenter(targetCamera);
            Vector2 to = toCell.GetScreenCenter(targetCamera);
            Vector2 lane = GetActionIndexOffset(
                from,
                to,
                source.ActionIndex);

            bool hoverRelated =
                hoveredCell != null &&
                hoveredCell.IsTargetedBy(
                    source);

            float alphaMultiplier =
                ResolveArrowAlpha(
                    focusMode,
                    hoverRelated ||
                    source == focusedEnemySlot,
                    idleEnemyArrowAlpha);

            used = DrawArrow(
                used,
                from + lane,
                to + lane,
                WithAlpha(
                    enemyArrowColor,
                    alphaMultiplier),
                normalThickness);
        }

        return used;
    }

    private int DrawPreviewClashes(
        IReadOnlyList<ClashPair> pairs,
        Character player,
        int used,
        bool focusMode,
        ActionSlot focusedEnemySlot,
        BattleWorldActionSlotCellUI hoveredCell)
    {
        if (pairs == null || player == null)
            return used;

        foreach (ClashPair pair in pairs)
        {
            if (pair == null ||
                !pair.IsClash ||
                pair.First == null ||
                pair.Second == null)
            {
                continue;
            }

            bool firstIsPlayer =
                pair.First.Owner == player;

            bool secondIsPlayer =
                pair.Second.Owner == player;

            if (firstIsPlayer == secondIsPlayer)
                continue;

            if (pair.First == hoverClashPlayerSlot ||
                pair.Second == hoverClashPlayerSlot ||
                pair.First == hoverClashEnemySlot ||
                pair.Second == hoverClashEnemySlot)
            {
                continue;
            }

            ActionSlot playerSlot =
                firstIsPlayer
                    ? pair.First
                    : pair.Second;

            ActionSlot enemySlot =
                firstIsPlayer
                    ? pair.Second
                    : pair.First;

            if (!BattleWorldActionSlotCellUI.TryGetPlanningCell(
                    playerSlot,
                    out BattleWorldActionSlotCellUI playerCell) ||
                !BattleWorldActionSlotCellUI.TryGetTargetCell(
                    enemySlot,
                    out BattleWorldActionSlotCellUI enemyCell))
            {
                continue;
            }

            Vector2 playerStart =
                playerCell.GetScreenCenter(targetCamera);

            Vector2 enemyStart =
                enemyCell.GetScreenCenter(targetCamera);

            Vector2 originalPlayerStart = playerStart;
            Vector2 originalEnemyStart = enemyStart;

            playerStart += GetActionIndexOffset(
                originalPlayerStart,
                originalEnemyStart,
                playerSlot.ActionIndex);

            enemyStart += GetActionIndexOffset(
                originalEnemyStart,
                originalPlayerStart,
                enemySlot.ActionIndex);

            bool hoverRelated =
                hoveredCell != null &&
                (hoveredCell.IsTargetedBy(
                     playerSlot) ||
                 hoveredCell.IsTargetedBy(
                     enemySlot));

            float alphaMultiplier =
                ResolveArrowAlpha(
                    focusMode,
                    hoverRelated ||
                    enemySlot == focusedEnemySlot,
                    idleClashArrowAlpha);

            Color clashColor =
                WithAlpha(
                    clashArrowColor,
                    alphaMultiplier);

            used = DrawTwoArrowsToCenter(
                used,
                playerStart,
                enemyStart,
                clashColor,
                clashColor);
        }

        return used;
    }

    private bool TryResolvePendingPlan(
        out BattleWorldActionSlotCellUI cell,
        out Character owner,
        out BodyPart part,
        out Skill skill,
        out int speed)
    {
        cell = null;
        owner = null;
        part = null;
        skill = null;
        speed = 0;

        if (BattleSkillDragContext.HasSlotPayload)
        {
            ActionSlot slot =
                BattleSkillDragContext.PlannedSlot;

            if (slot == null ||
                slot.Skill == null)
            {
                return false;
            }

            owner = slot.Owner;
            part = slot.Part;
            skill = slot.Skill;
            speed = slot.Speed;

            return BattleWorldActionSlotCellUI.TryGetPlanningCell(
                slot,
                out cell);
        }

        if (BattleSkillDragContext.HasSkillPayload &&
            uiManager.SelectedOwner != null &&
            uiManager.SelectedOwnerPart != null)
        {
            owner = uiManager.SelectedOwner;
            part = uiManager.SelectedOwnerPart;
            skill = BattleSkillDragContext.Skill;
            speed =
                battleManager?.SpeedManager?.GetSpeed(
                    owner,
                    part) ?? 0;

            return BattleWorldActionSlotCellUI.TryGetPlanningCell(
                owner,
                part,
                BattleSkillDragContext.ActionIndex,
                out cell);
        }

        if (!uiManager.IsSelectingTarget ||
            uiManager.Selection?.Skill == null ||
            uiManager.SelectedOwner == null ||
            uiManager.SelectedOwnerPart == null)
        {
            return false;
        }

        owner = uiManager.SelectedOwner;
        part = uiManager.SelectedOwnerPart;
        skill = uiManager.Selection.Skill;
        speed =
            battleManager?.SpeedManager?.GetSpeed(
                owner,
                part) ?? 0;

        return BattleWorldActionSlotCellUI.TryGetPlanningCell(
            owner,
            part,
            uiManager.SelectedActionIndex,
            out cell);
    }

    private bool TryBuildPendingHoverClash(
        BattleWorldActionSlotCellUI sourceCell,
        Character owner,
        BodyPart part,
        int speed,
        Skill skill,
        BattleWorldActionSlotCellUI hovered,
        out ActionSlot challenger,
        out ActionSlot incoming)
    {
        challenger = null;
        incoming = hovered?.TargetSlot;

        if (sourceCell == null ||
            owner == null ||
            skill == null ||
            incoming == null ||
            incoming.Owner == null ||
            incoming.Owner == owner ||
            skill.DefaultPhase != ActionPhase.COMBAT)
        {
            return false;
        }

        challenger =
            new ActionSlot
            {
                ActionId = -1,
                Owner = owner,
                Part = part,
                Skill = skill,
                Speed = speed,
                ActionIndex =
                    uiManager?.SelectedActionIndex ?? 0,
                Phase = ActionPhase.COMBAT,
                TargetCharacter = incoming.Owner,
                TargetPart = incoming.Part,
                TargetSlot = incoming
            };

        ClashMatchPolicy policy =
            new ClashMatchPolicy(
                new ActionPhaseSorter());

        return policy.CanChallenge(
            challenger,
            incoming);
    }

    private void RefreshHoverClashPreview(
        Character owner,
        BodyPart part,
        int speed,
        Skill skill)
    {
        BattleWorldActionSlotCellUI hovered =
            BattleWorldActionSlotCellUI.HoveredTargetCell;

        ActionSlot targetSlot =
            hovered?.TargetSlot;

        if (owner == null ||
            skill == null ||
            targetSlot == null ||
            battleManager?.BattleContext == null)
        {
            SetHoverPreviewVisible(false);
            return;
        }

        bool canClash =
            TryBuildPendingHoverClash(
                BattleWorldActionSlotCellUI
                    .TryGetPlanningCell(
                        owner,
                        part,
                        uiManager?.SelectedActionIndex ?? 0,
                        out BattleWorldActionSlotCellUI sourceCell)
                    ? sourceCell
                    : null,
                owner,
                part,
                speed,
                skill,
                hovered,
                out _,
                out _);

        // TargetSlot 지정은 항상 허용하되, 정확히 이 슬롯과 합이 성립하는지만 표시한다.
        if (!canClash)
        {
            string enemyTarget =
                targetSlot.TargetPart != null
                    ? GetPartLabel(
                        targetSlot.TargetPart.Type)
                    : "대상";

            string speedDetail =
                speed <= targetSlot.Speed
                    ? $"속도 {speed} ≤ {targetSlot.Speed} · 적 공격은 {enemyTarget} 유지"
                    : $"속도 {speed} / {targetSlot.Speed}";

            ShowHoverPreview(
                "일방공격",
                speedDetail,
                OneSidedPreviewColor());

            return;
        }

        if (!clashEstimator.TryEstimateClashWinRate(
                battleManager.BattleContext,
                owner,
                part,
                speed,
                skill,
                targetSlot,
                out float winRate))
        {
            ShowHoverPreview(
                "합",
                $"속도 {speed} / {targetSlot.Speed}",
                clashArrowColor);

            return;
        }

        string label =
            GetAdvantageLabel(
                winRate);

        Color color =
            GetAdvantageColor(
                winRate);

        ShowHoverPreview(
            $"합 · {label}",
            $"예상 승률 {winRate * 100f:0}% · 속도 {speed} / {targetSlot.Speed}",
            color);
    }

    private void ShowHoverPreview(
        string headline,
        string detail,
        Color color)
    {
        EnsureHoverPreview();

        if (hoverPreviewRoot == null ||
            hoverPreviewText == null)
        {
            return;
        }

        hoverPreviewText.color = color;
        hoverPreviewText.text =
            string.IsNullOrWhiteSpace(detail)
                ? headline
                : $"{headline}\n{detail}";

        Vector2 size =
            hoverPreviewSize;

        hoverPreviewRoot.sizeDelta =
            size;

        Vector2 position =
            (Vector2)Input.mousePosition +
            hoverPreviewMouseOffset;

        float minimumX = 8f;
        float maximumX =
            Mathf.Max(
                minimumX,
                Screen.width - size.x - 8f);

        float minimumY =
            size.y + 8f;

        float maximumY =
            Mathf.Max(
                minimumY,
                Screen.height - 8f);

        position.x =
            Mathf.Clamp(
                position.x,
                minimumX,
                maximumX);

        position.y =
            Mathf.Clamp(
                position.y,
                minimumY,
                maximumY);

        hoverPreviewRoot.anchoredPosition =
            position;

        SetHoverPreviewVisible(true);
    }

    private void SetHoverPreviewVisible(
        bool visible)
    {
        if (hoverPreviewRoot == null)
            return;

        if (hoverPreviewRoot.gameObject.activeSelf != visible)
        {
            hoverPreviewRoot.gameObject.SetActive(
                visible);
        }
    }

    private float ResolveArrowAlpha(
        bool focusMode,
        bool focused,
        float idleAlpha)
    {
        if (focusMode)
        {
            return focused
                ? 1f
                : unrelatedFocusAlpha;
        }

        if (!deEmphasizeIdleArrows)
            return 1f;

        return idleAlpha;
    }

    private static Color WithAlpha(
        Color color,
        float multiplier)
    {
        color.a *=
            Mathf.Clamp01(
                multiplier);

        return color;
    }

    private Color OneSidedPreviewColor()
    {
        Color color =
            playerArrowColor;

        color.a = 1f;
        return color;
    }

    private static string GetPartLabel(
        PartType type)
    {
        return type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼팔",
            PartType.RIGHT_HAND => "오른팔",
            PartType.LEGS => "다리",
            _ => "대상"
        };
    }

    private string GetAdvantageLabel(
        float winRate)
    {
        if (winRate < 0.25f)
            return "매우 불리";

        if (winRate < 0.45f)
            return "불리";

        if (winRate < 0.55f)
            return "균형";

        if (winRate < 0.75f)
            return "우세";

        return "매우 우세";
    }

    private Color GetAdvantageColor(
        float winRate)
    {
        if (winRate < 0.25f)
            return veryUnfavorableColor;

        if (winRate < 0.45f)
            return unfavorableColor;

        if (winRate < 0.55f)
            return balancedColor;

        if (winRate < 0.75f)
            return favorableColor;

        return veryFavorableColor;
    }

    private Vector2 GetActionIndexOffset(
        Vector2 start,
        Vector2 end,
        int actionIndex)
    {
        if (actionIndex <= 0 || actionLaneSpacing <= 0f)
            return Vector2.zero;

        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.01f)
            return Vector2.zero;

        direction.Normalize();

        Vector2 perpendicular =
            new(-direction.y, direction.x);

        int rank = (actionIndex + 1) / 2;
        float side =
            actionIndex % 2 == 1
                ? 1f
                : -1f;

        return perpendicular * rank * actionLaneSpacing * side;
    }

    private int DrawTwoArrowsToCenter(
        int index,
        Vector2 firstStart,
        Vector2 secondStart,
        Color firstColor,
        Color secondColor)
    {
        Vector2 center =
            (firstStart + secondStart) * 0.5f;

        Vector2 firstDir =
            center - firstStart;

        Vector2 secondDir =
            center - secondStart;

        if (firstDir.sqrMagnitude <= 0.01f ||
            secondDir.sqrMagnitude <= 0.01f)
        {
            return index;
        }

        firstDir.Normalize();
        secondDir.Normalize();

        Vector2 firstEnd =
            center - firstDir * clashCenterGap;

        Vector2 secondEnd =
            center - secondDir * clashCenterGap;

        index = DrawArrow(
            index,
            firstStart,
            firstEnd,
            firstColor,
            normalThickness);

        index = DrawArrow(
            index,
            secondStart,
            secondEnd,
            secondColor,
            normalThickness);

        return index;
    }

    private int DrawArrow(
        int index,
        Vector2 from,
        Vector2 to,
        Color color,
        float thickness)
    {
        ArrowVisual visual = GetArrow(index);
        index++;

        Vector2 delta = to - from;
        float distance = delta.magnitude;

        if (distance < 3f)
        {
            visual.SetActive(false);
            return index;
        }

        Vector2 direction = delta / distance;
        float padding =
            Mathf.Min(
                endpointPadding,
                distance * 0.25f);

        Vector2 paddedFrom =
            from + direction * padding;

        Vector2 paddedTo =
            to - direction * padding;

        Vector2 bodyDelta =
            paddedTo - paddedFrom;

        float bodyLength = bodyDelta.magnitude;

        if (bodyLength < 2f)
        {
            visual.SetActive(false);
            return index;
        }

        float angle =
            Mathf.Atan2(
                bodyDelta.y,
                bodyDelta.x) *
            Mathf.Rad2Deg;

        visual.SetActive(true);
        visual.SetColor(color);

        RectTransform body = visual.Body.rectTransform;
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.zero;
        body.pivot = new Vector2(0f, 0.5f);
        body.anchoredPosition = paddedFrom;
        body.sizeDelta = new Vector2(bodyLength, thickness);
        body.localRotation = Quaternion.Euler(0f, 0f, angle);

        ConfigureHead(
            visual.HeadLeft.rectTransform,
            paddedTo,
            angle + 180f - arrowHeadAngle,
            thickness);

        ConfigureHead(
            visual.HeadRight.rectTransform,
            paddedTo,
            angle + 180f + arrowHeadAngle,
            thickness);

        return index;
    }

    private void ConfigureHead(
        RectTransform head,
        Vector2 point,
        float angle,
        float thickness)
    {
        if (head == null)
            return;

        head.anchorMin = Vector2.zero;
        head.anchorMax = Vector2.zero;
        head.pivot = new Vector2(0f, 0.5f);
        head.anchoredPosition = point;
        head.sizeDelta = new Vector2(
            arrowHeadLength,
            Mathf.Max(thickness, 4f));
        head.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private ArrowVisual GetArrow(int index)
    {
        while (arrows.Count <= index)
            arrows.Add(CreateArrow(arrows.Count));

        return arrows[index];
    }

    private ArrowVisual CreateArrow(int index)
    {
        GameObject root = new GameObject(
            $"Arrow_{index}",
            typeof(RectTransform));

        root.transform.SetParent(
            arrowRoot,
            false);

        RectTransform rootRect =
            root.GetComponent<RectTransform>();

        Stretch(rootRect);

        Image body =
            CreateImage(
                "Body",
                root.transform);

        Image headLeft =
            CreateImage(
                "HeadLeft",
                root.transform);

        Image headRight =
            CreateImage(
                "HeadRight",
                root.transform);

        ArrowVisual visual =
            new()
            {
                Root = rootRect,
                Body = body,
                HeadLeft = headLeft,
                HeadRight = headRight
            };

        visual.SetActive(false);
        return visual;
    }

    private static Image CreateImage(
        string name,
        Transform parent)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        go.transform.SetParent(
            parent,
            false);

        Image image =
            go.GetComponent<Image>();

        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private void HideUnused(int used)
    {
        for (int i = used; i < arrows.Count; i++)
            arrows[i]?.SetActive(false);
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}