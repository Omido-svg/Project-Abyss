using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleStatusRowUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    [Header("0922 Projection Motion")]
    [SerializeField, Min(0.2f)] private float currentDwellSeconds = 1.35f;
    [SerializeField, Min(0.2f)] private float projectedDwellSeconds = 0.65f;
    [SerializeField, Min(0.01f)] private float crossFadeSeconds = 0.12f;

    private Coroutine presentationRoutine;
    private BattleStatusChipModel boundModel;
    private string boundSignature;
    private bool reducedMotion;
    private float phaseOffsetSeconds;

    public void Configure(
        Image iconImage,
        TMP_Text textLabel)
    {
        icon = iconImage;
        label = textLabel;
    }

    public void ConfigurePresentation(
        bool useReducedMotion,
        float phaseOffset)
    {
        bool changed =
            reducedMotion != useReducedMotion ||
            !Mathf.Approximately(phaseOffsetSeconds, Mathf.Max(0f, phaseOffset));

        reducedMotion = useReducedMotion;
        phaseOffsetSeconds = Mathf.Max(0f, phaseOffset);

        if (changed && boundModel != null)
            RestartPresentation();
    }

    /// <summary>
    /// 구 호출부 호환. Phase 11 StatusList는 chip model Bind를 사용한다.
    /// </summary>
    public void Bind(StatusEffect effect)
    {
        if (effect == null)
        {
            Bind((BattleStatusChipModel)null);
            return;
        }

        var models = BattleStatusPresentationProjector.BuildFromEffects(
            effect.Owner,
            new[] { effect });

        Bind(models.Count > 0 ? models[0] : null);
    }

    public void Bind(BattleStatusChipModel model)
    {
        if (label == null)
            return;

        if (model == null)
        {
            StopPresentation();
            boundModel = null;
            boundSignature = null;
            label.text = string.Empty;
            SetLabelAlpha(1f);

            if (icon != null)
                icon.enabled = false;

            return;
        }

        if (icon != null)
            icon.enabled = icon.sprite != null;

        label.richText = true;

        string signature = model.StateSignature;
        bool sameState =
            boundModel != null &&
            boundModel.StableKey == model.StableKey &&
            boundSignature == signature;

        boundModel = model;
        boundSignature = signature;

        if (sameState)
        {
            if (presentationRoutine == null &&
                !reducedMotion &&
                model.HasProjectionChange &&
                isActiveAndEnabled)
            {
                RestartPresentation();
            }

            return;
        }

        RestartPresentation();
    }

    private void RestartPresentation()
    {
        StopPresentation();

        if (label == null || boundModel == null)
            return;

        SetLabelAlpha(1f);

        if (reducedMotion || !boundModel.HasProjectionChange)
        {
            label.text = BattleStatusUiText.BuildChipLabel(
                boundModel,
                projected: false,
                reducedMotion: reducedMotion);
            return;
        }

        label.text = BattleStatusUiText.BuildChipLabel(
            boundModel,
            projected: false,
            reducedMotion: false);

        presentationRoutine = StartCoroutine(PresentProjectionLoop());
    }

    private IEnumerator PresentProjectionLoop()
    {
        if (phaseOffsetSeconds > 0f)
            yield return WaitUnscaled(phaseOffsetSeconds);

        while (boundModel != null && boundModel.HasProjectionChange)
        {
            label.text = BattleStatusUiText.BuildChipLabel(
                boundModel,
                projected: false,
                reducedMotion: false);
            SetLabelAlpha(1f);
            yield return WaitUnscaled(currentDwellSeconds);

            yield return FadeLabel(1f, 0f, crossFadeSeconds);

            label.text = BattleStatusUiText.BuildChipLabel(
                boundModel,
                projected: true,
                reducedMotion: false);
            yield return FadeLabel(0f, 1f, crossFadeSeconds);
            yield return WaitUnscaled(projectedDwellSeconds);

            yield return FadeLabel(1f, 0f, crossFadeSeconds);

            label.text = BattleStatusUiText.BuildChipLabel(
                boundModel,
                projected: false,
                reducedMotion: false);
            yield return FadeLabel(0f, 1f, crossFadeSeconds);
        }

        presentationRoutine = null;
    }

    private IEnumerator FadeLabel(
        float from,
        float to,
        float duration)
    {
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        SetLabelAlpha(from);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetLabelAlpha(Mathf.Lerp(from, to, elapsed / safeDuration));
            yield return null;
        }

        SetLabelAlpha(to);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetLabelAlpha(float alpha)
    {
        if (label == null)
            return;

        Color color = label.color;
        color.a = Mathf.Clamp01(alpha);
        label.color = color;
    }

    private void StopPresentation()
    {
        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }
    }

    private void OnEnable()
    {
        if (boundModel != null)
            RestartPresentation();
    }

    private void OnDisable()
    {
        StopPresentation();
        SetLabelAlpha(1f);
    }
}
