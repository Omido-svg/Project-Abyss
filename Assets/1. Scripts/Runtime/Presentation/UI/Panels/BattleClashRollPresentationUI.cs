using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleClashRollPresentationUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TMP_Text topSkillText;
    [SerializeField] private TMP_Text bottomSkillText;
    [SerializeField] private RectTransform topRollRow;
    [SerializeField] private RectTransform bottomRollRow;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Roll Colors")]
    [SerializeField] private Color attackColor =
        new(0.94f, 0.25f, 0.25f, 1f);

    [SerializeField] private Color defenseColor =
        new(0.30f, 0.62f, 1f, 1f);

    [SerializeField] private Color oneSidedAttackColor =
        new(1f, 0.78f, 0.08f, 1f);

    [Header("Whole Clash Panel")]
    [Tooltip(
        "왼쪽 아래 합 연출 영역 전체의 배율입니다. " +
        "스킬명, VS, 굴림 네모, Resolver 연출, 파괴 연출을 함께 확대합니다.")]
    [SerializeField, Range(0.5f, 2.5f)]
    private float clashPanelScale = 1f;

    [Tooltip(
        "전체 확대 기준점입니다. 기본값 (0, 0)은 왼쪽 아래를 고정하고 " +
        "오른쪽 위 방향으로 확대합니다.")]
    [SerializeField]
    private Vector2 clashPanelScalePivot =
        Vector2.zero;

    [Header("Roll Square Layout")]
    [SerializeField, Min(24f)]
    private float rollSquareSize = 78f;

    [SerializeField, Range(0.45f, 1f)]
    private float rollSquareVisualRatio = 0.795f;

    [SerializeField, Min(0f)]
    private float rollSquareSpacing = 7f;

    [SerializeField, Min(0f)]
    private float rollRowPadding = 4f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.14f;

    [Tooltip("Resolver별 코인/주사위/슬롯/친치로 결과가 완전히 드러나는 시간입니다.")]
    [SerializeField, Min(0.12f)]
    private float resolverRevealDuration = 0.88f;

    [SerializeField, Min(0.01f)] private float noiseDuration = 0.30f;
    [SerializeField, Min(0.02f)] private float rollValueUpdateInterval = 0.055f;
    [SerializeField, Min(0f)] private float noiseIntensity = 5.5f;
    [SerializeField, Min(1f)] private float noiseFrequency = 28f;
    [SerializeField, Min(0.01f)] private float resultDuration = 0.42f;
    [SerializeField, Min(0f)] private float shatterDistance = 44f;

    private readonly List<BattleClashRollSquareUI> topSquares = new();
    private readonly List<BattleClashRollSquareUI> bottomSquares = new();

    private BattleVisualRequest activeRequest;
    private List<BattleClashRollSquareUI> sourceSquares;
    private List<BattleClashRollSquareUI> opponentSquares;
    private BattleClashRollSquareUI activeOneSidedSquare;
    private bool isHidingImmediate;
    private int presentationVersion;

    public bool IsVisible =>
        canvasGroup != null &&
        canvasGroup.alpha > 0.001f &&
        gameObject.activeInHierarchy;

    public bool IsPresentationActive =>
        activeRequest != null &&
        isActiveAndEnabled &&
        !isHidingImmediate;

    public void Configure(
        BattleManager manager,
        TMP_FontAsset font = null)
    {
        battleManager = manager;

        if (font != null)
            fontAsset = font;

        SanitizeRollLayout();
        EnsureHierarchy();
        HideImmediate();
    }

    private void Awake()
    {
        SanitizeRollLayout();
        ResolveReferences();
        EnsureHierarchy();
        HideImmediate();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SanitizeRollLayout();

        if (!Application.isPlaying)
            ApplyClashPresentationLayout();
    }
