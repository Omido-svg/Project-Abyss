using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gameplay v5 기세 표시.
/// Slider 값은 0~1로 유지하고, 실제 -100~100 값은 중앙 0 기준 델타 바와
/// -70 / -30 / +30 / +70 구간 마커로 시각화한다.
/// </summary>
public sealed class MomentumScrollbarUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Slider momentumSlider;

    [Header("Display")]
    [SerializeField] private bool playerAdvantageIsRight = true;

    [Header("Range")]
    [SerializeField] private float minMomentum = -100f;
    [SerializeField] private float maxMomentum = 100f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float animateDuration = 0.35f;

    [Tooltip("합의 각 굴림 결과가 확정될 때 사용하는 짧은 이동 시간입니다.")]
    [SerializeField, Min(0f)] private float exchangeAnimateDuration = 0.24f;

    [Tooltip("기세 이동 중 현재 위치 마커가 살짝 커졌다가 복귀하는 강조량입니다.")]
    [SerializeField, Range(1f, 2f)] private float cursorMovePulseScale = 1.22f;

    [Tooltip("목표 기세에 도착했을 때 마커가 한 번 더 튀는 강조량입니다.")]
    [SerializeField, Range(1f, 2f)] private float cursorArrivalPulseScale = 1.34f;

    [SerializeField, Min(0f)] private float cursorArrivalPulseDuration = 0.10f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Gameplay v5 Visual")]
    [SerializeField] private bool showGameplayV5Zones = true;
    [SerializeField] private Color disadvantageColor =
        new Color(0.92f, 0.28f, 0.30f, 0.88f);
    [SerializeField] private Color balanceColor =
        new Color(0.72f, 0.76f, 0.84f, 0.78f);
    [SerializeField] private Color advantageColor =
        new Color(0.30f, 0.72f, 1f, 0.90f);
    [SerializeField] private Color overwhelmColor =
        new Color(1f, 0.76f, 0.22f, 0.95f);

    [Header("Debug")]
    [SerializeField] private bool logMomentumAnimation = true;

    private bool isLocked;
    private bool isInlineAnimating;
    private float displayedMomentum;
    private Coroutine animateRoutine;

    private RectTransform visualRoot;
    private RectTransform deltaFillRect;
    private Image deltaFillImage;
    private RectTransform cursorRect;
    private Image cursorImage;
    private RectTransform cursorMarkerRect;
    private Image cursorMarkerImage;
    private RectTransform cursorBadgeRect;
    private Image cursorBadgeImage;
    private TMP_Text cursorValueText;
    private readonly List<RectTransform> zoneRects = new();
    private readonly List<RectTransform> markerRects = new();
    private Image legacyFillImage;

    public float DisplayedMomentum => displayedMomentum;
    public int DisplayedMomentumRounded => Mathf.RoundToInt(displayedMomentum);
    public string DisplayedStateLabel => ResolveMomentumZoneLabel(displayedMomentum);

    private void Awake()
    {
        ResolveReferences();
        NormalizeRange();
        ConfigureSliderContract();
        EnsureGameplayV5Visuals();
        ForceRefresh();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ConfigureSliderContract();
        EnsureGameplayV5Visuals();
        UpdateStaticVisuals();
    }

    private void OnDisable()
    {
        StopAnimation();
        isLocked = false;
        isInlineAnimating = false;
    }

    private void Update()
    {
        if (isLocked ||
            isInlineAnimating ||
            animateRoutine != null)
        {
            return;
        }

        // 정상 경로에서는 AnimationDirector가 isLocked를 잡고 교환별 timeline을 재생한다.
        // 잠금이 없는 상태까지 IsResolving만 보고 막아버리면 UI 참조가 한 번 어긋났을 때
        // 기세바가 전투 내내 0에 고정되므로, 잠금이 없으면 실제 값 변화라도 애니메이션한다.
        float real = GetRealMomentum();

        if (Mathf.Approximately(real, displayedMomentum))
            return;

        // Presentation Timeline 밖에서 값이 바뀌는 경우(턴 시작 0 리셋, Debug/Skill 이동 등)도
        // 즉시 점프시키지 않고 같은 기세 이동 애니메이션을 사용한다.
        animateRoutine = StartCoroutine(
            AnimateUnlockedToRoutine(real));
    }

    public void LockCurrentDisplay()
    {
        StopAnimation();

        displayedMomentum =
            GetDisplayedMomentumFromSlider();

        isLocked = true;

        LogAnimation(
            "Lock Current",
            displayedMomentum,
            GetRealMomentum());
    }

    /// <summary>
    /// 전투 계산은 연출 전에 끝나므로 실제 MomentumManager 값은 이미 최종값일 수 있다.
    /// 연속 합 시작 시 계산 전 스냅샷으로 표시를 되돌리고 잠가,
    /// 각 교환의 기록을 순서대로 재생할 수 있게 한다.
    /// </summary>
    public void LockDisplayAt(
        float momentum)
    {
        StopAnimation();

        isLocked = true;
        displayedMomentum = ClampMomentum(momentum);
        ApplySlider(displayedMomentum);

        LogAnimation(
            "Lock Snapshot",
            displayedMomentum,
            GetRealMomentum());
    }

    /// <summary>
    /// 합의 한 굴림/교환이 화면에서 끝난 직후 호출한다.
    /// 잠금은 유지하면서 해당 교환의 MomentumAfter까지 이동한다.
    /// </summary>
    public IEnumerator AnimateLockedDisplayToRoutine(
        float targetMomentum)
    {
        StopAnimation();

        isLocked = true;
        isInlineAnimating = true;

        float from = displayedMomentum;
        float to = ClampMomentum(targetMomentum);

        LogAnimation(
            "Exchange Step",
            from,
            to);

        yield return AnimateMomentumInternal(
            from,
            to,
            exchangeAnimateDuration);

        isInlineAnimating = false;
        isLocked = true;
    }

    public void ReleaseAndAnimateToRealMomentum()
    {
        StopAnimation();
        animateRoutine = StartCoroutine(
            ReleaseRoutineWrapper());
    }

    public IEnumerator ReleaseAndAnimateToRealMomentumRoutine()
    {
        StopAnimation();

        isLocked = false;
        isInlineAnimating = true;

        float from = displayedMomentum;
        float to = GetRealMomentum();

        LogAnimation(
            "Release Routine",
            from,
            to);

        yield return AnimateMomentumInternal(
            from,
            to,
            animateDuration);

        isInlineAnimating = false;
        isLocked = false;
    }

    public void ForceRefresh()
    {
        StopAnimation();
        ResolveReferences();
        ConfigureSliderContract();
        EnsureGameplayV5Visuals();
        UpdateStaticVisuals();

        isLocked = false;
        isInlineAnimating = false;
        displayedMomentum = GetRealMomentum();
        ApplySlider(displayedMomentum);
    }

    private IEnumerator AnimateUnlockedToRoutine(
        float targetMomentum)
    {
        isInlineAnimating = true;

        float from = displayedMomentum;
        float to = ClampMomentum(targetMomentum);

        LogAnimation(
            "Live Change",
            from,
            to);

        yield return AnimateMomentumInternal(
            from,
            to,
            animateDuration);

        isInlineAnimating = false;
        animateRoutine = null;
    }

    private IEnumerator ReleaseRoutineWrapper()
    {
        isLocked = false;
        isInlineAnimating = true;

        float from = displayedMomentum;
        float to = GetRealMomentum();

        LogAnimation(
            "Release",
            from,
            to);

        yield return AnimateMomentumInternal(
            from,
            to,
            animateDuration);

        isInlineAnimating = false;
        isLocked = false;
        animateRoutine = null;
    }

    private IEnumerator AnimateMomentumInternal(
        float from,
        float to,
        float duration)
    {
        if (duration <= 0f ||
            Mathf.Approximately(from, to))
        {
            displayedMomentum = to;
            ApplySlider(displayedMomentum);
            SetCursorPresentationScale(1f);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration);

            // 위치는 빠르게 반응하고 끝에서 부드럽게 멈춘다.
            float eased =
                1f - Mathf.Pow(1f - t, 3f);

            displayedMomentum = Mathf.Lerp(from, to, eased);
            ApplySlider(displayedMomentum);

            // 이동 자체가 눈에 들어오도록 마커/값 배지를 동시에 펄스시킨다.
            float travelPulse =
                1f +
                (cursorMovePulseScale - 1f) *
                Mathf.Sin(Mathf.PI * t);
            SetCursorPresentationScale(travelPulse);

            yield return null;
        }

        displayedMomentum = to;
        ApplySlider(displayedMomentum);
        SetCursorPresentationScale(1f);

        yield return PlayCursorArrivalPulseRoutine();
    }

    private IEnumerator PlayCursorArrivalPulseRoutine()
    {
        if (cursorArrivalPulseDuration <= 0f ||
            cursorArrivalPulseScale <= 1f)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < cursorArrivalPulseDuration)
        {
            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / cursorArrivalPulseDuration);
            float pulse =
                1f +
                (cursorArrivalPulseScale - 1f) *
                Mathf.Sin(Mathf.PI * t);

            SetCursorPresentationScale(pulse);
            yield return null;
        }

        SetCursorPresentationScale(1f);
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>(
                    FindObjectsInactive.Include);
        }

        if (momentumSlider == null)
            momentumSlider = GetComponent<Slider>();
    }

    private void ConfigureSliderContract()
    {
        if (momentumSlider == null)
            return;

        momentumSlider.minValue = 0f;
        momentumSlider.maxValue = 1f;
        momentumSlider.wholeNumbers = false;
        momentumSlider.interactable = false;

        if (momentumSlider.fillRect != null)
        {
            legacyFillImage =
                momentumSlider.fillRect.GetComponent<Image>();

            if (legacyFillImage != null)
            {
                Color color = legacyFillImage.color;
                color.a = 0f;
                legacyFillImage.color = color;
                legacyFillImage.raycastTarget = false;
            }
        }
    }

    private void NormalizeRange()
    {
        if (maxMomentum > minMomentum)
            return;

        maxMomentum = minMomentum + 1f;
    }

    private void EnsureGameplayV5Visuals()
    {
        if (!showGameplayV5Zones ||
            momentumSlider == null ||
            visualRoot != null)
        {
            return;
        }

        Transform existing =
            momentumSlider.transform.Find("GameplayV5Visuals");

        if (existing != null)
        {
            visualRoot = existing as RectTransform;
            CacheGameplayV5Visuals(existing);
            UpdateStaticVisuals();
            ApplySlider(displayedMomentum);
            return;
        }

        visualRoot = CreateRect(
            momentumSlider.transform,
            "GameplayV5Visuals",
            Vector2.zero,
            Vector2.one);
        visualRoot.offsetMin = Vector2.zero;
        visualRoot.offsetMax = Vector2.zero;
        visualRoot.SetAsLastSibling();

        // 5개 상태 구간: 짓눌림 / 열세 / 균형 / 우세 / 짓누름.
        for (int i = 0; i < 5; i++)
        {
            RectTransform zone = CreateImageRect(
                visualRoot,
                $"Zone_{i}");
            zoneRects.Add(zone);
        }

        for (int i = 0; i < 5; i++)
        {
            RectTransform marker = CreateImageRect(
                visualRoot,
                $"Threshold_{i}");
            markerRects.Add(marker);
        }

        deltaFillRect = CreateImageRect(
            visualRoot,
            "MomentumDelta");
        deltaFillImage =
            deltaFillRect.GetComponent<Image>();

        cursorRect = CreateImageRect(
            visualRoot,
            "MomentumCursor");
        cursorImage =
            cursorRect.GetComponent<Image>();

        // 중앙 0선과 실제 현재 위치가 겹쳐도 확실히 구분되도록
        // 다이아몬드 마커 + 숫자 배지를 별도 레이어로 만든다.
        cursorMarkerRect = CreateImageRect(
            visualRoot,
            "MomentumCursorMarker");
        cursorMarkerImage =
            cursorMarkerRect.GetComponent<Image>();
        cursorMarkerRect.sizeDelta = new Vector2(10f, 10f);
        cursorMarkerRect.localEulerAngles =
            new Vector3(0f, 0f, 45f);

        cursorBadgeRect = CreateImageRect(
            visualRoot,
            "MomentumCursorBadge");
        cursorBadgeImage =
            cursorBadgeRect.GetComponent<Image>();
        cursorBadgeRect.sizeDelta = new Vector2(48f, 20f);
        cursorBadgeImage.color =
            new Color(0.015f, 0.020f, 0.030f, 0.96f);

        cursorValueText = CreateText(
            cursorBadgeRect,
            "Value",
            14f);
        cursorValueText.alignment =
            TextAlignmentOptions.Center;

        UpdateStaticVisuals();
    }

    private void CacheGameplayV5Visuals(
        Transform existing)
    {
        zoneRects.Clear();
        markerRects.Clear();

        for (int i = 0; i < 5; i++)
        {
            RectTransform zone =
                existing.Find($"Zone_{i}") as RectTransform;
            if (zone != null)
                zoneRects.Add(zone);

            RectTransform marker =
                existing.Find($"Threshold_{i}") as RectTransform;
            if (marker != null)
                markerRects.Add(marker);
        }

        deltaFillRect =
            existing.Find("MomentumDelta") as RectTransform;
        deltaFillImage =
            deltaFillRect != null
                ? deltaFillRect.GetComponent<Image>()
                : null;

        cursorRect =
            existing.Find("MomentumCursor") as RectTransform;
        cursorImage =
            cursorRect != null
                ? cursorRect.GetComponent<Image>()
                : null;

        cursorMarkerRect =
            existing.Find("MomentumCursorMarker") as RectTransform;
        cursorMarkerImage =
            cursorMarkerRect != null
                ? cursorMarkerRect.GetComponent<Image>()
                : null;

        cursorBadgeRect =
            existing.Find("MomentumCursorBadge") as RectTransform;
        cursorBadgeImage =
            cursorBadgeRect != null
                ? cursorBadgeRect.GetComponent<Image>()
                : null;
        cursorValueText =
            cursorBadgeRect != null
                ? cursorBadgeRect.Find("Value")?.GetComponent<TMP_Text>()
                : null;

        if (cursorMarkerRect == null)
        {
            cursorMarkerRect = CreateImageRect(
                visualRoot,
                "MomentumCursorMarker");
            cursorMarkerImage =
                cursorMarkerRect.GetComponent<Image>();
            cursorMarkerRect.sizeDelta =
                new Vector2(10f, 10f);
            cursorMarkerRect.localEulerAngles =
                new Vector3(0f, 0f, 45f);
        }

        if (cursorBadgeRect == null)
        {
            cursorBadgeRect = CreateImageRect(
                visualRoot,
                "MomentumCursorBadge");
            cursorBadgeImage =
                cursorBadgeRect.GetComponent<Image>();
            cursorBadgeRect.sizeDelta =
                new Vector2(48f, 20f);
        }

        if (cursorBadgeImage != null)
        {
            cursorBadgeImage.color =
                new Color(0.015f, 0.020f, 0.030f, 0.96f);
        }

        if (cursorValueText == null &&
            cursorBadgeRect != null)
        {
            cursorValueText = CreateText(
                cursorBadgeRect,
                "Value",
                14f);
            cursorValueText.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private void UpdateStaticVisuals()
    {
        if (visualRoot == null ||
            zoneRects.Count != 5 ||
            markerRects.Count != 5)
        {
            return;
        }

        MomentumRuleSettings rules =
            battleManager?.BattleRules?.Momentum;

        float lastStand = rules?.LastStandThreshold ?? -70f;
        float disadvantage = rules?.DisadvantageThreshold ?? -30f;
        float advantage = rules?.AdvantageThreshold ?? 30f;
        float overwhelm = rules?.OverwhelmThreshold ?? 70f;

        float[] boundaries =
        {
            minMomentum,
            lastStand,
            disadvantage,
            advantage,
            overwhelm,
            maxMomentum
        };

        Color[] colors =
        {
            WithAlpha(disadvantageColor, 0.28f),
            WithAlpha(disadvantageColor, 0.16f),
            WithAlpha(balanceColor, 0.12f),
            WithAlpha(advantageColor, 0.16f),
            WithAlpha(overwhelmColor, 0.26f)
        };

        for (int i = 0; i < zoneRects.Count; i++)
        {
            float a = ToDisplayNormalized(boundaries[i]);
            float b = ToDisplayNormalized(boundaries[i + 1]);

            RectTransform zone = zoneRects[i];
            zone.anchorMin = new Vector2(Mathf.Min(a, b), 0.12f);
            zone.anchorMax = new Vector2(Mathf.Max(a, b), 0.88f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;

            Image image = zone.GetComponent<Image>();
            if (image != null)
                image.color = colors[i];
        }

        float[] markers =
        {
            lastStand,
            disadvantage,
            0f,
            advantage,
            overwhelm
        };

        for (int i = 0; i < markerRects.Count; i++)
        {
            float x = ToDisplayNormalized(markers[i]);
            RectTransform marker = markerRects[i];
            marker.anchorMin = new Vector2(x, 0.05f);
            marker.anchorMax = new Vector2(x, 0.95f);
            marker.anchoredPosition = Vector2.zero;
            marker.sizeDelta = new Vector2(i == 2 ? 3f : 1.5f, 0f);

            Image image = marker.GetComponent<Image>();
            if (image != null)
            {
                image.color = i == 2
                    ? new Color(1f, 1f, 1f, 0.88f)
                    : new Color(1f, 1f, 1f, 0.45f);
            }
        }
    }

    private void StopAnimation()
    {
        if (animateRoutine == null)
            return;

        StopCoroutine(animateRoutine);
        animateRoutine = null;
    }

    private float GetRealMomentum()
    {
        ResolveReferences();

        return battleManager?.MomentumManager == null
            ? 0f
            : battleManager.MomentumManager.CurrentMomentum;
    }

    private float ClampMomentum(
        float momentum)
    {
        return Mathf.Clamp(
            momentum,
            minMomentum,
            maxMomentum);
    }

    private void ApplySlider(float momentum)
    {
        if (momentumSlider == null)
            return;

        ConfigureSliderContract();
        EnsureGameplayV5Visuals();

        float normalized = ToDisplayNormalized(momentum);
        momentumSlider.SetValueWithoutNotify(normalized);

        if (deltaFillRect != null)
        {
            float center = ToDisplayNormalized(0f);
            float min = Mathf.Min(center, normalized);
            float max = Mathf.Max(center, normalized);

            deltaFillRect.anchorMin = new Vector2(min, 0.24f);
            deltaFillRect.anchorMax = new Vector2(max, 0.76f);
            deltaFillRect.offsetMin = Vector2.zero;
            deltaFillRect.offsetMax = Vector2.zero;

            if (deltaFillImage != null)
                deltaFillImage.color = ResolveMomentumColor(momentum);
        }

        Color currentColor =
            ResolveMomentumColor(momentum);

        if (cursorRect != null)
        {
            cursorRect.anchorMin = new Vector2(normalized, 0.02f);
            cursorRect.anchorMax = new Vector2(normalized, 0.98f);
            cursorRect.anchoredPosition = Vector2.zero;
            cursorRect.sizeDelta = new Vector2(3f, 0f);

            if (cursorImage != null)
                cursorImage.color =
                    new Color(1f, 1f, 1f, 0.92f);
        }

        if (cursorMarkerRect != null)
        {
            cursorMarkerRect.anchorMin =
                new Vector2(normalized, 0.5f);
            cursorMarkerRect.anchorMax =
                new Vector2(normalized, 0.5f);
            cursorMarkerRect.anchoredPosition =
                Vector2.zero;

            if (cursorMarkerImage != null)
                cursorMarkerImage.color = currentColor;
        }

        if (cursorBadgeRect != null)
        {
            float badgeNormalized =
                Mathf.Clamp(normalized, 0.075f, 0.925f);

            cursorBadgeRect.anchorMin =
                new Vector2(badgeNormalized, 1f);
            cursorBadgeRect.anchorMax =
                new Vector2(badgeNormalized, 1f);
            cursorBadgeRect.anchoredPosition =
                new Vector2(0f, 13f);
        }

        if (cursorBadgeImage != null)
        {
            Color badgeColor = Color.Lerp(
                new Color(0.015f, 0.020f, 0.030f, 0.96f),
                currentColor,
                0.34f);
            badgeColor.a = 0.96f;
            cursorBadgeImage.color = badgeColor;
        }

        if (cursorValueText != null)
        {
            int rounded = Mathf.RoundToInt(momentum);
            cursorValueText.text =
                rounded > 0
                    ? $"+{rounded}"
                    : rounded.ToString();
            cursorValueText.color = Color.white;
        }
    }

    private void SetCursorPresentationScale(
        float scale)
    {
        float safe = Mathf.Max(0.01f, scale);

        if (cursorMarkerRect != null)
            cursorMarkerRect.localScale =
                Vector3.one * safe;

        if (cursorBadgeRect != null)
            cursorBadgeRect.localScale =
                Vector3.one * Mathf.Lerp(1f, safe, 0.72f);
    }

    private float GetDisplayedMomentumFromSlider()
    {
        if (momentumSlider == null)
            return displayedMomentum;

        float normalized = momentumSlider.value;

        if (!playerAdvantageIsRight)
            normalized = 1f - normalized;

        return Mathf.Lerp(
            minMomentum,
            maxMomentum,
            normalized);
    }

    private float ToDisplayNormalized(float momentum)
    {
        float normalized = Mathf.InverseLerp(
            minMomentum,
            maxMomentum,
            ClampMomentum(momentum));

        if (!playerAdvantageIsRight)
            normalized = 1f - normalized;

        return normalized;
    }

    private Color ResolveMomentumColor(float momentum)
    {
        MomentumRuleSettings rules =
            battleManager?.BattleRules?.Momentum;

        float lastStand =
            rules?.LastStandThreshold ?? -70f;
        float disadvantage =
            rules?.DisadvantageThreshold ?? -30f;
        float advantage =
            rules?.AdvantageThreshold ?? 30f;
        float overwhelm =
            rules?.OverwhelmThreshold ?? 70f;

        if (momentum <= lastStand)
            return disadvantageColor;
        if (momentum < disadvantage)
            return disadvantageColor;
        if (momentum <= advantage)
            return balanceColor;
        if (momentum < overwhelm)
            return advantageColor;
        return overwhelmColor;
    }

    private string ResolveMomentumZoneLabel(
        float momentum)
    {
        MomentumRuleSettings rules =
            battleManager?.BattleRules?.Momentum;

        float lastStand =
            rules?.LastStandThreshold ?? -70f;
        float disadvantage =
            rules?.DisadvantageThreshold ?? -30f;
        float advantage =
            rules?.AdvantageThreshold ?? 30f;
        float overwhelm =
            rules?.OverwhelmThreshold ?? 70f;

        if (momentum <= lastStand)
            return "짓눌림";
        if (momentum < disadvantage)
            return "열세";
        if (momentum <= advantage)
            return "균형";
        if (momentum < overwhelm)
            return "우세";
        return "짓누름";
    }

    private static RectTransform CreateRect(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static RectTransform CreateImageRect(
        Transform parent,
        string name)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect =
            go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text text =
            go.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.overflowMode =
            TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.richText = true;
        text.color = Color.white;
        return text;
    }

    private static Color WithAlpha(
        Color color,
        float alpha)
    {
        color.a = alpha;
        return color;
    }

    private void LogAnimation(
        string label,
        float from,
        float to)
    {
        if (!logMomentumAnimation)
            return;

        Debug.Log(
            $"[MomentumScrollbarUI] {label} / " +
            $"From={from}, To={to}, Real={GetRealMomentum()}");
    }
}