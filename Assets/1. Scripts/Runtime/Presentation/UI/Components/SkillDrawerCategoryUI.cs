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
    [SerializeField, Min(0f)] private float drawerVerticalPadding = 12f;
    [SerializeField, Min(32f)] private float minimumExpandedHeight = 64f;
    [SerializeField, Min(0.01f)] private float animationDuration = 0.22f;

    [Header("Runtime Card Layout")]
    [SerializeField, Min(32f)] private float fallbackCardHeight = 132f;
    [SerializeField, Min(0f)] private float runtimeCardSpacing = 8f;
    [SerializeField, Min(0)] private int runtimeHorizontalPadding = 8;
    [SerializeField, Min(0)] private int runtimeVerticalPadding = 8;

    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;
    [SerializeField] private bool startOpen;

    [Header("Part Access Visual")]
    [SerializeField] private Color unavailableHeaderColor =
        new Color(0.44f, 0.46f, 0.50f, 1f);

    private readonly List<BattleSkillCardButtonUI> generatedCards = new();
    private Sequence drawerSequence;
    private bool isOpen;
    private int visibleSkillCount;
    private float calculatedExpandedHeight;
    private bool partAccessAllowed = true;
    private Color availableHeaderColor = Color.white;
    private bool availableHeaderColorCaptured;

    public ActionType ActionType => actionType;
    public bool IsOpen => isOpen;
    public int VisibleSkillCount => visibleSkillCount;
    public bool HasVisibleSkills => visibleSkillCount > 0;

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
        calculatedExpandedHeight = expandedHeight;

        BindHeader();
        ConfigureHeaderText();
        CaptureHeaderColor();
        NormalizeCategoryLayout();
        SetOpenImmediate(startOpen);
    }

    private void Awake()
    {
        calculatedExpandedHeight =
            Mathf.Max(1f, expandedHeight);

        BindHeader();
        ConfigureHeaderText();
        CaptureHeaderColor();
        NormalizeCategoryLayout();
        SetOpenImmediate(startOpen);
    }

    private void BindHeader()
    {
        if (headerButton == null)
            return;

        headerButton.onClick.RemoveListener(Toggle);
        headerButton.onClick.AddListener(Toggle);
    }


    public void SetPartAccess(bool allowed)
    {
        partAccessAllowed = allowed;

        if (!partAccessAllowed)
            SetOpenImmediate(false);

        RefreshHeaderVisual();
    }

    public void Rebuild(
        IReadOnlyList<Skill> skills,
        int actionIndex,
        BattleUIManager manager)
    {
        ClearGeneratedCards();
        NormalizeContentLayout();

        int count = 0;

        if (partAccessAllowed &&
            skills != null &&
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

                PrepareGeneratedCardLayout(card);

                generatedCards.Add(card);
                count++;
            }
        }

        visibleSkillCount = count;

        RefreshHeaderVisual();

        if (headerButton != null)
            headerButton.interactable =
                partAccessAllowed &&
                count > 0;

        if (!partAccessAllowed ||
            count == 0)
        {
            SetOpenImmediate(false);
        }

        if (content != null)
        {
            RecalculateExpandedHeight();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        if (isOpen && count > 0)
            SetOpenImmediate(true);
    }

    public void Toggle()
    {
        SetOpen(!isOpen, true);
    }

    public void SetOpen(bool open, bool animate)
    {
        if (open && !HasVisibleSkills)
            open = false;

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

        float toHeight = open
            ? ResolveExpandedHeight()
            : 0f;
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

                        ApplyBodyHeight(value);
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
            ApplyBodyHeight(toHeight);

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

        if (drawerRoot != null)
            drawerRoot.gameObject.SetActive(open);

        ApplyBodyHeight(
            open
                ? ResolveExpandedHeight()
                : 0f);

        if (drawerCanvasGroup != null)
        {
            drawerCanvasGroup.alpha = open ? 1f : 0f;
            drawerCanvasGroup.interactable = open;
            drawerCanvasGroup.blocksRaycasts = open;
        }

        if (open)
        {
            if (content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            RectTransform drawerRect =
                transform as RectTransform;

            if (drawerRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(drawerRect);
        }
    }

    private void RecalculateExpandedHeight()
    {
        NormalizeContentLayout();

        float preferred =
            CalculateGeneratedCardsPreferredHeight();

        float minimum =
            Mathf.Max(32f, minimumExpandedHeight);

        if (preferred <= 0f)
        {
            preferred =
                Mathf.Max(
                    minimum,
                    expandedHeight);
        }

        // 기존 코드는 비활성 DrawerBody에서 LayoutUtility를 호출해
        // 카드 2~3장이 있어도 첫 카드 높이만 계산되는 경우가 있었다.
        // 생성된 카드의 LayoutElement를 직접 합산하므로 부모 활성 상태와 무관하다.
        calculatedExpandedHeight =
            Mathf.Max(
                minimum,
                preferred +
                Mathf.Max(0f, drawerVerticalPadding));

        if (content != null)
        {
            content.anchorMin =
                new Vector2(0f, 1f);
            content.anchorMax =
                new Vector2(1f, 1f);
            content.pivot =
                new Vector2(0.5f, 1f);
            content.anchoredPosition =
                Vector2.zero;
            content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(1f, preferred));
        }

        if (drawerLayout != null)
        {
            drawerLayout.minHeight = 0f;
            drawerLayout.flexibleHeight = 0f;
        }

        ApplyBodyHeight(
            isOpen
                ? calculatedExpandedHeight
                : 0f);
    }

    private void NormalizeCategoryLayout()
    {
        VerticalLayoutGroup categoryLayout =
            GetComponent<VerticalLayoutGroup>();

        if (categoryLayout != null)
        {
            categoryLayout.childControlWidth = true;
            categoryLayout.childControlHeight = true;
            categoryLayout.childForceExpandWidth = true;
            categoryLayout.childForceExpandHeight = false;
        }

        HorizontalLayoutGroup rowLayout =
            transform.parent != null
                ? transform.parent.GetComponent<HorizontalLayoutGroup>()
                : null;

        if (rowLayout != null)
        {
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
        }
    }

    private void ApplyBodyHeight(float requestedHeight)
    {
        float safeHeight =
            Mathf.Max(0f, requestedHeight);

        if (drawerLayout != null)
        {
            drawerLayout.minHeight = 0f;
            drawerLayout.preferredHeight = safeHeight;
            drawerLayout.flexibleHeight = 0f;
        }

        if (drawerRoot != null)
        {
            drawerRoot.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                safeHeight);
        }

        UpdateCategoryPreferredHeight(safeHeight);

        RectTransform categoryRect =
            transform as RectTransform;

        if (categoryRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(categoryRect);

        RectTransform rowRect =
            transform.parent as RectTransform;

        if (rowRect != null)
            LayoutRebuilder.MarkLayoutForRebuild(rowRect);
    }

    private void UpdateCategoryPreferredHeight(float bodyHeight)
    {
        LayoutElement categoryElement =
            GetComponent<LayoutElement>();

        if (categoryElement == null)
            return;

        NormalizeCategoryLayout();

        VerticalLayoutGroup categoryLayout =
            GetComponent<VerticalLayoutGroup>();

        float fixedChildrenHeight = 0f;
        int activeChildCount = 0;

        for (int index = 0;
             index < transform.childCount;
             index++)
        {
            RectTransform child =
                transform.GetChild(index) as RectTransform;

            if (child == null ||
                child == drawerRoot ||
                !child.gameObject.activeSelf)
            {
                continue;
            }

            float preferred =
                LayoutUtility.GetPreferredHeight(child);

            if (preferred <= 0f)
                preferred = child.rect.height;

            fixedChildrenHeight +=
                Mathf.Max(0f, preferred);
            activeChildCount++;
        }

        float spacing =
            categoryLayout != null
                ? categoryLayout.spacing *
                  Mathf.Max(0, activeChildCount)
                : 0f;

        float padding =
            categoryLayout?.padding != null
                ? categoryLayout.padding.top +
                  categoryLayout.padding.bottom
                : 0f;

        float preferredHeight =
            Mathf.Max(0f, fixedChildrenHeight) +
            Mathf.Max(0f, bodyHeight) +
            Mathf.Max(0f, spacing) +
            Mathf.Max(0f, padding);

        categoryElement.minHeight = preferredHeight;
        categoryElement.preferredHeight = preferredHeight;
        categoryElement.flexibleHeight = 0f;
    }

    private void NormalizeContentLayout()
    {
        if (content == null)
            return;

        VerticalLayoutGroup vertical =
            content.GetComponent<VerticalLayoutGroup>();

        if (vertical == null)
        {
            LayoutGroup existing =
                content.GetComponent<LayoutGroup>();

            // 현재 Scene에는 VerticalLayoutGroup이 존재한다.
            // 다른 LayoutGroup이 수동으로 배치된 경우 중복 Component를 추가하지 않는다.
            if (existing != null)
                return;

            vertical =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        vertical.padding =
            new RectOffset(
                Mathf.Max(0, runtimeHorizontalPadding),
                Mathf.Max(0, runtimeHorizontalPadding),
                Mathf.Max(0, runtimeVerticalPadding),
                Mathf.Max(0, runtimeVerticalPadding));
        vertical.spacing =
            Mathf.Max(0f, runtimeCardSpacing);
        vertical.childAlignment =
            TextAnchor.UpperLeft;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
    }

    private void PrepareGeneratedCardLayout(
        BattleSkillCardButtonUI card)
    {
        if (card == null)
            return;

        RectTransform rect =
            card.transform as RectTransform;

        if (rect != null)
            rect.localScale = Vector3.one;

        LayoutElement layout =
            card.GetComponent<LayoutElement>();

        if (layout == null)
            layout = card.gameObject.AddComponent<LayoutElement>();

        layout.ignoreLayout = false;
        layout.minHeight =
            Mathf.Max(
                32f,
                layout.minHeight > 0f
                    ? layout.minHeight
                    : fallbackCardHeight);
        layout.preferredHeight =
            Mathf.Max(
                layout.minHeight,
                layout.preferredHeight > 0f
                    ? layout.preferredHeight
                    : fallbackCardHeight);
        layout.flexibleHeight = 0f;

        card.transform.SetAsLastSibling();
    }

    private float CalculateGeneratedCardsPreferredHeight()
    {
        if (generatedCards.Count == 0)
            return 0f;

        float preferred = 0f;
        int activeCount = 0;

        for (int index = 0;
             index < generatedCards.Count;
             index++)
        {
            BattleSkillCardButtonUI card =
                generatedCards[index];

            if (card == null ||
                !card.gameObject.activeSelf)
            {
                continue;
            }

            LayoutElement layout =
                card.GetComponent<LayoutElement>();

            RectTransform rect =
                card.transform as RectTransform;

            float height =
                layout != null &&
                layout.preferredHeight > 0f
                    ? layout.preferredHeight
                    : rect != null &&
                      rect.rect.height > 0f
                        ? rect.rect.height
                        : fallbackCardHeight;

            preferred +=
                Mathf.Max(32f, height);
            activeCount++;
        }

        if (activeCount <= 0)
            return 0f;

        VerticalLayoutGroup vertical =
            content != null
                ? content.GetComponent<VerticalLayoutGroup>()
                : null;

        float spacing =
            vertical != null
                ? vertical.spacing
                : Mathf.Max(0f, runtimeCardSpacing);

        preferred +=
            spacing *
            Mathf.Max(0, activeCount - 1);

        if (vertical?.padding != null)
        {
            preferred +=
                vertical.padding.top +
                vertical.padding.bottom;
        }
        else
        {
            preferred +=
                Mathf.Max(0, runtimeVerticalPadding) * 2f;
        }

        return preferred;
    }

    private float ResolveExpandedHeight()
    {
        if (calculatedExpandedHeight <= 0f)
            RecalculateExpandedHeight();

        return Mathf.Max(
            Mathf.Max(32f, minimumExpandedHeight),
            calculatedExpandedHeight);
    }

    private static bool openingRequired(bool open)
    {
        return open;
    }

    private void CaptureHeaderColor()
    {
        if (headerText == null ||
            availableHeaderColorCaptured)
        {
            return;
        }

        availableHeaderColor = headerText.color;
        availableHeaderColorCaptured = true;
    }

    private void RefreshHeaderVisual()
    {
        if (headerText == null)
            return;

        CaptureHeaderColor();

        string categoryName =
            BattleSkillUiText.GetActionTypeName(
                actionType);

        headerText.text = partAccessAllowed
            ? $"{categoryName}  ({visibleSkillCount})"
            : $"{categoryName}  (사용 불가)";

        headerText.color = partAccessAllowed
            ? availableHeaderColor
            : unavailableHeaderColor;
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
