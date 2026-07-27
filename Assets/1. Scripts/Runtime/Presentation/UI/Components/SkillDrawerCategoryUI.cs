using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillDrawerCategoryUI : MonoBehaviour
{
    [SerializeField] private ActionType actionType;
    [SerializeField] private Button headerButton;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private RectTransform drawerRoot;
    [SerializeField] private LayoutElement drawerLayout;
    [SerializeField] private CanvasGroup drawerCanvasGroup;
    [SerializeField] private RectTransform content;
    [SerializeField] private BattleSkillCardButtonUI cardTemplate;
    [SerializeField, Min(0f)] private float expandedHeight = 500f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.22f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;
    [SerializeField] private bool startOpen;

    private readonly List<BattleSkillCardButtonUI> generatedCards = new();
    private Sequence drawerSequence;
    private bool isOpen;

    public ActionType ActionType => actionType;
    public bool IsOpen => isOpen;

    public void Configure(
        ActionType type,
        Button header,
        TMP_Text label,
        RectTransform drawer,
        LayoutElement layout,
        CanvasGroup group,
        RectTransform contentRoot,
        BattleSkillCardButtonUI template,
        float height)
    {
        actionType = type;
        headerButton = header;
        headerText = label;
        drawerRoot = drawer;
        drawerLayout = layout;
        drawerCanvasGroup = group;
        content = contentRoot;
        cardTemplate = template;
        expandedHeight = Mathf.Max(1f, height);

        BindHeader();
        ConfigureHeaderText();
        SetOpenImmediate(startOpen);
    }

    private void Awake()
    {
        BindHeader();
        ConfigureHeaderText();
        SetOpenImmediate(startOpen);
    }

    private void BindHeader()
    {
        if (headerButton == null)
            return;

        headerButton.onClick.RemoveListener(Toggle);
        headerButton.onClick.AddListener(Toggle);
    }

    public void Rebuild(
        IReadOnlyList<Skill> skills,
        int actionIndex,
        BattleUIManager manager)
    {
        ClearGeneratedCards();

        int count = 0;

        if (skills != null &&
            content != null &&
            cardTemplate != null)
        {
            foreach (Skill skill in skills)
            {
                if (skill == null ||
                    skill.ActionType != actionType)
                {
                    continue;
                }

                bool usable =
                    manager != null &&
                    manager.IsSkillSelectable(
                        manager.SelectedOwnerPart,
                        skill);

                string reason =
                    manager != null
                        ? manager.GetSkillSelectionReason(
                            manager.SelectedOwnerPart,
                            skill)
                        : "전투 UI 연결 필요";

                BattleSkillCardButtonUI card =
                    Instantiate(
                        cardTemplate,
                        content);

                card.name =
                    $"SkillCard_{actionType}_{skill.SkillName}";

                card.gameObject.SetActive(true);
                System.Action<Skill, int> onSelected =
                    manager != null
                        ? manager.OnSkillSelectedFromPanel
                        : null;

                card.Bind(
                    skill,
                    actionIndex,
                    usable,
                    reason,
                    onSelected);

                generatedCards.Add(card);
                count++;
            }
        }

        if (headerText != null)
        {
            headerText.text =
                $"{BattleSkillUiText.GetActionTypeName(actionType)}  ({count})";
        }

        if (headerButton != null)
            headerButton.interactable = count > 0;

        if (count == 0)
            SetOpenImmediate(false);

        if (content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    public void Toggle()
    {
        SetOpen(!isOpen, true);
    }

    public void SetOpen(bool open, bool animate)
    {
        isOpen = open;
        KillDrawerTween();

        if (!animate || !gameObject.activeInHierarchy)
        {
            SetOpenImmediate(open);
            return;
        }

        if (drawerRoot != null && openingRequired(open))
            drawerRoot.gameObject.SetActive(true);

        float fromHeight = drawerLayout != null
            ? Mathf.Max(0f, drawerLayout.preferredHeight)
            : 0f;

        float toHeight = open ? expandedHeight : 0f;
        float toAlpha = open ? 1f : 0f;

        if (drawerCanvasGroup != null)
        {
            drawerCanvasGroup.interactable = false;
            drawerCanvasGroup.blocksRaycasts = false;
        }

        drawerSequence = DOTween.Sequence()
            .SetUpdate(true);

        if (drawerLayout != null)
        {
            Tween heightTween = DOVirtual.Float(
                    fromHeight,
                    toHeight,
                    animationDuration,
                    value =>
                    {
                        if (drawerLayout == null)
                            return;

                        drawerLayout.preferredHeight = value;

                        RectTransform rect = transform as RectTransform;
                        if (rect != null)
                            LayoutRebuilder.MarkLayoutForRebuild(rect);
                    })
                .SetEase(open ? openEase : closeEase);

            drawerSequence.Join(heightTween);
        }

        if (drawerCanvasGroup != null)
        {
            drawerSequence.Join(
                drawerCanvasGroup
                    .DOFade(toAlpha, animationDuration)
                    .SetEase(open ? openEase : closeEase));
        }

        drawerSequence.OnComplete(() =>
        {
            if (drawerLayout != null)
                drawerLayout.preferredHeight = toHeight;

            if (drawerCanvasGroup != null)
            {
                drawerCanvasGroup.alpha = toAlpha;
                drawerCanvasGroup.interactable = open;
                drawerCanvasGroup.blocksRaycasts = open;
            }

            if (drawerRoot != null && !open)
                drawerRoot.gameObject.SetActive(false);

            drawerSequence = null;
        });
    }

    public void SetOpenImmediate(bool open)
    {
        KillDrawerTween();
        isOpen = open;

        if (drawerLayout != null)
            drawerLayout.preferredHeight = open
                ? expandedHeight
                : 0f;

        if (drawerCanvasGroup != null)
        {
            drawerCanvasGroup.alpha = open ? 1f : 0f;
            drawerCanvasGroup.interactable = open;
            drawerCanvasGroup.blocksRaycasts = open;
        }

        if (drawerRoot != null)
            drawerRoot.gameObject.SetActive(open);
    }

    private static bool openingRequired(bool open)
    {
        return open;
    }

    private void ConfigureHeaderText()
    {
        if (headerText == null)
            return;

        headerText.richText = false;
        headerText.textWrappingMode =
            TextWrappingModes.NoWrap;
        headerText.enableAutoSizing = false;
        headerText.fontSize = 24f;
        headerText.overflowMode = TextOverflowModes.Overflow;
        headerText.alignment = TextAlignmentOptions.Center;
    }

    private void ClearGeneratedCards()
    {
        for (int i = generatedCards.Count - 1; i >= 0; i--)
        {
            BattleSkillCardButtonUI card = generatedCards[i];

            if (card == null)
                continue;

            card.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(card.gameObject);
            else
                DestroyImmediate(card.gameObject);
        }

        generatedCards.Clear();
    }

    private void KillDrawerTween()
    {
        if (drawerSequence == null)
            return;

        drawerSequence.Kill(false);
        drawerSequence = null;
    }

    private void OnDestroy()
    {
        KillDrawerTween();
        ClearGeneratedCards();
    }
}