using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BattleParticipantSide
{
    Player,
    Enemy
}

/// <summary>
/// 전투 참가자와 부위 버튼을 생성한다.
///
/// v1.2.4부터 부위 버튼을 Container에 바로 펼치지 않고,
/// Character Group Panel 아래에 묶는다.
///
/// Side Container
/// └─ ParticipantGroup_0
///    ├─ CharacterHeader
///    └─ PartRow
///       ├─ LEGS
///       ├─ RIGHT_HAND
///       ├─ LEFT_HAND
///       └─ HEAD
///
/// 따라서 같은 캐릭터의 부위가 가로 한 줄로 묶이고,
/// 각 캐릭터 패널의 폭은 실제 부위 버튼 수만큼만 차지한다.
/// 적 캐릭터 그룹들은 EnemySpawn/context 순서대로 상단 한 줄에 배치되며,
/// 플레이어 그룹도 그 바로 아래의 상단 영역에 표시된다.
/// </summary>
public sealed class BattleParticipantButtonFactory : MonoBehaviour
{
    [Header("Containers")]
    [SerializeField] private RectTransform playerContainer;
    [SerializeField] private RectTransform enemyContainer;

    [Header("Templates")]
    [Tooltip(
        "Scene의 비활성 Template 또는 BodyPartButton Prefab을 사용할 수 있습니다.")]
    [SerializeField] private BodyPartButton playerButtonTemplate;
    [SerializeField] private BodyPartButton enemyButtonTemplate;

    [Header("Runtime Cleanup")]
    [SerializeField] private bool removeLegacyButtonsOnBuild = true;

    [SerializeField] private Vector2 cellSize = new(150f, 120f);
    [SerializeField] private Vector2 spacing = new(10f, 10f);
    [SerializeField, Min(1)] private int playerColumns = 4;
    [SerializeField, Min(1)] private int enemyColumns = 4;

    [Tooltip(
        "기존 Scene 직렬화 호환용입니다. Group Layout은 항상 오른쪽 기준으로 정렬합니다.")]
    [SerializeField] private bool horizontalClashOverview;

    [Header("Character Group Layout")]
    [SerializeField]
    private Vector2 groupedPartCellSize =
        new Vector2(150f, 105f);

    [SerializeField, Min(0f)]
    private float groupedPartSpacing = 10f;

    [SerializeField, Min(0f)]
    private float participantGroupSpacing = 10f;

    [SerializeField, Min(18f)]
    private float participantHeaderHeight = 34f;

    [SerializeField, Min(0f)]
    private float participantGroupPadding = 8f;

    [SerializeField, Range(10f, 24f)]
    private float participantHeaderFontSize = 18f;

    [Header("Automatic Screen Placement")]
    [SerializeField]
    private bool autoPositionContainers = true;

    [SerializeField, Min(0f)]
    private float rightMargin = 40f;

    [SerializeField, Min(0f)]
    private float enemyTopMargin = 40f;

    [Tooltip(
        "플레이어 그룹과 적 그룹 가로 스트립 사이의 세로 간격입니다.")]
    [SerializeField, Min(0f)]
    private float playerStripVerticalGap = 72f;

    [Tooltip(
        "적 그룹을 현재 카메라에서 보이는 좌→우 배치 순서로 정렬합니다. " +
        "카메라가 없으면 BattleContext 순서를 유지합니다.")]
    [SerializeField]
    private bool orderEnemiesByScreenPlacement = true;

    [Tooltip(
        "적 부위 행과 플레이어 부위 행 사이에서 타깃 화살표가 충분히 보이도록 확보할 최소 간격입니다.")]
    [SerializeField, Min(12f)]
    private float minimumArrowLaneHeight = 72f;

    [Header("Overview Panel")]
    [SerializeField]
    private bool enableOverviewCollapse = true;

    [SerializeField, Min(0f)]
    private float overviewTitleReservedHeight = 44f;

    [SerializeField, Min(48f)]
    private float overviewToggleWidth = 92f;

    [SerializeField, Min(22f)]
    private float overviewToggleHeight = 30f;

    private sealed class EnemyDisplayEntry
    {
        public Character Character;
        public int RosterIndex;
        public float ScreenOrder;
    }

    private readonly List<BodyPartButton> generatedButtons =
        new List<BodyPartButton>();

    private readonly List<GameObject> generatedGroups =
        new List<GameObject>();

    private RectTransform playerRuntimeStrip;
    private RectTransform enemyRuntimeStrip;

    private RectTransform overviewRoot;
    private Button overviewToggleButton;
    private TMP_Text overviewToggleLabel;
    private TMP_Text overviewTitleText;
    private GameObject overviewArrowLayer;
    private bool overviewCollapsed;
    private bool overviewStateLoaded;

    private const string OverviewCollapsedPrefKey =
        "ProjectAbyss.BattleOverview.Collapsed";

    public IReadOnlyList<BodyPartButton> GeneratedButtons =>
        generatedButtons;

    public RectTransform PlayerContainer => playerContainer;
    public RectTransform EnemyContainer => enemyContainer;
    public BodyPartButton PlayerTemplate => playerButtonTemplate;
    public BodyPartButton EnemyTemplate => enemyButtonTemplate;

    public bool IsConfigured =>
        playerContainer != null &&
        enemyContainer != null &&
        playerButtonTemplate != null &&
        ResolveEnemyTemplate() != null;

    public void Configure(
        BodyPartButton newPlayerTemplate,
        BodyPartButton newEnemyTemplate,
        RectTransform newPlayerContainer,
        RectTransform newEnemyContainer)
    {
        playerButtonTemplate = newPlayerTemplate;
        enemyButtonTemplate = newEnemyTemplate;
        playerContainer = newPlayerContainer;
        enemyContainer = newEnemyContainer;

        PrepareTemplates();
    }

