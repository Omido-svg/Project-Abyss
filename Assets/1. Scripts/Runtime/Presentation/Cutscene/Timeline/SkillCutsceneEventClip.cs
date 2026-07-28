using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum SkillCutsceneEventType
{
    Hit = 0,
    Vfx = 1,
    CameraShake = 2,
    TargetHitReaction = 3,
    SetTimeScale = 4,
    RestoreTimeScale = 5,
    ReturnOverview = 6,
    Custom = 7
}

[Serializable]
public sealed class SkillCutsceneEventClip :
    PlayableAsset,
    ITimelineClipAsset
{
    [Header("Event")]
    public SkillCutsceneEventType EventType =
        SkillCutsceneEventType.Hit;

    [Tooltip(
        "-1이면 실행된 Hit Event 순서대로 자동 번호를 사용합니다.")]
    public int HitIndex =
        -1;

    [Header("VFX")]
    public BattleVfxTiming VfxTiming =
        BattleVfxTiming.OnHitFrame;

    [Header("Time Scale")]
    [Min(0.01f)]
    public float TimeScale =
        1f;

    [Header("Custom")]
    public string CustomEventKey;

    public ClipCaps clipCaps =>
        ClipCaps.ClipIn;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ScriptPlayable<
            SkillCutsceneEventBehaviour>
            playable =
                ScriptPlayable<
                    SkillCutsceneEventBehaviour>
                    .Create(graph);

        playable.GetBehaviour().Asset =
            this;

        return playable;
    }
}

