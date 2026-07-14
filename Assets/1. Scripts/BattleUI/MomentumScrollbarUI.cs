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
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool logMomentumAnimation = true;

    private bool isLocked;
    private float displayedMomentum;
    private Coroutine animateRoutine;

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
    }

    private void Update()
    {
        if (isLocked || animateRoutine != null)
            return;

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

        if (logMomentumAnimation)
        {
            Debug.Log(
                $"[MomentumScrollbarUI] Lock / " +
                $"Displayed={displayedMomentum}, Real={GetRealMomentum()}");
        }
    }

    public void ReleaseAndAnimateToRealMomentum()
    {
        StopAnimation();
        animateRoutine = StartCoroutine(
            ReleaseAndAnimateToRealMomentumRoutine());
    }

    public IEnumerator ReleaseAndAnimateToRealMomentumRoutine()
    {
        isLocked = false;

        float from = displayedMomentum;
        float to = GetRealMomentum();

        if (logMomentumAnimation)
        {
            Debug.Log(
                $"[MomentumScrollbarUI] Release Routine / " +
                $"From={from}, To={to}");
        }

        yield return AnimateMomentum(from, to);
    }

    public void ForceRefresh()
    {
        StopAnimation();
        isLocked = false;
        displayedMomentum = GetRealMomentum();
        ApplySlider(displayedMomentum);
    }

    private IEnumerator AnimateMomentum(float from, float to)
    {
        if (animateDuration <= 0f ||
            Mathf.Approximately(from, to))
        {
            displayedMomentum = to;
            ApplySlider(displayedMomentum);
            animateRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < animateDuration)
        {
            elapsed += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / animateDuration);

            float eased = Mathf.SmoothStep(0f, 1f, t);

            displayedMomentum = Mathf.Lerp(from, to, eased);
            ApplySlider(displayedMomentum);

            yield return null;
        }

        displayedMomentum = to;
        ApplySlider(displayedMomentum);
        animateRoutine = null;
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
}
