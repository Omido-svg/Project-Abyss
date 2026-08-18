using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 머리 위에 행동 슬롯을 표시한다.
/// 플레이어 슬롯 클릭 -> 해당 부위/인덱스의 스킬 패널,
/// 적 슬롯 클릭/드롭 -> 정확한 TargetSlot에 합 계약을 고정한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleWorldActionSlotStripUI : MonoBehaviour
{
    private Character character;
    private BattleUIManager uiManager;
    private BattleManager battleManager;
    private RectTransform root;
    private int lastSignature = int.MinValue;

    public void Configure(Character target)
    {
        character = target;
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
        Rebuild(force: false);
    }

    private void ResolveReferences()
    {
        uiManager ??= FindFirstObjectByType<BattleUIManager>(FindObjectsInactive.Include);
        battleManager ??= FindFirstObjectByType<BattleManager>();
    }

    private void EnsureView()
    {
        if (root != null)
            return;

        GameObject go = new GameObject(
            "ActionSlotStrip",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup));
        go.transform.SetParent(transform, false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 12f);
        root.sizeDelta = new Vector2(500f, 84f);

        HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void Rebuild(bool force)
    {
        if (character == null || uiManager == null || battleManager?.ActionManager == null || root == null)
            return;

        bool player = uiManager.IsPlayer(character);
        int signature = player ? BuildPlayerSignature() : BuildEnemySignature();
        if (!force && signature == lastSignature)
            return;

        lastSignature = signature;
        ClearChildren();

        if (player)
            BuildPlayerCells();
        else
            BuildEnemyCells();
    }

    private int BuildPlayerSignature()
    {
        int hash = 17;
        if (character.BodyParts == null)
            return hash;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null)
                continue;

            hash = hash * 31 + (int)part.Type;
            int count = character.GetMaxActionSlotsForPart(part);
            hash = hash * 31 + count;
            hash = hash * 31 + (part.IsBroken ? 1 : 0);

            for (int i = 0; i < count; i++)
            {
                ActionSlot planned =
                    ResolvePlayerDisplaySlot(
                        part,
                        i);

                hash = hash * 31 + (planned?.ActionId.GetHashCode() ?? 0);
                hash = hash * 31 + (planned?.Speed ?? 0);
                hash = hash * 31 + (planned?.Skill?.SkillName?.GetHashCode() ?? 0);
                hash = hash * 31 + (planned?.TargetSlot?.ActionId.GetHashCode() ?? 0);
            }
        }

        return hash;
    }

    private int BuildEnemySignature()
    {
        int hash = 17;
        IReadOnlyList<ActionSlot> slots = battleManager.ActionManager.Slots;
        if (slots == null)
            return hash;

        foreach (ActionSlot slot in slots)
        {
            if (slot?.Owner != character)
                continue;

            hash = hash * 31 + slot.ActionId.GetHashCode();
            hash = hash * 31 + slot.Speed;
            hash = hash * 31 + (slot.Skill?.SkillName?.GetHashCode() ?? 0);
        }

        return hash;
    }

    private void BuildPlayerCells()
    {
        if (character.BodyParts == null)
            return;

        foreach (BodyPart part in character.BodyParts)
        {
            if (part == null || part.IsBroken || !part.IsUsable)
                continue;

            int count = character.GetMaxActionSlotsForPart(part);
            for (int i = 0; i < count; i++)
            {
                ActionSlot planned =
                    ResolvePlayerDisplaySlot(
                        part,
                        i);

                CreateCell(null, part, i, planned);
            }
        }
    }

    private ActionSlot ResolvePlayerDisplaySlot(
        BodyPart part,
        int actionIndex)
    {
        return battleManager?.ActionManager?.FindSlot(
            character,
            part,
            actionIndex);
    }

    private void BuildEnemyCells()
    {
        List<ActionSlot> slots = new();
        IReadOnlyList<ActionSlot> all = battleManager.ActionManager.Slots;
        if (all == null)
            return;

        foreach (ActionSlot slot in all)
        {
            if (slot?.Owner == character && slot.Phase == ActionPhase.COMBAT)
                slots.Add(slot);
        }

        slots.Sort((a, b) =>
        {
            int speed = b.Speed.CompareTo(a.Speed);
            return speed != 0 ? speed : a.ActionIndex.CompareTo(b.ActionIndex);
        });

        foreach (ActionSlot slot in slots)
            CreateCell(slot, slot.Part, slot.ActionIndex, null);
    }

    private void CreateCell(ActionSlot targetSlot, BodyPart part, int index, ActionSlot plannedSlot)
    {
        GameObject cell = new GameObject(
            targetSlot == null ? $"Plan_{part?.Type}_{index}" : $"Target_{targetSlot.ActionId}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline),
            typeof(CanvasGroup),
            typeof(BattleWorldActionSlotCellUI));
        cell.transform.SetParent(root, false);

        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(74f, 78f);

        Image image = cell.GetComponent<Image>();
        image.color = targetSlot == null
            ? new Color(0.12f, 0.16f, 0.24f, 0.94f)
            : new Color(0.36f, 0.10f, 0.10f, 0.95f);

        Outline outline = cell.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.48f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = false;

        TMP_Text speedText = CreateSpeedText(cell.transform);
        TMP_Text skillText = CreateSkillText(cell.transform);

        BattleWorldActionSlotCellUI logic = cell.GetComponent<BattleWorldActionSlotCellUI>();
        if (targetSlot == null)
        {
            logic.ConfigurePlanning(
                uiManager,
                character,
                part,
                index,
                plannedSlot,
                speedText,
                skillText,
                image,
                outline);
        }
        else
        {
            logic.ConfigureTarget(
                uiManager,
                targetSlot,
                speedText,
                skillText,
                image,
                outline);
        }
    }

    private static TMP_Text CreateSpeedText(Transform parent)
    {
        GameObject go = new GameObject(
            "Speed",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.69f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(3f, 0f);
        rect.offsetMax = new Vector2(-3f, -2f);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 24f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 17f;
        text.fontSizeMax = 24f;
        text.color = new Color(1f, 0.86f, 0.16f, 1f);
        text.outlineColor = Color.black;
        text.outlineWidth = 0.22f;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static TMP_Text CreateSkillText(Transform parent)
    {
        GameObject go = new GameObject(
            "SkillName",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0.70f);
        rect.offsetMin = new Vector2(5f, 3f);
        rect.offsetMax = new Vector2(-5f, -1f);

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Top;
        text.fontSize = 17f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 17f;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void ClearChildren()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }
}