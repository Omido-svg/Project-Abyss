using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public enum BattleDamageNumberStyle
{
    NormalHp = 0,
    CriticalHp = 1,
    Stagger = 2,
    Custom = 3
}

public class DamageNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Motion")]
    [SerializeField] private float moveY = 82f;
    [SerializeField] private float duration = 0.82f;
    [SerializeField] private float lateralDrift = 24f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Readability")]
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.22f;
    [SerializeField] private Color outlineColor = new(0f, 0f, 0f, 0.96f);

    [Header("Damage Colors")]
    [SerializeField] private Color normalHpColor = Color.white;
    [SerializeField] private Color criticalHpColor = new(1f, 0.82f, 0.14f, 1f);
    [SerializeField] private Color staggerColor = new(0.24f, 0.70f, 1f, 1f);

    private static readonly int GlowColorId =
        Shader.PropertyToID("_GlowColor");

    private static readonly int GlowOffsetId =
        Shader.PropertyToID("_GlowOffset");

    private static readonly int GlowOuterId =
        Shader.PropertyToID("_GlowOuter");

    private static readonly int GlowPowerId =
        Shader.PropertyToID("_GlowPower");

    private static int playbackSerial;

    private RectTransform rectTransform;
    private Sequence sequence;
    private Action<DamageNumberUI> releaseHandler;
    private Material runtimeFontMaterial;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Play(
        int damage)
    {
        Play(
            damage,
            BattleDamageNumberStyle.NormalHp);
    }

    // 기존 Status/VFX 호출부 호환.
    public void Play(
        int damage,
        Color color)
    {
        PlayInternal(
            damage,
            BattleDamageNumberStyle.Custom,
            color);
    }

    public void Play(
        int damage,
        BattleDamageNumberStyle style)
    {
        PlayInternal(
            damage,
            style,
            ResolveBaseColor(style));
    }

    private void PlayInternal(
        int damage,
        BattleDamageNumberStyle style,
        Color color)
    {
        ResolveReferences();
        StopSequence();

        if (damageText != null)
        {
            damageText.text =
                Mathf.Max(0, damage)
                    .ToString();

            ApplyTextStyle(
                style,
                color);
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        int serial =
            ++playbackSerial;

        float direction =
            (serial & 1) == 0
                ? -1f
                : 1f;

        float styleScale =
            style switch
            {
                BattleDamageNumberStyle.CriticalHp => 1.58f,
                BattleDamageNumberStyle.Stagger => 1.36f,
                _ => 1.18f
            };

        float styleMove =
            style == BattleDamageNumberStyle.CriticalHp
                ? moveY * 1.12f
                : moveY;

        Vector2 startPosition =
            rectTransform != null
                ? rectTransform.anchoredPosition
                : Vector2.zero;

        float horizontal =
            direction *
            (lateralDrift +
             (serial % 3) * 5f);

        transform.localScale =
            Vector3.one *
            Mathf.Max(0.45f, styleScale * 0.58f);

        transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                -direction * 5f);

        sequence =
            DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetTarget(this);

        sequence.Append(
            transform.DOScale(
                    Vector3.one * styleScale,
                    0.09f)
                .SetEase(Ease.OutBack));

        sequence.Append(
            transform.DOScale(
                    Vector3.one,
                    0.13f)
                .SetEase(Ease.OutCubic));

        sequence.Insert(
            0f,
            transform.DOLocalRotate(
                    Vector3.zero,
                    0.20f)
                .SetEase(Ease.OutCubic));

        if (rectTransform != null)
        {
            sequence.Insert(
                0f,
                rectTransform.DOAnchorPos(
                        startPosition +
                        new Vector2(
                            horizontal,
                            styleMove),
                        duration)
                    .SetEase(Ease.OutCubic));
        }

        if (canvasGroup != null)
        {
            float fadeStart =
                Mathf.Min(
                    0.30f,
                    duration * 0.45f);

            sequence.Insert(
                fadeStart,
                canvasGroup.DOFade(
                    0f,
                    Mathf.Max(
                        0.12f,
                        duration - fadeStart)));
        }

        sequence.OnComplete(
            CompletePlayback);
    }

    internal void PrepareForUse(
        Action<DamageNumberUI> onRelease)
    {
        StopSequence();
        ResolveReferences();
        releaseHandler = onRelease;
    }

    internal void ResetForPool()
    {
        releaseHandler = null;
        StopSequence();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        ApplyGlow(
            enabled: false,
            Color.clear);
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (damageText == null)
            damageText = GetComponentInChildren<TMP_Text>(true);

        if (damageText != null)
        {
            damageText.richText = true;
            damageText.overflowMode =
                TextOverflowModes.Overflow;

            damageText.fontStyle =
                FontStyles.Bold;

            EnsureRuntimeFontMaterial();
        }
    }

    private Color ResolveBaseColor(
        BattleDamageNumberStyle style)
    {
        return style switch
        {
            BattleDamageNumberStyle.CriticalHp => criticalHpColor,
            BattleDamageNumberStyle.Stagger => staggerColor,
            _ => normalHpColor
        };
    }

    private void ApplyTextStyle(
        BattleDamageNumberStyle style,
        Color color)
    {
        if (damageText == null)
            return;

        damageText.color = color;
        damageText.outlineColor = outlineColor;
        damageText.outlineWidth =
            style == BattleDamageNumberStyle.CriticalHp
                ? Mathf.Clamp01(outlineWidth + 0.05f)
                : outlineWidth;

        switch (style)
        {
            case BattleDamageNumberStyle.CriticalHp:
                ApplyGlow(
                    enabled: true,
                    new Color(3.6f, 2.1f, 0.16f, 1f));
                break;

            case BattleDamageNumberStyle.Stagger:
                ApplyGlow(
                    enabled: true,
                    new Color(0.12f, 1.0f, 4.0f, 1f));
                break;

            default:
                ApplyGlow(
                    enabled: false,
                    Color.clear);
                break;
        }
    }

    private void EnsureRuntimeFontMaterial()
    {
        if (runtimeFontMaterial != null ||
            damageText?.fontSharedMaterial == null)
        {
            return;
        }

        runtimeFontMaterial =
            new Material(
                damageText.fontSharedMaterial)
            {
                name =
                    $"{damageText.fontSharedMaterial.name} [DamageNumber Runtime]"
            };

        damageText.fontMaterial =
            runtimeFontMaterial;
    }

    private void ApplyGlow(
        bool enabled,
        Color glowColor)
    {
        EnsureRuntimeFontMaterial();

        if (runtimeFontMaterial == null)
            return;

        if (enabled)
            runtimeFontMaterial.EnableKeyword("GLOW_ON");
        else
            runtimeFontMaterial.DisableKeyword("GLOW_ON");

        if (runtimeFontMaterial.HasProperty(GlowColorId))
        {
            runtimeFontMaterial.SetColor(
                GlowColorId,
                glowColor);
        }

        if (runtimeFontMaterial.HasProperty(GlowOffsetId))
        {
            runtimeFontMaterial.SetFloat(
                GlowOffsetId,
                enabled ? 0.05f : 0f);
        }

        if (runtimeFontMaterial.HasProperty(GlowOuterId))
        {
            runtimeFontMaterial.SetFloat(
                GlowOuterId,
                enabled ? 0.44f : 0f);
        }

        if (runtimeFontMaterial.HasProperty(GlowPowerId))
        {
            runtimeFontMaterial.SetFloat(
                GlowPowerId,
                enabled ? 0.62f : 0f);
        }

        damageText.UpdateMeshPadding();
        damageText.SetMaterialDirty();
    }

    private void CompletePlayback()
    {
        sequence = null;

        Action<DamageNumberUI> callback =
            releaseHandler;

        if (callback != null)
        {
            callback(this);
            return;
        }

        Destroy(gameObject);
    }

    private void StopSequence()
    {
        if (sequence == null)
            return;

        sequence.Kill(false);
        sequence = null;
    }

    private void OnDisable()
    {
        StopSequence();

        // activeSelf가 true인 채 OnDisable되었다면 이 객체가 아니라
        // Canvas/Scene의 상위 hierarchy가 비활성화되는 중이다.
        if (gameObject.activeSelf)
            return;

        Action<DamageNumberUI> callback =
            releaseHandler;

        if (callback == null)
            return;

        releaseHandler = null;
        callback(this);
    }

    private void OnDestroy()
    {
        releaseHandler = null;
        StopSequence();

        if (runtimeFontMaterial != null)
        {
            Destroy(runtimeFontMaterial);
            runtimeFontMaterial = null;
        }
    }
}
