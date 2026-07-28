using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SkillCameraDefinition",
    menuName = "Battle/Visual/Skill Camera Definition")]
public class SkillCameraDefinition : ScriptableObject
{
    [Header("Legacy Shot Migration Source")]
    [Tooltip(
        "Timeline 마이그레이션이 기존 Shot을 Camera Clip으로 옮길 때만 사용합니다. " +
        "런타임과 신규 Authoring에서는 사용하지 않습니다.")]
    [HideInInspector]
    public List<SkillCameraShot> Shots = new();

    [Header("Impact Zoom Pulses")]
    [Tooltip(
        "현재 Shot 위에 겹쳐 재생되는 빠른 FOV 줌인/줌아웃입니다. " +
        "위에서부터 검사하며 조건을 처음 만족한 Pulse 하나를 재생합니다.")]
    public List<SkillCameraImpactPulse> ImpactPulses = new();

    [Header("Legacy Return Migration Source")]
    [HideInInspector]
    public bool ReturnToOverviewAfterAction = true;

    [HideInInspector]
    public bool OverrideReturnBrainBlend = true;

    [HideInInspector]
    public CinemachineBlendDefinition.Styles ReturnBlendStyle =
        CinemachineBlendDefinition.Styles.EaseOut;

    [HideInInspector, Min(0f)]
    public float ReturnBlendTime = 0.25f;

    public SkillCameraImpactPulse FindImpactPulse(
        SkillCameraImpactTiming timing,
        int hitIndex,
        int exchangeIndex,
        int damage,
        bool isCritical,
        bool brokePart,
        bool wasKilled,
        bool isClash,
        bool isOneSided)
    {
        if (ImpactPulses == null)
            return null;

        foreach (SkillCameraImpactPulse pulse in ImpactPulses)
        {
            if (pulse == null)
                continue;

            if (pulse.Matches(
                    timing,
                    hitIndex,
                    exchangeIndex,
                    damage,
                    isCritical,
                    brokePart,
                    wasKilled,
                    isClash,
                    isOneSided))
            {
                return pulse;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        Shots ??=
            new List<SkillCameraShot>();

        ImpactPulses ??=
            new List<SkillCameraImpactPulse>();

        ReturnBlendTime =
            Mathf.Max(
                0f,
                ReturnBlendTime);

        foreach (SkillCameraImpactPulse pulse in ImpactPulses)
            pulse?.Sanitize();
    }
}
