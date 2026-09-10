using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Planning 전용 Screen-space 행동 순서 보조 레일.
///
/// 핵심 원칙:
/// - 대상 지정은 여전히 캐릭터 머리 위 World ActionSlot에서 한다.
/// - 이 레일은 3D Perspective에서 비교하기 어려운 "속도/행동 순서"만 보조한다.
/// - 실제 합 여부는 ClashBuilder Preview 결과를 그대로 사용한다.
/// - Raycast를 받지 않아 기존 월드 슬롯 클릭을 절대 가로막지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleActionOrderRailUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private SkillSelectPanelUI skillSelectPanel;

    [Header("Editor Authoring Seed (Play Mode does not overwrite RectTransform)")]
    [SerializeField, Min(320f)] private float width = 420f;
    [SerializeField, Min(420f)] private float height = 680f;
    [SerializeField] private Vector2 topLeftOffset = new(18f, -144f);
    [SerializeField, Range(4, 20)] private int maximumRows = 10;

    [Header("Maximize / Minimize")]
    [SerializeField] private bool startCollapsed;
    [SerializeField, Min(44f)] private float collapsedHeight = 58f;

    [Header("Visual")]
    [SerializeField] private Color panelColor =
        new(0.025f, 0.035f, 0.055f, 0.78f);

    [SerializeField] private Color playerAccent =
        new(0.18f, 0.52f, 1f, 0.95f);

    [SerializeField] private Color enemyAccent =
        new(1f, 0.18f, 0.18f, 0.95f);

    [SerializeField] private Color clashAccent =
        new(1f, 0.82f, 0.12f, 1f);

    [SerializeField] private Color selectedBackground =
        new(0.12f, 0.30f, 0.52f, 0.82f);

    [SerializeField] private Color hoveredTargetBackground =
        new(0.44f, 0.28f, 0.06f, 0.86f);

    [SerializeField] private Color hoveredPlayerBackground =
        new(0.78f, 0.58f, 0.04f, 0.96f);

    [SerializeField] private Color reactiveRollAccent =
        new(0.78f, 0.34f, 1f, 1f);

    [SerializeField] private Color reactiveRollBackground =
        new(0.22f, 0.08f, 0.34f, 0.94f);

    [Header("Resolution")]
    [SerializeField, Min(0.08f)] private float completedRowFadeDuration = 0.22f;
    [SerializeField, Min(0f)] private float completedRowSlideDistance = 34f;
    [SerializeField, Min(0f)] private float completedRowStaggerDelay = 0.06f;
    [SerializeField, Min(0.02f)] private float missingVisualFallbackDelay = 0.08f;
    [SerializeField, Min(0.05f)] private float reactiveRowAppearDuration = 0.18f;
    [SerializeField, Min(0.15f)] private float reactiveRowMinimumVisibleDuration = 0.90f;

    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform panelRoot;
    private RectTransform contentRoot;
    private TMP_Text headerText;
    private Button sizeToggleButton;
    private TMP_Text sizeToggleLabel;
    private readonly List<GameObject> generatedRows = new();
    private readonly Dictionary<long, GameObject> rowsByActionId = new();
    private readonly Dictionary<long, GameObject> reactiveRowsByEventId = new();
    private readonly Dictionary<long, float> reactiveRowStartedAt = new();
    private readonly HashSet<long> completedResolutionActions = new();
    private readonly HashSet<long> domainCompletedResolutionActions = new();
    private readonly ActionPhaseSorter sorter = new();
    private readonly Dictionary<ActionSlot, ActionSlot> clashPartnerBySlot = new();
    private readonly HashSet<GameObject> completedResolutionRows = new();

    private BattleEvent boundBattleEvent;
    private BattleAnimationDirector animationDirector;
    private BattleAnimationDirector boundAnimationDirector;

    private bool wasResolving;
    private int resolutionRemainingActionCount;
    private int lastSignature = int.MinValue;
    private bool missingSceneViewLogged;
    private bool collapseStateInitialized;
    private bool isCollapsed;
    private float expandedSizeDeltaY;

    public bool IsCollapsed => isCollapsed;

    public void Configure(
        BattleManager manager,
        BattleUIManager managerUi,
        Camera _)
    {
        battleManager = manager;
        uiManager = managerUi;
        ResolveReferences();
        EnsureView();
        InitializeCollapseStateIfNeeded();
        EnsureBattleEventBinding();
        EnsureAnimationDirectorBinding();
        Rebuild(force: true);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureView();
        InitializeCollapseStateIfNeeded();
        EnsureBattleEventBinding();
        EnsureAnimationDirectorBinding();
    }

    private void OnDestroy()
    {
        DOTween.Kill(
            this,
            complete: false);

        UnbindBattleEvent();
        UnbindAnimationDirector();
        UnbindSizeToggle();
        ClearRows();

        // Scene-authored Overlay는 이 컴포넌트가 소유/생성한 런타임 임시 객체가 아니다.
        // Scene unload가 수명을 관리하므로 여기서 Destroy하지 않는다.
    }

    /// <summary>
    /// 행동 순서 레일은 항상 독립 Root Screen-space Overlay Canvas로 유지한다.
    /// 따라서 Resolution에서 전투 UI Root 전체가 꺼져도 레일은 영향을 받지 않는다.
    /// </summary>
    public void PreserveForResolution(
        Transform _)
    {
        ResolveReferences();
        EnsureView();
        EnsureBattleEventBinding();
        EnsureAnimationDirectorBinding();
        EnterResolutionIfNeeded();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        EnsureOverlayIsRootCanvas();

        if (overlayCanvas != null)
            overlayCanvas.sortingOrder = 80;
    }

    /// <summary>
    /// Resolution 종료 후 일반 Planning 정렬 순서로 되돌린다.
    /// </summary>
    public void RestoreAfterResolution()
    {
        EnsureOverlayIsRootCanvas();

        if (overlayCanvas != null)
            overlayCanvas.sortingOrder = 18;
    }

    private void LateUpdate()
    {
        ResolveReferences();
        EnsureView();
        InitializeCollapseStateIfNeeded();

        if (overlayCanvas == null ||
            canvasGroup == null)
        {
            return;
        }

        EnsureBattleEventBinding();
        EnsureAnimationDirectorBinding();

        bool resolving =
            battleManager?.TurnManager?.IsResolving == true;

        bool hidden =
            battleManager == null ||
            battleManager.ActionManager == null ||
            battleManager.BattleContext == null ||
            (!resolving &&
             skillSelectPanel != null &&
             skillSelectPanel.BlocksWorldPlanningOverlay);

        canvasGroup.alpha =
            hidden
                ? 0f
                : 1f;
        canvasGroup.interactable = !hidden;
        canvasGroup.blocksRaycasts = !hidden;

        if (hidden)
            return;

        if (resolving)
        {
            EnterResolutionIfNeeded();

            // Domain OnActionEnd는 합의 경우 연출 전에 발생한다.
            // 실제 Timeline이 재생 중이면 VisualRequestCompleted를 기다리고,
            // Visual이 없는 행동만 fallback으로 즉시 완료 처리한다.
            if (animationDirector?.IsPlaying != true &&
                domainCompletedResolutionActions.Count > 0)
            {
                FlushDomainCompletedFallback();
            }

            return;
        }

        if (wasResolving)
        {
            wasResolving = false;
            completedResolutionActions.Clear();
            domainCompletedResolutionActions.Clear();
            completedResolutionRows.Clear();
            resolutionRemainingActionCount = 0;
            Rebuild(force: true);
            return;
        }

        Rebuild(force: false);
    }

    private void ResolveReferences()
    {
        battleManager ??=
            FindFirstObjectByType<BattleManager>();

        uiManager ??=
            FindFirstObjectByType<BattleUIManager>(
                FindObjectsInactive.Include);

        if (skillSelectPanel == null)
        {
            skillSelectPanel =
                FindFirstObjectByType<SkillSelectPanelUI>(
                    FindObjectsInactive.Include);
        }

        if (animationDirector == null)
        {
            animationDirector =
                FindFirstObjectByType<BattleAnimationDirector>(
                    FindObjectsInactive.Include);
        }
    }

    private void EnsureBattleEventBinding()
    {
        BattleEvent current =
            battleManager?.BattleContext?._battleEvent;

        if (ReferenceEquals(
                current,
                boundBattleEvent))
        {
            return;
        }

        UnbindBattleEvent();
        boundBattleEvent = current;

        if (boundBattleEvent != null)
        {
            boundBattleEvent.OnActionEnd += HandleActionEnd;
            boundBattleEvent.OnReactiveRollStarted += HandleReactiveRollStarted;
            boundBattleEvent.OnReactiveRollResolved += HandleReactiveRollResolved;
        }
    }

    private void UnbindBattleEvent()
    {
        if (boundBattleEvent != null)
        {
            boundBattleEvent.OnActionEnd -= HandleActionEnd;
            boundBattleEvent.OnReactiveRollStarted -= HandleReactiveRollStarted;
            boundBattleEvent.OnReactiveRollResolved -= HandleReactiveRollResolved;
        }

        boundBattleEvent = null;
    }

    private void EnsureAnimationDirectorBinding()
    {
        BattleAnimationDirector current =
            animationDirector;

        if (ReferenceEquals(
                current,
                boundAnimationDirector))
        {
            return;
        }

        UnbindAnimationDirector();
        boundAnimationDirector = current;

        if (boundAnimationDirector != null)
        {
            boundAnimationDirector.VisualRequestCompleted +=
                HandleVisualRequestCompleted;
        }
    }

    private void UnbindAnimationDirector()
    {
        if (boundAnimationDirector != null)
        {
            boundAnimationDirector.VisualRequestCompleted -=
                HandleVisualRequestCompleted;
        }

        boundAnimationDirector = null;
    }

    private void EnterResolutionIfNeeded()
    {
        if (wasResolving)
            return;

        wasResolving = true;
        completedResolutionActions.Clear();
        domainCompletedResolutionActions.Clear();
        completedResolutionRows.Clear();

        // BattleManager는 TurnManager.IsResolving=true가 되기 직전에
        // Resolution UI를 먼저 연다. 그 프레임에도 정확한 남은 행동 수를 보존한다.
        resolutionRemainingActionCount =
            CountCurrentActionSlots();

        Rebuild(force: true);
        UpdateHeaderCount(
            resolutionRemainingActionCount);
    }

    private int CountCurrentActionSlots()
    {
        IReadOnlyList<ActionSlot> slots =
            battleManager?.ActionManager?.Slots;

        if (slots == null)
            return 0;

        int count = 0;
        int combatFallbackCount = 0;

        foreach (ActionSlot slot in slots)
        {
            if (slot == null ||
                !IsVisibleActionPhase(slot.Phase))
            {
                continue;
            }

            if (slot.Phase == ActionPhase.COMBAT)
                combatFallbackCount++;
            else
                count++;
        }

        IReadOnlyList<ClashPair> combatPairs =
            battleManager?.ClashBuilder
                ?.BuildClashPreview(slots);

        // COMBAT는 실제 실행 큐와 같은 ClashPair 단위로 센다.
        // 합은 두 ActionSlot이더라도 전투 순서상 하나의 실행 단위다.
        count +=
            combatPairs != null
                ? combatPairs.Count
                : combatFallbackCount;

        return count;
    }

    private static bool IsVisibleActionPhase(
        ActionPhase? phase)
    {
        return phase == ActionPhase.PRETURN ||
               phase == ActionPhase.FORESIGHT ||
               phase == ActionPhase.COMBAT;
    }

    private void HandleReactiveRollStarted(
        BattleReactiveRollEvent reactiveRoll)
    {
        if (reactiveRoll == null ||
            reactiveRoll.EventId <= 0 ||
            battleManager?.TurnManager?.IsResolving != true)
        {
            return;
        }

        EnterResolutionIfNeeded();

        if (reactiveRowsByEventId.ContainsKey(
                reactiveRoll.EventId))
        {
            return;
        }

        CreateReactiveRollRow(
            reactiveRoll);

        reactiveRowStartedAt[reactiveRoll.EventId] =
            Time.unscaledTime;

        resolutionRemainingActionCount++;
        UpdateHeaderCount(
            resolutionRemainingActionCount);
    }

    private void HandleReactiveRollResolved(
        BattleReactiveRollEvent reactiveRoll)
    {
        if (reactiveRoll == null ||
            reactiveRoll.EventId <= 0 ||
            !reactiveRowsByEventId.ContainsKey(reactiveRoll.EventId))
        {
            return;
        }

        float startedAt =
            reactiveRowStartedAt.TryGetValue(
                reactiveRoll.EventId,
                out float value)
                ? value
                : Time.unscaledTime;

        float elapsed =
            Mathf.Max(
                0f,
                Time.unscaledTime - startedAt);

        float delay =
            Mathf.Max(
                0f,
                reactiveRowMinimumVisibleDuration - elapsed);

        DOVirtual.DelayedCall(
                delay,
                () => CompleteReactiveRollRow(
                    reactiveRoll.EventId),
                ignoreTimeScale: true)
            .SetTarget(this);
    }

    private void HandleActionEnd(
        BattleAction action)
    {
        if (action == null ||
            action.ActionId <= 0 ||
            battleManager?.TurnManager?.IsResolving != true)
        {
            return;
        }

        EnterResolutionIfNeeded();

        // 합의 Domain ActionEnd는 전투 결과 확정 직후, Timeline 재생 전에 발생한다.
        // 여기서는 완료 후보만 기록하고 실제 UI 제거는 Presentation 완료 이벤트에 맡긴다.
        domainCompletedResolutionActions.Add(
            action.ActionId);

        ScheduleDomainCompletionFallback(
            action.ActionId);
    }

    private void ScheduleDomainCompletionFallback(
        long actionId)
    {
        if (actionId <= 0)
            return;

        DOVirtual.DelayedCall(
                Mathf.Max(
                    0.02f,
                    missingVisualFallbackDelay),
                () =>
                {
                    if (!domainCompletedResolutionActions.Contains(actionId) ||
                        battleManager?.TurnManager?.IsResolving != true)
                    {
                        return;
                    }

                    // 정상 Timeline이 시작되었다면 Presentation 완료 이벤트가
                    // 정확한 제거 시점을 알려주므로 여기서는 기다린다.
                    if (animationDirector?.IsPlaying == true)
                        return;

                    // VisualDefinition 누락/재생 불가 같은 경우에도
                    // 행동 자체는 끝났으므로 레일이 영원히 남지 않게 한다.
                    CompleteResolutionAction(
                        actionId,
                        0f);
                },
                ignoreTimeScale: true)
            .SetTarget(this);
    }

    private void HandleVisualRequestCompleted(
        BattleVisualRequest request)
    {
        if (request == null ||
            battleManager?.TurnManager?.IsResolving != true)
        {
            return;
        }

        EnterResolutionIfNeeded();

        List<long> completed =
            new List<long>(2);

        AddVisualCompletedAction(
            completed,
            request.SourceAction);

        AddVisualCompletedAction(
            completed,
            request.OpponentAction);

        // 합의 두 ActionId는 같은 UI Row를 공유한다.
        // 같은 VisualRequest에서 두 행동이 함께 완료되어도 Row는 한 번만 제거한다.
        completed.Sort(
            (left, right) =>
                GetRowSiblingIndex(left)
                    .CompareTo(
                        GetRowSiblingIndex(right)));

        for (int index = 0;
             index < completed.Count;
             index++)
        {
            CompleteResolutionAction(
                completed[index],
                index * completedRowStaggerDelay);
        }
    }

    private void AddVisualCompletedAction(
        List<long> destination,
        BattleAction action)
    {
        if (destination == null ||
            action == null ||
            action.ActionId <= 0 ||
            !rowsByActionId.TryGetValue(
                action.ActionId,
                out GameObject row) ||
            row == null)
        {
            return;
        }

        // 이미 같은 실행 단위(Row)가 들어 있다면 합 상대 ActionId는 중복 추가하지 않는다.
        foreach (long existingActionId in destination)
        {
            if (rowsByActionId.TryGetValue(
                    existingActionId,
                    out GameObject existingRow) &&
                existingRow == row)
            {
                return;
            }
        }

        destination.Add(
            action.ActionId);
    }

    private int GetRowSiblingIndex(
        long actionId)
    {
        if (rowsByActionId.TryGetValue(
                actionId,
                out GameObject row) &&
            row != null)
        {
            return row.transform.GetSiblingIndex();
        }

        return int.MaxValue;
    }

    private void FlushDomainCompletedFallback()
    {
        if (domainCompletedResolutionActions.Count == 0)
            return;

        List<long> pending =
            new List<long>(
                domainCompletedResolutionActions);

        foreach (long actionId in pending)
            CompleteResolutionAction(actionId, 0f);
    }

    private void CompleteResolutionAction(
        BattleAction action)
    {
        if (action == null)
            return;

        CompleteResolutionAction(
            action.ActionId,
            0f);
    }

    private void CompleteResolutionAction(
        long actionId,
        float delay)
    {
        if (actionId <= 0)
            return;

        domainCompletedResolutionActions.Remove(
            actionId);

        if (!rowsByActionId.TryGetValue(
                actionId,
                out GameObject row) ||
            row == null ||
            !completedResolutionRows.Add(row))
        {
            return;
        }

        // 합 Row는 두 ActionId가 같은 GameObject를 가리킨다.
        // 한쪽 행동이 완료되면 같은 실행 단위에 속한 모든 ActionId를 함께 완료 처리한다.
        List<long> groupedActionIds =
            new List<long>(2);

        foreach (KeyValuePair<long, GameObject> pair
                 in rowsByActionId)
        {
            if (pair.Value == row)
                groupedActionIds.Add(pair.Key);
        }

        foreach (long groupedActionId in groupedActionIds)
        {
            domainCompletedResolutionActions.Remove(
                groupedActionId);
            completedResolutionActions.Add(
                groupedActionId);
            rowsByActionId.Remove(
                groupedActionId);
        }

        // Header count는 ActionSlot 수가 아니라 실제 실행 단위(Row) 수다.
        resolutionRemainingActionCount =
            Mathf.Max(
                0,
                resolutionRemainingActionCount - 1);

        UpdateHeaderCount(
            resolutionRemainingActionCount);

        AnimateCompletedRow(
            row,
            delay);
    }

    private void AnimateCompletedRow(
        GameObject row,
        float delay)
    {
        if (row == null)
            return;

        CanvasGroup group =
            row.GetComponent<CanvasGroup>();

        RectTransform rect =
            row.transform as RectTransform;

        LayoutElement element =
            row.GetComponent<LayoutElement>();

        if (group == null ||
            rect == null ||
            element == null)
        {
            RemoveGeneratedRow(row);
            return;
        }

        DOTween.Kill(
            row,
            complete: false);

        element.minHeight = 0f;

        float startHeight =
            Mathf.Max(
                0f,
                element.preferredHeight);

        Sequence sequence =
            DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(row)
                .SetDelay(
                    Mathf.Max(0f, delay));

        sequence.Join(
            group.DOFade(
                0f,
                completedRowFadeDuration));

        sequence.Join(
            rect.DOAnchorPosX(
                    rect.anchoredPosition.x -
                    completedRowSlideDistance,
                    completedRowFadeDuration)
                .SetEase(Ease.InCubic));

        sequence.Join(
            rect.DOScale(
                    0.90f,
                    completedRowFadeDuration)
                .SetEase(Ease.InBack));

        sequence.Join(
            DOTween.To(
                    () => element.preferredHeight,
                    value => element.preferredHeight = value,
                    0f,
                    completedRowFadeDuration)
                .From(startHeight)
                .SetEase(Ease.InCubic));

        sequence.OnComplete(
            () => RemoveGeneratedRow(row));
    }

    private void RemoveGeneratedRow(
        GameObject row)
    {
        if (row == null)
            return;

        DOTween.Kill(
            row,
            complete: false);

        generatedRows.Remove(
            row);

        row.SetActive(false);

        if (Application.isPlaying)
            Destroy(row);
        else
            DestroyImmediate(row);
    }

    private void UpdateHeaderCount(
        int count)
    {
        if (headerText == null)
            return;

        headerText.text =
            count > 0
                ? $"행동 순서  <size=75%>{count}</size>"
                : "행동 순서";
    }

    private void EnsureView()
    {
        if (overlayCanvas != null &&
            panelRoot != null &&
            contentRoot != null)
        {
            return;
        }

        if (TryResolveSceneView())
        {
            missingSceneViewLogged = false;
            return;
        }

        // Play Mode에서는 고정 HUD Canvas/Panel을 생성하거나 배치하지 않는다.
        if (Application.isPlaying)
        {
            if (!missingSceneViewLogged)
            {
                missingSceneViewLogged = true;
                Debug.LogWarning(
                    "[BattleActionOrderRailUI] Scene-authored 행동 순서 HUD가 없습니다. " +
                    "Editor 변환 도구로 Scene에 생성한 뒤 RectTransform을 직접 배치하세요.",
                    this);
            }
            return;
        }

        Transform existing =
            transform.Find(
                "PlanningActionOrderOverlay");

        GameObject canvasGo;

        if (existing != null)
        {
            canvasGo =
                existing.gameObject;
        }
        else
        {
            canvasGo =
                new GameObject(
                    "PlanningActionOrderOverlay",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(CanvasGroup));

            canvasGo.transform.SetParent(
                transform,
                false);
        }

        // Scene authoring 단계에서 host 아래 독립 Overlay Canvas로 유지한다.
        // Play Mode에서는 hierarchy/RectTransform을 변경하지 않는다.

        overlayCanvas =
            canvasGo.GetComponent<Canvas>();

        overlayCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        overlayCanvas.overrideSorting =
            true;

        // 화살표(20)보다 뒤, 일반 전장보다 앞.
        overlayCanvas.sortingOrder = 18;

        CanvasScaler scaler =
            canvasGo.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f);

        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster =
            canvasGo.GetComponent<GraphicRaycaster>();

        // 레일 본체는 정보 전용이지만 최대화/최소화 버튼만 입력을 받아야 한다.
        raycaster.enabled = true;

        canvasGroup =
            canvasGo.GetComponent<CanvasGroup>();

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        RectTransform canvasRect =
            canvasGo.GetComponent<RectTransform>();

        Stretch(canvasRect);

        Transform panelExisting =
            canvasGo.transform.Find("Panel");

        GameObject panelGo;

        if (panelExisting != null)
        {
            panelGo =
                panelExisting.gameObject;
        }
        else
        {
            panelGo =
                new GameObject(
                    "Panel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));
            panelGo.transform.SetParent(
                canvasGo.transform,
                false);
        }

        panelRoot =
            panelGo.GetComponent<RectTransform>();

        panelRoot.anchorMin =
            new Vector2(0f, 1f);

        panelRoot.anchorMax =
            new Vector2(0f, 1f);

        panelRoot.pivot =
            new Vector2(0f, 1f);

        panelRoot.anchoredPosition =
            topLeftOffset;

        panelRoot.sizeDelta =
            new Vector2(
                Mathf.Max(400f, width),
                Mathf.Max(600f, height));

        Image panelImage =
            panelGo.GetComponent<Image>();

        panelImage.color =
            panelColor;

        panelImage.raycastTarget = false;

        Outline outline =
            panelGo.GetComponent<Outline>();

        outline.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.78f);

        outline.effectDistance =
            new Vector2(
                2f,
                -2f);

        headerText =
            CreateText(
                "Header",
                panelRoot,
                28f,
                TextAlignmentOptions.Left);

        RectTransform headerRect =
            headerText.rectTransform;

        headerRect.anchorMin =
            new Vector2(0f, 1f);

        headerRect.anchorMax =
            new Vector2(1f, 1f);

        headerRect.pivot =
            new Vector2(0.5f, 1f);

        headerRect.anchoredPosition =
            new Vector2(0f, -10f);

        headerRect.sizeDelta =
            new Vector2(-24f, 46f);

        headerText.fontStyle =
            FontStyles.Bold;

        headerText.text =
            "행동 순서";

        Transform contentExisting =
            panelGo.transform.Find("Content");

        GameObject contentGo;

        if (contentExisting != null)
        {
            contentGo =
                contentExisting.gameObject;
        }
        else
        {
            contentGo =
                new GameObject(
                    "Content",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup));

            contentGo.transform.SetParent(
                panelGo.transform,
                false);
        }

        contentRoot =
            contentGo.GetComponent<RectTransform>();

        contentRoot.anchorMin =
            new Vector2(0f, 0f);

        contentRoot.anchorMax =
            new Vector2(1f, 1f);

        contentRoot.offsetMin =
            new Vector2(12f, 12f);

        contentRoot.offsetMax =
            new Vector2(-12f, -62f);

        VerticalLayoutGroup layout =
            contentGo.GetComponent<VerticalLayoutGroup>();

        layout.spacing = 7f;
        layout.childAlignment =
            TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private bool TryResolveSceneView()
    {
        Transform existing =
            transform.Find(
                "PlanningActionOrderOverlay");

        if (existing == null)
            return false;

        Canvas canvas = existing.GetComponent<Canvas>();
        CanvasGroup group = existing.GetComponent<CanvasGroup>();
        RectTransform panel =
            existing.Find("Panel") as RectTransform;
        RectTransform content =
            panel != null
                ? panel.Find("Content") as RectTransform
                : null;
        TMP_Text header =
            panel != null
                ? panel.Find("Header")?.GetComponent<TMP_Text>()
                : null;
        Transform toggleTransform =
            panel != null
                ? panel.Find("SizeToggle")
                : null;
        Button toggleButton =
            toggleTransform != null
                ? toggleTransform.GetComponent<Button>()
                : null;
        TMP_Text toggleLabel =
            toggleTransform != null
                ? toggleTransform.Find("Label")?.GetComponent<TMP_Text>()
                : null;

        if (canvas == null ||
            group == null ||
            panel == null ||
            content == null ||
            header == null)
        {
            return false;
        }

        overlayCanvas = canvas;
        canvasGroup = group;
        panelRoot = panel;
        contentRoot = content;
        headerText = header;
        sizeToggleButton = toggleButton;
        sizeToggleLabel = toggleLabel;
        BindSizeToggle();
        return true;
    }

