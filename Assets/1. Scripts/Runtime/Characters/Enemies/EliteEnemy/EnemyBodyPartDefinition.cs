using System;
using System.Collections.Generic;
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

    [Header("Broken debuff — data defined")]
    [Tooltip("이 부위가 파괴된 동안 소유자의 모든 행동 굴림 수를 감소시킵니다. 최소 1굴림은 보장됩니다.")]
    [Min(0)] public int BrokenRollCountPenalty;

    [Tooltip("이 부위가 파괴된 동안 소유자의 속도 굴림 최댓값을 감소시킵니다.")]
    [Min(0)] public int BrokenSpeedMaxPenalty;

    [Tooltip("이 부위가 파괴될 때 최대 빛을 감소시킵니다. 부위가 복구되면 원복됩니다.")]
    [Min(0)] public int BrokenEnergyMaxPenalty;

    [Tooltip("이 부위가 파괴된 동안 평타 외 스킬 사용을 금지합니다.")]
    public bool BrokenNormalAttackOnly;

    [Tooltip("이 부위가 파괴된 동안 사용할 수 없는 스킬 ID. 비워 두면 추가 제한이 없습니다.")]
    public List<string> BrokenForbiddenSkillIds = new();

    [Tooltip("이 부위가 파괴된 동안 적 자세 로테이션에서 건너뛸 자세입니다.")]
    public EnemyPostureMask BrokenForbiddenPostures = EnemyPostureMask.None;

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
            NormalAttackOnly,
            BrokenRollCountPenalty,
            BrokenSpeedMaxPenalty,
            BrokenEnergyMaxPenalty,
            BrokenNormalAttackOnly,
            BrokenForbiddenSkillIds);
    }
}
