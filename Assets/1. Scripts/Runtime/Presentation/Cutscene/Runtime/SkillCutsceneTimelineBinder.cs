using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Timeline Track을 런타임에 생성된 Attacker/Target/CameraRig에 연결한다.
///
/// Track 이름 계약:
/// [Abyss] Attacker Animation
/// [Abyss] Target Animation (legacy, runtime unbound)
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
                // v8 구조에서는 스킬 Timeline이 타깃 Animator를 절대 제어하지 않습니다.
                // 실제 피격 Clip은 현재 타깃의 CharacterPresentationProfile이 선택합니다.
                // Legacy Track에 Clip이 남아 있더라도 런타임에서는 바인딩하지 않습니다.
                director.ClearGenericBinding(
                    track);

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