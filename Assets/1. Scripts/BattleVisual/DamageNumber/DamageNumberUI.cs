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

    private RectTransform rectTransform;
    private Sequence sequence;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (damageText == null)
            damageText = GetComponentInChildren<TMP_Text>();
    }

    public void Play(int damage)
    {
        if (damageText != null)
            damageText.text = damage.ToString();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        transform.localScale =
            Vector3.one * startScale;

        sequence?.Kill();

        sequence = DOTween.Sequence();

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

        sequence.OnComplete(() =>
        {
            Destroy(gameObject);
        });
    }

    private void OnDestroy()
    {
        sequence?.Kill();
    }
}