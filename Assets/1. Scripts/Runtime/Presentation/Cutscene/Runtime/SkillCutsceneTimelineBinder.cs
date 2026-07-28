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
                 in timeline.GetOutputTracks())
        {
            if (track == null)
                continue;

            if (track is
                    SkillCameraTimelineTrack ||
                track is
                    SkillCutsceneEventTrack)
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
