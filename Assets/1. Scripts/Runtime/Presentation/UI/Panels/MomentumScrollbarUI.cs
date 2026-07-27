using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField, Min(0f)] private float exchangeAnimateDuration = 0.18f;

    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool logMomentumAnimation = true;

    private bool isLocked;
    private bool isInlineAnimating;
    private float displayedMomentum;
    private Coroutine animateRoutine;

    public float DisplayedMomentum => displayedMomentum;

    private void Awake()
    {
        ResolveReferences();
        NormalizeRange();

        if (momentumSlider != null)
        {
            momentumSlider.minValue = 0f;
            momentumSlider.maxValue = 1f;
        }

        ForceRefresh();
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

        float real = GetRealMomentum();

        if (Mathf.Approximately(real, displayedMomentum))
            return;

        displayedMomentum = real;
        ApplySlider(displayedMomentum);
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
        isLocked = false;
        isInlineAnimating = false;
        displayedMomentum = GetRealMomentum();
        ApplySlider(displayedMomentum);
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

            float eased = Mathf.SmoothStep(0f, 1f, t);

            displayedMomentum = Mathf.Lerp(from, to, eased);
            ApplySlider(displayedMomentum);

            yield return null;
        }

        displayedMomentum = to;
        ApplySlider(displayedMomentum);
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (momentumSlider == null)
            momentumSlider = GetComponent<Slider>();
    }

    private void NormalizeRange()
    {
        if (maxMomentum > minMomentum)
            return;

        maxMomentum = minMomentum + 1f;
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

        float normalized = Mathf.InverseLerp(
            minMomentum,
            maxMomentum,
            momentum);

        if (!playerAdvantageIsRight)
            normalized = 1f - normalized;

        momentumSlider.SetValueWithoutNotify(normalized);
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
