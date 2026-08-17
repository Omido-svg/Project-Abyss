using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleWorldCharacterPlateUI : MonoBehaviour
{
    [SerializeField] private Character character;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image hpFill;
    [SerializeField] private Image hpDamageTrail;

    [Header("Follow")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 followLocalPosition;

    [Header("Camera Alignment")]
    [Tooltip("체력바 본체(높이 92 UI unit)가 화면에서 차지할 목표 픽셀 높이입니다. 슬롯/고유 게이지도 같은 비율로 따라갑니다.")]
    [SerializeField, Min(24f)] private float targetBodyHeightPixels = 64f;

    [Tooltip("Orthographic Overview에서는 UI도 캐릭터와 같은 비율로 확대/축소되도록 고정 World Scale을 사용합니다.")]
    [SerializeField] private bool proportionalOrthographicScale = true;
    [SerializeField, Min(0.0001f)] private float orthographicWorldScale = 0.0062f;

    [SerializeField, Min(0.0001f)] private float minimumWorldScale = 0.001f;
    [SerializeField, Min(0.0001f)] private float maximumWorldScale = 0.05f;

    [Header("HP Bar Damage Animation")]
    [SerializeField] private bool animateHpChanges = true;
    [SerializeField, Min(0.01f)] private float minimumFrontDrainDuration = 0.09f;
    [SerializeField, Min(0.01f)] private float maximumFrontDrainDuration = 0.28f;
    [SerializeField, Min(0f)] private float minimumTrailHoldDuration = 0.06f;
    [SerializeField, Min(0f)] private float maximumTrailHoldDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float minimumTrailDrainDuration = 0.20f;
    [SerializeField, Min(0.01f)] private float maximumTrailDrainDuration = 0.82f;
    [SerializeField, Min(0.01f)] private float recoveryDuration = 0.22f;

    private bool hasHpOverride;
    private int hpOverride;
    private RectTransform hpFillRect;
    private RectTransform hpDamageTrailRect;
    private Coroutine hpAnimationRoutine;
    private bool hpVisualInitialized;
    private float requestedHpRatio = -1f;
    private float displayedHpRatio = 1f;
    private float displayedTrailRatio = 1f;
    private bool hideWhenAnimationCompletes;
    private RectTransform selfRect;
    private bool orthographicScaleCaptured;

    public void Configure(
        Character target,
        Camera camera,
        TMP_Text displayName,
        TMP_Text hpValue,
        Image fill,
        Transform movementTarget,
        Vector3 localFollowPosition)
    {
        Configure(
            target,
            camera,
            displayName,
            hpValue,
            fill,
            null,
            movementTarget,
            localFollowPosition);
    }

    public void Configure(
        Character target,
        Camera camera,
        TMP_Text displayName,
        TMP_Text hpValue,
        Image fill,
        Image damageTrail,
        Transform movementTarget,
        Vector3 localFollowPosition)
    {
        character = target;
        targetCamera = camera;
        selfRect = transform as RectTransform;
        nameText = displayName;
        hpText = hpValue;
        hpFill = fill;
        hpDamageTrail = damageTrail;
        followTarget = movementTarget;
        followLocalPosition = localFollowPosition;
        CaptureOrthographicWorldScale();

        ConfigureText(nameText, 29f, TextAlignmentOptions.Left);
        ConfigureText(hpText, 24f, TextAlignmentOptions.Right);
        ConfigureHpBars();
        EnsureSupplementalHud();
        ApplyFollowPosition();
        Refresh(forceImmediate: true);
    }

    public void SetHpOverride(
        int hp,
        bool forceImmediate = false)
    {
        hasHpOverride = true;
        hpOverride = Mathf.Max(0, hp);
        Refresh(
            forceImmediate,
            snapFrontAtDamage: false);
    }

    public void SetHpOverrideAtHit(int hp)
    {
        hasHpOverride = true;
        hpOverride = Mathf.Max(0, hp);

        // 실제 Timeline Hit Event가 발생한 프레임에
        // 빨간 전면 바와 숫자는 즉시 목표 HP로 맞춘다.
        // 주황 잔상만 뒤늦게 따라오게 해 판정 시점과 표시 시점이 어긋나지 않게 한다.
        Refresh(
            forceImmediate: false,
            snapFrontAtDamage: true);
    }

    public void ClearHpOverride()
    {
        if (!hasHpOverride)
            return;

        hasHpOverride = false;
        Refresh();
    }

    public void RefreshNow()
    {
        Refresh();
    }

    private void Awake()
    {
        selfRect = transform as RectTransform;
        ConfigureText(nameText, 29f, TextAlignmentOptions.Left);
        ConfigureText(hpText, 24f, TextAlignmentOptions.Right);
        ConfigureHpBars();
        EnsureSupplementalHud();
        ResolveFollowTargetIfNeeded();
    }

    private void EnsureSupplementalHud()
    {
        BattleWorldActionSlotStripUI strip =
            GetComponent<BattleWorldActionSlotStripUI>();

        if (strip == null)
            strip = gameObject.AddComponent<BattleWorldActionSlotStripUI>();

        strip.Configure(character);

        BattleWorldUniqueGaugeUI gauges =
            GetComponent<BattleWorldUniqueGaugeUI>();

        if (gauges == null)
            gauges = gameObject.AddComponent<BattleWorldUniqueGaugeUI>();

        gauges.Configure(character);
    }

    private void OnDisable()
    {
        if (hpAnimationRoutine != null)
        {
            StopCoroutine(hpAnimationRoutine);
            hpAnimationRoutine = null;
        }
    }

    private void LateUpdate()
    {
        ResolveFollowTargetIfNeeded();
        ApplyFollowPosition();

        if (targetCamera == null ||
            !targetCamera.isActiveAndEnabled)
        {
            targetCamera = Camera.main;
        }

        ApplyCameraAlignment();
        Refresh();
    }

    private void ApplyCameraAlignment()
    {
        if (targetCamera == null)
            return;

        // 카메라의 up/forward를 그대로 사용해 월드 UI의 화면 수평/수직축을
        // 실제 렌더 카메라와 정확히 일치시킨다.
        transform.rotation = targetCamera.transform.rotation;

        selfRect ??= transform as RectTransform;
        if (selfRect == null || targetCamera.pixelHeight <= 0)
            return;

        if (targetCamera.orthographic &&
            proportionalOrthographicScale)
        {
            // Orthographic Overview에서는 "화면에서 항상 64px"을 유지하면
            // 카메라를 Zoom Out할수록 캐릭터만 작아지고 UI는 그대로 남아 서로 겹친다.
            // 고정 World Scale을 사용하면 캐릭터/슬롯/체력바가 동일한 비율로 축소/확대된다.
            ApplyWorldScale(
                Mathf.Clamp(
                    orthographicWorldScale,
                    minimumWorldScale,
                    Mathf.Max(
                        minimumWorldScale,
                        maximumWorldScale)));
            return;
        }

        float worldUnitsPerPixel;

        if (targetCamera.orthographic)
        {
            worldUnitsPerPixel =
                targetCamera.orthographicSize * 2f /
                Mathf.Max(1, targetCamera.pixelHeight);
        }
        else
        {
            float depth = Vector3.Dot(
                transform.position - targetCamera.transform.position,
                targetCamera.transform.forward);

            depth = Mathf.Max(0.05f, depth);

            float verticalWorldSize =
                2f * depth *
                Mathf.Tan(
                    targetCamera.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad);

            worldUnitsPerPixel =
                verticalWorldSize /
                Mathf.Max(1, targetCamera.pixelHeight);
        }

        float rectHeight =
            Mathf.Max(
                1f,
                selfRect.rect.height);

        float worldScale =
            Mathf.Clamp(
                worldUnitsPerPixel *
                targetBodyHeightPixels /
                rectHeight,
                minimumWorldScale,
                Mathf.Max(
                    minimumWorldScale,
                    maximumWorldScale));

        ApplyWorldScale(worldScale);
    }

    private void CaptureOrthographicWorldScale()
    {
        if (orthographicScaleCaptured)
            return;

        // Manager가 Configure 전에 canvasScale(기본 0.0062)을 이미 넣어둔다.
        // 이 값을 Overview 기준 World Scale로 보존한다.
        Vector3 lossy = transform.lossyScale;

        float candidate =
            Mathf.Max(
                Mathf.Abs(lossy.x),
                Mathf.Abs(lossy.y));

        if (candidate >= 0.0001f)
        {
            orthographicWorldScale = candidate;
            orthographicScaleCaptured = true;
        }
    }

    private void ApplyWorldScale(float worldScale)
    {
        Vector3 parentScale =
            transform.parent != null
                ? transform.parent.lossyScale
                : Vector3.one;

        transform.localScale = new Vector3(
            worldScale / SafeScaleAxis(parentScale.x),
            worldScale / SafeScaleAxis(parentScale.y),
            worldScale / SafeScaleAxis(parentScale.z));
    }

    private static float SafeScaleAxis(float value)
    {
        float magnitude = Mathf.Abs(value);
        return magnitude < 0.0001f ? 1f : magnitude;
    }

    private void ResolveFollowTargetIfNeeded()
    {
        if (followTarget != null ||
            character == null)
        {
            return;
        }

        CharacterActionMover mover =
            character.GetComponent<CharacterActionMover>();

        followTarget =
            mover != null && mover.VisualRoot != null
                ? mover.VisualRoot
                : character.transform;

        followLocalPosition =
            followTarget.InverseTransformPoint(
                transform.position);
    }

    private void ApplyFollowPosition()
    {
        if (followTarget == null)
            return;

        transform.position =
            followTarget.TransformPoint(
                followLocalPosition);
    }

    private void ConfigureHpBars()
    {
        ConfigureBarImage(
            hpDamageTrail,
            out hpDamageTrailRect);

        ConfigureBarImage(
            hpFill,
            out hpFillRect);
    }

    private static void ConfigureBarImage(
        Image image,
        out RectTransform rect)
    {
        if (image == null)
        {
            rect = null;
            return;
        }

        // Source Sprite가 없는 런타임 Image에서도 확실히 줄어들도록
        // fillAmount 대신 RectTransform의 오른쪽 Anchor를 직접 제어한다.
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.fillAmount = 1f;

        rect = image.rectTransform;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SetHealthVisualTarget(
        float ratio,
        bool forceImmediate,
        bool snapFrontAtDamage)
    {
        float clampedRatio = Mathf.Clamp01(ratio);

        if (!hpVisualInitialized ||
            forceImmediate ||
            !animateHpChanges)
        {
            StopHpAnimation();
            requestedHpRatio = clampedRatio;
            displayedHpRatio = clampedRatio;
            displayedTrailRatio = clampedRatio;
            hpVisualInitialized = true;
            ApplyBarRatios();
            TryHidePlateAfterAnimation();
            return;
        }

        if (Mathf.Approximately(requestedHpRatio, clampedRatio))
            return;

        float previousRequestedRatio =
            requestedHpRatio < 0f
                ? displayedHpRatio
                : requestedHpRatio;

        requestedHpRatio = clampedRatio;
        StopHpAnimation();

        if (clampedRatio < previousRequestedRatio)
        {
            float damageRatio =
                Mathf.Clamp01(
                    previousRequestedRatio - clampedRatio);

            hpAnimationRoutine =
                StartCoroutine(
                    AnimateDamageRoutine(
                        clampedRatio,
                        damageRatio,
                        snapFrontAtDamage));
            return;
        }

        hpAnimationRoutine =
            StartCoroutine(
                AnimateRecoveryRoutine(
                    clampedRatio));
    }

    private IEnumerator AnimateDamageRoutine(
        float targetRatio,
        float damageRatio,
        bool snapFrontAtDamage)
    {
        float startFrontRatio = displayedHpRatio;
        float startTrailRatio =
            Mathf.Max(
                displayedTrailRatio,
                startFrontRatio);

        // 최대 체력 대비 손실 비율이 클수록
        // 앞 바의 절삭 시간, 잔상 유지 시간, 잔상 소거 시간이 길어진다.
        // 위치/크기/회전은 건드리지 않고 오직 바의 오른쪽 끝만 이동한다.
        float impact =
            Mathf.Clamp01(
                damageRatio / 0.45f);

        float frontDuration =
            Mathf.Lerp(
                minimumFrontDrainDuration,
                maximumFrontDrainDuration,
                impact);

        float trailHoldDuration =
            Mathf.Lerp(
                minimumTrailHoldDuration,
                maximumTrailHoldDuration,
                impact);

        float trailDrainDuration =
            Mathf.Lerp(
                minimumTrailDrainDuration,
                maximumTrailDrainDuration,
                impact);

        float elapsed = 0f;

        if (snapFrontAtDamage)
        {
            // HP 판정은 이미 끝났더라도 화면에는 Hit Event 전까지 이전 HP를 유지한다.
            // Hit Event가 도착한 바로 그 프레임에 전면 바를 목표값으로 스냅한다.
            displayedHpRatio = targetRatio;
            displayedTrailRatio = startTrailRatio;
            ApplyBarRatios();
        }
        else
        {
            while (elapsed < frontDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float progress =
                    frontDuration <= 0f
                        ? 1f
                        : Mathf.Clamp01(
                            elapsed / frontDuration);

                displayedHpRatio =
                    Mathf.Lerp(
                        startFrontRatio,
                        targetRatio,
                        EaseOutCubic(progress));

                displayedTrailRatio = startTrailRatio;
                ApplyBarRatios();
                yield return null;
            }

            displayedHpRatio = targetRatio;
            displayedTrailRatio = startTrailRatio;
            ApplyBarRatios();
        }

        elapsed = 0f;

        while (elapsed < trailHoldDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < trailDrainDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress =
                trailDrainDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(
                        elapsed / trailDrainDuration);

            displayedTrailRatio =
                Mathf.Lerp(
                    startTrailRatio,
                    targetRatio,
                    EaseInOutCubic(progress));

            ApplyBarRatios();
            yield return null;
        }

        displayedHpRatio = targetRatio;
        displayedTrailRatio = targetRatio;
        ApplyBarRatios();

        hpAnimationRoutine = null;
        TryHidePlateAfterAnimation();
    }

    private IEnumerator AnimateRecoveryRoutine(
        float targetRatio)
    {
        float startFrontRatio = displayedHpRatio;
        float startTrailRatio = displayedTrailRatio;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, recoveryDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed / duration);

            float eased =
                EaseInOutCubic(progress);

            displayedHpRatio =
                Mathf.Lerp(
                    startFrontRatio,
                    targetRatio,
                    eased);

            displayedTrailRatio =
                Mathf.Lerp(
                    startTrailRatio,
                    targetRatio,
                    eased);

            ApplyBarRatios();
            yield return null;
        }

        displayedHpRatio = targetRatio;
        displayedTrailRatio = targetRatio;
        ApplyBarRatios();

        hpAnimationRoutine = null;
        TryHidePlateAfterAnimation();
    }

    private void StopHpAnimation()
    {
        if (hpAnimationRoutine == null)
            return;

        StopCoroutine(hpAnimationRoutine);
        hpAnimationRoutine = null;
    }

    private void ApplyBarRatios()
    {
        ApplyBarRatio(
            hpDamageTrail,
            ref hpDamageTrailRect,
            displayedTrailRatio);

        ApplyBarRatio(
            hpFill,
            ref hpFillRect,
            displayedHpRatio);
    }

    private static void ApplyBarRatio(
        Image image,
        ref RectTransform rect,
        float ratio)
    {
        if (image == null)
            return;

        if (rect == null ||
            rect != image.rectTransform)
        {
            ConfigureBarImage(
                image,
                out rect);
        }

        if (rect == null)
            return;

        float clampedRatio =
            Mathf.Clamp01(ratio);

        rect.anchorMin = Vector2.zero;
        rect.anchorMax =
            new Vector2(
                clampedRatio,
                1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.fillAmount = 1f;
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - Mathf.Clamp01(value);
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOutCubic(float value)
    {
        float clamped = Mathf.Clamp01(value);

        if (clamped < 0.5f)
            return 4f * clamped * clamped * clamped;

        float inverse = -2f * clamped + 2f;
        return 1f - inverse * inverse * inverse * 0.5f;
    }

    private static void ConfigureText(
        TMP_Text text,
        float size,
        TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.richText = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private void Refresh(
        bool forceImmediate = false,
        bool snapFrontAtDamage = false)
    {
        if (character == null)
            return;

        int maximum =
            Mathf.Max(
                1,
                character.MaxCombatHP);

        int current = hasHpOverride
            ? Mathf.Clamp(
                hpOverride,
                0,
                maximum)
            : Mathf.Clamp(
                character.CurrentHP,
                0,
                maximum);

        if (nameText != null)
        {
            nameText.text =
                character.Data?.CharacterName ??
                character.name;
        }

        if (hpText != null)
            hpText.text = $"{current} / {maximum}";

        hideWhenAnimationCompletes =
            !hasHpOverride &&
            character.IsDead &&
            current <= 0;

        if (!hideWhenAnimationCompletes &&
            !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        SetHealthVisualTarget(
            (float)current / maximum,
            forceImmediate,
            snapFrontAtDamage);
    }

    private void TryHidePlateAfterAnimation()
    {
        if (!hideWhenAnimationCompletes ||
            hpAnimationRoutine != null)
        {
            return;
        }

        gameObject.SetActive(false);
    }
}