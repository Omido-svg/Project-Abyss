using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CharacterSlotConfig
{
    public string SlotId = "SLOT_01";
    public string DisplayName = "행동 슬롯 1";
    public bool Enabled = true;

    [Header("Optional body-part link")]
    public bool HasLinkedPart = true;
    public PartType LinkedPartType = PartType.HEAD;

    [Header("Optional speed override")]
    public bool OverrideSpeedRange;
    public int MinSpeed = 3;
    public int MaxSpeed = 8;

    [Header("Allowed skill categories")]
    public List<ActionType> AllowedActionTypes = new()
    {
        ActionType.NormalAttack,
        ActionType.Duel,
        ActionType.Preparation,
        ActionType.Prestige
    };

    public bool Allows(ActionType actionType) =>
        Enabled && AllowedActionTypes != null && AllowedActionTypes.Contains(actionType);

    public void Sanitize(int index)
    {
        if (string.IsNullOrWhiteSpace(SlotId))
            SlotId = $"SLOT_{index + 1:00}";
        if (string.IsNullOrWhiteSpace(DisplayName))
            DisplayName = $"행동 슬롯 {index + 1}";
        if (MaxSpeed < MinSpeed)
            MaxSpeed = MinSpeed;
        AllowedActionTypes ??= new List<ActionType>();
    }
}
