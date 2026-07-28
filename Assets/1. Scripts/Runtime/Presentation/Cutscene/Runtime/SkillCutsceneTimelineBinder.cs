using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline Track을 런타임에 생성된 Attacker/Target/CameraRig에 연결한다.
///
/// Track 이름 계약:
/// [Abyss] Attacker Animation
/// [Abyss] Target Animation
/// [AbyssRig] CM_Overview
/// [AbyssRig] CM_Follow
/// [Abyss FX] VFX
/// [Abyss FX] Shader
/// </summary>
public static class SkillCutsceneTimelineBinder
{
    public const string AttackerAnimationTrackName =
        "[Abyss] Attacker Animation";

    public const string TargetAnimationTrackName =
        "[Abyss] Target Animation";

    public const string RigAnimationPrefix =
        "[AbyssRig] ";

    public static void Bind(
        PlayableDirector director,
        TimelineAsset timeline,
        SkillCutsceneRuntimeContext context)
    {
        if (director == null ||
            timeline == null ||
            context == null)
        {
            return;
        }

        context.PrepareTimelineBlendMap(
            timeline);

        foreach (TrackAsset track
                 in SkillTimelineTrackUtility.EnumerateAllTracks(timeline))
        {
            if (track == null)
                continue;

            if (track is
                    SkillCameraTimelineTrack ||
                track is
                    SkillCutsceneEventTrack ||
                track is
                    SkillVfxTimelineTrack ||
                track is
                    SkillShaderTimelineTrack)
            {
                director.SetGenericBinding(
                    track,
                    context);

                continue;
            }

            if (track is not
                AnimationTrack)
            {
                continue;
            }

            if (string.Equals(
                    track.name,
                    AttackerAnimationTrackName,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                BindAnimation(
                    director,
                    track,
                    context.AttackerAnimator);

                continue;
            }

            if (string.Equals(
                    track.name,
                    TargetAnimationTrackName,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                // 공격 스킬 Timeline은 특정 타깃의 Hit AnimationClip을 고정 소유하지 않는다.
                // 빈 Target Track을 Animator에 바인딩하면 Timeline 출력이 타깃의
                // 상태 Animator(Hit/Dead/Idle)를 덮을 수 있으므로 바인딩하지 않는다.
                if (!HasPlayableAnimationClip(
                        track as AnimationTrack))
                {
                    director.ClearGenericBinding(
                        track);
                    continue;
                }

                BindAnimation(
                    director,
                    track,
                    context.TargetAnimator);

                continue;
            }

            if (!track.name.StartsWith(
                    RigAnimationPrefix,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                continue;
            }

            string cameraKey =
                track.name.Substring(
                    RigAnimationPrefix.Length)
                .Trim();

            GameObject cameraObject =
                context.CameraRig?
                    .GetCamera(cameraKey)
                    ?.gameObject;

            if (cameraObject == null)
                continue;

            Animator animator =
                cameraObject.GetComponent<
                    Animator>();

            if (animator == null)
            {
                animator =
                    cameraObject.AddComponent<
                        Animator>();
            }

            BindAnimation(
                director,
                track,
                animator);
        }
    }

    private static bool HasPlayableAnimationClip(
        AnimationTrack track)
    {
        if (track == null)
            return false;

        foreach (TimelineClip clip in
                 track.GetClips())
        {
            if (clip == null ||
                clip.asset == null ||
                clip.duration <= 0d)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void BindAnimation(
        PlayableDirector director,
        TrackAsset track,
        Animator animator)
    {
        if (director == null ||
            track == null ||
            animator == null)
        {
            return;
        }

        director.SetGenericBinding(
            track,
            animator);
    }
}