    public void ConfigureFromLegacy(
        IReadOnlyList<BodyPartButton> playerButtons,
        IReadOnlyList<BodyPartButton> enemyButtons)
    {
        if (playerButtonTemplate == null)
            playerButtonTemplate = FindFirstValid(playerButtons);

        if (enemyButtonTemplate == null)
            enemyButtonTemplate = FindFirstValid(enemyButtons);

        if (playerContainer == null &&
            playerButtonTemplate != null)
        {
            playerContainer =
                playerButtonTemplate.transform.parent as RectTransform;
        }

        if (enemyContainer == null &&
            enemyButtonTemplate != null)
        {
            enemyContainer =
                enemyButtonTemplate.transform.parent as RectTransform;
        }

        PrepareTemplates();
    }

    public bool Rebuild(
        BattleContext context,
        BattleUIManager uiManager,
        BodyPartButtonRegistry registry)
    {
        if (context == null ||
            context.Player == null)
        {
            Debug.LogError(
                "[BattleParticipantButtonFactory] " +
                "BattleContext 또는 Player가 없습니다.");

            return false;
        }

        PrepareTemplates();
        NormalizeLayoutDefaults();

        if (playerContainer != null)
            playerContainer.gameObject.SetActive(true);

        if (enemyContainer != null)
            enemyContainer.gameObject.SetActive(true);

        if (!IsConfigured)
        {
            Debug.LogError(
                "[BattleParticipantButtonFactory] " +
                "Container와 Button Template 연결이 필요합니다.");

            return false;
        }

        ClearGeneratedButtons();

        if (removeLegacyButtonsOnBuild)
        {
            RemoveRuntimeGroupObjects(playerContainer);
            RemoveRuntimeGroupObjects(enemyContainer);

            RemoveNonTemplateDirectButtons(
                playerContainer,
                playerButtonTemplate);

            RemoveNonTemplateDirectButtons(
                enemyContainer,
                ResolveEnemyTemplate());
        }

        ConfigureGroupedContainerLayout(
            playerContainer,
            playerColumns,
            BattleParticipantSide.Player);

        ConfigureGroupedContainerLayout(
            enemyContainer,
            enemyColumns,
            BattleParticipantSide.Enemy);

        playerRuntimeStrip =
            CreateParticipantStrip(
                playerContainer,
                BattleParticipantSide.Player);

        enemyRuntimeStrip =
            CreateParticipantStrip(
                enemyContainer,
                BattleParticipantSide.Enemy);

        BuildCharacterButtons(
            context.Player,
            BattleParticipantSide.Player,
            0,
            playerRuntimeStrip,
            playerButtonTemplate,
            uiManager,
            registry);

        BuildEnemyButtonsInPlacementOrder(
            context.Enemies,
            enemyRuntimeStrip,
            ResolveEnemyTemplate(),
            uiManager,
            registry);

        registry?.RebuildFromScene();

        ForceContainerLayout(playerRuntimeStrip);
        ForceContainerLayout(enemyRuntimeStrip);
        ForceContainerLayout(playerContainer);
        ForceContainerLayout(enemyContainer);

        AlignParticipantStrips();
        EnsureOverviewPanelControls();
        ApplyOverviewCollapsedState();

        BattleDebugLog.UI(
            "[BattleParticipantButtonFactory] " +
            $"CharacterGroups={generatedGroups.Count}, " +
            $"PartButtons={generatedButtons.Count}",
            BattleLogLevel.Info,
            this);

        return true;
    }

    [ContextMenu("Apply Grouped Right Side Layout")]
    public void ApplyContainerLayoutNow()
    {
        NormalizeLayoutDefaults();

        ConfigureGroupedContainerLayout(
            playerContainer,
            playerColumns,
            BattleParticipantSide.Player);

        ConfigureGroupedContainerLayout(
            enemyContainer,
            enemyColumns,
            BattleParticipantSide.Enemy);

        ForceContainerLayout(playerRuntimeStrip);
        ForceContainerLayout(enemyRuntimeStrip);
        ForceContainerLayout(playerContainer);
        ForceContainerLayout(enemyContainer);

        AlignParticipantStrips();
        EnsureOverviewPanelControls();
        ApplyOverviewCollapsedState();
    }

    public void ClearGeneratedButtons()
    {
        for (int i = generatedButtons.Count - 1;
             i >= 0;
             i--)
        {
            BodyPartButton button =
                generatedButtons[i];

            if (button != null)
                button.ReleaseForDestruction();
        }

        for (int i = generatedGroups.Count - 1;
             i >= 0;
             i--)
        {
            GameObject group =
                generatedGroups[i];

            DestroyGeneratedObject(group);
        }

        DestroyGeneratedObject(
            playerRuntimeStrip != null
                ? playerRuntimeStrip.gameObject
                : null);

        DestroyGeneratedObject(
            enemyRuntimeStrip != null
                ? enemyRuntimeStrip.gameObject
                : null);

        playerRuntimeStrip = null;
        enemyRuntimeStrip = null;

        generatedButtons.Clear();
        generatedGroups.Clear();
    }

    public void PrepareTemplates()
    {
        playerButtonTemplate?.ConfigureAsTemplate();

        if (enemyButtonTemplate != null &&
            enemyButtonTemplate != playerButtonTemplate)
        {
            enemyButtonTemplate.ConfigureAsTemplate();
        }
    }

