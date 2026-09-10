using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum BattleCharacterDetailTab
{
    Summary,
    Skills,
    Passives,
    Status
}

[DisallowMultipleComponent]
public sealed class BattleCharacterDetailPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private BattleScreenModeController modeController;
    [SerializeField] private BattleCameraDirector cameraDirector;
    [SerializeField] private Button closeButton;

    [Header("DOTween")]
    [SerializeField] private RectTransform animatedSurface;
    [SerializeField, Min(0f)] private float slideDistance = 180f;
    [SerializeField, Min(0.01f)] private float showDuration = 0.24f;
    [SerializeField, Min(0.01f)] private float hideDuration = 0.18f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    [Header("Header")]
    [SerializeField] private Image artworkImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text resourceText;

    [Header("Tabs")]
    [SerializeField] private Button summaryTabButton;
    [SerializeField] private Button skillsTabButton;
    [SerializeField] private Button passivesTabButton;
    [SerializeField] private Button statusTabButton;

    [Header("Content")]
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private Button listButtonTemplate;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailBodyText;

    [Header("Scroll Views")]
    [SerializeField] private ScrollRect summaryScrollRect;
    [SerializeField] private ScrollRect listScrollRect;
    [SerializeField] private ScrollRect detailScrollRect;

    [Header("Keywords")]
    [SerializeField] private RectTransform keywordContent;
    [SerializeField] private Button keywordButtonTemplate;
    [SerializeField] private GameObject keywordPopup;
    [SerializeField] private TMP_Text keywordPopupTitle;
    [SerializeField] private TMP_Text keywordPopupBody;
    [SerializeField] private Button keywordPopupCloseButton;

    [Header("Camera Points")]
    [FormerlySerializedAs("frontCameraPointKey")]
    [SerializeField] private string detailCameraPointKey = "DetailCameraPoint";
    [SerializeField] private bool allowCloseCameraFallback = true;

    [Header("Camera Point Validation")]
    [Tooltip("CameraPoint.forward가 대상 시각 중심을 이 정도 이상 바라봐야 정상으로 판단합니다.")]
    [SerializeField, Range(-1f, 1f)]
    private float minimumTargetFacingDot = 0.15f;

    [Tooltip("잘못된 CameraPoint 회전을 런타임 보정 Transform으로 교체합니다.")]
    [SerializeField]
    private bool autoCorrectMisalignedCameraPoint = true;

    [Tooltip("CameraPoint와 대상 시각 중심이 너무 가까울 때 자동 거리 보정을 사용합니다.")]
    [SerializeField, Min(0.1f)]
    private float minimumDetailCameraDistance = 0.75f;

    private readonly List<GameObject> generatedListItems = new();
    private readonly List<GameObject> generatedKeywordItems = new();

    private Character character;
    private BattleCharacterDetailTab activeTab;
    private Skill selectedSkill;
    private Sequence visibilitySequence;
    private Vector2 shownSurfacePosition;
    private bool shownSurfacePositionCaptured;
    private bool isHiding;
    private CharacterDetailCameraPresenter cameraPresenter;
    private int detailRequestVersion;

    public bool IsVisible =>
        canvasGroup != null &&
        canvasGroup.alpha > 0.001f &&
        gameObject.activeInHierarchy;

    public Character CurrentCharacter => character;

    public void Configure(
        CanvasGroup group,
        BattleScreenModeController screenMode,
        BattleCameraDirector director,
        Button close,
        Image artwork,
        TMP_Text characterName,
        TMP_Text role,
        TMP_Text hp,
        TMP_Text resources,
        Button summaryTab,
        Button skillsTab,
        Button passivesTab,
        Button statusTab,
        TMP_Text summary,
        RectTransform listRoot,
        Button listTemplate,
        TMP_Text detailTitle,
        TMP_Text detailBody,
        RectTransform keywordRoot,
        Button keywordTemplate,
        GameObject popup,
        TMP_Text popupTitle,
        TMP_Text popupBody,
        Button popupClose)
    {
        canvasGroup = group;
        modeController = screenMode;
        cameraDirector = director;
        closeButton = close;
        artworkImage = artwork;
        nameText = characterName;
        roleText = role;
        hpText = hp;
        resourceText = resources;
        summaryTabButton = summaryTab;
        skillsTabButton = skillsTab;
        passivesTabButton = passivesTab;
        statusTabButton = statusTab;
        summaryText = summary;
        listContent = listRoot;
        listButtonTemplate = listTemplate;
        detailTitleText = detailTitle;
        detailBodyText = detailBody;
        keywordContent = keywordRoot;
        keywordButtonTemplate = keywordTemplate;
        keywordPopup = popup;
        keywordPopupTitle = popupTitle;
        keywordPopupBody = popupBody;
        keywordPopupCloseButton = popupClose;
        animatedSurface ??= closeButton != null
            ? closeButton.transform.parent as RectTransform
            : null;

        ResolveUiReferences();
        EnsureCameraPresenter();
        ConfigureScrollViews();
        BindButtons();
        ApplyReadableTextSettings();
        CaptureShownSurfacePosition();
        HideImmediate(false);
    }

    private void Awake()
    {
        ResolveUiReferences();
        EnsureCameraPresenter();
        ConfigureScrollViews();
        canvasGroup ??= GetComponent<CanvasGroup>();

        if (modeController == null)
        {
            modeController =
                FindFirstObjectByType<BattleScreenModeController>(
                    FindObjectsInactive.Include);
        }

        if (cameraDirector == null)
            cameraDirector = FindFirstObjectByType<BattleCameraDirector>();

        animatedSurface ??= closeButton != null
            ? closeButton.transform.parent as RectTransform
            : GetComponentInChildren<RectTransform>(true);

        BindButtons();
        ApplyReadableTextSettings();
        CaptureShownSurfacePosition();

        // 비활성 CharacterDetailLayer가 처음 켜지며 Awake가 실행되는 경우,
        // Show가 설정한 상세 모드를 다시 숨기지 않는다.
        if (modeController == null ||
            modeController.CurrentMode != BattleUiScreenMode.CharacterDetails)
        {
            HideImmediate(false);
        }
    }

    private void Update()
    {
        if (!IsVisible)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetMouseButtonDown(1))
        {
            Hide();
        }
    }

    private void LateUpdate()
    {
        if (IsVisible)
            RefreshHeader();
    }

    private void BindButtons()
    {
        BindButton(closeButton, Hide);
        BindButton(keywordPopupCloseButton, HideKeywordPopup);
        BindButton(summaryTabButton, () => SelectTab(BattleCharacterDetailTab.Summary));
        BindButton(skillsTabButton, () => SelectTab(BattleCharacterDetailTab.Skills));
        BindButton(passivesTabButton, () => SelectTab(BattleCharacterDetailTab.Passives));
        BindButton(statusTabButton, () => SelectTab(BattleCharacterDetailTab.Status));
    }

    public void Show(Character target)
    {
        if (BattlePresentationInteractionLock.IsLocked)
            return;

        if (target == null)
            return;

        ResolveUiReferences();
        EnsureCameraPresenter();
        ConfigureScrollViews();
        BindButtons();
        ApplyReadableTextSettings();

        character = target;
        selectedSkill = null;
        detailRequestVersion++;

        Debug.Log(
            "[CharacterDetailPanel][SHOW] " +
            $"Request={detailRequestVersion}, " +
            $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
            $"TargetType={target.GetType().Name}, " +
            $"TargetPath={BattleCharacterPointerRouter.GetHierarchyPath(target.transform)}",
            target);

        // 모드를 먼저 바꿔 비활성 부모의 Awake가 완료된 뒤 표시한다.
        modeController?.ShowCharacterDetailMode();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        CaptureShownSurfacePosition();
        KillVisibilityTween();
        isHiding = false;

        SelectTab(BattleCharacterDetailTab.Summary);
        RefreshHeader();
        LogBoundDetailData(target);
        cameraPresenter?.RequestFocus(target);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (animatedSurface != null)
        {
            animatedSurface.anchoredPosition =
                shownSurfacePosition + Vector2.right * slideDistance;
        }

        visibilitySequence = DOTween.Sequence()
            .SetUpdate(true);

        if (canvasGroup != null)
            visibilitySequence.Join(canvasGroup.DOFade(1f, showDuration));

        if (animatedSurface != null)
        {
            visibilitySequence.Join(
                animatedSurface
                    .DOAnchorPos(shownSurfacePosition, showDuration)
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

    public void HideForResolution()
    {
        cameraPresenter?.CancelPendingFocus();
        KillVisibilityTween();
        isHiding = false;
        character = null;
        selectedSkill = null;
        ClearGeneratedListItems();
        ClearGeneratedKeywordItems();
        HideKeywordPopup();
        HideImmediate(false);
        cameraPresenter?.ReturnFromInteraction();
        BattleCharacterPointerRouter.ClearPersistentSelection();
    }

    public void Hide()
    {
        if (isHiding)
            return;

        HideKeywordPopup();

        if (!gameObject.activeInHierarchy ||
            canvasGroup == null ||
            canvasGroup.alpha <= 0.001f)
        {
            CompleteHide();
            return;
        }

        CaptureShownSurfacePosition();
        KillVisibilityTween();
        isHiding = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        visibilitySequence = DOTween.Sequence()
            .SetUpdate(true);

        visibilitySequence.Join(canvasGroup.DOFade(0f, hideDuration));

        if (animatedSurface != null)
        {
            visibilitySequence.Join(
                animatedSurface
                    .DOAnchorPos(
                        shownSurfacePosition + Vector2.right * slideDistance,
                        hideDuration)
                    .SetEase(hideEase));
        }

        visibilitySequence.OnComplete(() =>
        {
            visibilitySequence = null;
            CompleteHide();
        });
    }

    private void CompleteHide()
    {
        cameraPresenter?.CancelPendingFocus();
        character = null;
        selectedSkill = null;
        ClearGeneratedListItems();
        ClearGeneratedKeywordItems();
        HideKeywordPopup();
        HideImmediate(false);

        cameraPresenter?.ReturnFromInteraction();
        BattleCharacterPointerRouter.ClearPersistentSelection();
        modeController?.ShowDefaultMode();
    }

    private void HideImmediate(bool showDefaultMode)
    {
        KillVisibilityTween();
        isHiding = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (animatedSurface != null && shownSurfacePositionCaptured)
            animatedSurface.anchoredPosition = shownSurfacePosition;

        if (keywordPopup != null)
            keywordPopup.SetActive(false);

        if (showDefaultMode)
            modeController?.ShowDefaultMode();
    }

    private void SelectTab(
        BattleCharacterDetailTab tab)
    {
        activeTab =
            tab;

        ClearGeneratedListItems();
        ClearGeneratedKeywordItems();
        HideKeywordPopup();

        bool showSummary =
            tab ==
            BattleCharacterDetailTab.Summary;

        GameObject summaryPanel =
            summaryScrollRect != null
                ? summaryScrollRect.gameObject
                : summaryText != null
                    ? summaryText.gameObject
                    : null;

        GameObject listPanel =
            listScrollRect != null
                ? listScrollRect.gameObject
                : listContent != null &&
                  listContent.parent != null &&
                  listContent.parent.parent != null
                    ? listContent.parent.parent.gameObject
                    : null;

        RectTransform descriptionRoot =
            FindNamedComponent<RectTransform>(
                "DescriptionPanel");

        GameObject descriptionPanel =
            descriptionRoot != null
                ? descriptionRoot.gameObject
                : detailBodyText != null &&
                  detailBodyText.transform.parent != null
                    ? detailBodyText.transform.parent.gameObject
                    : null;

        if (summaryPanel != null)
            summaryPanel.SetActive(showSummary);

        if (listPanel != null)
            listPanel.SetActive(!showSummary);

        if (descriptionPanel != null)
            descriptionPanel.SetActive(!showSummary);

        switch (tab)
        {
            case BattleCharacterDetailTab.Summary:
                BuildSummary();
                break;

            case BattleCharacterDetailTab.Skills:
                BuildSkillList();
                break;

            case BattleCharacterDetailTab.Passives:
                BuildPassiveList();
                break;

            case BattleCharacterDetailTab.Status:
                BuildStatusList();
                break;
        }

        SetTabState(
            summaryTabButton,
            tab ==
            BattleCharacterDetailTab.Summary);

        SetTabState(
            skillsTabButton,
            tab ==
            BattleCharacterDetailTab.Skills);

        SetTabState(
            passivesTabButton,
            tab ==
            BattleCharacterDetailTab.Passives);

        SetTabState(
            statusTabButton,
            tab ==
            BattleCharacterDetailTab.Status);

        ResetActiveTabScrolls();
    }

    private void RefreshHeader()
    {
        if (character == null)
            return;

        CharacterData data = character.Data;

        if (nameText != null)
            nameText.text = data?.CharacterName ?? character.name;

        if (roleText != null)
        {
            roleText.text =
                !string.IsNullOrWhiteSpace(data?.RoleName)
                    ? data.RoleName
                    : data?.CombatantTier.ToString() ?? "전투원";
        }

        if (hpText != null)
        {
            hpText.text =
                $"체력  <b>{character.CurrentHP}/{character.MaxCombatHP}</b>";
        }

        if (resourceText != null)
        {
            int prestige = character.RuntimeStatus?.currentPrestige ?? 0;
            int maxPrestige = character.CurrentStatus?.maxPrestige ?? 0;

            resourceText.text =
                $"빛 <b>{character.CurrentEnergy}/{character.MaxEnergy}</b>  " +
                $"위세 <b>{prestige}/{maxPrestige}</b>  " +
                $"가드 <b>{character.RuntimeStatus?.currentBlock ?? 0}</b>";
        }

        if (artworkImage != null)
        {
            Sprite artwork =
                data?.DetailArtwork != null
                    ? data.DetailArtwork
                    : data?.Portrait;

            artworkImage.sprite = artwork;
            artworkImage.enabled = artwork != null;
            artworkImage.preserveAspect = true;
        }
    }

    private void BuildSummary()
    {
        if (character == null || summaryText == null)
            return;

        StringBuilder builder = new();
        CharacterData data = character.Data;

        if (!string.IsNullOrWhiteSpace(data?.UiSummary))
        {
            builder.AppendLine(data.UiSummary);
            builder.AppendLine();
        }

        builder.AppendLine("<b>신체 상태</b>");

        if (character.BodyParts == null ||
            character.BodyParts.Count == 0)
        {
            builder.AppendLine(
                $"본체 HP  {character.CurrentHP}/{character.MaxCombatHP}");
        }
        else
        {
            foreach (BodyPart part in character.BodyParts)
            {
                if (part == null)
                    continue;

                builder.AppendLine(
                    $"{GetPartName(part.Type),-4}  " +
                    $"{Mathf.RoundToInt(part.PartHP)}/" +
                    $"{Mathf.RoundToInt(part.MaxPartHP)}  " +
                    $"[{GetPartStateName(part.State)}]");
            }
        }

        builder.AppendLine();
        builder.AppendLine("<b>기본 전투 수치</b>");
        builder.AppendLine(
            $"속도  {character.CurrentStatus?.minSpeed ?? 0}" +
            $"~{character.CurrentStatus?.maxSpeed ?? 0}");

        StaggerGaugeMechanic stagger =
            character.GetMechanic<StaggerGaugeMechanic>();

        PhysicalResistanceProfile hpResistance =
            data?.PhysicalResistances;

        if (hpResistance != null)
        {
            builder.AppendLine();
            builder.AppendLine("<b>HP 물리 내성</b>");

            if (stagger?.IsVulnerabilityWindowOpen == true)
            {
                float vulnerable =
                    character.BattleContext?.Rules?.Stagger?
                        .VulnerabilityHpResistanceOverride ?? 2f;

                builder.AppendLine(
                    $"<color=#FF7A7A>흐트러짐 취약 · 절단/둔격/관통 전부 ×{vulnerable:0.##}</color>");
            }
            else
            {
                builder.AppendLine(
                    $"절단 ×{hpResistance.GetMultiplier(PhysicalDamageType.Cut):0.##}   " +
                    $"둔격 ×{hpResistance.GetMultiplier(PhysicalDamageType.Blunt):0.##}   " +
                    $"관통 ×{hpResistance.GetMultiplier(PhysicalDamageType.Pierce):0.##}");
            }
        }

        PhysicalResistanceProfile staggerResistance =
            data?.StaggerResistances;

        if (staggerResistance != null)
        {
            builder.AppendLine();
            builder.AppendLine("<b>흐트러짐 내성</b>");
            builder.AppendLine(
                $"절단 ×{staggerResistance.GetMultiplier(PhysicalDamageType.Cut):0.##}   " +
                $"둔격 ×{staggerResistance.GetMultiplier(PhysicalDamageType.Blunt):0.##}   " +
                $"관통 ×{staggerResistance.GetMultiplier(PhysicalDamageType.Pierce):0.##}");
        }

        if (stagger != null)
        {
            builder.AppendLine();
            builder.AppendLine(
                stagger.IsVulnerabilityWindowOpen
                    ? "<b>흐트러짐</b>  <color=#FF7A7A>취약 창 OPEN</color>"
                    : $"<b>흐트러짐</b>  {stagger.CurrentGauge}/{stagger.MaxGauge}");
        }

        summaryText.text = builder.ToString();

        if (detailTitleText != null)
            detailTitleText.text = "전투 요약";

        if (detailBodyText != null)
        {
            detailBodyText.text =
                "캐릭터는 현재 위치와 회전을 유지합니다. " +
                "상세 화면에서는 기존 CharacterCameraPointSet의 정면 CameraPoint로 카메라만 이동합니다.";
        }
    }

    private void BuildSkillList()
    {
        if (character == null)
            return;

        List<Skill> skills = CollectDisplaySkills(character);

        if (skills.Count == 0)
        {
            SetDetailText(
                "스킬",
                "표시 가능한 런타임 스킬이 없습니다. " +
                "Character.RuntimeSkills, CombatLoadout, " +
                "슬롯별 GetSelectableSkills를 모두 확인했습니다.");
            return;
        }

        foreach (Skill skill in skills)
        {
            if (skill == null)
                continue;

            string clashSummary =
                BattleSkillUiText.BuildRollSummary(
                    skill);

            Button item =
                CreateListButton(
                    $"{BattleSkillUiText.GetActionTypeName(skill.ActionType)}  ·  " +
                    $"{skill.SkillName}\n" +
                    $"{clashSummary}  ·  빛 {skill.EnergyCost}");

            if (item == null)
                continue;

            Skill captured = skill;
            item.onClick.AddListener(() => SelectSkill(captured));
        }

        Canvas.ForceUpdateCanvases();

        if (listContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);

        SelectSkill(skills[0]);
    }

    private static List<Skill> CollectDisplaySkills(Character target)
    {
        List<Skill> result = new();
        HashSet<object> visited = new();

        void AddSkill(Skill skill)
        {
            if (skill == null)
                return;

            object key = skill.Definition != null
                ? skill.Definition
                : $"{skill.GetType().FullName}|{skill.ActionType}|{skill.SkillName}";

            if (visited.Add(key))
                result.Add(skill);
        }

        void AddRange(IReadOnlyList<Skill> source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
                AddSkill(source[i]);
        }

        AddRange(target.RuntimeSkills);

        if (target.BodyParts != null &&
            target.BodyParts.Count > 0)
        {
            foreach (BodyPart part in target.BodyParts)
            {
                if (part == null)
                    continue;

                AddRange(character.GetSelectableSkills(part, 0));

                int maxSlots = Mathf.Max(
                    1,
                    target.GetMaxActionSlotsForPart(part));

                for (int actionIndex = 0;
                     actionIndex < maxSlots;
                     actionIndex++)
                {
                    AddRange(
                        target.GetSelectableSkills(
                            part,
                            actionIndex));
                }
            }
        }
        else
        {
            int maxSlots = Mathf.Max(1, target.GetMaxActionSlots());

            for (int actionIndex = 0;
                 actionIndex < maxSlots;
                 actionIndex++)
            {
                AddRange(
                    target.GetSelectableSkills(
                        null,
                        actionIndex));
            }
        }

        return result;
    }

    private void SelectSkill(Skill skill)
    {
        selectedSkill = skill;

        if (detailTitleText != null)
            detailTitleText.text = skill?.SkillName ?? "스킬";

        if (detailBodyText != null)
            detailBodyText.text = BattleSkillUiText.BuildDescription(skill);

        BuildKeywordButtons(
            BattleSkillUiText.BuildKeywords(skill));

        ResetDetailScroll();
    }

    private void BuildPassiveList()
    {
        if (character == null)
            return;

        IReadOnlyList<CombatMechanic> mechanics =
            character.Mechanics;

        if (mechanics == null || mechanics.Count == 0)
        {
            SetDetailText("패시브", "등록된 전투 패시브가 없습니다.");
            return;
        }

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            Button item =
                CreateListButton(mechanic.MechanicName);

            if (item == null)
                continue;

            CombatMechanic captured = mechanic;
            item.onClick.AddListener(
                () => SetDetailText(
                    captured.MechanicName,
                    BuildMechanicDescription(captured)));
        }

        CombatMechanic first = mechanics[0];

        if (first != null)
        {
            SetDetailText(
                first.MechanicName,
                BuildMechanicDescription(first));
        }
    }

    private void BuildStatusList()
    {
        if (character == null)
            return;

        List<StatusEffect> effects = new();

        if (character.StatusEffects != null)
            effects.AddRange(character.StatusEffects);

        if (character.BodyParts != null)
        {
            foreach (BodyPart part in character.BodyParts)
            {
                if (part?.StatusEffects == null)
                    continue;

                effects.AddRange(part.StatusEffects);
            }
        }

        if (effects.Count == 0)
        {
            SetDetailText("상태 효과", "현재 적용 중인 상태 효과가 없습니다.");
            return;
        }

        foreach (StatusEffect effect in effects)
        {
            if (effect == null)
                continue;

            Button item =
                CreateListButton(
                    BattleStatusUiText.BuildListLabel(
                        effect,
                        character));

            if (item == null)
                continue;

            StatusEffect captured = effect;
            item.onClick.AddListener(
                () => SetDetailText(
                    BattleStatusUiText.GetDisplayName(captured),
                    BuildStatusDescription(
                        captured,
                        character)));
        }

        StatusEffect first = effects[0];

        if (first != null)
        {
            SetDetailText(
                BattleStatusUiText.GetDisplayName(first),
                BuildStatusDescription(
                    first,
                    character));
        }
    }

    private Button CreateListButton(string label)
    {
        if (listButtonTemplate == null ||
            listContent == null)
        {
            return null;
        }

        Button item =
            Instantiate(
                listButtonTemplate,
                listContent);

        item.gameObject.SetActive(true);
        item.onClick.RemoveAllListeners();

        TMP_Text text =
            item.GetComponentInChildren<TMP_Text>(true);

        if (text != null)
        {
            text.richText = true;
            text.enableAutoSizing = false;
            text.textWrappingMode =
                TextWrappingModes.Normal;
            text.fontSize = 18f;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = label;
        }

        // 상태/스킬처럼 2줄 요약을 사용하는 항목은 기존 1줄 템플릿 높이에서
        // 잘리지 않도록 런타임 LayoutElement만 확장한다.
        if (!string.IsNullOrEmpty(label) &&
            label.Contains("\n"))
        {
            LayoutElement layout =
                item.GetComponent<LayoutElement>();

            if (layout == null)
                layout = item.gameObject.AddComponent<LayoutElement>();

            layout.minHeight =
                Mathf.Max(
                    layout.minHeight,
                    54f);

            layout.preferredHeight =
                Mathf.Max(
                    layout.preferredHeight,
                    58f);
        }

        generatedListItems.Add(item.gameObject);
        return item;
    }

    private void BuildKeywordButtons(
        IReadOnlyList<SkillKeywordEntry> keywords)
    {
        ClearGeneratedKeywordItems();

        bool hasKeywords =
            keywords != null &&
            keywords.Count > 0 &&
            keywordContent != null &&
            keywordButtonTemplate != null;

        if (keywordContent != null)
        {
            keywordContent.gameObject.SetActive(
                hasKeywords);
        }

        if (!hasKeywords)
        {
            ResetDetailScroll();
            return;
        }

        foreach (SkillKeywordEntry keyword
                 in keywords)
        {
            if (keyword == null ||
                string.IsNullOrWhiteSpace(
                    keyword.Name))
            {
                continue;
            }

            Button button =
                Instantiate(
                    keywordButtonTemplate,
                    keywordContent);

            button.gameObject.SetActive(
                true);

            button.onClick.RemoveAllListeners();

            TMP_Text text =
                button.GetComponentInChildren<
                    TMP_Text>(
                        true);

            if (text != null)
            {
                text.richText = true;
                text.text =
                    BattleKeywordGlossary.ColorizeKeyword(
                        keyword.Name);
            }

            SkillKeywordEntry captured =
                keyword;

            button.onClick.AddListener(
                () => ShowKeywordPopup(
                    captured));

            generatedKeywordItems.Add(
                button.gameObject);
        }

        Canvas.ForceUpdateCanvases();

        if (detailScrollRect?.content != null)
        {
            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    detailScrollRect.content);
        }

        ResetDetailScroll();
    }

    private void ShowKeywordPopup(SkillKeywordEntry keyword)
    {
        if (keyword == null)
            return;

        if (keywordPopup != null)
            keywordPopup.SetActive(true);

        if (keywordPopupTitle != null)
        {
            keywordPopupTitle.richText = true;
            keywordPopupTitle.text =
                BattleKeywordGlossary.ColorizeKeyword(
                    keyword.Name);
        }

        if (keywordPopupBody != null)
        {
            string description =
                string.IsNullOrWhiteSpace(keyword.Description)
                    ? BattleKeywordGlossary.GetDescription(keyword.Name)
                    : keyword.Description;

            keywordPopupBody.text =
                BattleKeywordGlossary.ColorizeText(
                    description,
                    new[] { keyword.Name });
        }
    }

    private void HideKeywordPopup()
    {
        if (keywordPopup != null)
            keywordPopup.SetActive(false);
    }

    private static void LogBoundDetailData(
        Character target)
    {
        if (target == null)
            return;

        CharacterData data =
            target.Data;

        Debug.Log(
            "[CharacterDetailPanel][DATA_BIND] " +
            $"Target={GetCharacterName(target)}#{target.GetInstanceID()}, " +
            $"TargetType={target.GetType().Name}, " +
            $"DataAsset={(data != null ? data.name : "NULL")}, " +
            $"DataCharacterName={(data != null ? data.CharacterName : "NULL")}, " +
            $"CombatantTier={(data != null ? data.CombatantTier.ToString() : "NULL")}, " +
            $"RoleName={(data != null ? data.RoleName : "NULL")}, " +
            $"Portrait={(data != null && data.Portrait != null ? data.Portrait.name : "NULL")}, " +
            $"DetailArtwork={(data != null && data.DetailArtwork != null ? data.DetailArtwork.name : "NULL")}",
            target);
    }

    private static string GetCharacterName(Character target)
    {
        if (target == null)
            return "NULL";

        return target.Data?.CharacterName ?? target.name;
    }

    private void EnsureCameraPresenter()
    {
        cameraPresenter ??=
            new CharacterDetailCameraPresenter(
                this,
                () => character,
                () =>
                    modeController?.CurrentMode ==
                    BattleUiScreenMode.CharacterDetails);

        cameraPresenter.Configure(
            cameraDirector,
            detailCameraPointKey,
            allowCloseCameraFallback,
            minimumTargetFacingDot,
            autoCorrectMisalignedCameraPoint,
            minimumDetailCameraDistance);
    }

    private void ResolveUiReferences()
    {
        canvasGroup ??=
            GetComponent<CanvasGroup>();

        if (modeController == null)
        {
            modeController =
                FindFirstObjectByType<
                    BattleScreenModeController>(
                        FindObjectsInactive.Include);
        }

        if (cameraDirector == null)
        {
            cameraDirector =
                FindFirstObjectByType<
                    BattleCameraDirector>();
        }

        animatedSurface ??=
            FindNamedComponent<RectTransform>(
                "LimbusStyleDetailSurface");

        closeButton ??=
            FindNamedComponent<Button>(
                "CloseButton");

        artworkImage ??=
            FindNamedComponent<Image>(
                "Artwork");

        nameText ??=
            FindNamedComponent<TMP_Text>(
                "CharacterName");

        roleText ??=
            FindNamedComponent<TMP_Text>(
                "RoleName");

        hpText ??=
            FindNamedComponent<TMP_Text>(
                "HpText");

        resourceText ??=
            FindNamedComponent<TMP_Text>(
                "ResourceText");

        summaryTabButton ??=
            FindNamedComponent<Button>(
                "SummaryTab");

        skillsTabButton ??=
            FindNamedComponent<Button>(
                "SkillsTab");

        passivesTabButton ??=
            FindNamedComponent<Button>(
                "PassivesTab");

        statusTabButton ??=
            FindNamedComponent<Button>(
                "StatusTab");

        summaryText ??=
            FindNamedComponent<TMP_Text>(
                "SummaryText");

        summaryScrollRect ??=
            FindNamedComponent<ScrollRect>(
                "SummaryScroll");

        listScrollRect ??=
            FindNamedComponent<ScrollRect>(
                "ListScroll");

        detailScrollRect ??=
            FindNamedComponent<ScrollRect>(
                "DetailScroll");

        listContent ??=
            listScrollRect != null
                ? listScrollRect.content
                : null;

        listButtonTemplate ??=
            FindNamedComponent<Button>(
                "ListButtonTemplate");

        detailTitleText ??=
            FindNamedComponent<TMP_Text>(
                "DetailTitle");

        detailBodyText ??=
            FindNamedComponent<TMP_Text>(
                "DetailBody");

        keywordContent ??=
            FindNamedComponent<RectTransform>(
                "KeywordRow");

        keywordButtonTemplate ??=
            FindNamedComponent<Button>(
                "KeywordTemplate");

        RectTransform popupRect =
            FindNamedComponent<RectTransform>(
                "KeywordPopup");

        keywordPopup ??=
            popupRect != null
                ? popupRect.gameObject
                : null;

        keywordPopupTitle ??=
            FindNamedComponent<TMP_Text>(
                "PopupTitle");

        keywordPopupBody ??=
            FindNamedComponent<TMP_Text>(
                "PopupBody");

        keywordPopupCloseButton ??=
            FindNamedComponent<Button>(
                "PopupClose");
    }

    private T FindNamedComponent<T>(string objectName)
        where T : Component
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        T[] components = GetComponentsInChildren<T>(true);

        foreach (T component in components)
        {
            if (component != null &&
                component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private void ConfigureScrollViews()
    {
        ConfigureVerticalScrollRect(
            summaryScrollRect);

        ConfigureVerticalScrollRect(
            listScrollRect);

        ConfigureVerticalScrollRect(
            detailScrollRect);
    }

    private static void ConfigureVerticalScrollRect(
        ScrollRect scrollRect)
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontal =
            false;

        scrollRect.vertical =
            true;

        scrollRect.movementType =
            ScrollRect.MovementType.Clamped;

        scrollRect.inertia =
            true;

        scrollRect.scrollSensitivity =
            32f;
    }

    private void ResetActiveTabScrolls()
    {
        Canvas.ForceUpdateCanvases();

        if (activeTab ==
            BattleCharacterDetailTab.Summary)
        {
            ResetScrollRect(
                summaryScrollRect);

            return;
        }

        ResetScrollRect(
            listScrollRect);

        ResetScrollRect(
            detailScrollRect);
    }

    private static void ResetScrollRect(
        ScrollRect scrollRect)
    {
        if (scrollRect == null)
            return;

        if (scrollRect.content != null)
        {
            LayoutRebuilder
                .ForceRebuildLayoutImmediate(
                    scrollRect.content);
        }

        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition =
            1f;
    }

    private void ResetDetailScroll()
    {
        Canvas.ForceUpdateCanvases();
        ResetScrollRect(
            detailScrollRect);
    }

    private void ApplyReadableTextSettings()
    {
        ConfigureText(nameText, 34f, TextAlignmentOptions.Left, false);
        ConfigureText(roleText, 20f, TextAlignmentOptions.Left, false);
        ConfigureText(hpText, 21f, TextAlignmentOptions.Left, false);
        ConfigureText(resourceText, 20f, TextAlignmentOptions.Right, false);
        ConfigureText(summaryText, 20f, TextAlignmentOptions.TopLeft, true);
        ConfigureText(detailTitleText, 25f, TextAlignmentOptions.Left, false);
        ConfigureText(detailBodyText, 19f, TextAlignmentOptions.TopLeft, true);
        ConfigureText(keywordPopupTitle, 23f, TextAlignmentOptions.Left, false);
        ConfigureText(keywordPopupBody, 18f, TextAlignmentOptions.TopLeft, true);
    }

    private static void ConfigureText(
        TMP_Text text,
        float size,
        TextAlignmentOptions alignment,
        bool wrap)
    {
        if (text == null)
            return;

        text.richText = true;
        text.enableAutoSizing = false;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode =
            wrap
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
        text.overflowMode = wrap
            ? TextOverflowModes.Overflow
            : TextOverflowModes.Ellipsis;
    }

    private void CaptureShownSurfacePosition()
    {
        if (shownSurfacePositionCaptured || animatedSurface == null)
            return;

        shownSurfacePosition = animatedSurface.anchoredPosition;
        shownSurfacePositionCaptured = true;
    }

    private void KillVisibilityTween()
    {
        if (visibilitySequence == null)
            return;

        visibilitySequence.Kill(false);
        visibilitySequence = null;
    }

    private void SetDetailText(
        string title,
        string body)
    {
        if (detailTitleText != null)
            detailTitleText.text = title;

        if (detailBodyText != null)
            detailBodyText.text = body;

        ClearGeneratedKeywordItems();
        ResetDetailScroll();
    }

    private static string BuildMechanicDescription(
        CombatMechanic mechanic)
    {
        if (mechanic == null)
            return "설명이 없습니다.";

        return
            $"런타임 타입: {mechanic.GetType().Name}\n" +
            $"등록 상태: {(mechanic.IsRegistered ? "활성" : "비활성")}\n" +
            "구체적인 수치와 발동 조건은 해당 CombatMechanic 구현 및 캐릭터 데이터에 따릅니다.";
    }

    private static string BuildStatusDescription(
        StatusEffect effect,
        Character viewedCharacter)
    {
        return BattleStatusUiText.BuildDescription(
            effect,
            viewedCharacter);
    }

    private void ClearGeneratedListItems()
    {
        DestroyGenerated(generatedListItems);
    }

    private void ClearGeneratedKeywordItems()
    {
        DestroyGenerated(
            generatedKeywordItems);

        if (keywordContent != null)
        {
            keywordContent.gameObject.SetActive(
                false);
        }
    }

    private static void DestroyGenerated(List<GameObject> targets)
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            GameObject target = targets[i];

            if (target == null)
                continue;

            if (target.activeSelf)
                target.SetActive(false);

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        targets.Clear();
    }

    private static void BindButton(Button button, Action action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => action());
    }

    private static void SetTabState(Button button, bool active)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();

        if (image != null)
        {
            image.color = active
                ? new Color(0.12f, 0.45f, 0.82f, 1f)
                : new Color(0.10f, 0.12f, 0.16f, 0.96f);
        }
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

    private static string GetPartStateName(BodyPartState state)
    {
        return state switch
        {
            BodyPartState.Normal => "정상",
            BodyPartState.Weakened => "약화",
            BodyPartState.Broken => "파괴",
            _ => state.ToString()
        };
    }

    private void OnDisable()
    {
        cameraPresenter?.CancelPendingFocus();
    }

    private void OnDestroy()
    {
        cameraPresenter?.Dispose();
        cameraPresenter = null;
        KillVisibilityTween();
        ClearGeneratedListItems();
        ClearGeneratedKeywordItems();
    }
}