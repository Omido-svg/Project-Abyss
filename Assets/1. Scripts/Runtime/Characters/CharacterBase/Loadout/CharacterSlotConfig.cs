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

    [Header("Optional shared body-part speed override")]
    [Tooltip(
        "이 슬롯 하나만의 속도가 아니라, 같은 BodyPart에 연결된 모든 행동 슬롯이 " +
        "공유하는 턴 속도 범위를 Override합니다.")]
    public bool OverrideSpeedRange;

    [Min(0)]
    public int MinSpeed = 3;

    [Min(0)]
    public int MaxSpeed = 8;

    [Header("Fixed / fallback skill (optional)")]
    [Tooltip("설정하면 이 슬롯은 해당 SkillDefinition을 우선 사용합니다.")]
    public SkillDefinition FixedSkill;

    [Tooltip("FixedSkill의 에너지가 부족할 때 AI가 사용할 동일 구성의 fallback. Stage 1 Boss B처럼 Duel 플래그만 해제하는 용도입니다.")]
    public SkillDefinition InsufficientEnergyFallbackSkill;

    [Header("AI targeting")]
    public AITargetingPolicy TargetingPolicy = AITargetingPolicy.Default;

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