    private void BuildEnemyButtonsInPlacementOrder(
        IReadOnlyList<Character> enemies,
        RectTransform container,
        BodyPartButton template,
        BattleUIManager uiManager,
        BodyPartButtonRegistry registry)
    {
        if (enemies == null ||
            container == null ||
            template == null)
        {
            return;
        }

        List<EnemyDisplayEntry> entries =
            new List<EnemyDisplayEntry>();

        Camera activeCamera =
            Camera.main;

        if (activeCamera == null)
        {
            activeCamera =
                FindFirstObjectByType<Camera>();
        }

        for (int rosterIndex = 0;
             rosterIndex < enemies.Count;
             rosterIndex++)
        {
            Character enemy =
                enemies[rosterIndex];

            if (enemy == null)
                continue;

            float order = rosterIndex;

            if (orderEnemiesByScreenPlacement &&
                activeCamera != null)
            {
                Vector3 screenPoint =
                    activeCamera.WorldToScreenPoint(
                        enemy.transform.position);

                if (screenPoint.z > 0f)
                {
                    order = screenPoint.x;
                }
                else
                {
                    order = Vector3.Dot(
                        activeCamera.transform.right,
                        enemy.transform.position -
                        activeCamera.transform.position);
                }
            }

            entries.Add(
                new EnemyDisplayEntry
                {
                    Character = enemy,
                    RosterIndex = rosterIndex,
                    ScreenOrder = order
                });
        }

        entries.Sort(
            (left, right) =>
            {
                int orderCompare =
                    left.ScreenOrder.CompareTo(
                        right.ScreenOrder);

                if (orderCompare != 0)
                    return orderCompare;

                return left.RosterIndex.CompareTo(
                    right.RosterIndex);
            });

        for (int visualIndex = 0;
             visualIndex < entries.Count;
             visualIndex++)
        {
            BuildCharacterButtons(
                entries[visualIndex].Character,
                BattleParticipantSide.Enemy,
                visualIndex,
                container,
                template,
                uiManager,
                registry);
        }
    }

    private void BuildCharacterButtons(
        Character character,
        BattleParticipantSide side,
        int participantIndex,
        RectTransform container,
        BodyPartButton template,
        BattleUIManager uiManager,
        BodyPartButtonRegistry registry)
    {
        if (character == null ||
            container == null ||
            template == null)
        {
            return;
        }

        List<BodyPart> orderedParts =
            BuildOrderedPartList(character);

        int displayedPartCount =
            character.IsSingleHpTarget
                ? 1
                : orderedParts.Count;

        if (displayedPartCount <= 0)
            return;

        RectTransform partRow =
            CreateParticipantGroup(
                character,
                side,
                participantIndex,
                displayedPartCount,
                container);

        if (partRow == null)
            return;

        if (character.IsSingleHpTarget)
        {
            CreateButton(
                character,
                null,
                side,
                participantIndex,
                partRow,
                template,
                uiManager,
                registry);

            if (side == BattleParticipantSide.Player)
            {
                Debug.LogWarning(
                    "[BattleParticipantButtonFactory] " +
                    "단일 HP 캐릭터가 Player에 배치되었습니다. " +
                    "현재 플레이어 행동 선택은 부위형 Character를 권장합니다.");
            }

            return;
        }

        for (int i = 0;
             i < orderedParts.Count;
             i++)
        {
            CreateButton(
                character,
                orderedParts[i],
                side,
                participantIndex,
                partRow,
                template,
                uiManager,
                registry);
        }
    }

