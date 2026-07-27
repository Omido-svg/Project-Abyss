using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillSelectPanelUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private BattleScreenModeController modeController;
    [SerializeField] private TMP_Text slotHeaderText;
    [SerializeField] private TMP_Text helpText;
    [SerializeField] private Button closeButton;
    [SerializeField] private ActionSlotSelectorUI actionSlotSelector;
    [SerializeField] private SkillDrawerCategoryUI[] drawers;

    [Header("DOTween")]
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField, Min(0f)] private float enterOffsetY = 120f;
    [SerializeField, Min(0.01f)] private float showDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float hideDuration = 0.16f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    private BattleUIManager uiManager;
    private BodyPart selectedPart;
    private int selectedActionIndex;
    private int maxActionSlots = 1;

    private Sequence visibilitySequence;
    private Vector2 shownPosition;
    private bool shownPositionCaptured;
    private bool isHiding;

    public bool IsVisible =>
        canvasGroup != null &&
        canvasGroup.alpha > 0.001f &&
        gameObject.activeInHierarchy &&
        !isHiding;

    public void Configure(
        CanvasGroup group,
        BattleScreenModeController screenMode,
        TMP_Text header,
        TMP_Text help,
        Button close,
        ActionSlotSelectorUI slotSelector,
        SkillDrawerCategoryUI[] categoryDrawers)
    {
        canvasGroup = group;
        modeController = screenMode;
        slotHeaderText = header;
        helpText = help;
        closeButton = close;
        actionSlotSelector = slotSelector;
        drawers = categoryDrawers;
        animatedRoot ??= transform as RectTransform;

        BindCloseButton();
        ApplyTextSettings();
        CaptureShownPosition();
        HideImmediate(false);
    }

    private void Awake()
    {
        canvasGroup ??= GetComponentInParent<CanvasGroup>(true);
        animatedRoot ??= transform as RectTransform;

        if (modeController == null)
        {
            modeController =
                FindFirstObjectByType<BattleScreenModeController>(
                    FindObjectsInactive.Include);
        }

        if (actionSlotSelector == null)
            actionSlotSelector = GetComponent<ActionSlotSelectorUI>();

        if (drawers == null || drawers.Length == 0)
            drawers = GetComponentsInChildren<SkillDrawerCategoryUI>(true);

        BindCloseButton();
        ApplyTextSettings();
        CaptureShownPosition();

        // 비활성 SkillSelectionLayer가 ShowSkillMode로 처음 켜질 때
        // Awake가 뒤늦게 실행되어 alpha를 다시 0으로 만드는 문제를 방지한다.
        if (modeController == null ||
            modeController.CurrentMode != BattleUiScreenMode.SkillSelection)
        {
            HideImmediate(false);
        }
    }

    private void Update()
    {
        if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
            Cancel();
    }

    private void BindCloseButton()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveListener(Cancel);
        closeButton.onClick.AddListener(Cancel);
    }

    public void Show(
        BattleUIManager manager,
        BodyPart part)
    {
        Show(manager, part, 0, 1);
    }

    public void Show(
        BattleUIManager manager,
        BodyPart part,
        int actionIndex,
        int maxSlots)
    {
        uiManager = manager;
        selectedPart = part;
        maxActionSlots = Mathf.Max(1, maxSlots);
        selectedActionIndex =
            Mathf.Clamp(
                actionIndex,
                0,
                maxActionSlots - 1);

        if (uiManager == null || selectedPart == null)
        {
            Hide();
            return;
        }

        // 순서가 중요하다. 모드를 먼저 바꾸면 비활성 부모가 활성화되며
        // Awake가 실행되고, 그 다음 alpha와 위치를 실제 표시 상태로 만든다.
        modeController?.ShowSkillMode();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        CaptureShownPosition();
        KillVisibilityTween();
        isHiding = false;

        RefreshSlotHeader();
        RebuildActionSlotButtons();
        RebuildDrawers();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (animatedRoot != null)
        {
            animatedRoot.anchoredPosition =
                shownPosition + Vector2.up * enterOffsetY;
        }

        visibilitySequence = DOTween.Sequence()
            .SetUpdate(true);

        if (canvasGroup != null)
            visibilitySequence.Join(canvasGroup.DOFade(1f, showDuration));

        if (animatedRoot != null)
        {
            visibilitySequence.Join(
                animatedRoot
                    .DOAnchorPos(shownPosition, showDuration)
                    .SetEase(showEase));
        }

        visibilitySequence.OnComplete(() =>
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            visibilitySequence = null;
        });

        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        selectedPart = null;
        selectedActionIndex = 0;
        maxActionSlots = 1;

        actionSlotSelector?.ClearGeneratedButtons();

        if (drawers != null)
        {
            foreach (SkillDrawerCategoryUI drawer in drawers)
                drawer?.SetOpenImmediate(false);
        }

        if (!gameObject.activeInHierarchy ||
            canvasGroup == null ||
            canvasGroup.alpha <= 0.001f)
        {
            HideImmediate(true);
            return;
        }

        CaptureShownPosition();
        KillVisibilityTween();
        isHiding = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        visibilitySequence = DOTween.Sequence()
            .SetUpdate(true);

        visibilitySequence.Join(canvasGroup.DOFade(0f, hideDuration));

        if (animatedRoot != null)
        {
            visibilitySequence.Join(
                animatedRoot
                    .DOAnchorPos(
                        shownPosition + Vector2.up * enterOffsetY * 0.55f,
                        hideDuration)
                    .SetEase(hideEase));
        }

        visibilitySequence.OnComplete(() =>
        {
            visibilitySequence = null;
            HideImmediate(true);
        });
    }

    private void HideImmediate(bool showDefaultMode)
    {
        KillVisibilityTween();
        isHiding = false;

        if (slotHeaderText != null)
            slotHeaderText.text = string.Empty;

        if (helpText != null)
            helpText.text = "행동 유형을 열고 스킬을 선택하세요.";

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (animatedRoot != null && shownPositionCaptured)
            animatedRoot.anchoredPosition = shownPosition;

        if (showDefaultMode)
        {
            modeController?.ShowDefaultMode();

            // DefaultBattleLayer가 다시 활성화되는 순간 BodyPartButton들이
            // Registry에 재등록된다. 이 시점 이후에 갱신해야 적 버튼의
            // interactable 상태가 SelectTarget으로 올바르게 바뀐다.
            uiManager?.RefreshAllBodyPartButtons();
        }
    }

    public void RefreshVisibleButtons()
    {
        if (!IsVisible ||
            uiManager == null ||
            selectedPart == null)
        {
            return;
        }

        selectedActionIndex =
            Mathf.Clamp(
                uiManager.SelectedActionIndex,
                0,
                Mathf.Max(0, maxActionSlots - 1));

        RefreshSlotHeader();
        RebuildActionSlotButtons();
        RebuildDrawers();
    }

    private void RefreshSlotHeader()
    {
        if (slotHeaderText == null)
            return;

        string ownerName =
            selectedPart?.Owner?.Data?.CharacterName ??
            selectedPart?.Owner?.name ??
            "플레이어";

        slotHeaderText.text =
            $"{ownerName} · {GetPartName(selectedPart.Type)} · " +
            $"행동 슬롯 {selectedActionIndex + 1}/{maxActionSlots}";
    }

    private void RebuildActionSlotButtons()
    {
        actionSlotSelector?.Rebuild(
            uiManager,
            selectedPart?.Owner,
            selectedPart,
            selectedActionIndex,
            maxActionSlots);
    }

    private void RebuildDrawers()
    {
        IReadOnlyList<Skill> skills =
            uiManager?.GetSelectableSkillsForCurrentSlot(
                selectedPart);

        int total = skills?.Count ?? 0;

        if (helpText != null)
        {
            helpText.text = total > 0
                ? $"장착 스킬 {total}개 · 행동 유형을 누르면 서랍이 열립니다."
                : "이 행동 슬롯에 장착된 스킬이 없습니다.";
        }

        if (drawers == null)
            return;

        foreach (SkillDrawerCategoryUI drawer in drawers)
        {
            drawer?.Rebuild(
                skills,
                selectedActionIndex,
                uiManager);
        }
    }

    private void ApplyTextSettings()
    {
        ConfigureText(slotHeaderText, 27f, TextAlignmentOptions.Left);
        ConfigureText(helpText, 19f, TextAlignmentOptions.Left);
    }

    private static void ConfigureText(
        TMP_Text text,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.richText = false;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private void CaptureShownPosition()
    {
        if (shownPositionCaptured || animatedRoot == null)
            return;

        shownPosition = animatedRoot.anchoredPosition;
        shownPositionCaptured = true;
    }

    private void KillVisibilityTween()
    {
        if (visibilitySequence == null)
            return;

        visibilitySequence.Kill(false);
        visibilitySequence = null;
    }

    private void Cancel()
    {
        if (uiManager != null)
        {
            uiManager.CancelCurrentSelection();
            return;
        }

        Hide();
    }

    private void OnDestroy()
    {
        KillVisibilityTween();
    }

    private static string GetPartName(PartType type)
    {
        return type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼손",
            PartType.RIGHT_HAND => "오른손",
            PartType.LEGS => "다리",
            _ => type.ToString()
        };
    }
}