using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 혼합전투에서도 적 행동의 소유 적/행동 부위/속도/스킬/대상 부위를
/// 림버스식 가로 슬롯으로 읽을 수 있게 만드는 런타임 의도 리본이다.
/// 기존 전투 UI 에셋을 파괴하지 않고 Canvas 아래에 자체 생성된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyIntentRibbonUI : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.15f;

    private RectTransform content;
    private readonly List<GameObject> cells = new();
    private float nextRefresh;
    private string signature;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<EnemyIntentRibbonUI>(FindObjectsInactive.Include) != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
            return;

        GameObject root = new GameObject(
            "EnemyIntentRibbon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(EnemyIntentRibbonUI));
        root.transform.SetParent(canvas.transform, false);
    }

    private void Awake()
    {
        battleManager = FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
        BuildView();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    private void BuildView()
    {
        RectTransform root = transform as RectTransform;
        root.anchorMin = new Vector2(1f, 1f);
        root.anchorMax = new Vector2(1f, 1f);
        root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-24f, -22f);
        root.sizeDelta = new Vector2(760f, 116f);

        Image bg = GetComponent<Image>();
        bg.color = new Color(0.025f, 0.035f, 0.055f, 0.88f);
        bg.raycastTarget = true;

        GameObject titleGo = CreateText("Title", root, 18f);
        RectTransform title = titleGo.transform as RectTransform;
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(0f, 1f);
        title.pivot = new Vector2(0f, 1f);
        title.anchoredPosition = new Vector2(12f, -6f);
        title.sizeDelta = new Vector2(170f, 24f);
        titleGo.GetComponent<TMP_Text>().text = "적 행동 · 부위 슬롯";

        GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        content = contentGo.GetComponent<RectTransform>();
        content.SetParent(root, false);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 1f);
        content.offsetMin = new Vector2(10f, 8f);
        content.offsetMax = new Vector2(-10f, -32f);

        HorizontalLayoutGroup layout = contentGo.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
    }

    private void Refresh()
    {
        ActionManager manager = battleManager?.ActionManager;
        BattleContext context = battleManager?.BattleContext;
        if (manager == null || context?.Enemies == null)
            return;

        List<ActionSlot> slots = new();
        foreach (ActionSlot slot in manager.Slots)
        {
            if (slot?.Owner != null && IsEnemy(context, slot.Owner))
                slots.Add(slot);
        }

        slots.Sort((a, b) => b.Speed.CompareTo(a.Speed));
        string next = BuildSignature(slots);
        if (next == signature)
            return;
        signature = next;

        foreach (GameObject cell in cells)
            if (cell != null) Destroy(cell);
        cells.Clear();

        foreach (ActionSlot slot in slots)
            cells.Add(CreateCell(slot));

        gameObject.SetActive(slots.Count > 0);
    }

    private static bool IsEnemy(BattleContext context, Character character)
    {
        if (context?.Enemies == null || character == null)
            return false;

        foreach (Character enemy in context.Enemies)
        {
            if (enemy == character)
                return true;
        }

        return false;
    }

    private GameObject CreateCell(ActionSlot slot)
    {
        GameObject cell = new GameObject(
            $"Intent_{slot.Owner.name}_{slot.ActionIndex}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        cell.transform.SetParent(content, false);
        cell.GetComponent<RectTransform>().sizeDelta = new Vector2(132f, 74f);
        LayoutElement element = cell.GetComponent<LayoutElement>();
        element.preferredWidth = 132f;
        element.minWidth = 116f;

        Image image = cell.GetComponent<Image>();
        image.color = ResolveEnemyColor(slot.Owner);
        image.raycastTarget = false;

        TMP_Text text = CreateText("Label", cell.transform as RectTransform, 14f).GetComponent<TMP_Text>();
        RectTransform rect = text.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 3f);
        rect.offsetMax = new Vector2(-5f, -3f);
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.text = BuildLabel(slot);
        return cell;
    }

    private static string BuildLabel(ActionSlot slot)
    {
        string owner = slot.Owner?.Data?.CharacterName ?? slot.Owner?.name ?? "적";
        if (owner.Length > 7) owner = owner.Substring(0, 7);
        string sourcePart = slot.Part == null ? "BODY" : slot.Part.Type.ToString();
        string targetPart = slot.TargetPart == null ? "BODY" : slot.TargetPart.Type.ToString();
        string skill = slot.Skill?.SkillName ?? "행동 없음";
        return $"<b>{owner}</b>  <size=18><b>{slot.Speed}</b></size>\n" +
               $"<color=#FFD36A>{sourcePart}</color> · {skill}\n" +
               $"→ <color=#9ED7FF>{targetPart}</color>";
    }

    private static Color ResolveEnemyColor(Character enemy)
    {
        if (enemy?.Data?.CombatantTier == CombatantTier.Boss)
            return new Color(0.30f, 0.055f, 0.055f, 0.96f);
        if (enemy?.Data?.CombatantTier == CombatantTier.EliteEnemy)
            return new Color(0.24f, 0.10f, 0.07f, 0.96f);
        return new Color(0.09f, 0.13f, 0.20f, 0.96f);
    }

    private static string BuildSignature(List<ActionSlot> slots)
    {
        System.Text.StringBuilder b = new();
        foreach (ActionSlot slot in slots)
            b.Append(slot.ActionId).Append(':').Append(slot.Speed).Append(':')
             .Append(slot.Skill?.SkillName).Append(':').Append(slot.Part?.Type).Append('|');
        return b.ToString();
    }

    private static GameObject CreateText(string name, RectTransform parent, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return go;
    }
}
