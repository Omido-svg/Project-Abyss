using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleSkillCardButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rollText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Image accentImage;

    [Header("Rejected Selection Feedback")]
    [Tooltip("비워 두면 카드 내부의 안전한 비주얼 RectTransform을 자동으로 찾습니다.")]
    [SerializeField] private RectTransform feedbackTarget;
    [SerializeField] private Color rejectedColor =
        new Color(1f, 0.16f, 0.16f, 1f);
    [SerializeField, Min(0.05f)] private float feedbackDuration = 0.38f;
    [SerializeField, Range(0f, 12f)] private float shakeAngle = 3.2f;

    private Skill skill;
    private int actionIndex;
    private Action<Skill, int> callback;
    private bool usable;
    private string unavailableReason;

    private Sequence feedbackSequence;
    private Color titleNormalColor = Color.white;
    private Color costNormalColor = Color.white;
    private Color reasonNormalColor = Color.white;
    private Vector3 normalScale = Vector3.one;
    private Quaternion normalRotation = Quaternion.identity;
    private bool visualCaptured;

    public void Configure(
        Button sourceButton,
        TMP_Text title,
        TMP_Text rolls,
        TMP_Text cost,
        TMP_Text reason,
        Image accent)
    {
        button = sourceButton;
        titleText = title;
        rollText = rolls;
        costText = cost;
        reasonText = reason;
        accentImage = accent;

        // 런타임 생성 카드에서는 Configure 시점에 레이아웃 위치가 아직
        // 확정되지 않을 수 있다. 위치는 저장하거나 애니메이션하지 않는다.
        visualCaptured = false;
        ResolveFeedbackTarget();
        CaptureVisual();
    }

    private void Awake()
    {
        button ??= GetComponent<Button>();
        ResolveFeedbackTarget();
        CaptureVisual();
    }

    private void OnEnable()
    {
        // 풀링되거나 패널이 다시 열린 카드가 이전 Tween 상태를 이어받지 않게 한다.
        KillFeedback(restore: true);
    }

    public void Bind(
        Skill value,
        int selectedActionIndex,
        bool isUsable,
        string reason,
        Action<Skill, int> onClicked)
    {
        KillFeedback(restore: true);

        skill = value;
        actionIndex = Mathf.Max(0, selectedActionIndex);
        callback = onClicked;
        usable = isUsable;
        unavailableReason = reason;

        ResolveFeedbackTarget();
        CaptureVisual();

        if (titleText != null)
            titleText.text = skill?.SkillName ?? "스킬 없음";

        if (rollText != null)
            rollText.text = BattleSkillUiText.BuildRollSummary(skill);

        if (costText != null)
        {
            costText.text = skill == null
                ? string.Empty
                : $"빛 {skill.EnergyCost}";
        }

        if (reasonText != null)
        {
            reasonText.gameObject.SetActive(!usable);
            reasonText.text = usable
                ? string.Empty
                : string.IsNullOrWhiteSpace(unavailableReason)
                    ? "현재 사용 불가"
                    : unavailableReason;
        }

        if (accentImage != null)
        {
            Color color = GetActionColor(skill?.ActionType);
            color.a = usable ? 1f : 0.55f;
            accentImage.color = color;
        }

        if (button == null)
            return;

        button.onClick.RemoveAllListeners();

        // 비활성화하면 클릭 자체가 막혀 부족 피드백을 줄 수 없다.
        // 실제 선택 가능 여부는 InvokeClick에서 판정한다.
        button.interactable = skill != null;

        if (button.interactable)
            button.onClick.AddListener(InvokeClick);

        MarkParentLayoutForRebuild();
    }

    private void InvokeClick()
    {
        if (skill == null)
            return;

        if (!usable)
        {
            PlayRejectedFeedback();

            // BattleUIManager도 같은 선택을 검증해 상단 빛 UI 피드백을 재생한다.
            // 거절된 선택 이후에는 정상 선택 흐름으로 절대 진행하지 않는다.
            callback?.Invoke(skill, actionIndex);
            return;
        }

        callback?.Invoke(skill, actionIndex);
    }

    private void PlayRejectedFeedback()
    {
        ResolveFeedbackTarget();
        CaptureVisual();
        KillFeedback(restore: true);

        if (titleText != null)
            titleText.color = rejectedColor;

        if (costText != null)
            costText.color = rejectedColor;

        if (reasonText != null)
        {
            reasonText.gameObject.SetActive(true);
            reasonText.color = rejectedColor;
            reasonText.text =
                string.IsNullOrWhiteSpace(unavailableReason)
                    ? "빛 또는 조건이 부족합니다."
                    : unavailableReason;
        }

        RectTransform target = ResolveFeedbackTarget();

        if (target == null)
            return;

        // LayoutGroup이 관리하는 카드 루트의 anchoredPosition은 절대 변경하지 않는다.
        // 회전과 스케일만 사용하므로 재클릭/레이아웃 갱신 시 카드가 이동하지 않는다.
        target.localScale = normalScale;
        target.localRotation = normalRotation;

        feedbackSequence = DOTween.Sequence()
            .SetUpdate(true);

        feedbackSequence.Join(
            target.DOShakeRotation(
                feedbackDuration,
                new Vector3(0f, 0f, shakeAngle),
                20,
                85f,
                true));

        feedbackSequence.Join(
            target.DOPunchScale(
                new Vector3(0.045f, 0.045f, 0f),
                feedbackDuration * 0.8f,
                7,
                0.7f));

        feedbackSequence.OnComplete(() =>
        {
            feedbackSequence = null;
            RestoreVisual();
        });

        MarkParentLayoutForRebuild();
    }

    private RectTransform ResolveFeedbackTarget()
    {
        if (feedbackTarget != null)
            return feedbackTarget;

        // 버튼의 배경 Graphic이 별도 자식이면 가장 안전한 비주얼 대상이다.
        if (button != null &&
            button.targetGraphic != null)
        {
            RectTransform graphicRect =
                button.targetGraphic.rectTransform;

            if (graphicRect != null &&
                graphicRect != transform)
            {
                feedbackTarget = graphicRect;
                return feedbackTarget;
            }
        }

        // 텍스트들을 감싸는 Content 자식이 있으면 그 자식을 사용한다.
        if (titleText != null &&
            titleText.rectTransform.parent is RectTransform content &&
            content != transform)
        {
            feedbackTarget = content;
            return feedbackTarget;
        }

        // 별도 자식이 없는 템플릿에서도 위치는 건드리지 않으므로 안전하다.
        feedbackTarget = transform as RectTransform;
        return feedbackTarget;
    }

    private void CaptureVisual()
    {
        if (visualCaptured)
            return;

        RectTransform target = ResolveFeedbackTarget();

        if (target != null)
        {
            normalScale = target.localScale;
            normalRotation = target.localRotation;
        }

        if (titleText != null)
            titleNormalColor = titleText.color;

        if (costText != null)
            costNormalColor = costText.color;

        if (reasonText != null)
            reasonNormalColor = reasonText.color;

        visualCaptured = true;
    }

    private void KillFeedback(bool restore)
    {
        if (feedbackSequence != null)
        {
            feedbackSequence.Kill(false);
            feedbackSequence = null;
        }

        if (restore)
            RestoreVisual();
    }

    private void RestoreVisual()
    {
        if (!visualCaptured)
            return;

        RectTransform target = ResolveFeedbackTarget();

        if (target != null)
        {
            target.localScale = normalScale;
            target.localRotation = normalRotation;
        }

        if (titleText != null)
            titleText.color = titleNormalColor;

        if (costText != null)
            costText.color = costNormalColor;

        if (reasonText != null)
        {
            reasonText.color = reasonNormalColor;
            reasonText.gameObject.SetActive(!usable);
            reasonText.text = usable
                ? string.Empty
                : string.IsNullOrWhiteSpace(unavailableReason)
                    ? "현재 사용 불가"
                    : unavailableReason;
        }

        MarkParentLayoutForRebuild();
    }

    private void MarkParentLayoutForRebuild()
    {
        if (transform.parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    private static Color GetActionColor(ActionType? type)
    {
        return type switch
        {
            ActionType.NormalAttack => new Color(0.18f, 0.55f, 1f, 1f),
            ActionType.Duel => new Color(0.95f, 0.22f, 0.18f, 1f),
            ActionType.Preparation => new Color(0.14f, 0.75f, 0.62f, 1f),
            ActionType.Prestige => new Color(0.95f, 0.60f, 0.12f, 1f),
            _ => Color.white
        };
    }

    private void OnDisable()
    {
        KillFeedback(restore: true);
    }

    private void OnDestroy()
    {
        KillFeedback(restore: false);
    }
}