#endif

    private void OnDisable()
    {
        HideImmediate();
    }

    public IEnumerator ShowSequence(
        BattleVisualRequest request)
    {
        if (request == null ||
            request.SourceAction == null)
        {
            yield break;
        }

        int sessionVersion =
            ++presentationVersion;

        ResolveReferences();
        EnsureHierarchy();
        ClearRows();

        activeRequest = request;
        activeOneSidedSquare = null;

        BattleAction sourceAction =
            request.SourceAction;

        BattleAction opponentAction =
            request.OpponentAction;

        Character player =
            battleManager?.BattleContext?.Player;

        bool sourceIsPlayer =
            player != null &&
            sourceAction.Owner == player;

        bool opponentIsPlayer =
            player != null &&
            opponentAction?.Owner == player;

        // Player 판별이 불가능한 테스트/독립 연출에서는 Source를 아래쪽으로 둔다.
        if (opponentIsPlayer)
        {
            BuildRow(
                bottomRollRow,
                bottomSkillText,
                opponentAction,
                bottomSquares);

            BuildRow(
                topRollRow,
                topSkillText,
                sourceAction,
                topSquares);

            sourceSquares = topSquares;
            opponentSquares = bottomSquares;
        }
        else
        {
            BuildRow(
                bottomRollRow,
                bottomSkillText,
                sourceAction,
                bottomSquares);

            BuildRow(
                topRollRow,
                topSkillText,
                opponentAction,
                topSquares);

            sourceSquares = bottomSquares;
            opponentSquares = topSquares;
        }

        if (sourceIsPlayer && opponentAction == null)
        {
            sourceSquares = bottomSquares;
            opponentSquares = topSquares;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.alpha = 0f;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            if (!IsPresentationSessionCurrent(
                    sessionVersion))
            {
                yield break;
            }

            elapsed += BattlePlaybackSpeedController.BattleUnscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        if (!IsPresentationSessionCurrent(
                sessionVersion))
        {
            yield break;
        }

        canvasGroup.alpha = 1f;
    }

    public IEnumerator PlayPairedExchange(
        BattleClashVisualExchange exchange,
        int timingIndex)
    {
        int sessionVersion =
            presentationVersion;

        if (exchange == null ||
            exchange.IsOneSided ||
            activeRequest == null ||
            !IsPresentationSessionCurrent(
                sessionVersion))
        {
            yield break;
        }

        BattleClashRollSquareUI sourceSquare =
            GetSquare(
                sourceSquares,
                exchange.ExchangeIndex);

        BattleClashRollSquareUI opponentSquare =
            GetSquare(
                opponentSquares,
                exchange.ExchangeIndex);

        if (sourceSquare == null ||
            opponentSquare == null)
        {
            yield break;
        }

        RollResult sourceRoll =
            exchange.DisplayStep.AttackerRollResult;

        RollResult opponentRoll =
            exchange.DisplayStep.TargetRollResult;

        float revealDuration =
            GetResolverRevealDuration();

        if (!TryStartPresentationCoroutine(
                sourceSquare.PlayRollReveal(
                    sourceRoll,
                    exchange.DisplayStep.AttackerClashPower,
                    revealDuration,
                    rollValueUpdateInterval,
                    timingIndex * 41 + 5),
                sessionVersion) ||
            !TryStartPresentationCoroutine(
                opponentSquare.PlayRollReveal(
                    opponentRoll,
                    exchange.DisplayStep.TargetClashPower,
                    revealDuration,
                    rollValueUpdateInterval,
                    timingIndex * 41 + 19),
                sessionVersion) ||
            !TryStartPresentationCoroutine(
                sourceSquare.PlayNoise(
                    revealDuration,
                    noiseIntensity,
                    noiseFrequency,
                    timingIndex * 17 + 3),
                sessionVersion) ||
            !TryStartPresentationCoroutine(
                opponentSquare.PlayNoise(
                    revealDuration,
                    noiseIntensity,
                    noiseFrequency,
                    timingIndex * 17 + 11),
                sessionVersion))
        {
            yield break;
        }

        yield return WaitUnscaled(
            revealDuration);

        if (!IsPresentationSessionCurrent(
                sessionVersion))
        {
            yield break;
        }

        // 결과 판정 직전에 실제 계산값을 다시 고정해
        // 승리 네모에는 값이 남고 패배 네모는 값과 함께 부서진다.
        sourceSquare.ShowFinalRoll(
            sourceRoll,
            exchange.DisplayStep.AttackerClashPower);

        opponentSquare.ShowFinalRoll(
            opponentRoll,
            exchange.DisplayStep.TargetClashPower);

        if (exchange.WasCancelled)
        {
            if (!TryStartPresentationCoroutine(
                    sourceSquare.PlayUnusedDefense(
                        resultDuration * 0.6f),
                    sessionVersion) ||
                !TryStartPresentationCoroutine(
                    opponentSquare.PlayUnusedDefense(
                        resultDuration * 0.6f),
                    sessionVersion))
            {
                yield break;
            }

            yield return WaitUnscaled(
                resultDuration * 0.6f);
            yield break;
        }

        if (exchange.IsTie ||
            exchange.DisplayStep.IsTie)
        {
            if (!TryStartPresentationCoroutine(
                    sourceSquare.PlayTieFlash(
                        resultDuration),
                    sessionVersion) ||
                !TryStartPresentationCoroutine(
                    opponentSquare.PlayTieFlash(
                        resultDuration),
                    sessionVersion))
            {
                yield break;
            }

            yield return WaitUnscaled(
                resultDuration);
            yield break;
        }

        bool sourceWon =
            exchange.DisplayStep.AttackerWon;

        BattleClashRollSquareUI winner =
            sourceWon
                ? sourceSquare
                : opponentSquare;

        BattleClashRollSquareUI loser =
            sourceWon
                ? opponentSquare
                : sourceSquare;

        if (!TryStartPresentationCoroutine(
                winner.PlayWinnerSparkle(
                    resultDuration),
                sessionVersion) ||
            !TryStartPresentationCoroutine(
                loser.PlayBreak(
                    resultDuration,
                    shatterDistance),
                sessionVersion))
        {
            yield break;
        }

        yield return WaitUnscaled(
            resultDuration);
    }

    public void BeginOneSidedExchange(
        BattleClashVisualExchange exchange)
    {
        int sessionVersion =
            presentationVersion;

        EndOneSidedExchange();

        if (exchange == null ||
            !exchange.IsOneSided ||
            activeRequest == null ||
            !IsPresentationSessionCurrent(
                sessionVersion))
        {
            return;
        }

        bool useSource =
            ResolveOneSidedUsesSource(
                exchange);

        activeOneSidedSquare =
            GetSquare(
                useSource
                    ? sourceSquares
                    : opponentSquares,
                exchange.ExchangeIndex);

        if (activeOneSidedSquare == null)
            return;

        RollResult oneSidedRoll =
            useSource
                ? exchange.DisplayStep.AttackerRollResult
                : exchange.DisplayStep.TargetRollResult;

        int oneSidedClashPower =
            useSource
                ? exchange.DisplayStep.AttackerClashPower
                : exchange.DisplayStep.TargetClashPower;

        if (!TryStartPresentationCoroutine(
                activeOneSidedSquare.PlayRollReveal(
                    oneSidedRoll,
                    oneSidedClashPower,
                    Mathf.Min(
                        0.58f,
                        GetResolverRevealDuration()),
                    rollValueUpdateInterval,
                    exchange.ExchangeIndex * 53 + 7),
                sessionVersion))
        {
            activeOneSidedSquare = null;
            return;
        }

        if (exchange.HasAttack &&
            activeOneSidedSquare.RollType ==
            CombatRollType.Attack)
        {
            activeOneSidedSquare.BeginOneSidedHighlight(
                oneSidedAttackColor);
            return;
        }

        TryStartPresentationCoroutine(
            activeOneSidedSquare.PlayUnusedDefense(
                resultDuration * 0.65f),
            sessionVersion);
    }

    public void EndOneSidedExchange()
    {
        if (activeOneSidedSquare == null)
            return;

        activeOneSidedSquare.EndOneSidedHighlight(
            dimAfter: true);

        activeOneSidedSquare = null;
    }

    public IEnumerator Hide()
    {
        if (canvasGroup == null)
            yield break;

        EndOneSidedExchange();

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += BattlePlaybackSpeedController.BattleUnscaledDeltaTime;
            canvasGroup.alpha =
                Mathf.Lerp(
                    startAlpha,
                    0f,
                    Mathf.Clamp01(elapsed / duration));

            yield return null;
        }

        HideImmediate();
    }

    public void HideImmediate()
    {
        if (isHidingImmediate)
            return;

        isHidingImmediate = true;

        try
        {
            presentationVersion++;
            StopAllCoroutines();
            EndOneSidedExchange();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            activeRequest = null;
            sourceSquares = null;
            opponentSquares = null;

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
        finally
        {
            isHidingImmediate = false;
        }
    }

    public void CancelActivePresentation()
    {
        HideImmediate();
    }

    private bool IsPresentationSessionCurrent(
        int sessionVersion)
    {
        return
            sessionVersion == presentationVersion &&
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            !isHidingImmediate;
    }

    private bool TryStartPresentationCoroutine(
        IEnumerator routine,
        int sessionVersion)
    {
        if (routine == null ||
            !IsPresentationSessionCurrent(
                sessionVersion))
        {
            return false;
        }

        StartCoroutine(routine);
        return true;
    }

    [ContextMenu("Apply Whole Clash Panel Scale")]
    public void ApplyWholeClashPanelScale()
    {
        SanitizeRollLayout();
        ResolvePanelReference();
        ApplyPanelScale();
    }

    [ContextMenu("Apply Clash Presentation Layout")]
    public void ApplyClashPresentationLayout()
    {
        ApplyWholeClashPanelScale();
        ApplyRollSquareLayout();
    }

    [ContextMenu("Apply Roll Square Layout")]
    public void ApplyRollSquareLayout()
    {
        SanitizeRollLayout();
        ResolvePanelReference();
        ApplyPanelScale();

        ApplyRowLayout(topRollRow);
        ApplyRowLayout(bottomRollRow);

        ApplySquareLayout(topRollRow);
        ApplySquareLayout(bottomRollRow);

        if (topRollRow != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(topRollRow);

        if (bottomRollRow != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(bottomRollRow);
    }

    [ContextMenu("Rebuild Clash Roll UI Hierarchy")]
    public void RebuildHierarchy()
    {
        ResolveReferences();
        EnsureHierarchy(forceRebuild: true);
        HideImmediate();
    }

    private void ResolveReferences()
    {
        canvasGroup ??= GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();
    }

    private void EnsureHierarchy(
        bool forceRebuild = false)
    {
        RectTransform rootRect =
            transform as RectTransform;

        if (rootRect == null)
            return;

        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;

        if (forceRebuild)
        {
            for (int index = transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                DestroyObject(
                    transform.GetChild(index).gameObject);
            }

            panel = null;
            topSkillText = null;
            bottomSkillText = null;
            topRollRow = null;
            bottomRollRow = null;
        }

        panel ??=
            FindRect("ClashRollPanel");

        if (panel == null)
        {
            // ClashRollPanel은 배치용 RectTransform일 뿐이다.
            // 검은 배경 Image를 만들지 않는다.
            GameObject panelObject =
                CreateUiObject(
                    "ClashRollPanel",
                    transform);

            panel = panelObject.GetComponent<RectTransform>();
        }

        SetRect(
            panel,
            new Vector2(0.025f, 0.055f),
            new Vector2(0.57f, 0.39f),
            Vector2.zero,
            Vector2.zero);

        ApplyPanelScale();

        // v4.3으로 이미 생성된 Scene에는 검은 Image가 남아 있을 수 있다.
        // Runtime에서도 즉시 비활성화해 Installer 재실행 전부터 배경을 제거한다.
        Image panelImage =
            panel.GetComponent<Image>();

        if (panelImage != null)
        {
            panelImage.color = Color.clear;
            panelImage.raycastTarget = false;
            panelImage.enabled = false;
        }

        topSkillText =
            EnsureText(
                "EnemySkillName",
                panel,
                new Vector2(0.03f, 0.80f),
                new Vector2(0.97f, 0.98f),
                24f);

        topRollRow =
            EnsureRow(
                "EnemyRollRow",
                panel,
                new Vector2(0.03f, 0.53f),
                new Vector2(0.97f, 0.79f));

        bottomRollRow =
            EnsureRow(
                "AllyRollRow",
                panel,
                new Vector2(0.03f, 0.24f),
                new Vector2(0.97f, 0.50f));

        bottomSkillText =
            EnsureText(
                "AllySkillName",
                panel,
                new Vector2(0.03f, 0.02f),
                new Vector2(0.97f, 0.22f),
                24f);

        TMP_Text versus =
            EnsureText(
                "Versus",
                panel,
                new Vector2(0.43f, 0.465f),
                new Vector2(0.57f, 0.56f),
                18f);

        versus.text = "VS";
        versus.color = new Color(1f, 0.83f, 0.28f, 0.9f);
    }

    private void BuildRow(
        RectTransform row,
        TMP_Text skillLabel,
        BattleAction action,
        List<BattleClashRollSquareUI> destination)
    {
        if (row == null ||
            destination == null)
        {
            return;
        }

        destination.Clear();

        if (skillLabel != null)
        {
            skillLabel.text =
                action?.Skill?.SkillName ??
                string.Empty;

            skillLabel.gameObject.SetActive(
                action?.Skill != null);
        }

        row.gameObject.SetActive(
            action?.Skill != null);

        if (action?.Skill == null)
            return;

        int authoredRollCount =
            Mathf.Max(
                1,
                action.Skill.ExchangeRollCount);

        // 상태/메커닉이 추가한 실제 굴림 개수를 Presentation도 그대로 사용한다.
        // authoredRollCount를 넘는 굴림은 보라색 + 배지로 "런타임 추가 굴림"임을 표시한다.
        int rollCount =
            Mathf.Max(
                1,
                action.GetEffectiveExchangeRollCount());

        for (int index = 0;
             index < rollCount;
             index++)
        {
            CombatRollType type =
                ResolveRollType(
                    action.Skill,
                    index);

            GameObject squareObject =
                CreateUiObject(
                    $"Roll_{index + 1}_{type}",
                    row,
                    typeof(LayoutElement),
                    typeof(BattleClashRollSquareUI));

            LayoutElement layout =
                squareObject.GetComponent<LayoutElement>();

            ApplySquareLayoutElement(
                layout);

            BattleClashRollSquareUI square =
                squareObject.GetComponent<BattleClashRollSquareUI>();

            PhysicalDamageType physicalType =
                PhysicalDamageResolver.Resolve(
                    action,
                    index);

            bool generatedRoll =
                index >= authoredRollCount;

            square.Configure(
                type,
                physicalType,
                generatedRoll,
                attackColor,
                defenseColor,
                fontAsset,
                GetRollSquareVisualSize());

            destination.Add(square);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(row);
    }

    private void ClearRows()
    {
        ClearRow(topRollRow, topSquares);
        ClearRow(bottomRollRow, bottomSquares);
    }

    private static void ClearRow(
        RectTransform row,
        List<BattleClashRollSquareUI> squares)
    {
        squares?.Clear();

        if (row == null)
            return;

        for (int index = row.childCount - 1;
             index >= 0;
             index--)
        {
            DestroyObject(
                row.GetChild(index).gameObject);
        }
    }

    private bool ResolveOneSidedUsesSource(
        BattleClashVisualExchange exchange)
    {
        BattleAction activeAction =
            exchange.AttackRequest?.SourceAction;

        if (activeAction != null)
        {
            if (activeAction == activeRequest.SourceAction)
                return true;

            if (activeAction == activeRequest.OpponentAction)
                return false;
        }

        bool sourceHasRoll =
            exchange.DisplayStep.AttackerRollResult != null;

        bool opponentHasRoll =
            exchange.DisplayStep.TargetRollResult != null;

        if (sourceHasRoll != opponentHasRoll)
            return sourceHasRoll;

        return exchange.DisplayStep.AttackerValue != 0 ||
               exchange.DisplayStep.TargetValue == 0;
    }

    private static BattleClashRollSquareUI GetSquare(
        List<BattleClashRollSquareUI> source,
        int index)
    {
        if (source == null ||
            index < 0 ||
            index >= source.Count)
        {
            return null;
        }

        return source[index];
    }

    private static CombatRollType ResolveRollType(
        Skill skill,
        int index)
    {
        // 전투 로직과 같은 Skill.GetRollData/GetRollType 경로를 사용한다.
        // 런타임 추가 굴림은 마지막 authored RollData를 재사용할 수 있으므로
        // UI가 Rolls.Count 밖을 무조건 Attack(빨강)으로 폴백하면 안 된다.
        return skill?.GetRollType(
                   Mathf.Max(0, index)) ??
               CombatRollType.Attack;
    }

    private void SanitizeRollLayout()
    {
        clashPanelScale =
            Mathf.Clamp(
                clashPanelScale,
                0.5f,
                2.5f);

        clashPanelScalePivot.x =
            Mathf.Clamp01(
                clashPanelScalePivot.x);

        clashPanelScalePivot.y =
            Mathf.Clamp01(
                clashPanelScalePivot.y);

        rollSquareSize =
            Mathf.Max(
                24f,
                rollSquareSize);

        rollSquareVisualRatio =
            Mathf.Clamp(
                rollSquareVisualRatio,
                0.45f,
                1f);

        rollSquareSpacing =
            Mathf.Max(
                0f,
                rollSquareSpacing);

        rollRowPadding =
            Mathf.Max(
                0f,
                rollRowPadding);
    }

    private void ResolvePanelReference()
    {
        if (panel == null)
        {
            panel =
                FindRect(
                    "ClashRollPanel");
        }
    }

    private void ApplyPanelScale()
    {
        if (panel == null)
            return;

        panel.pivot =
            clashPanelScalePivot;

        panel.localScale =
            Vector3.one *
            clashPanelScale;
    }

    private float GetRollSquareVisualSize()
    {
        return
            Mathf.Max(
                16f,
                rollSquareSize *
                rollSquareVisualRatio);
    }

    private void ApplyRowLayout(
        RectTransform row)
    {
        if (row == null)
            return;

        HorizontalLayoutGroup layout =
            row.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();

        int padding =
            Mathf.RoundToInt(
                rollRowPadding);

        layout.padding =
            new RectOffset(
                padding,
                padding,
                padding,
                padding);

        layout.spacing =
            rollSquareSpacing;

        layout.childAlignment =
            TextAnchor.MiddleCenter;

        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void ApplySquareLayout(
        RectTransform row)
    {
        if (row == null)
            return;

        float visualSize =
            GetRollSquareVisualSize();

        for (int index = 0;
             index < row.childCount;
             index++)
        {
            Transform child =
                row.GetChild(index);

            if (child == null)
                continue;

            LayoutElement layout =
                child.GetComponent<LayoutElement>();

            if (layout != null)
            {
                ApplySquareLayoutElement(
                    layout);
            }

            BattleClashRollSquareUI square =
                child.GetComponent<
                    BattleClashRollSquareUI>();

            if (square != null)
            {
                square.SetVisualSize(
                    visualSize);
            }
        }
    }

    private void ApplySquareLayoutElement(
        LayoutElement layout)
    {
        if (layout == null)
            return;

        float safeSize =
            Mathf.Max(
                24f,
                rollSquareSize);

        layout.minWidth = safeSize;
        layout.preferredWidth = safeSize;
        layout.minHeight = safeSize;
        layout.preferredHeight = safeSize;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    private TMP_Text EnsureText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize)
    {
        Transform existing =
            parent.Find(objectName);

        TextMeshProUGUI text =
            existing != null
                ? existing.GetComponent<TextMeshProUGUI>()
                : null;

        if (text == null)
        {
            GameObject textObject =
                CreateUiObject(
                    objectName,
                    parent,
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        SetRect(
            text.rectTransform,
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);

        if (fontAsset != null)
            text.font = fontAsset;

        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = Color.white;

        return text;
    }

    private RectTransform EnsureRow(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        Transform existing =
            parent.Find(objectName);

        RectTransform row =
            existing as RectTransform;

        if (row == null)
        {
            GameObject rowObject =
                CreateUiObject(
                    objectName,
                    parent,
                    typeof(HorizontalLayoutGroup));

            row = rowObject.GetComponent<RectTransform>();
        }

        SetRect(
            row,
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);

        HorizontalLayoutGroup layout =
            row.GetComponent<HorizontalLayoutGroup>();

        if (layout == null)
            layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();

        ApplyRowLayout(
            row);

        return row;
    }

    private RectTransform FindRect(
        string objectName)
    {
        Transform found =
            transform.Find(objectName);

        return found as RectTransform;
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent,
        params System.Type[] components)
    {
        GameObject target =
            new(
                objectName,
                typeof(RectTransform));

        target.transform.SetParent(parent, false);

        if (components != null)
        {
            foreach (System.Type componentType
                     in components)
            {
                if (componentType == null ||
                    componentType == typeof(RectTransform) ||
                    target.GetComponent(componentType) != null)
                {
                    continue;
                }

                target.AddComponent(componentType);
            }
        }

        return target;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private float GetResolverRevealDuration()
    {
        return
            Mathf.Max(
                0.12f,
                Mathf.Max(
                    resolverRevealDuration,
                    noiseDuration));
    }

    private static IEnumerator WaitUnscaled(
        float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += BattlePlaybackSpeedController.BattleUnscaledDeltaTime;
            yield return null;
        }
    }

    private static void DestroyObject(
        GameObject target)
    {
        if (target == null)
            return;

        if (target.activeSelf)
            target.SetActive(false);

        if (Application.isPlaying)
            Object.Destroy(target);
        else
            Object.DestroyImmediate(target);
    }
}