    private RectTransform CreateParticipantStrip(
        RectTransform container,
        BattleParticipantSide side)
    {
        if (container == null)
            return null;

        string stripName =
            side == BattleParticipantSide.Player
                ? "ParticipantStrip_Player"
                : "ParticipantStrip_Enemy";

        Transform existing =
            container.Find(stripName);

        if (existing != null)
        {
            DestroyGeneratedObject(
                existing.gameObject);
        }

        GameObject stripObject =
            new GameObject(
                stripName,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(ContentSizeFitter));

        RectTransform stripRect =
            stripObject.GetComponent<RectTransform>();

        stripRect.SetParent(container, false);
        stripRect.anchorMin = new Vector2(1f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(1f, 1f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.sizeDelta = Vector2.zero;
        stripRect.localScale = Vector3.one;

        HorizontalLayoutGroup layout =
            stripObject.GetComponent<HorizontalLayoutGroup>();

        layout.padding =
            new RectOffset(0, 0, 0, 0);
        layout.spacing =
            Mathf.Max(
                0f,
                participantGroupSpacing);
        layout.childAlignment =
            TextAnchor.UpperLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

#if UNITY_2021_2_OR_NEWER
        layout.reverseArrangement = false;
#endif

        ContentSizeFitter fitter =
            stripObject.GetComponent<ContentSizeFitter>();

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        return stripRect;
    }

    private RectTransform CreateParticipantGroup(
        Character character,
        BattleParticipantSide side,
        int participantIndex,
        int partCount,
        RectTransform container)
    {
        float groupWidth =
            CalculateParticipantGroupWidth(
                partCount);

        float groupHeight =
            participantGroupPadding * 2f +
            participantHeaderHeight +
            4f +
            Mathf.Max(
                1f,
                groupedPartCellSize.y);

        GameObject groupObject =
            new GameObject(
                BuildGroupName(
                    character,
                    side,
                    participantIndex),
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(UnityEngine.UI.Outline),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));

        RectTransform groupRect =
            groupObject.GetComponent<RectTransform>();

        groupRect.SetParent(container, false);
        groupRect.anchorMin = new Vector2(1f, 1f);
        groupRect.anchorMax = new Vector2(1f, 1f);
        groupRect.pivot = new Vector2(1f, 1f);
        groupRect.sizeDelta =
            new Vector2(
                groupWidth,
                groupHeight);

        Image groupImage =
            groupObject.GetComponent<Image>();

        Color groupColor =
            ResolveGroupColor(
                character,
                side);

        groupImage.color = groupColor;
        groupImage.raycastTarget = false;

        UnityEngine.UI.Outline outline =
            groupObject.GetComponent<UnityEngine.UI.Outline>();

        outline.effectColor =
            ResolveGroupAccent(
                character,
                side);

        outline.effectDistance =
            new Vector2(2f, -2f);

        outline.useGraphicAlpha = true;

        VerticalLayoutGroup groupLayout =
            groupObject.GetComponent<VerticalLayoutGroup>();

        int padding =
            Mathf.RoundToInt(
                Mathf.Max(
                    0f,
                    participantGroupPadding));

        groupLayout.padding =
            new RectOffset(
                padding,
                padding,
                padding,
                padding);

        groupLayout.spacing = 4f;
        groupLayout.childAlignment =
            TextAnchor.UpperRight;
        groupLayout.childControlWidth = true;
        groupLayout.childControlHeight = false;
        groupLayout.childForceExpandWidth = true;
        groupLayout.childForceExpandHeight = false;

        LayoutElement groupElement =
            groupObject.GetComponent<LayoutElement>();

        groupElement.preferredWidth = groupWidth;
        groupElement.preferredHeight = groupHeight;
        groupElement.minWidth = groupWidth;
        groupElement.minHeight = groupHeight;
        groupElement.flexibleWidth = 0f;
        groupElement.flexibleHeight = 0f;

        CreateCharacterHeader(
            groupRect,
            character,
            side,
            participantIndex,
            partCount);

        RectTransform partRow =
            CreatePartRow(
                groupRect,
                partCount);

        generatedGroups.Add(groupObject);

        return partRow;
    }

    private void CreateCharacterHeader(
        RectTransform parent,
        Character character,
        BattleParticipantSide side,
        int participantIndex,
        int partCount)
    {
        GameObject headerObject =
            new GameObject(
                "CharacterHeader",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));

        RectTransform headerRect =
            headerObject.GetComponent<RectTransform>();

        headerRect.SetParent(parent, false);
        headerRect.sizeDelta =
            new Vector2(
                0f,
                participantHeaderHeight);

        Image headerImage =
            headerObject.GetComponent<Image>();

        Color accent =
            ResolveGroupAccent(
                character,
                side);

        headerImage.color =
            new Color(
                accent.r,
                accent.g,
                accent.b,
                0.32f);

        headerImage.raycastTarget = false;

        LayoutElement headerElement =
            headerObject.GetComponent<LayoutElement>();

        headerElement.preferredHeight =
            participantHeaderHeight;
        headerElement.minHeight =
            participantHeaderHeight;
        headerElement.flexibleHeight = 0f;

        GameObject labelObject =
            new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        RectTransform labelRect =
            labelObject.GetComponent<RectTransform>();

        labelRect.SetParent(headerRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(7f, 0f);
        labelRect.offsetMax = new Vector2(-7f, 0f);

        TextMeshProUGUI label =
            labelObject.GetComponent<TextMeshProUGUI>();

        label.raycastTarget = false;
        label.richText = true;
        label.fontSize =
            participantHeaderFontSize;
        label.textWrappingMode =
            TextWrappingModes.NoWrap;
        label.alignment =
            TextAlignmentOptions.Left;
        label.color = Color.white;
        label.text =
            BuildCharacterHeaderText(
                character,
                side,
                participantIndex,
                partCount);
    }

    private RectTransform CreatePartRow(
        RectTransform parent,
        int partCount)
    {
        GameObject rowObject =
            new GameObject(
                "PartRow",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));

        RectTransform rowRect =
            rowObject.GetComponent<RectTransform>();

        rowRect.SetParent(parent, false);
        rowRect.sizeDelta =
            new Vector2(
                0f,
                Mathf.Max(
                    1f,
                    groupedPartCellSize.y));

        HorizontalLayoutGroup rowLayout =
            rowObject.GetComponent<HorizontalLayoutGroup>();

        rowLayout.padding =
            new RectOffset(0, 0, 0, 0);

        rowLayout.spacing =
            Mathf.Max(
                0f,
                groupedPartSpacing);

        rowLayout.childAlignment =
            TextAnchor.MiddleRight;

        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        LayoutElement rowElement =
            rowObject.GetComponent<LayoutElement>();

        rowElement.preferredHeight =
            Mathf.Max(
                1f,
                groupedPartCellSize.y);

        rowElement.minHeight =
            rowElement.preferredHeight;

        rowElement.preferredWidth =
            CalculatePartRowWidth(partCount);

        rowElement.flexibleWidth = 1f;
        rowElement.flexibleHeight = 0f;

        return rowRect;
    }

    private void CreateButton(
        Character owner,
        BodyPart part,
        BattleParticipantSide side,
        int participantIndex,
        RectTransform container,
        BodyPartButton template,
        BattleUIManager uiManager,
        BodyPartButtonRegistry registry)
    {
        BodyPartButton button =
            Instantiate(
                template,
                container);

        button.ConfigureRuntime(
            uiManager,
            registry,
            side,
            participantIndex);

        button.name =
            BuildButtonName(
                owner,
                part,
                side,
                participantIndex);

        ConfigureGeneratedButtonLayout(button);
        button.Bind(owner, part);
        button.gameObject.SetActive(true);

        generatedButtons.Add(button);
    }

    private void ConfigureGeneratedButtonLayout(
        BodyPartButton button)
    {
        RectTransform rect =
            button?.transform as RectTransform;

        if (rect == null)
            return;

        rect.localScale = Vector3.one;
        rect.sizeDelta =
            new Vector2(
                Mathf.Max(
                    1f,
                    groupedPartCellSize.x),
                Mathf.Max(
                    1f,
                    groupedPartCellSize.y));

        LayoutElement layout =
            button.GetComponent<LayoutElement>();

        if (layout == null)
        {
            layout =
                button.gameObject
                    .AddComponent<LayoutElement>();
        }

        layout.minWidth = rect.sizeDelta.x;
        layout.preferredWidth = rect.sizeDelta.x;
        layout.flexibleWidth = 0f;
        layout.minHeight = rect.sizeDelta.y;
        layout.preferredHeight = rect.sizeDelta.y;
        layout.flexibleHeight = 0f;
    }