#if UNITY_EDITOR
    public void EditorAuthorSceneView()
    {
        EnsureView();
        EnsureSizeToggleEditor();

        // 기존 Scene-authored HUD를 다시 resolve해 새 버튼 참조를 캐시한다.
        overlayCanvas = null;
        canvasGroup = null;
        panelRoot = null;
        contentRoot = null;
        headerText = null;
        sizeToggleButton = null;
        sizeToggleLabel = null;
        TryResolveSceneView();
    }

    private void EnsureSizeToggleEditor()
    {
        if (panelRoot == null)
            return;

        Transform existing =
            panelRoot.Find("SizeToggle");

        GameObject buttonObject;

        if (existing != null)
        {
            buttonObject = existing.gameObject;
        }
        else
        {
            buttonObject =
                new GameObject(
                    "SizeToggle",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
            buttonObject.transform.SetParent(
                panelRoot,
                false);
        }

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        // 아래 값은 Editor에서 최초 생성할 때만 쓰는 seed다.
        // Play Mode에서는 위치/Anchor를 절대 다시 덮어쓰지 않는다.
        if (existing == null)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-8f, -8f);
            rect.sizeDelta = new Vector2(78f, 34f);
        }

        Image image =
            buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.18f, 0.96f);
        image.raycastTarget = true;

        Button button =
            buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        Transform labelExisting =
            buttonObject.transform.Find("Label");
        TMP_Text label;

        if (labelExisting != null)
        {
            label =
                labelExisting.GetComponent<TMP_Text>();
        }
        else
        {
            GameObject labelObject =
                new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(
                buttonObject.transform,
                false);

            RectTransform labelRect =
                labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            label =
                labelObject.GetComponent<TextMeshProUGUI>();
        }

        if (label != null)
        {
            label.text = "최소화";
            label.fontSize = 18f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 11f;
            label.fontSizeMax = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        GraphicRaycaster raycaster =
            overlayCanvas != null
                ? overlayCanvas.GetComponent<GraphicRaycaster>()
                : null;
        if (raycaster != null)
            raycaster.enabled = true;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
#endif

    private void InitializeCollapseStateIfNeeded()
    {
        if (collapseStateInitialized ||
            panelRoot == null ||
            contentRoot == null)
        {
            return;
        }

        collapseStateInitialized = true;
        expandedSizeDeltaY = panelRoot.sizeDelta.y;

        // Authoring 값이 우연히 접힌 높이보다 작더라도 복원 가능한 값을 확보한다.
        if (Mathf.Abs(expandedSizeDeltaY) < collapsedHeight + 1f)
            expandedSizeDeltaY = Mathf.Max(height, collapsedHeight + 120f);

        BindSizeToggle();
        SetCollapsed(startCollapsed, rebuildWhenExpanded: false);
    }

    private void BindSizeToggle()
    {
        if (sizeToggleButton == null)
            return;

        sizeToggleButton.onClick.RemoveListener(ToggleCollapsed);
        sizeToggleButton.onClick.AddListener(ToggleCollapsed);
    }

    private void UnbindSizeToggle()
    {
        if (sizeToggleButton != null)
            sizeToggleButton.onClick.RemoveListener(ToggleCollapsed);
    }

    private void ToggleCollapsed()
    {
        SetCollapsed(!isCollapsed, rebuildWhenExpanded: true);
    }

    public void SetCollapsed(bool collapsed)
    {
        SetCollapsed(collapsed, rebuildWhenExpanded: true);
    }

    private void SetCollapsed(
        bool collapsed,
        bool rebuildWhenExpanded)
    {
        if (panelRoot == null ||
            contentRoot == null)
        {
            return;
        }

        if (!collapseStateInitialized)
        {
            collapseStateInitialized = true;
            expandedSizeDeltaY = panelRoot.sizeDelta.y;
        }

        if (!isCollapsed &&
            !collapsed &&
            Mathf.Abs(panelRoot.sizeDelta.y) > collapsedHeight + 1f)
        {
            expandedSizeDeltaY = panelRoot.sizeDelta.y;
        }

        isCollapsed = collapsed;
        contentRoot.gameObject.SetActive(!collapsed);

        Vector2 size = panelRoot.sizeDelta;
        size.y = collapsed
            ? Mathf.Max(44f, collapsedHeight)
            : expandedSizeDeltaY;
        panelRoot.sizeDelta = size;

        RefreshSizeToggleLabel();

        if (!collapsed && rebuildWhenExpanded)
        {
            lastSignature = int.MinValue;
            Rebuild(force: true);
        }
    }

    private void RefreshSizeToggleLabel()
    {
        if (sizeToggleLabel != null)
        {
            sizeToggleLabel.text =
                isCollapsed
                    ? "최대화"
                    : "최소화";
        }

        if (sizeToggleButton != null)
        {
            sizeToggleButton.gameObject.name =
                "SizeToggle";
        }
    }

    private void Rebuild(
        bool force)
    {
        IReadOnlyList<ActionSlot> slots =
            battleManager?.ActionManager?.Slots;

        if (slots == null ||
            contentRoot == null)
        {
            return;
        }

        int signature =
            BuildSignature(
                slots);

        if (!force &&
            signature == lastSignature)
        {
            return;
        }

        lastSignature = signature;

        List<ActionSlot> orderedActions =
            new List<ActionSlot>();

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot == null ||
                !IsVisibleActionPhase(slot.Phase))
            {
                continue;
            }

            orderedActions.Add(slot);
        }

        // 실제 Resolution과 동일한 정렬기를 사용한다.
        // PRETURN(위세) -> FORESIGHT(도사림) -> COMBAT(속도순).
        orderedActions.Sort(
            sorter.CompareForExecution);

        ClearRows();

        BuildClashSet(
            slots);

        HashSet<ActionSlot> consumed =
            new HashSet<ActionSlot>();

        int totalExecutionUnits = 0;
        int shownExecutionUnits = 0;

        foreach (ActionSlot slot in orderedActions)
        {
            if (slot == null ||
                consumed.Contains(slot))
            {
                continue;
            }

            ActionSlot partner = null;

            bool isClashUnit =
                slot.Phase == ActionPhase.COMBAT &&
                clashPartnerBySlot.TryGetValue(
                    slot,
                    out partner) &&
                partner != null &&
                !consumed.Contains(partner);

            consumed.Add(slot);

            if (isClashUnit)
                consumed.Add(partner);

            totalExecutionUnits++;

            if (shownExecutionUnits >= maximumRows)
                continue;

            if (isClashUnit)
            {
                CreateClashRow(
                    slot,
                    partner);
            }
            else
            {
                CreateRow(
                    slot);
            }

            shownExecutionUnits++;
        }

        if (battleManager?.TurnManager?.IsResolving == true)
        {
            resolutionRemainingActionCount =
                totalExecutionUnits;
        }

        int hidden =
            totalExecutionUnits - shownExecutionUnits;

        if (hidden > 0)
        {
            CreateOverflowRow(
                hidden);
        }

        UpdateHeaderCount(
            battleManager?.TurnManager?.IsResolving == true
                ? resolutionRemainingActionCount
                : totalExecutionUnits);
    }

    private int BuildSignature(
        IReadOnlyList<ActionSlot> slots)
    {
        int hash = 17;

        hash =
            hash * 31 +
            Screen.width;

        hash =
            hash * 31 +
            Screen.height;

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot == null ||
                !IsVisibleActionPhase(slot.Phase))
            {
                continue;
            }

            hash =
                hash * 31 +
                slot.ActionId.GetHashCode();

            hash =
                hash * 31 +
                (int)slot.Phase;

            hash =
                hash * 31 +
                slot.Speed;

            hash =
                hash * 31 +
                slot.ActionIndex;

            hash =
                hash * 31 +
                (slot.Skill?.SkillName
                    ?.GetHashCode() ?? 0);

            hash =
                hash * 31 +
                (slot.TargetSlot?.ActionId
                    .GetHashCode() ?? 0);

            hash =
                hash * 31 +
                (slot.TargetCharacter
                    ?.GetInstanceID() ?? 0);

            hash =
                hash * 31 +
                (slot.TargetPart
                    ?.GetHashCode() ?? 0);
        }

        hash =
            hash * 31 +
            (uiManager?.SelectedOwner
                ?.GetInstanceID() ?? 0);

        hash =
            hash * 31 +
            (uiManager?.SelectedOwnerPart
                ?.GetHashCode() ?? 0);

        hash =
            hash * 31 +
            (uiManager?.SelectedActionIndex ?? 0);

        hash =
            hash * 31 +
            (BattleWorldActionSlotCellUI
                .HoveredTargetCell
                ?.TargetSlot
                ?.ActionId
                .GetHashCode() ?? 0);

        hash =
            hash * 31 +
            (BattleWorldActionSlotCellUI
                .HoveredCell
                ?.GetRepresentedActionSlot()
                ?.ActionId
                .GetHashCode() ?? 0);

        return hash;
    }

    private void BuildClashSet(
        IReadOnlyList<ActionSlot> slots)
    {
        clashPartnerBySlot.Clear();

        IReadOnlyList<ClashPair> pairs =
            battleManager?.ClashBuilder
                ?.BuildClashPreview(slots);

        if (pairs == null)
            return;

        foreach (ClashPair pair
                 in pairs)
        {
            if (pair == null ||
                !pair.IsClash ||
                pair.First == null ||
                pair.Second == null)
            {
                continue;
            }

            clashPartnerBySlot[pair.First] =
                pair.Second;

            clashPartnerBySlot[pair.Second] =
                pair.First;
        }
    }

    private void CreateRow(
        ActionSlot slot)
    {
        Character player =
            battleManager?.BattleContext?.Player;

        bool playerSide =
            slot?.Owner == player;

        bool selected =
            slot != null &&
            playerSide &&
            uiManager != null &&
            uiManager.IsWorldPlanningSlotSelected(
                slot.Owner,
                slot.Part,
                slot.ActionIndex);

        bool hoveredTarget =
            slot != null &&
            BattleWorldActionSlotCellUI
                .HoveredTargetCell
                ?.TargetSlot == slot;

        long hoveredActionId =
            BattleWorldActionSlotCellUI
                .HoveredCell
                ?.GetRepresentedActionSlot()
                ?.ActionId ?? 0;

        bool hoveredPlayer =
            slot != null &&
            playerSide &&
            slot.ActionId > 0 &&
            hoveredActionId == slot.ActionId;

        GameObject row =
            new GameObject(
                $"Order_{slot?.ActionId ?? 0}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(LayoutElement));

        row.transform.SetParent(
            contentRoot,
            false);

        generatedRows.Add(
            row);

        RegisterRowAction(
            row,
            slot);

        Image background =
            row.GetComponent<Image>();

        background.color =
            hoveredPlayer
                ? hoveredPlayerBackground
                : hoveredTarget
                    ? hoveredTargetBackground
                    : selected
                        ? selectedBackground
                        : new Color(
                            0.04f,
                            0.055f,
                            0.08f,
                            0.76f);

        background.raycastTarget = false;

        LayoutElement element =
            row.GetComponent<LayoutElement>();

        element.preferredHeight = 62f;
        element.minHeight = 56f;

        Image accent =
            CreateImage(
                "Accent",
                row.transform,
                playerSide
                    ? playerAccent
                    : enemyAccent);

        RectTransform accentRect =
            accent.rectTransform;

        accentRect.anchorMin =
            new Vector2(0f, 0f);

        accentRect.anchorMax =
            new Vector2(0f, 1f);

        accentRect.pivot =
            new Vector2(0f, 0.5f);

        accentRect.anchoredPosition =
            Vector2.zero;

        accentRect.sizeDelta =
            new Vector2(4f, 0f);

        TMP_Text label =
            CreateText(
                "Label",
                row.transform,
                22f,
                TextAlignmentOptions.Left);

        RectTransform labelRect =
            label.rectTransform;

        labelRect.anchorMin =
            Vector2.zero;

        labelRect.anchorMax =
            Vector2.one;

        labelRect.offsetMin =
            new Vector2(14f, 4f);

        labelRect.offsetMax =
            new Vector2(-10f, -4f);

        string side =
            playerSide
                ? "아군"
                : "적";

        string owner =
            GetCharacterName(
                slot?.Owner);

        string part =
            GetPartLabel(
                slot?.Part);

        string skill =
            slot?.Skill?.SkillName ??
            "행동";

        string relation =
            playerSide
                ? "<color=#4FA5FF>→</color>"
                : "<color=#FF5A5A>→</color>";

        string target =
            slot?.TargetCharacter != null
                ? $"{GetCharacterName(slot.TargetCharacter)} · {GetPartLabel(slot.TargetPart)}"
                : string.Empty;

        string orderLabel =
            GetOrderLabel(slot);

        label.text =
            $"<b>{orderLabel}</b>  " +
            $"<size=78%>{side} · {owner} · {part}</size>\n" +
            $"<size=78%>{skill}  {relation}" +
            $"{(string.IsNullOrEmpty(target) ? string.Empty : $"  {target}")}</size>";

        if (hoveredPlayer)
        {
            label.color =
                new Color(
                    1f,
                    0.91f,
                    0.28f,
                    1f);
        }
        else if (hoveredTarget)
        {
            label.color =
                new Color(
                    1f,
                    0.92f,
                    0.66f,
                    1f);
        }
    }

    private void CreateClashRow(
        ActionSlot first,
        ActionSlot second)
    {
        if (first == null || second == null)
            return;

        Character player =
            battleManager?.BattleContext?.Player;

        ActionSlot playerAction =
            first.Owner == player
                ? first
                : second.Owner == player
                    ? second
                    : first;

        ActionSlot opponentAction =
            ReferenceEquals(playerAction, first)
                ? second
                : first;

        bool selected =
            IsSelectedPlayerAction(first, player) ||
            IsSelectedPlayerAction(second, player);

        long hoveredActionId =
            BattleWorldActionSlotCellUI
                .HoveredCell
                ?.GetRepresentedActionSlot()
                ?.ActionId ?? 0;

        bool hoveredPlayer =
            hoveredActionId > 0 &&
            (hoveredActionId == first.ActionId ||
             hoveredActionId == second.ActionId);

        ActionSlot hoveredTargetSlot =
            BattleWorldActionSlotCellUI
                .HoveredTargetCell
                ?.TargetSlot;

        bool hoveredTarget =
            hoveredTargetSlot == first ||
            hoveredTargetSlot == second;

        GameObject row =
            new GameObject(
                $"Clash_{first.ActionId}_{second.ActionId}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(LayoutElement));

        row.transform.SetParent(
            contentRoot,
            false);

        generatedRows.Add(row);

        // 합은 두 ActionId가 하나의 실행 단위 UI를 공유한다.
        RegisterRowAction(row, first);
        RegisterRowAction(row, second);

        Image background =
            row.GetComponent<Image>();

        background.color =
            hoveredPlayer
                ? hoveredPlayerBackground
                : hoveredTarget
                    ? hoveredTargetBackground
                    : selected
                        ? selectedBackground
                        : new Color(
                            0.16f,
                            0.125f,
                            0.025f,
                            0.90f);

        background.raycastTarget = false;

        LayoutElement element =
            row.GetComponent<LayoutElement>();

        element.preferredHeight = 88f;
        element.minHeight = 82f;

        Image accent =
            CreateImage(
                "ClashAccent",
                row.transform,
                clashAccent);

        RectTransform accentRect =
            accent.rectTransform;

        accentRect.anchorMin =
            new Vector2(0f, 0f);
        accentRect.anchorMax =
            new Vector2(0f, 1f);
        accentRect.pivot =
            new Vector2(0f, 0.5f);
        accentRect.anchoredPosition =
            Vector2.zero;
        accentRect.sizeDelta =
            new Vector2(7f, 0f);

        TMP_Text label =
            CreateText(
                "Label",
                row.transform,
                20f,
                TextAlignmentOptions.Left);

        RectTransform labelRect =
            label.rectTransform;

        labelRect.anchorMin =
            Vector2.zero;
        labelRect.anchorMax =
            Vector2.one;
        labelRect.offsetMin =
            new Vector2(16f, 4f);
        labelRect.offsetMax =
            new Vector2(-10f, -4f);

        string speedLabel =
            first.Speed == second.Speed
                ? $"속도 {first.Speed}"
                : $"속도 {first.Speed} ↔ {second.Speed}";

        label.text =
            $"<b><color=#FFD12A>[ 합 ]</color>  {speedLabel}</b>\n" +
            $"<size=78%><color=#62AEFF>아군  {FormatClashParticipant(playerAction)}</color></size>\n" +
            $"<size=78%><color=#FFD12A>VS</color>  <color=#FF6B6B>적  {FormatClashParticipant(opponentAction)}</color></size>";

        if (hoveredPlayer)
        {
            label.color =
                new Color(
                    1f,
                    0.93f,
                    0.44f,
                    1f);
        }
        else if (hoveredTarget)
        {
            label.color =
                new Color(
                    1f,
                    0.92f,
                    0.66f,
                    1f);
        }
    }

    private bool IsSelectedPlayerAction(
        ActionSlot slot,
        Character player)
    {
        return slot != null &&
               slot.Owner == player &&
               uiManager != null &&
               uiManager.IsWorldPlanningSlotSelected(
                   slot.Owner,
                   slot.Part,
                   slot.ActionIndex);
    }

    private void RegisterRowAction(
        GameObject row,
        ActionSlot slot)
    {
        if (row == null ||
            slot == null ||
            slot.ActionId <= 0)
        {
            return;
        }

        rowsByActionId[slot.ActionId] = row;
    }

    private string FormatClashParticipant(
        ActionSlot slot)
    {
        if (slot == null)
            return "--";

        string owner =
            GetCharacterName(slot.Owner);

        string part =
            GetPartLabel(slot.Part);

        string skill =
            slot.Skill?.SkillName ??
            "행동";

        return $"{owner} · {part} · {skill}";
    }

    private static string GetOrderLabel(
        ActionSlot slot)
    {
        if (slot == null)
            return "--";

        return slot.Phase switch
        {
            ActionPhase.PRETURN => "위세",
            ActionPhase.FORESIGHT => "도사림",
            _ => $"속도 {slot.Speed}"
        };
    }

    private void CreateReactiveRollRow(
        BattleReactiveRollEvent reactiveRoll)
    {
        if (reactiveRoll == null ||
            contentRoot == null)
        {
            return;
        }

        GameObject row =
            new GameObject(
                $"ReactiveRoll_{reactiveRoll.EventId}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(LayoutElement));

        row.transform.SetParent(
            contentRoot,
            false);

        generatedRows.Add(row);
        reactiveRowsByEventId[reactiveRoll.EventId] = row;

        int insertIndex = 0;
        long sourceActionId =
            reactiveRoll.SourceAction?.ActionId ?? 0;

        if (sourceActionId > 0 &&
            rowsByActionId.TryGetValue(
                sourceActionId,
                out GameObject sourceRow) &&
            sourceRow != null)
        {
            insertIndex =
                Mathf.Clamp(
                    sourceRow.transform.GetSiblingIndex() + 1,
                    0,
                    contentRoot.childCount - 1);
        }

        row.transform.SetSiblingIndex(insertIndex);

        Image background = row.GetComponent<Image>();
        background.color = reactiveRollBackground;
        background.raycastTarget = false;

        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minHeight = 0f;
        element.preferredHeight = 0f;

        Image accent =
            CreateImage(
                "ReactiveAccent",
                row.transform,
                reactiveRollAccent);

        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(7f, 0f);

        TMP_Text label =
            CreateText(
                "Label",
                row.transform,
                21f,
                TextAlignmentOptions.Left);

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(18f, 4f);
        labelRect.offsetMax = new Vector2(-10f, -4f);

        string owner =
            GetCharacterName(
                reactiveRoll.Owner);

        string symbol =
            PhysicalDamageResolver.GetSymbol(
                reactiveRoll.PhysicalType);

        string sequence =
            reactiveRoll.SequenceCount > 1
                ? $" {reactiveRoll.DisplaySequenceNumber}/{reactiveRoll.SequenceCount}"
                : string.Empty;

        label.text =
            $"<color=#D99CFF><b>+ 추가 굴림{sequence}</b></color>  " +
            $"<color=#F1D8FF><b>{symbol}</b></color>\n" +
            $"<size=78%>{owner} · {reactiveRoll.DisplayName} · 위력 {reactiveRoll.Power}</size>";

        label.color =
            new Color(
                0.95f,
                0.84f,
                1f,
                1f);

        CanvasGroup group = row.GetComponent<CanvasGroup>();
        RectTransform rect = row.transform as RectTransform;

        group.alpha = 0f;
        rect.localScale = new Vector3(0.90f, 0.90f, 1f);

        DOTween.Kill(row, complete: false);

        Sequence sequenceTween =
            DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(row);

        sequenceTween.Join(
            group.DOFade(
                1f,
                reactiveRowAppearDuration));

        sequenceTween.Join(
            rect.DOScale(
                    1f,
                    reactiveRowAppearDuration)
                .SetEase(Ease.OutBack));

        sequenceTween.Join(
            DOTween.To(
                    () => element.preferredHeight,
                    value => element.preferredHeight = value,
                    62f,
                    reactiveRowAppearDuration)
                .SetEase(Ease.OutCubic));
    }

    private void CompleteReactiveRollRow(
        long eventId)
    {
        if (!reactiveRowsByEventId.TryGetValue(
                eventId,
                out GameObject row) ||
            row == null)
        {
            reactiveRowsByEventId.Remove(eventId);
            reactiveRowStartedAt.Remove(eventId);
            return;
        }

        reactiveRowsByEventId.Remove(eventId);
        reactiveRowStartedAt.Remove(eventId);

        resolutionRemainingActionCount =
            Mathf.Max(
                0,
                resolutionRemainingActionCount - 1);

        UpdateHeaderCount(
            resolutionRemainingActionCount);

        CanvasGroup group = row.GetComponent<CanvasGroup>();
        RectTransform rect = row.transform as RectTransform;
        LayoutElement element = row.GetComponent<LayoutElement>();

        if (group == null || rect == null || element == null)
        {
            RemoveGeneratedRow(row);
            return;
        }

        DOTween.Kill(row, complete: false);
        element.minHeight = 0f;

        Sequence sequence =
            DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(row);

        sequence.Join(
            group.DOFade(
                0f,
                completedRowFadeDuration));

        sequence.Join(
            rect.DOAnchorPosX(
                    rect.anchoredPosition.x -
                    completedRowSlideDistance,
                    completedRowFadeDuration)
                .SetEase(Ease.InCubic));

        sequence.Join(
            rect.DOScale(
                    0.88f,
                    completedRowFadeDuration)
                .SetEase(Ease.InBack));

        sequence.Join(
            DOTween.To(
                    () => element.preferredHeight,
                    value => element.preferredHeight = value,
                    0f,
                    completedRowFadeDuration)
                .SetEase(Ease.InCubic));

        sequence.OnComplete(
            () => RemoveGeneratedRow(row));
    }

    private void CreateOverflowRow(
        int hidden)
    {
        TMP_Text label =
            CreateText(
                "Overflow",
                contentRoot,
                18f,
                TextAlignmentOptions.Center);

        LayoutElement element =
            label.gameObject.AddComponent<LayoutElement>();

        element.preferredHeight = 34f;

        label.text =
            $"+ {hidden}개 행동";
        label.color =
            new Color(
                0.68f,
                0.72f,
                0.80f,
                1f);

        generatedRows.Add(
            label.gameObject);
    }

    private void ClearRows()
    {
        for (int i = generatedRows.Count - 1;
             i >= 0;
             i--)
        {
            GameObject row =
                generatedRows[i];

            if (row == null)
                continue;

            DOTween.Kill(
                row,
                complete: false);

            row.SetActive(false);

            if (Application.isPlaying)
                Destroy(row);
            else
                DestroyImmediate(row);
        }

        generatedRows.Clear();
        rowsByActionId.Clear();
        clashPartnerBySlot.Clear();
        completedResolutionRows.Clear();
        reactiveRowsByEventId.Clear();
        reactiveRowStartedAt.Clear();
    }

    private void EnsureOverlayIsRootCanvas()
    {
        if (overlayCanvas == null)
            return;

        // 위치/부모 Transform은 Scene authoring 값 그대로 둔다.
        // 런타임에서는 표시 계약에 필요한 Canvas mode/sorting만 보장한다.
        overlayCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        overlayCanvas.overrideSorting = true;
    }

    private void DestroyOverlayView()
    {
        GameObject overlay =
            overlayCanvas != null
                ? overlayCanvas.gameObject
                : null;

        overlayCanvas = null;
        canvasGroup = null;
        panelRoot = null;
        contentRoot = null;
        headerText = null;
        sizeToggleButton = null;
        sizeToggleLabel = null;
        collapseStateInitialized = false;

        if (overlay == null)
            return;

        if (Application.isPlaying)
            Destroy(overlay);
        else
            DestroyImmediate(overlay);
    }


    private static string GetCharacterName(
        Character character)
    {
        string name =
            character?.Data?.CharacterName;

        if (string.IsNullOrWhiteSpace(name))
            name = character?.name;

        if (string.IsNullOrWhiteSpace(name))
            name = "캐릭터";

        name =
            name.Replace(
                "(Clone)",
                string.Empty)
                .Trim();

        if (name.Length > 8)
            name =
                name.Substring(0, 8);

        return name;
    }

    private static string GetPartLabel(
        BodyPart part)
    {
        if (part == null)
            return "행동";

        return part.Type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼팔",
            PartType.RIGHT_HAND => "오른팔",
            PartType.LEGS => "다리",
            _ => "행동"
        };
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        go.transform.SetParent(
            parent,
            false);

        Image image =
            go.GetComponent<Image>();

        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        go.transform.SetParent(
            parent,
            false);

        TextMeshProUGUI text =
            go.GetComponent<TextMeshProUGUI>();

        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin =
            Mathf.Max(
                9f,
                size - 5f);

        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.overflowMode =
            TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(
        RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
