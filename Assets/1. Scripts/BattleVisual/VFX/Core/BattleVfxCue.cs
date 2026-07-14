using System;
using UnityEngine;

public enum BattleVfxCueRepeatMode
{
    OncePerRequest,
    OncePerHitIndex,
    EveryInvocation
}

[Serializable]
public class BattleVfxCue
{
    [Header("Identity")]
    [Tooltip("비워 두면 리스트 인덱스와 VFX 이름으로 자동 키를 생성합니다.")]
    public string CueKey;

    [Header("Timing")]
    public BattleVfxTiming Timing = BattleVfxTiming.OnHitFrame;

    [Header("VFX")]
    public BattleVfxDefinition Vfx;

    [Header("Anchor")]
    public BattleVfxAnchorType AnchorType = BattleVfxAnchorType.TargetBodyPart;

    [Header("Repeat")]
    public BattleVfxCueRepeatMode RepeatMode = BattleVfxCueRepeatMode.OncePerRequest;

    [Header("Hit Filter")]
    public bool UseHitIndexFilter = false;
    [Min(0)] public int HitIndex = 0;

    [Header("Optional Runtime Filter")]
    public CharacterData RequiredAttackerData;
    public string RequiredSkillNameContains;

    [Tooltip("현재 HitFrame에 분배된 표시 피해가 1 이상일 때만 재생합니다. 다중 타격 VFX에는 보통 사용하지 않습니다.")]
    public bool RequirePositiveDamage = false;

    [Tooltip("행동 전체의 실제 해결 피해가 1 이상일 때만 재생합니다. 다중 타격에서 각 HitFrame 피해가 0이어도 실제 공격이 적중했다면 VFX를 허용합니다.")]
    public bool RequirePositiveResolvedDamage = false;

    [Header("Delay")]
    [Min(0f)] public float Delay = 0f;

    public bool Matches(BattleVfxContext context)
    {
        if (context == null)
            return false;

        if (UseHitIndexFilter && HitIndex != context.HitIndex)
            return false;

        if (RequirePositiveDamage && context.Damage <= 0)
            return false;

        if (RequirePositiveResolvedDamage &&
            ResolveTotalDamage(context) <= 0)
        {
            return false;
        }

        if (RequiredAttackerData != null &&
            context.Attacker?.Data != RequiredAttackerData)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(RequiredSkillNameContains))
        {
            string skillName = context.SourceAction?.Skill?.SkillName;

            if (string.IsNullOrEmpty(skillName) ||
                !skillName.Contains(RequiredSkillNameContains))
            {
                return false;
            }
        }

        return true;
    }

    public string BuildRuntimeKey(
        int cueIndex,
        BattleVfxContext context)
    {
        string baseKey = !string.IsNullOrEmpty(CueKey)
            ? CueKey
            : $"{Vfx?.name ?? "NULL"}:{cueIndex}:{Timing}";

        return RepeatMode switch
        {
            BattleVfxCueRepeatMode.OncePerHitIndex =>
                $"{baseKey}:HIT:{context?.HitIndex ?? -1}",

            BattleVfxCueRepeatMode.EveryInvocation =>
                string.Empty,

            _ => baseKey
        };
    }

    private static int ResolveTotalDamage(
        BattleVfxContext context)
    {
        if (context == null)
            return 0;

        if (context.DamageContext != null)
            return Mathf.Max(0, context.DamageContext.GetDisplayDamage());

        if (context.DamageResult != null)
            return Mathf.Max(0, context.DamageResult.GetDisplayDamage());

        return Mathf.Max(0, context.Damage);
    }
}
