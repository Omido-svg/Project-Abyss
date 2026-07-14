using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamageNumberUI : MonoBehaviour
{
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Tween")]
    [SerializeField] private float moveY = 80f;
    [SerializeField] private float duration = 0.8f;
    [SerializeField] private float startScale = 1.4f;
    [SerializeField] private float endScale = 1f;
    [SerializeField] private bool useUnscaledTime = false;

    private RectTransform rectTransform;
    private Sequence sequence;
    private Action<DamageNumberUI> releaseHandler;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Play(
        int damage)
    {
        Play(
            damage,
            Color.white);
    }

    public void Play(
        int damage,
        Color color)
    {
        ResolveReferences();
        StopSequence();

        if (damageText != null)
        {
            damageText.text = damage.ToString();
            damageText.color = color;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        transform.localScale =
            Vector3.one * startScale;

        sequence = DOTween.Sequence();
        sequence.SetUpdate(useUnscaledTime);

        sequence.Join(
            transform.DOScale(
                Vector3.one * endScale,
                0.15f));

        if (rectTransform != null)
        {
            sequence.Join(
                rectTransform.DOAnchorPosY(
                    rectTransform.anchoredPosition.y + moveY,
                    duration));
        }

        if (canvasGroup != null)
        {
            sequence.Join(
                canvasGroup.DOFade(
                    0f,
                    duration));
        }

        sequence.OnComplete(CompletePlayback);
    }

    internal void PrepareForUse(Action<DamageNumberUI> onRelease)
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
            damageText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void CompletePlayback()
    {
        sequence = null;

        Action<DamageNumberUI> callback = releaseHandler;

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

        Action<DamageNumberUI> callback = releaseHandler;

        if (callback == null)
            return;

        releaseHandler = null;
        callback(this);
    }

    private void OnDestroy()
    {
        releaseHandler = null;
        StopSequence();
    }
}
