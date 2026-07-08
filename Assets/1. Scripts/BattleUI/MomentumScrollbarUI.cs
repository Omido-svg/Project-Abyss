using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MomentumScrollbarUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private Slider momentumSlider;

    [Header("Display")]
    [SerializeField] private bool playerAdvantageIsRight = true;

    [Header("Range")]
    [SerializeField] private float minMomentum = -100f;
    [SerializeField] private float maxMomentum = 100f;

    [Header("Animation")]
    [SerializeField] private float animateDuration = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logMomentumAnimation = true;

    private bool isLocked;
    private float displayedMomentum;
    private Coroutine animateRoutine;

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (momentumSlider == null)
            momentumSlider = GetComponent<Slider>();

        if (momentumSlider != null)
        {
            momentumSlider.minValue = 0f;
            momentumSlider.maxValue = 1f;
        }

        displayedMomentum = GetRealMomentum();
        ApplySlider(displayedMomentum);
    }

    private void Update()
    {
        if (isLocked)
            return;

        if (animateRoutine != null)
            return;

        displayedMomentum = GetRealMomentum();
        ApplySlider(displayedMomentum);
    }

    public void LockCurrentDisplay()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        displayedMomentum =
            GetDisplayedMomentumFromSlider();

        isLocked = true;

        if (logMomentumAnimation)
        {
            Debug.Log(
                $"[MomentumScrollbarUI] Lock / Displayed={displayedMomentum}, Real={GetRealMomentum()}");
        }
    }

    public void ReleaseAndAnimateToRealMomentum()
    {
        if (animateRoutine != null)
            StopCoroutine(animateRoutine);

        animateRoutine =
            StartCoroutine(
                ReleaseAndAnimateToRealMomentumRoutine());
    }

    public IEnumerator ReleaseAndAnimateToRealMomentumRoutine()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        isLocked = false;

        float from =
            displayedMomentum;

        float to =
            GetRealMomentum();

        if (logMomentumAnimation)
        {
            Debug.Log(
                $"[MomentumScrollbarUI] Release Routine / From={from}, To={to}");
        }

        yield return AnimateMomentum(
            from,
            to);
    }

    public void ForceRefresh()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        isLocked = false;

        displayedMomentum = GetRealMomentum();
        ApplySlider(displayedMomentum);
    }

    private IEnumerator AnimateMomentum(
        float from,
        float to)
    {
        if (Mathf.Approximately(from, to))
        {
            displayedMomentum = to;
            ApplySlider(displayedMomentum);

            animateRoutine = null;

            yield break;
        }

        float elapsed = 0f;

        while (elapsed < animateDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / animateDuration);

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t);

            displayedMomentum =
                Mathf.Lerp(
                    from,
                    to,
                    eased);

            ApplySlider(displayedMomentum);

            yield return null;
        }

        displayedMomentum = to;
        ApplySlider(displayedMomentum);

        animateRoutine = null;
    }

    private float GetRealMomentum()
    {
        if (battleManager == null)
            return 0f;

        if (battleManager.MomentumManager == null)
            return 0f;

        return battleManager.MomentumManager.CurrentMomentum;
    }

    private void ApplySlider(
        float momentum)
    {
        if (momentumSlider == null)
            return;

        float normalized =
            Mathf.InverseLerp(
                minMomentum,
                maxMomentum,
                momentum);

        if (!playerAdvantageIsRight)
            normalized = 1f - normalized;

        momentumSlider.value = normalized;
    }

    private float GetDisplayedMomentumFromSlider()
    {
        if (momentumSlider == null)
            return displayedMomentum;

        float normalized =
            momentumSlider.value;

        if (!playerAdvantageIsRight)
            normalized = 1f - normalized;

        return Mathf.Lerp(
            minMomentum,
            maxMomentum,
            normalized);
    }
}