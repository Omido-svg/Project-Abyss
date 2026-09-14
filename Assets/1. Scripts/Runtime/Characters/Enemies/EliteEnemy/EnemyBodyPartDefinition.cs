using System;
using UnityEngine;

[Serializable]
public sealed class EnemyBodyPartDefinition
{
    public string PartId = "part";
    public string DisplayName = "부위";
    public PartType LegacyType = PartType.CUSTOM;
    [Min(1f)] public float MaxPartHP = 50f;
    public BodyPartSlotRole SlotRole = BodyPartSlotRole.Attack;

    [Header("Weakened debuff — data defined")]
    [Min(0)] public int RollCountPenalty;
    [Min(0)] public int SpeedMaxPenalty;
    public bool NormalAttackOnly;

    public BodyPart CreateRuntimePart(int index)
    {
        string id = string.IsNullOrWhiteSpace(PartId) ? $"enemy_part_{index + 1}" : PartId.Trim();
        return new BodyPart(
            id,
            DisplayName,
            LegacyType,
            MaxPartHP,
            SlotRole,
            true,
            RollCountPenalty,
            SpeedMaxPenalty,
            NormalAttackOnly);
    }
}
