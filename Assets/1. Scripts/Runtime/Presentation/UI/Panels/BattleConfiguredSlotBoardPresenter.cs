using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Read-only projection and input routing for configured action slots.
/// UI View receives the resulting display text and interactability flags.
/// </summary>
public sealed class BattleConfiguredSlotBoardPresenter
{
    public BattleConfiguredSlotBoardPresentation Project(BattleManager manager)
    {
        Character player = manager?.BattleContext?.Player;
        IReadOnlyList<CharacterSlotConfig> configs =
            player?.CombatRulesRuntime?.GetActiveSlotConfigs();
        var items = new List<BattleConfiguredSlotItem>();

        if (player != null && configs != null)
        {
            foreach (CharacterSlotConfig config in configs)
            {
                if (config == null || !config.Enabled) continue;

                BodyPart part = player.CombatRulesRuntime.GetLinkedPart(config);
                ActionSlot planned = manager.ActionManager?.FindSlot(player, config.SlotId);
                bool disabled = config.HasLinkedPart && (part == null || part.IsBroken);
                items.Add(new BattleConfiguredSlotItem(
                    config.SlotId ?? string.Empty,
                    BuildLabel(config, part, planned, disabled),
                    !disabled,
                    player,
                    config));
            }
        }

        // Use displayed values, not only ActionId/TargetPart.Type, to detect
        // same-part retargets and other relevant UI changes.
        StringBuilder signature = new();
        signature.Append(player != null ? player.GetInstanceID() : 0);
        signature.Append('|').Append(items.Count);
        foreach (BattleConfiguredSlotItem item in items)
        {
            signature.Append('|').Append(item.SlotId)
                .Append(':').Append(item.CanSelect)
                .Append(':').Append(RuntimeHelpers.GetHashCode(item.Config))
                .Append(':').Append(item.Label.Length)
                .Append(':').Append(item.Label);
        }
        return new BattleConfiguredSlotBoardPresentation(signature.ToString(), items);
    }

    public void TryOpenSlot(BattleUIManager uiManager, BattleConfiguredSlotItem item)
    {
        Character owner = item.Owner;
        CharacterSlotConfig config = item.Config;
        if (owner == null || config == null || uiManager == null) return;

        BodyPart part = owner.CombatRulesRuntime?.GetLinkedPart(config);
        int index = owner.CombatRulesRuntime?.GetActionIndex(config) ?? -1;
        if (part == null)
        {
            Debug.LogWarning(
                $"[BattleConfiguredSlotBoardUI] {config.DisplayName}은 독립 슬롯입니다. " +
                "현재 캐릭터 입력기는 부위 연결 슬롯만 직접 편집할 수 있습니다.");
            return;
        }
        if (index >= 0) uiManager.OpenOwnerActionSlot(owner, part, index);
    }

    private static string BuildLabel(CharacterSlotConfig config, BodyPart linkedPart,
        ActionSlot planned, bool disabled)
    {
        string partText = config.HasLinkedPart
            ? linkedPart?.Type.ToString() ?? config.LinkedPartType.ToString()
            : "독립";
        string speedText = config.OverrideSpeedRange
            ? $"{config.MinSpeed}~{config.MaxSpeed}"
            : planned != null ? planned.Speed.ToString() : "기본";
        string skillText = planned?.Skill?.SkillName ?? "행동 미선택";
        string targetText = planned?.TargetCharacter == null ? "-"
            : planned.TargetPart == null
                ? planned.TargetCharacter.Data?.CharacterName ?? planned.TargetCharacter.name
                : $"{planned.TargetCharacter.Data?.CharacterName ?? planned.TargetCharacter.name} " +
                  $"{BattleBodyPartUiText.GetDisplayName(planned.TargetPart)}";
        string costText = planned?.Skill == null ? "-" : planned.Skill.EnergyCost.ToString();
        string state = disabled ? "<color=#FCA5A5>행동 불능</color>"
            : planned == null ? "<color=#C9D2E3>비어 있음</color>"
            : "<color=#A7F3D0>계획 완료</color>";
        return $"<b>{config.DisplayName}</b>  " +
               $"<color=#C9D2E3>{config.SlotId}</color>\n" +
               $"{state} · 연결 {partText} · SPD {speedText}\n" +
               $"{skillText}  →  {targetText}  · 빛 {costText}";
    }
}

/// <summary>One configured slot and its current display state.</summary>
public sealed class BattleConfiguredSlotItem
{
    public string SlotId { get; }
    public string Label { get; }
    public bool CanSelect { get; }
    internal Character Owner { get; }
    internal CharacterSlotConfig Config { get; }

    public BattleConfiguredSlotItem(string slotId, string label, bool canSelect,
        Character owner, CharacterSlotConfig config)
    {
        SlotId = slotId;
        Label = label;
        CanSelect = canSelect;
        Owner = owner;
        Config = config;
    }
}

public sealed class BattleConfiguredSlotBoardPresentation
{
    public string Signature { get; }
    public IReadOnlyList<BattleConfiguredSlotItem> Items { get; }

    public BattleConfiguredSlotBoardPresentation(string signature,
        IReadOnlyList<BattleConfiguredSlotItem> items)
    {
        Signature = signature;
        Items = items;
    }
}