    private void ConfigureGroupedContainerLayout(
        RectTransform container,
        int maxPartsPerCharacter,
        BattleParticipantSide side)
    {
        if (container == null)
            return;

        ConfigureContainerAnchor(container);

        if (autoPositionContainers)
        {
            ConfigureContainerPlacement(
                container,
                maxPartsPerCharacter,
                side);
        }

        // 기존 Scene에는 GridLayoutGroup이 직렬화되어 있다.
        // v1.2.4.4부터 외부 Container는 배치 기준점으로만 사용하고,
        // 실제 캐릭터 그룹 배치는 ParticipantStrip의
        // HorizontalLayoutGroup이 담당한다.
        LayoutGroup[] oldGroups =
            container.GetComponents<LayoutGroup>();

        for (int i = 0;
             i < oldGroups.Length;
             i++)
        {
            if (oldGroups[i] != null)
                oldGroups[i].enabled = false;
        }

        ContentSizeFitter oldFitter =
            container.GetComponent<ContentSizeFitter>();

        if (oldFitter != null)
            oldFitter.enabled = false;

        container.localScale = Vector3.one;
    }

    private static void ConfigureContainerAnchor(
        RectTransform container)
    {
        if (container == null)
            return;

        Vector2 topRight =
            new Vector2(1f, 1f);

        container.anchorMin = topRight;
        container.anchorMax = topRight;
        container.pivot = topRight;
        container.localScale = Vector3.one;
    }

    private void ConfigureContainerPlacement(
        RectTransform container,
        int maxPartsPerCharacter,
        BattleParticipantSide side)
    {
        if (container == null)
            return;

        ConfigureContainerAnchor(container);

        float groupHeight =
            GetParticipantGroupHeight();

        float contentTop =
            Mathf.Max(
                Mathf.Max(0f, enemyTopMargin),
                Mathf.Max(0f, overviewTitleReservedHeight));

        float arrowLane =
            Mathf.Max(
                Mathf.Max(0f, playerStripVerticalGap),
                Mathf.Max(12f, minimumArrowLaneHeight));

        float initialYOffset =
            side == BattleParticipantSide.Enemy
                ? contentTop
                : contentTop +
                  groupHeight +
                  arrowLane;

        container.anchoredPosition =
            new Vector2(
                -Mathf.Max(
                    0f,
                    rightMargin),
                -initialYOffset);

        // 실제 폭은 ParticipantStrip의 ContentSizeFitter가
        // 캐릭터 그룹들의 가변 폭 합계로 결정한다.
        container.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            1f);

