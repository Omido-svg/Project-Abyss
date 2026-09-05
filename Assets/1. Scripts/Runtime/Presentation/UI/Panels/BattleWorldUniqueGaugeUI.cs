using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 체력바 바로 위에 전용 게이지와 공용 무력화 게이지를 표시한다.
/// 기존 캐릭터별 전용 패널을 대체하는 월드 HUD 계층이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleWorldUniqueGaugeUI : MonoBehaviour
{
    private Character character;
    private RectTransform root;
    private GaugeRow uniqueRow;
    private GaugeRow staggerRow;
    private ICharacterUniqueGaugeProvider uniqueProvider;
    private ICharacterUniqueGaugeProvider staggerProvider;
    private int cachedMechanicCount = -1;

    private sealed class GaugeRow
    {
        public GameObject Root;
        public TMP_Text Label;
        public TMP_Text Value;
        public Image Fill;
        public RectTransform FillRect;
        public int LastStateVersion = int.MinValue;
        public bool LastActive;
    }

    public void Configure(Character target)
    {
        character = target;
        cachedMechanicCount = -1;
        EnsureView();
        Refresh();
    }

    private void Awake()
    {
        EnsureView();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void EnsureView()
    {
        if (root != null)
            return;

        GameObject rootGo = new GameObject("CharacterGaugeStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
        rootGo.transform.SetParent(transform, false);
        root = rootGo.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 94f);
        root.sizeDelta = new Vector2(430f, 62f);

        VerticalLayoutGroup layout = rootGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        uniqueRow = CreateRow("UniqueGauge", 26f);
        staggerRow = CreateRow("StaggerGauge", 22f);
    }

    private GaugeRow CreateRow(string name, float height)
    {
        GameObject rowGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowGo.transform.SetParent(root, false);
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(430f, height);
        LayoutElement element = rowGo.GetComponent<LayoutElement>();
        element.preferredHeight = height;

        Image background = rowGo.GetComponent<Image>();
        background.color = new Color(0.035f, 0.04f, 0.055f, 0.92f);

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(rowGo.transform, false);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        Image fill = fillGo.GetComponent<Image>();
        fill.color = new Color(0.82f, 0.50f, 0.10f, 0.88f);
        fill.raycastTarget = false;

        TMP_Text label = CreateText(rowGo.transform, "Label", TextAlignmentOptions.Left);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 0f);
        labelRect.offsetMax = new Vector2(-120f, 0f);

        TMP_Text value = CreateText(rowGo.transform, "Value", TextAlignmentOptions.Right);
        RectTransform valueRect = value.rectTransform;
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(120f, 0f);
        valueRect.offsetMax = new Vector2(-8f, 0f);

        return new GaugeRow
        {
            Root = rowGo,
            Label = label,
            Value = value,
            Fill = fill,
            FillRect = fillRect
        };
    }

    private static TMP_Text CreateText(Transform parent, string name, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.alignment = alignment;
        text.fontSize = 15f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void Refresh()
    {
        if (character == null || uniqueRow == null || staggerRow == null)
            return;

        ResolveProvidersIfNeeded();
        ApplyIfChanged(uniqueRow, uniqueProvider, new Color(0.80f, 0.42f, 0.08f, 0.9f));
        ApplyIfChanged(staggerRow, staggerProvider, new Color(0.28f, 0.65f, 0.86f, 0.9f));
    }

    private void ResolveProvidersIfNeeded()
    {
        IReadOnlyList<CombatMechanic> mechanics = character.Mechanics;
        int mechanicCount = mechanics?.Count ?? 0;
        if (mechanicCount == cachedMechanicCount)
            return;

        cachedMechanicCount = mechanicCount;
        uniqueProvider = null;
        staggerProvider = null;

        for (int i = 0; i < mechanicCount; i++)
        {
            CombatMechanic mechanic = mechanics[i];
            if (mechanic is not ICharacterUniqueGaugeProvider provider)
                continue;

            if (mechanic is StaggerGaugeMechanic)
                staggerProvider = provider;
            else if (uniqueProvider == null)
                uniqueProvider = provider;
        }

        // Provider identity changed: force one full visual refresh.
        uniqueRow.LastStateVersion = int.MinValue;
        staggerRow.LastStateVersion = int.MinValue;
    }

    private static void ApplyIfChanged(GaugeRow row, ICharacterUniqueGaugeProvider provider, Color color)
    {
        if (row?.Root == null)
            return;

        bool active = provider != null;
        if (row.LastActive != active)
        {
            row.Root.SetActive(active);
            row.LastActive = active;
            row.LastStateVersion = int.MinValue;
        }

        if (!active)
            return;

        int version = provider.GaugeStateVersion;
        if (row.LastStateVersion == version)
            return;

        row.LastStateVersion = version;
        row.Label.text = provider.GaugeLabel ?? string.Empty;
        row.Value.text = provider.GaugeValueText ?? string.Empty;
        row.Fill.color = color;

        float ratio = Mathf.Clamp01(provider.GaugeNormalized);
        Vector2 max = row.FillRect.anchorMax;
        max.x = ratio;
        row.FillRect.anchorMax = max;
        row.FillRect.offsetMin = new Vector2(2f, 2f);
        row.FillRect.offsetMax = new Vector2(-2f, -2f);
    }
}