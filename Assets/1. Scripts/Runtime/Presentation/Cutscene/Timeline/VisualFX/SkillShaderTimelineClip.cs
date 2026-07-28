using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public sealed class SkillShaderTimelineClip :
    PlayableAsset,
    ITimelineClipAsset
{
    [Header("Effect")]
    public SkillShaderEffectDefinition Definition;

    [Header("Target")]
    public SkillShaderTargetCharacter TargetCharacter =
        SkillShaderTargetCharacter.Attacker;
    public SkillShaderRendererBinding RendererBinding =
        SkillShaderRendererBinding.AnchorChildren;
    public string AnchorKey = "WeaponMain";
    public string RendererName;
    [Tooltip("-1이면 선택된 모든 Material Slot에 적용합니다.")]
    public int MaterialSlot = -1;
    public bool IncludeInactive = true;

    [Header("Timing")]
    [Min(0.01f)] public float PlaybackSpeed = 1f;
    public AnimationCurve StrengthCurve =
        new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 1f),
            new Keyframe(0.85f, 1f),
            new Keyframe(1f, 0f));


    public ClipCaps clipCaps =>
        ClipCaps.Blending |
        ClipCaps.ClipIn |
        ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ScriptPlayable<SkillShaderTimelineBehaviour> playable =
            ScriptPlayable<SkillShaderTimelineBehaviour>.Create(graph);
        playable.GetBehaviour().Asset = this;
        return playable;
    }
}

public sealed class SkillShaderTimelineBehaviour :
    PlayableBehaviour
{
    public SkillShaderTimelineClip Asset;
}

public readonly struct SkillShaderTimelineContribution
{
    public SkillShaderTimelineContribution(
        SkillShaderTimelineClip clip,
        float normalizedTime,
        float timelineWeight)
    {
        Clip = clip;
        NormalizedTime = normalizedTime;
        TimelineWeight = timelineWeight;
    }

    public SkillShaderTimelineClip Clip { get; }
    public float NormalizedTime { get; }
    public float TimelineWeight { get; }
}
