using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Planning 전용 Screen-space 행동 순서 보조 레일.
///
/// 핵심 원칙:
/// - 대상 지정은 여전히 캐릭터 머리 위 World ActionSlot에서 한다.
/// - 이 레일은 3D Perspective에서 비교하기 어려운 "속도/행동 순서"만 보조한다.
/// - 실제 합 여부는 ClashBuilder Preview 결과를 그대로 사용한다.
/// - Raycast를 받지 않아 기존 월드 슬롯 클릭을 절대 가로막지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleActionOrderRailUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager uiManager;
    [SerializeField] private SkillSelectPanelUI skillSelectPanel;

    [Header("Layout")]
    [SerializeField, Min(140f)] private float width = 220f;
    [SerializeField, Min(240f)] private float height = 520f;
    [SerializeField] private Vector2 topLeftOffset = new(14f, -104f);
    [SerializeField, Range(4, 20)] private int maximumRows = 10;

    [Header("Visual")]
    [SerializeField] private Color panelColor =
        new(0.025f, 0.035f, 0.055f, 0.78f);

    [SerializeField] private Color playerAccent =
        new(0.18f, 0.52f, 1f, 0.95f);

    [SerializeField] private Color enemyAccent =
        new(1f, 0.18f, 0.18f, 0.95f);

    [SerializeField] private Color clashAccent =
        new(1f, 0.82f, 0.12f, 1f);

    [SerializeField] private Color selectedBackground =
        new(0.12f, 0.30f, 0.52f, 0.82f);

    [SerializeField] private Color hoveredTargetBackground =
        new(0.44f, 0.28f, 0.06f, 0.86f);

    private Canvas overlayCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform panelRoot;
    private RectTransform contentRoot;
    private TMP_Text headerText;
    private readonly List<GameObject> generatedRows = new();
    private readonly ActionPhaseSorter sorter = new();
    private readonly HashSet<ActionSlot> clashSlots = new();

    private int lastSignature = int.MinValue;

    public void Configure(
        BattleManager manager,
        BattleUIManager managerUi,
        Camera _)
    {
        battleManager = manager;
        uiManager = managerUi;
        ResolveReferences();
        EnsureView();
        Rebuild(force: true);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureView();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        EnsureView();

        if (overlayCanvas == null ||
            canvasGroup == null)
        {
            return;
        }

        bool hidden =
            battleManager == null ||
            battleManager.ActionManager == null ||
            battleManager.BattleContext == null ||
            battleManager.TurnManager?.IsResolving == true ||
            skillSelectPanel != null &&
            skillSelectPanel.BlocksWorldPlanningOverlay;

        canvasGroup.alpha =
            hidden
                ? 0f
                : 1f;

        if (hidden)
            return;

        Rebuild(force: false);
    }

    private void ResolveReferences()
    {
        battleManager ??=
            FindFirstObjectByType<BattleManager>();

        uiManager ??=
            FindFirstObjectByType<BattleUIManager>(
                FindObjectsInactive.Include);

        if (skillSelectPanel == null)
        {
            skillSelectPanel =
                FindFirstObjectByType<SkillSelectPanelUI>(
                    FindObjectsInactive.Include);
        }
    }

    private void EnsureView()
    {
        if (overlayCanvas != null &&
            panelRoot != null &&
            contentRoot != null)
        {
            return;
        }

        Transform existing =
            transform.Find(
                "PlanningActionOrderOverlay");

        GameObject canvasGo;

        if (existing != null)
        {
            canvasGo =
                existing.gameObject;
        }
        else
        {
            canvasGo =
                new GameObject(
                    "PlanningActionOrderOverlay",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster),
                    typeof(CanvasGroup));

            canvasGo.transform.SetParent(
                transform,
                false);
        }

        overlayCanvas =
            canvasGo.GetComponent<Canvas>();

        overlayCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        overlayCanvas.overrideSorting =
            true;

        // 화살표(20)보다 뒤, 일반 전장보다 앞.
        overlayCanvas.sortingOrder = 18;

        CanvasScaler scaler =
            canvasGo.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f);

        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster =
            canvasGo.GetComponent<GraphicRaycaster>();

        // 이 레일은 정보 전용이다.
        raycaster.enabled = false;

        canvasGroup =
            canvasGo.GetComponent<CanvasGroup>();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform canvasRect =
            canvasGo.GetComponent<RectTransform>();

        Stretch(canvasRect);

        Transform panelExisting =
            canvasGo.transform.Find("Panel");

        GameObject panelGo;

        if (panelExisting != null)
        {
            panelGo =
                panelExisting.gameObject;
        }
        else
        {
            panelGo =
                new GameObject(
                    "Panel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));
            panelGo.transform.SetParent(
                canvasGo.transform,
                false);
        }

        panelRoot =
            panelGo.GetComponent<RectTransform>();

        panelRoot.anchorMin =
            new Vector2(0f, 1f);

        panelRoot.anchorMax =
            new Vector2(0f, 1f);

        panelRoot.pivot =
            new Vector2(0f, 1f);

        panelRoot.anchoredPosition =
            topLeftOffset;

        panelRoot.sizeDelta =
            new Vector2(
                width,
                height);

        Image panelImage =
            panelGo.GetComponent<Image>();

        panelImage.color =
            panelColor;

        panelImage.raycastTarget = false;

        Outline outline =
            panelGo.GetComponent<Outline>();

        outline.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.78f);

        outline.effectDistance =
            new Vector2(
                2f,
                -2f);

        headerText =
            CreateText(
                "Header",
                panelRoot,
                20f,
                TextAlignmentOptions.Left);

        RectTransform headerRect =
            headerText.rectTransform;

        headerRect.anchorMin =
            new Vector2(0f, 1f);

        headerRect.anchorMax =
            new Vector2(1f, 1f);

        headerRect.pivot =
            new Vector2(0.5f, 1f);

        headerRect.anchoredPosition =
            new Vector2(0f, -8f);

        headerRect.sizeDelta =
            new Vector2(-18f, 34f);

        headerText.fontStyle =
            FontStyles.Bold;

        headerText.text =
            "행동 순서";

        Transform contentExisting =
            panelGo.transform.Find("Content");

        GameObject contentGo;

        if (contentExisting != null)
        {
            contentGo =
                contentExisting.gameObject;
        }
        else
        {
            contentGo =
                new GameObject(
                    "Content",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup));

            contentGo.transform.SetParent(
                panelGo.transform,
                false);
        }

        contentRoot =
            contentGo.GetComponent<RectTransform>();

        contentRoot.anchorMin =
            new Vector2(0f, 0f);

        contentRoot.anchorMax =
            new Vector2(1f, 1f);

        contentRoot.offsetMin =
            new Vector2(8f, 10f);

        contentRoot.offsetMax =
            new Vector2(-8f, -46f);

        VerticalLayoutGroup layout =
            contentGo.GetComponent<VerticalLayoutGroup>();

        layout.spacing = 4f;
        layout.childAlignment =
            TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private void Rebuild(
        bool force)
    {
        IReadOnlyList<ActionSlot> slots =
            battleManager?.ActionManager?.Slots;

        if (slots == null ||
            contentRoot == null)
        {
            return;
        }

        int signature =
            BuildSignature(
                slots);

        if (!force &&
            signature == lastSignature)
        {
            return;
        }

        lastSignature = signature;

        List<ActionSlot> combat =
            new List<ActionSlot>();

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot == null ||
                slot.Phase !=
                    ActionPhase.COMBAT)
            {
                continue;
            }

            combat.Add(slot);
        }

        combat.Sort(
            sorter.CompareForExecution);

        BuildClashSet(
            slots);

        ClearRows();

        int shown =
            Mathf.Min(
                maximumRows,
                combat.Count);

        for (int index = 0;
             index < shown;
             index++)
        {
            CreateRow(
                combat[index]);
        }

        int hidden =
            combat.Count - shown;

        if (hidden > 0)
        {
            CreateOverflowRow(
                hidden);
        }

        if (headerText != null)
        {
            headerText.text =
                combat.Count > 0
                    ? $"행동 순서  <size=75%>{combat.Count}</size>"
                    : "행동 순서";
        }
    }

    private int BuildSignature(
        IReadOnlyList<ActionSlot> slots)
    {
        int hash = 17;

        hash =
            hash * 31 +
            Screen.width;

        hash =
            hash * 31 +
            Screen.height;

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot == null ||
                slot.Phase !=
                    ActionPhase.COMBAT)
            {
                continue;
            }

            hash =
                hash * 31 +
                slot.ActionId.GetHashCode();

            hash =
                hash * 31 +
                slot.Speed;

            hash =
                hash * 31 +
                slot.ActionIndex;

            hash =
                hash * 31 +
                (slot.Skill?.SkillName
                    ?.GetHashCode() ?? 0);

            hash =
                hash * 31 +
                (slot.TargetSlot?.ActionId
                    .GetHashCode() ?? 0);

            hash =
                hash * 31 +
                (slot.TargetCharacter
                    ?.GetInstanceID() ?? 0);

            hash =
                hash * 31 +
                (slot.TargetPart
                    ?.GetHashCode() ?? 0);
        }

        hash =
            hash * 31 +
            (uiManager?.SelectedOwner
                ?.GetInstanceID() ?? 0);

        hash =
            hash * 31 +
            (uiManager?.SelectedOwnerPart
                ?.GetHashCode() ?? 0);

        hash =
            hash * 31 +
            (uiManager?.SelectedActionIndex ?? 0);

        hash =
            hash * 31 +
            (BattleWorldActionSlotCellUI
                .HoveredTargetCell
                ?.TargetSlot
                ?.ActionId
                .GetHashCode() ?? 0);

        return hash;
    }

    private void BuildClashSet(
        IReadOnlyList<ActionSlot> slots)
    {
        clashSlots.Clear();

        IReadOnlyList<ClashPair> pairs =
            battleManager?.ClashBuilder
                ?.BuildClashPreview(slots);

        if (pairs == null)
            return;

        foreach (ClashPair pair
                 in pairs)
        {
            if (pair == null ||
                !pair.IsClash ||
                pair.First == null ||
                pair.Second == null)
            {
                continue;
            }

            clashSlots.Add(
                pair.First);

            clashSlots.Add(
                pair.Second);
        }
    }

    private void CreateRow(
        ActionSlot slot)
    {
        Character player =
            battleManager?.BattleContext?.Player;

        bool playerSide =
            slot?.Owner == player;

        bool clash =
            slot != null &&
            clashSlots.Contains(slot);

        bool selected =
            slot != null &&
            playerSide &&
            uiManager != null &&
            uiManager.IsWorldPlanningSlotSelected(
                slot.Owner,
                slot.Part,
                slot.ActionIndex);

        bool hoveredTarget =
            slot != null &&
            BattleWorldActionSlotCellUI
                .HoveredTargetCell
                ?.TargetSlot == slot;

        GameObject row =
            new GameObject(
                $"Order_{slot?.ActionId ?? 0}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));

        row.transform.SetParent(
            contentRoot,
            false);

        generatedRows.Add(
            row);

        Image background =
            row.GetComponent<Image>();

        background.color =
            hoveredTarget
                ? hoveredTargetBackground
                : selected
                    ? selectedBackground
                    : new Color(
                        0.04f,
                        0.055f,
                        0.08f,
                        0.76f);

        background.raycastTarget = false;

        LayoutElement element =
            row.GetComponent<LayoutElement>();

        element.preferredHeight = 38f;
        element.minHeight = 34f;

        Image accent =
            CreateImage(
                "Accent",
                row.transform,
                clash
                    ? clashAccent
                    : playerSide
                        ? playerAccent
                        : enemyAccent);

        RectTransform accentRect =
            accent.rectTransform;

        accentRect.anchorMin =
            new Vector2(0f, 0f);

        accentRect.anchorMax =
            new Vector2(0f, 1f);

        accentRect.pivot =
            new Vector2(0f, 0.5f);

        accentRect.anchoredPosition =
            Vector2.zero;

        accentRect.sizeDelta =
            new Vector2(4f, 0f);

        TMP_Text label =
            CreateText(
                "Label",
                row.transform,
                16f,
                TextAlignmentOptions.Left);

        RectTransform labelRect =
            label.rectTransform;

        labelRect.anchorMin =
            Vector2.zero;

        labelRect.anchorMax =
            Vector2.one;

        labelRect.offsetMin =
            new Vector2(10f, 2f);

        labelRect.offsetMax =
            new Vector2(-6f, -2f);

        string side =
            playerSide
                ? "아군"
                : "적";

        string owner =
            GetCharacterName(
                slot?.Owner);

        string part =
            GetPartLabel(
                slot?.Part);

        string skill =
            slot?.Skill?.SkillName ??
            "행동";

        string relation =
            clash
                ? "<color=#FFD12A>합</color>"
                : playerSide
                    ? "<color=#4FA5FF>→</color>"
                    : "<color=#FF5A5A>→</color>";

        label.text =
            $"<b>{slot?.Speed ?? 0,2}</b>  " +
            $"<size=78%>{side} · {owner} · {part}</size>\n" +
            $"<size=78%>{skill}  {relation}</size>";

        if (hoveredTarget)
        {
            label.color =
                new Color(
                    1f,
                    0.92f,
                    0.66f,
                    1f);
        }
    }

    private void CreateOverflowRow(
        int hidden)
    {
        TMP_Text label =
            CreateText(
                "Overflow",
                contentRoot,
                14f,
                TextAlignmentOptions.Center);

        LayoutElement element =
            label.gameObject.AddComponent<LayoutElement>();

        element.preferredHeight = 28f;

        label.text =
            $"+ {hidden}개 행동";
        label.color =
            new Color(
                0.68f,
                0.72f,
                0.80f,
                1f);

        generatedRows.Add(
            label.gameObject);
    }

    private void ClearRows()
    {
        for (int i = generatedRows.Count - 1;
             i >= 0;
             i--)
        {
            GameObject row =
                generatedRows[i];

            if (row == null)
                continue;

            row.SetActive(false);

            if (Application.isPlaying)
                Destroy(row);
            else
                DestroyImmediate(row);
        }

        generatedRows.Clear();
    }

    private static string GetCharacterName(
        Character character)
    {
        string name =
            character?.Data?.CharacterName;

        if (string.IsNullOrWhiteSpace(name))
            name = character?.name;

        if (string.IsNullOrWhiteSpace(name))
            name = "캐릭터";

        name =
            name.Replace(
                "(Clone)",
                string.Empty)
                .Trim();

        if (name.Length > 8)
            name =
                name.Substring(0, 8);

        return name;
    }

    private static string GetPartLabel(
        BodyPart part)
    {
        if (part == null)
            return "행동";

        return part.Type switch
        {
            PartType.HEAD => "머리",
            PartType.LEFT_HAND => "왼팔",
            PartType.RIGHT_HAND => "오른팔",
            PartType.LEGS => "다리",
            _ => "행동"
        };
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Color color)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        go.transform.SetParent(
            parent,
            false);

        Image image =
            go.GetComponent<Image>();

        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        go.transform.SetParent(
            parent,
            false);

        TextMeshProUGUI text =
            go.GetComponent<TextMeshProUGUI>();

        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin =
            Mathf.Max(
                9f,
                size - 5f);

        text.fontSizeMax = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.overflowMode =
            TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(
        RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
