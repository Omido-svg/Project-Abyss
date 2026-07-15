using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BattleParticipantSide
{
    Player,
    Enemy
}

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

    [Header("Automatic Grid Layout")]
    [SerializeField] private bool configureGridLayout = true;
    [SerializeField] private Vector2 cellSize = new(150f, 120f);
    [SerializeField] private Vector2 spacing = new(10f, 10f);
    [SerializeField, Min(1)] private int playerColumns = 4;
    [SerializeField, Min(1)] private int enemyColumns = 4;

    [SerializeField]
    private bool fitContainerHeightToGeneratedRows = true;

    [Header("Automatic Screen Placement")]
    [SerializeField]
    private bool autoPositionContainers = true;

    [SerializeField, Min(0f)]
    private float rightMargin = 40f;

    [SerializeField, Min(0f)]
    private float enemyTopMargin = 40f;

    [SerializeField, Min(0f)]
    private float playerBottomMargin = 40f;

    [Tooltip(
        "켜면 Cell Size와 Column 수를 사용해서 컨테이너 폭을 자동 계산합니다.")]
    [SerializeField]
    private bool fitContainerWidthToColumns = true;

    [SerializeField, Min(1f)]
    private float fallbackContainerWidth = 640f;

    private readonly List<BodyPartButton> generatedButtons = new();

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
        if (context == null || context.Player == null)
        {
            Debug.LogError(
                "[BattleParticipantButtonFactory] " +
                "BattleContext 또는 Player가 없습니다.");

            return false;
        }

        PrepareTemplates();

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
            RemoveNonTemplateButtons(
                playerContainer,
                playerButtonTemplate);

            RemoveNonTemplateButtons(
                enemyContainer,
                ResolveEnemyTemplate());
        }

        ConfigureLayout(
            playerContainer,
            playerColumns,
            BattleParticipantSide.Player);

        ConfigureLayout(
            enemyContainer,
            enemyColumns,
            BattleParticipantSide.Enemy);

        BuildCharacterButtons(
            context.Player,
            BattleParticipantSide.Player,
            0,
            playerContainer,
            playerButtonTemplate,
            uiManager,
            registry);

        if (context.Enemies != null)
        {
            for (int enemyIndex = 0;
                 enemyIndex < context.Enemies.Count;
                 enemyIndex++)
            {
                Character enemy = context.Enemies[enemyIndex];

                if (enemy == null)
                    continue;

                BuildCharacterButtons(
                    enemy,
                    BattleParticipantSide.Enemy,
                    enemyIndex,
                    enemyContainer,
                    ResolveEnemyTemplate(),
                    uiManager,
                    registry);
            }
        }

        registry?.RebuildFromScene();

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            playerContainer);

        LayoutRebuilder.ForceRebuildLayoutImmediate(
            enemyContainer);

        BattleDebugLog.UI(
            "[BattleParticipantButtonFactory] " +
            $"Generated Buttons={generatedButtons.Count}",
            BattleLogLevel.Info,
            this);

        return true;
    }

    [ContextMenu("Apply Right Side Container Layout")]
    public void ApplyContainerLayoutNow()
    {
        ConfigureLayout(
            playerContainer,
            playerColumns,
            BattleParticipantSide.Player);

        ConfigureLayout(
            enemyContainer,
            enemyColumns,
            BattleParticipantSide.Enemy);

        if (playerContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                playerContainer);
        }

        if (enemyContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(
                enemyContainer);
        }
    }

    public void ClearGeneratedButtons()
    {
        for (int i = generatedButtons.Count - 1;
             i >= 0;
             i--)
        {
            BodyPartButton button = generatedButtons[i];

            if (button == null)
                continue;

            button.Bind(null, null);
            button.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(button.gameObject);
            else
                DestroyImmediate(button.gameObject);
        }

        generatedButtons.Clear();
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

        if (character.IsSingleHpTarget)
        {
            CreateButton(
                character,
                null,
                side,
                participantIndex,
                container,
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

        if (character.BodyParts == null)
            return;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            CreateButton(
                character,
                part,
                side,
                participantIndex,
                container,
                template,
                uiManager,
                registry);
        }
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

        button.Bind(owner, part);
        button.gameObject.SetActive(true);

        generatedButtons.Add(button);
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

    private BodyPartButton ResolveEnemyTemplate()
    {
        return enemyButtonTemplate != null
            ? enemyButtonTemplate
            : playerButtonTemplate;
    }

    private void RemoveNonTemplateButtons(
        RectTransform container,
        BodyPartButton template)
    {
        if (container == null)
            return;

        BodyPartButton[] buttons =
            container.GetComponentsInChildren<BodyPartButton>(true);

        foreach (BodyPartButton button in buttons)
        {
            if (button == null ||
                button == template ||
                generatedButtons.Contains(button))
            {
                continue;
            }

            button.Bind(null, null);
            button.gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(button.gameObject);
            else
                DestroyImmediate(button.gameObject);
        }
    }

    private void ConfigureLayout(
        RectTransform container,
        int columnCount,
        BattleParticipantSide side)
    {
        if (container == null)
            return;

        int safeColumnCount =
            Mathf.Max(
                1,
                columnCount);

        if (autoPositionContainers)
        {
            ConfigureContainerPlacement(
                container,
                safeColumnCount,
                side);
        }

        if (!configureGridLayout)
            return;

        GridLayoutGroup grid =
            container.GetComponent<GridLayoutGroup>();

        if (grid == null)
        {
            grid =
                container.gameObject
                    .AddComponent<GridLayoutGroup>();
        }

        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint =
            GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = safeColumnCount;

        if (side == BattleParticipantSide.Enemy)
        {
            grid.startCorner =
                GridLayoutGroup.Corner.UpperRight;

            grid.childAlignment =
                TextAnchor.UpperRight;
        }
        else
        {
            grid.startCorner =
                GridLayoutGroup.Corner.LowerRight;

            grid.childAlignment =
                TextAnchor.LowerRight;
        }

        if (!fitContainerHeightToGeneratedRows)
            return;

        ContentSizeFitter fitter =
            container.GetComponent<ContentSizeFitter>();

        if (fitter == null)
        {
            fitter =
                container.gameObject
                    .AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;

        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ConfigureContainerPlacement(
        RectTransform container,
        int columnCount,
        BattleParticipantSide side)
    {
        Vector2 anchor =
            side == BattleParticipantSide.Enemy
                ? new Vector2(1f, 1f)
                : new Vector2(1f, 0f);

        container.anchorMin = anchor;
        container.anchorMax = anchor;
        container.pivot = anchor;

        container.anchoredPosition =
            side == BattleParticipantSide.Enemy
                ? new Vector2(
                    -Mathf.Max(0f, rightMargin),
                    -Mathf.Max(0f, enemyTopMargin))
                : new Vector2(
                    -Mathf.Max(0f, rightMargin),
                    Mathf.Max(0f, playerBottomMargin));

        float width =
            fitContainerWidthToColumns
                ? CalculateContainerWidth(columnCount)
                : Mathf.Max(
                    1f,
                    fallbackContainerWidth);

        container.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width);

        container.localScale =
            Vector3.one;
    }

    private float CalculateContainerWidth(
        int columnCount)
    {
        int safeColumnCount =
            Mathf.Max(
                1,
                columnCount);

        float cellsWidth =
            Mathf.Max(
                1f,
                cellSize.x) *
            safeColumnCount;

        float spacingWidth =
            Mathf.Max(
                0f,
                spacing.x) *
            Mathf.Max(
                0,
                safeColumnCount - 1);

        return cellsWidth + spacingWidth;
    }

    private static BodyPartButton FindFirstValid(
        IReadOnlyList<BodyPartButton> source)
    {
        if (source == null)
            return null;

        for (int i = 0; i < source.Count; i++)
        {
            if (source[i] != null)
                return source[i];
        }

        return null;
    }

    private void OnDestroy()
    {
        ClearGeneratedButtons();
    }
}