        container.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            groupHeight);

        container.localScale =
            Vector3.one;
    }

    /// <summary>
    /// 적 스트립은 제목 바로 아래의 첫 번째 행에 둔다.
    /// 플레이어 스트립은 적 전체 폭의 중앙 아래에 배치하며,
    /// 두 행 사이에 타깃 화살표 전용 공간을 확보한다.
    /// </summary>
    private void AlignParticipantStrips()
    {
        if (playerContainer == null ||
            enemyContainer == null ||
            playerRuntimeStrip == null ||
            enemyRuntimeStrip == null)
        {
            return;
        }

        ConfigureContainerAnchor(playerContainer);
        ConfigureContainerAnchor(enemyContainer);

        ForceContainerLayout(playerRuntimeStrip);
        ForceContainerLayout(enemyRuntimeStrip);

        float playerWidth =
            GetPreferredLayoutWidth(
                playerRuntimeStrip);

        float enemyWidth =
            GetPreferredLayoutWidth(
                enemyRuntimeStrip);

        float groupHeight =
            GetParticipantGroupHeight();

        float contentTop =
            Mathf.Max(
                Mathf.Max(0f, enemyTopMargin),
                Mathf.Max(0f, overviewTitleReservedHeight));

        float arrowLane =
            Mathf.Max(
                Mathf.Max(0f, playerStripVerticalGap),
                Mathf.Max(12f, minimumArrowLaneHeight));

        float enemyRightX =
            -Mathf.Max(
                0f,
                rightMargin);

        float playerRightX =
            enemyRightX -
            ((enemyWidth - playerWidth) * 0.5f);

        enemyContainer.anchoredPosition =
            new Vector2(
                enemyRightX,
                -contentTop);

        playerContainer.anchoredPosition =
            new Vector2(
                playerRightX,
                -(contentTop +
                  groupHeight +
                  arrowLane));

        playerContainer.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Max(1f, playerWidth));

        enemyContainer.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            Mathf.Max(1f, enemyWidth));

        playerContainer.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            groupHeight);

        enemyContainer.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            groupHeight);

        ForceContainerLayout(playerContainer);
        ForceContainerLayout(enemyContainer);
    }

    private static float GetPreferredLayoutWidth(
        RectTransform value)
    {
        if (value == null)
            return 1f;

        Canvas.ForceUpdateCanvases();

        float preferred =
            LayoutUtility.GetPreferredWidth(value);

        if (preferred <= 0f)
            preferred = value.rect.width;

        return Mathf.Max(
            1f,
            preferred);
    }

    private float GetParticipantGroupHeight()
    {
        return
            participantGroupPadding * 2f +
            participantHeaderHeight +
            4f +
            Mathf.Max(
                1f,
                groupedPartCellSize.y);
    }

    private void EnsureOverviewPanelControls()
    {
        if (!enableOverviewCollapse ||
            playerContainer == null ||
            enemyContainer == null)
        {
            return;
        }

        RectTransform playerParent =
            playerContainer.parent as RectTransform;

        RectTransform enemyParent =
            enemyContainer.parent as RectTransform;

        if (playerParent == null ||
            playerParent != enemyParent)
        {
            return;
        }

        overviewRoot = playerParent;

        // 전체 전투 현황 패널은 배경 없이 동작한다.
        Image panelImage =
            overviewRoot.GetComponent<Image>();

        if (panelImage != null)
        {
            Color color = panelImage.color;
            color.a = 0f;
            panelImage.color = color;
            panelImage.raycastTarget = false;
        }

        UnityEngine.UI.Outline panelOutline =
            overviewRoot.GetComponent<UnityEngine.UI.Outline>();

        if (panelOutline != null)
            panelOutline.enabled = false;

        Transform titleTransform =
            overviewRoot.Find("Title");

        overviewTitleText =
            titleTransform != null
                ? titleTransform.GetComponent<TMP_Text>()
                : null;

        if (overviewTitleText != null)
        {
            RectTransform titleRect =
                overviewTitleText.transform as RectTransform;

            titleRect.anchorMin =
                new Vector2(1f, 1f);
            titleRect.anchorMax =
                new Vector2(1f, 1f);
            titleRect.pivot =
                new Vector2(1f, 1f);
            titleRect.anchoredPosition =
                new Vector2(
                    -(overviewToggleWidth + 18f),
                    -6f);
            titleRect.sizeDelta =
                new Vector2(
                    420f,
                    34f);

            overviewTitleText.alignment =
                TextAlignmentOptions.Right;
            overviewTitleText.textWrappingMode =
                TextWrappingModes.NoWrap;
            overviewTitleText.raycastTarget = false;
        }

        HideLegacyOverviewLabel("PlayerSideLabel");
        HideLegacyOverviewLabel("EnemySideLabel");
        HideLegacyOverviewLabel("OverviewHint");

        Transform arrow =
            overviewRoot.Find("ClashArrowLayer");

        overviewArrowLayer =
            arrow != null
                ? arrow.gameObject
                : null;

        ConfigureOverviewArrowLayer(arrow);

        Transform existing =
            overviewRoot.Find(
                "OverviewCollapseButton");

        if (existing != null)
        {
            overviewToggleButton =
                existing.GetComponent<Button>();

            overviewToggleLabel =
                existing.GetComponentInChildren<TMP_Text>(
                    true);
        }

        if (overviewToggleButton == null)
        {
            GameObject buttonObject =
                new GameObject(
                    "OverviewCollapseButton",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));

            RectTransform buttonRect =
                buttonObject.GetComponent<RectTransform>();

            buttonRect.SetParent(
                overviewRoot,
                false);
            buttonRect.anchorMin =
                new Vector2(1f, 1f);
            buttonRect.anchorMax =
                new Vector2(1f, 1f);
            buttonRect.pivot =
                new Vector2(1f, 1f);
            buttonRect.anchoredPosition =
                new Vector2(-8f, -7f);
            buttonRect.sizeDelta =
                new Vector2(
                    overviewToggleWidth,
                    overviewToggleHeight);

            Image buttonImage =
                buttonObject.GetComponent<Image>();

            buttonImage.color =
                new Color(
                    0.055f,
                    0.11f,
                    0.18f,
                    0.92f);

            overviewToggleButton =
                buttonObject.GetComponent<Button>();

            ColorBlock colors =
                overviewToggleButton.colors;

            colors.normalColor =
                new Color(
                    0.055f,
                    0.11f,
                    0.18f,
                    0.92f);
            colors.highlightedColor =
                new Color(
                    0.10f,
                    0.25f,
                    0.40f,
                    1f);
            colors.pressedColor =
                new Color(
                    0.04f,
                    0.08f,
                    0.13f,
                    1f);
            colors.selectedColor =
                colors.highlightedColor;
            overviewToggleButton.colors = colors;

            GameObject labelObject =
                new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            RectTransform labelRect =
                labelObject.GetComponent<RectTransform>();

            labelRect.SetParent(
                buttonRect,
                false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            overviewToggleLabel =
                labelObject.GetComponent<TextMeshProUGUI>();

            overviewToggleLabel.alignment =
                TextAlignmentOptions.Center;
            overviewToggleLabel.fontSize = 15f;
            overviewToggleLabel.fontStyle =
                FontStyles.Bold;

            if (overviewTitleText != null &&
                overviewTitleText.font != null)
            {
                overviewToggleLabel.font =
                    overviewTitleText.font;
            }

            overviewToggleLabel.color =
                Color.white;
            overviewToggleLabel.textWrappingMode =
                TextWrappingModes.NoWrap;
            overviewToggleLabel.raycastTarget = false;
        }

        if (arrow != null)
            arrow.SetAsLastSibling();

        if (overviewTitleText != null)
            overviewTitleText.transform.SetAsLastSibling();

        if (overviewToggleButton != null)
            overviewToggleButton.transform.SetAsLastSibling();

        overviewToggleButton.onClick.RemoveListener(
            ToggleOverviewCollapsed);

        overviewToggleButton.onClick.AddListener(
            ToggleOverviewCollapsed);

        if (!overviewStateLoaded)
        {
            overviewCollapsed =
                PlayerPrefs.GetInt(
                    OverviewCollapsedPrefKey,
                    0) != 0;

            overviewStateLoaded = true;
        }
    }

    private static void ConfigureOverviewArrowLayer(
        Transform arrowLayer)
    {
        RectTransform arrowRect =
            arrowLayer as RectTransform;

        if (arrowRect == null)
            return;

        arrowRect.anchorMin = Vector2.zero;
        arrowRect.anchorMax = Vector2.one;
        arrowRect.pivot =
            new Vector2(0.5f, 0.5f);
        arrowRect.offsetMin = Vector2.zero;
        arrowRect.offsetMax = Vector2.zero;
        arrowRect.localScale = Vector3.one;

        CanvasGroup group =
            arrowRect.GetComponent<CanvasGroup>();

        if (group == null)
        {
            group =
                arrowRect.gameObject
                    .AddComponent<CanvasGroup>();
        }

        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void HideLegacyOverviewLabel(
        string childName)
    {
        if (overviewRoot == null ||
            string.IsNullOrWhiteSpace(childName))
        {
            return;
        }

        Transform child =
            overviewRoot.Find(childName);

        if (child != null)
            child.gameObject.SetActive(false);
    }

    private void ToggleOverviewCollapsed()
    {
        overviewCollapsed =
            !overviewCollapsed;

        PlayerPrefs.SetInt(
            OverviewCollapsedPrefKey,
            overviewCollapsed ? 1 : 0);

        PlayerPrefs.Save();

        BattleCharacterPointerRouter
            .BlockWorldInputForFrames(2);

        ApplyOverviewCollapsedState();
    }

    private void ApplyOverviewCollapsedState()
    {
        if (!enableOverviewCollapse)
            return;

        bool showContent =
            !overviewCollapsed;

        if (playerContainer != null)
            playerContainer.gameObject.SetActive(showContent);

        if (enemyContainer != null)
            enemyContainer.gameObject.SetActive(showContent);

        if (overviewArrowLayer != null)
            overviewArrowLayer.SetActive(showContent);

        if (overviewToggleLabel != null)
        {
            overviewToggleLabel.text =
                overviewCollapsed
                    ? "최대화"
                    : "최소화";
        }

        if (overviewTitleText != null)
        {
            overviewTitleText.text =
                overviewCollapsed
                    ? "전체 전투 현황  ·  접힘"
                    : "전체 전투 현황";
        }
    }

    private void NormalizeLayoutDefaults()
    {
        if (groupedPartCellSize.x < 48f)
        {
            groupedPartCellSize.x =
                Mathf.Max(
                    150f,
                    cellSize.x);
        }

        if (groupedPartCellSize.y < 40f)
        {
            groupedPartCellSize.y =
                Mathf.Max(
                    105f,
                    cellSize.y * 0.88f);
        }

        if (groupedPartSpacing <= 0f)
        {
            groupedPartSpacing =
                Mathf.Max(
                    10f,
                    spacing.x);
        }

        if (participantGroupSpacing <= 0f)
        {
            participantGroupSpacing =
                Mathf.Max(
                    10f,
                    spacing.y);
        }

        if (participantHeaderHeight < 18f)
            participantHeaderHeight = 34f;

        if (participantGroupPadding < 2f)
            participantGroupPadding = 8f;

        if (participantHeaderFontSize < 10f)
            participantHeaderFontSize = 18f;

        if (enemyTopMargin < 18f)
            enemyTopMargin = 38f;

        if (playerStripVerticalGap < 64f)
            playerStripVerticalGap = 72f;

        if (minimumArrowLaneHeight < 64f)
            minimumArrowLaneHeight = 72f;

        if (overviewTitleReservedHeight < 36f)
            overviewTitleReservedHeight = 44f;

        if (overviewToggleWidth < 48f)
            overviewToggleWidth = 92f;

        if (overviewToggleHeight < 22f)
            overviewToggleHeight = 30f;
    }

    private float CalculateParticipantGroupWidth(
        int partCount)
    {
        return
            participantGroupPadding * 2f +
            CalculatePartRowWidth(
                Mathf.Max(
                    1,
                    partCount));
    }

    private float CalculateGroupedContainerWidth(
        int maxPartsPerCharacter)
    {
        int partCapacity =
            Mathf.Max(
                1,
                maxPartsPerCharacter);

        float width =
            participantGroupPadding * 2f +
            Mathf.Max(
                1f,
                groupedPartCellSize.x) *
            partCapacity +
            Mathf.Max(
                0f,
                groupedPartSpacing) *
            Mathf.Max(
                0,
                partCapacity - 1);

        return Mathf.Max(
            1f,
            width);
    }

    private float CalculatePartRowWidth(
        int partCount)
    {
        int safePartCount =
            Mathf.Max(
                1,
                partCount);

        return
            Mathf.Max(
                1f,
                groupedPartCellSize.x) *
            safePartCount +
            Mathf.Max(
                0f,
                groupedPartSpacing) *
            Mathf.Max(
                0,
                safePartCount - 1);
    }

    private static List<BodyPart> BuildOrderedPartList(
        Character character)
    {
        List<BodyPart> result =
            new List<BodyPart>();

        if (character?.BodyParts == null)
            return result;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part != null)
                result.Add(part);
        }

        result.Sort(
            (left, right) =>
                GetPartDisplayOrder(left) -
                GetPartDisplayOrder(right));

        return result;
    }

    private static int GetPartDisplayOrder(
        BodyPart part)
    {
        if (part == null)
            return int.MaxValue;

        return part.Type switch
        {
            PartType.LEGS => 0,
            PartType.RIGHT_HAND => 1,
            PartType.LEFT_HAND => 2,
            PartType.HEAD => 3,
            _ => 99
        };
    }

    private static string BuildGroupName(
        Character character,
        BattleParticipantSide side,
        int participantIndex)
    {
        string characterName =
            character?.Data?.CharacterName ??
            character?.name ??
            "NULL";

        return
            $"ParticipantGroup_{side}_" +
            $"{participantIndex}_{characterName}";
    }

    private static string BuildButtonName(
        Character owner,
        BodyPart part,
        BattleParticipantSide side,
        int participantIndex)
    {
        string characterName =
            owner?.Data?.CharacterName ??
            owner?.name ??
            "NULL";

        string targetName =
            part == null
                ? "SINGLE_HP"
                : part.Type.ToString();

        return
            $"{side}_{participantIndex}_" +
            $"{characterName}_{targetName}";
    }

    private static string BuildCharacterHeaderText(
        Character character,
        BattleParticipantSide side,
        int participantIndex,
        int partCount)
    {
        string name =
            character?.Data?.CharacterName ??
            character?.name ??
            "이름 없음";

        string ownerLabel =
            side == BattleParticipantSide.Player
                ? "플레이어"
                : $"적 {participantIndex + 1}";

        string tier =
            GetTierLabel(
                character?.Data?.CombatantTier ??
                (side == BattleParticipantSide.Player
                    ? CombatantTier.Player
                    : CombatantTier.NormalEnemy));

        string partLabel =
            character?.IsSingleHpTarget == true
                ? "본체"
                : $"{Mathf.Max(0, partCount)}부위";

        return
            $"<b>{ownerLabel}</b>  ·  " +
            $"{name}  ·  {tier}  ·  {partLabel}";
    }

    private static string GetTierLabel(
        CombatantTier tier)
    {
        return tier switch
        {
            CombatantTier.Player => "캐릭터",
            CombatantTier.NormalEnemy => "일반",
            CombatantTier.EliteEnemy => "정예",
            CombatantTier.Boss => "보스",
            _ => tier.ToString()
        };
    }

    private static Color ResolveGroupColor(
        Character character,
        BattleParticipantSide side)
    {
        if (side == BattleParticipantSide.Player)
        {
            return new Color(
                0.025f,
                0.11f,
                0.19f,
                0.92f);
        }

        CombatantTier tier =
            character?.Data?.CombatantTier ??
            CombatantTier.NormalEnemy;

        return tier switch
        {
            CombatantTier.EliteEnemy =>
                new Color(
                    0.18f,
                    0.10f,
                    0.045f,
                    0.94f),

            CombatantTier.Boss =>
                new Color(
                    0.22f,
                    0.035f,
                    0.045f,
                    0.95f),

            _ =>
                new Color(
                    0.055f,
                    0.15f,
                    0.09f,
                    0.93f)
        };
    }

    private static Color ResolveGroupAccent(
        Character character,
        BattleParticipantSide side)
    {
        if (side == BattleParticipantSide.Player)
        {
            return new Color(
                0.18f,
                0.62f,
                1f,
                0.95f);
        }

        CombatantTier tier =
            character?.Data?.CombatantTier ??
            CombatantTier.NormalEnemy;

        return tier switch
        {
            CombatantTier.EliteEnemy =>
                new Color(
                    1f,
                    0.52f,
                    0.15f,
                    0.95f),

            CombatantTier.Boss =>
                new Color(
                    1f,
                    0.16f,
                    0.20f,
                    0.98f),

            _ =>
                new Color(
                    0.30f,
                    0.86f,
                    0.48f,
                    0.92f)
        };
    }

    private BodyPartButton ResolveEnemyTemplate()
    {
        return enemyButtonTemplate != null
            ? enemyButtonTemplate
            : playerButtonTemplate;
    }

    private void RemoveRuntimeGroupObjects(
        RectTransform container)
    {
        if (container == null)
            return;

        for (int i = container.childCount - 1;
             i >= 0;
             i--)
        {
            Transform child =
                container.GetChild(i);

            if (child == null)
                continue;

            bool isRuntimeGroup =
                child.name.StartsWith(
                    "ParticipantGroup_",
                    System.StringComparison.Ordinal);

            bool isRuntimeStrip =
                child.name.StartsWith(
                    "ParticipantStrip_",
                    System.StringComparison.Ordinal);

            if (!isRuntimeGroup &&
                !isRuntimeStrip)
            {
                continue;
            }

            BodyPartButton[] buttons =
                child.GetComponentsInChildren<BodyPartButton>(
                    true);

            for (int buttonIndex = 0;
                 buttonIndex < buttons.Length;
                 buttonIndex++)
            {
                buttons[buttonIndex]?
                    .ReleaseForDestruction();
            }

            DestroyGeneratedObject(
                child.gameObject);
        }
    }

    private void RemoveNonTemplateDirectButtons(
        RectTransform container,
        BodyPartButton template)
    {
        if (container == null)
            return;

        BodyPartButton[] buttons =
            container.GetComponentsInChildren<BodyPartButton>(
                true);

        for (int i = 0;
             i < buttons.Length;
             i++)
        {
            BodyPartButton button =
                buttons[i];

            if (button == null ||
                button == template ||
                generatedButtons.Contains(button) ||
                button.transform.parent != container)
            {
                continue;
            }

            button.ReleaseForDestruction();
            DestroyGeneratedObject(button.gameObject);
        }
    }

    private static void ForceContainerLayout(
        RectTransform container)
    {
        if (container == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            container);

        Canvas.ForceUpdateCanvases();

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            container);
    }

    private static void DestroyGeneratedObject(
        Object value)
    {
        if (value == null)
            return;

        GameObject gameObject =
            value as GameObject;

        if (gameObject != null &&
            gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }

        if (Application.isPlaying)
            Object.Destroy(value);
        else
            Object.DestroyImmediate(value);
    }

    private static BodyPartButton FindFirstValid(
        IReadOnlyList<BodyPartButton> source)
    {
        if (source == null)
            return null;

        for (int i = 0;
             i < source.Count;
             i++)
        {
            if (source[i] != null)
                return source[i];
        }

        return null;
    }

    private void OnDestroy()
    {
        // Scene unload 중에는 자식 GameObject도 이미 파괴 절차에 들어간다.
        // 일반 재빌드 정리는 ClearGeneratedButtons()가 담당한다.
        generatedButtons.Clear();
        generatedGroups.Clear();
    }
}
