using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public sealed class SkillVfxTimelineClip :
    PlayableAsset,
    ITimelineClipAsset
{
    [Header("VFX Asset")]
    public BattleVfxDefinition Definition;

    [Header("Binding")]
    public SkillVisualFxBinding Binding =
        SkillVisualFxBinding.CombatFrame;
    public string AnchorKey = "Center";
    public SkillTimelineVfxFollowMode FollowMode =
        SkillTimelineVfxFollowMode.SpawnWorldFixed;

    [Header("Transform At Clip Start")]
    public Vector3 StartPosition;
    public Vector3 StartEuler;
    public Vector3 StartScale = Vector3.one;

    [Header("Transform Motion")]
    public bool AnimateTransform;
    public Vector3 EndPosition;
    public Vector3 EndEuler;
    public Vector3 EndScale = Vector3.one;
    public AnimationCurve PositionCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve RotationCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve ScaleCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Playback")]
    [Min(0.01f)] public float PlaybackSpeed = 1f;
    public SkillTimelineVfxPlaybackMode PlaybackMode =
        SkillTimelineVfxPlaybackMode.ClipControlled;
    public bool UseClipDurationAsLifetime = true;
    [Min(0f)] public float LifetimeOverride;
    public bool ReleaseAtClipEnd = true;

    [Header("Optional Battle Filter")]
    public bool UseHitIndexFilter;
    [Min(0)] public int HitIndex;
    public bool RequirePositiveDamage;
    public bool RequirePositiveResolvedDamage;
    public bool RequireCritical;
    public bool RequireKill;
    public bool RequirePartBreak;
    public CharacterData RequiredAttackerData;
    public string RequiredSkillNameContains;

    [Header("Preview")]
    public bool PreviewInEditMode = true;

    public ClipCaps clipCaps =>
        ClipCaps.Blending |
        ClipCaps.ClipIn |
        ClipCaps.SpeedMultiplier;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ScriptPlayable<SkillVfxTimelineBehaviour> playable =
            ScriptPlayable<SkillVfxTimelineBehaviour>.Create(graph);
        playable.GetBehaviour().Asset = this;
        return playable;
    }
}

public sealed class SkillVfxTimelineBehaviour :
    PlayableBehaviour
{
    public SkillVfxTimelineClip Asset;
}
