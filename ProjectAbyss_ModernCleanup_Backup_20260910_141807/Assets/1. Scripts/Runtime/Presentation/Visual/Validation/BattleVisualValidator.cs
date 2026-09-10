using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Timeline;

public static class BattleVisualValidator
{
    private static readonly HashSet<string> LoggedDuplicateCueWarnings = new();
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        LoggedDuplicateCueWarnings.Clear();
    }

    public static bool ValidateRequest(
        BattleVisualRequest request,
        bool logWarnings = true)
    {
        if (request == null)
            return false;

        bool valid = true;

        if (request.Attacker == null)
        {
            valid = false;

            if (logWarnings)
                Debug.LogWarning("[BATTLE VISUAL VALIDATION] Attacker가 없습니다.");
        }

        if (request.VisualDefinition == null)
        {
            valid = false;

            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[BATTLE VISUAL VALIDATION] VisualDefinition이 없습니다. / ActionId={request.SourceAction?.ActionId ?? 0}");
            }
        }

        if (request.Target != null && request.Target.UsesBodyParts && request.TargetPart == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning(
                    $"[BATTLE VISUAL VALIDATION] 부위형 대상인데 TargetPart가 없습니다. / Target={request.Target.Data?.CharacterName}");
            }
        }

        if (request.VisualDefinition != null)
        {
            valid &= ValidateDefinition(
                request.VisualDefinition,
                request.Attacker,
                logWarnings);
        }

        if (request.Target != null &&
            logWarnings)
        {
            CharacterView targetView =
                BattleCameraTargetResolver.GetView(
                    request.Target);

            if (targetView != null &&
                targetView.PresentationProfile == null)
            {
                Debug.LogWarning(
                    "[BATTLE VISUAL VALIDATION] 타깃 CharacterPresentationProfile이 없습니다. " +
                    "피격 반응은 기존 Animator Hit/Dead 상태로 폴백합니다. " +
                    $"Target={request.Target.Data?.CharacterName ?? request.Target.name}",
                    targetView);
            }
        }

        return valid;
    }

    public static bool ValidateDefinition(
        SkillVisualDefinition visual,
        Character actor = null,
        bool logWarnings = true)
    {
        if (visual == null)
            return false;

        bool valid = ValidateRequiredTimeline(
            visual,
            logWarnings);

        if (visual.HasHitFrameDamage &&
            visual.TargetReaction == HitReactionKey.None &&
            logWarnings)
        {
            Debug.LogWarning(
                "[BATTLE VISUAL VALIDATION] 피해가 있지만 TargetReaction=None입니다. " +
                $"Visual={visual.name}",
                visual);
        }

        HashSet<string> cueKeys = new();

        if (visual.VfxCues != null)
        {
            for (int i = 0; i < visual.VfxCues.Count; i++)
            {
                BattleVfxCue cue = visual.VfxCues[i];

                if (cue == null)
                    continue;

                valid &= VfxDefinitionValidator.Validate(cue.Vfx, visual, logWarnings);

                if (string.IsNullOrEmpty(cue.CueKey))
                    continue;

                string collisionIdentity =
                    BuildCollisionIdentity(cue);

                if (cueKeys.Add(collisionIdentity))
                    continue;

                if (CanAutoDistributeDuplicateHitCue(
                        visual,
                        cue,
                        i))
                {
                    continue;
                }

                valid = false;

                if (logWarnings)
                {
                    string warningKey =
                        visual.GetInstanceID() + "|" + collisionIdentity;

                    if (LoggedDuplicateCueWarnings.Add(warningKey))
                    {
                        Debug.LogWarning(
                            $"[BATTLE VISUAL VALIDATION] 실제 충돌하는 VFX Cue / " +
                            $"Visual={visual.name}, Key={cue.CueKey}, " +
                            $"Timing={cue.Timing}, HitFilter=" +
                            $"{(cue.UseHitIndexFilter ? cue.HitIndex.ToString() : "NONE")}",
                            visual);
                    }
                }
            }
        }

        if (actor != null && visual.RequiredAnimatorStates != null)
        {
            Animator animator = actor.GetComponentInChildren<Animator>(true);
            valid &= AnimatorStateValidator.Validate(
                animator,
                visual.RequiredAnimatorStates,
                visual,
                logWarnings);
        }

        return valid;
    }

    private static bool ValidateRequiredTimeline(
        SkillVisualDefinition visual,
        bool logWarnings)
    {
        SkillVisualDefinition definition =
            visual;

        if (definition == null)
        {
            if (logWarnings)
            {
                Debug.LogError(
                    $"[BATTLE VISUAL VALIDATION] Timeline-only 정책 위반: " +
                    $"{visual?.name ?? "NULL"}에 통합 Presentation Timeline 데이터가 없습니다.",
                    visual);
            }
            return false;
        }

        bool valid = true;

        if (!definition.HasCompleteTimelineSet)
        {
            valid = false;
            if (logWarnings)
            {
                Debug.LogError(
                    $"[BATTLE VISUAL VALIDATION] Action/ClashAttack Timeline과 Camera Rig가 완전하지 않습니다. " +
                    $"Visual={visual.name}, Missing={string.Join(", ", definition.GetMissingRequirements())}",
                    definition);
            }
        }

        foreach (SkillCutsceneSegment segment in
                 SkillCutsceneSegmentUtility
                     .EnumerateActiveAttackerSegments())
        {
            TimelineAsset timeline = definition.GetTimeline(segment);
            if (timeline == null)
                continue;

            valid &= ValidateTimelineTracks(
                visual,
                definition,
                timeline,
                segment,
                logWarnings);
        }

        return valid;
    }

    private static bool ValidateTimelineTracks(
        SkillVisualDefinition visual,
        SkillVisualDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneSegment segment,
        bool logWarnings)
    {
        bool valid = true;
        TrackAsset[] tracks = SkillTimelineTrackUtility
            .EnumerateAllTracks(timeline)
            .ToArray();

        bool hasCameraClip = tracks
            .OfType<SkillCameraTimelineTrack>()
            .SelectMany(track => track.GetClips())
            .Any();

        if (!hasCameraClip)
        {
            valid = false;
            LogTimelineIssue(
                logWarnings,
                timeline,
                $"{segment}: Skill Camera Clip이 없습니다.");
        }

        if (segment == SkillCutsceneSegment.Action ||
            segment == SkillCutsceneSegment.ClashAttack)
        {
            bool hasAttackerAnimation = tracks
                .OfType<AnimationTrack>()
                .Where(track => string.Equals(
                    track.name,
                    SkillCutsceneTimelineBinder.AttackerAnimationTrackName,
                    StringComparison.OrdinalIgnoreCase))
                .SelectMany(track => track.GetClips())
                .Any();

            if (!hasAttackerAnimation)
            {
                valid = false;
                LogTimelineIssue(
                    logWarnings,
                    timeline,
                    $"{segment}: Attacker Animation Clip이 없습니다.");
            }

            if (visual.HasHitFrameDamage)
            {
                TimelineClip[] hitClips = tracks
                    .OfType<SkillCutsceneEventTrack>()
                    .SelectMany(track => track.GetClips())
                    .Where(clip =>
                        clip.asset is SkillCutsceneEventClip asset &&
                        asset.EventType == SkillCutsceneEventType.Hit)
                    .OrderBy(clip => clip.start)
                    .ToArray();

                int expected = Mathf.Max(1, visual.ExpectedHitFrameCount);
                if (hitClips.Length < expected)
                {
                    valid = false;
                    LogTimelineIssue(
                        logWarnings,
                        timeline,
                        $"{segment}: Hit Event가 부족합니다. Expected={expected}, Actual={hitClips.Length}");
                }

                if (visual.UseHitCameraShake)
                {
                    int shakeCount = tracks
                        .OfType<SkillCutsceneEventTrack>()
                        .SelectMany(track => track.GetClips())
                        .Count(clip =>
                            clip.asset is SkillCutsceneEventClip asset &&
                            asset.EventType == SkillCutsceneEventType.CameraShake);

                    if (shakeCount < hitClips.Length)
                    {
                        valid = false;
                        LogTimelineIssue(
                            logWarnings,
                            timeline,
                            $"{segment}: CameraShake Event가 Hit보다 적습니다. Hit={hitClips.Length}, Shake={shakeCount}");
                    }
                }

                AnimationTrack targetTrack = tracks
                    .OfType<AnimationTrack>()
                    .FirstOrDefault(track =>
                        string.Equals(
                            track.name,
                            SkillCutsceneTimelineBinder.TargetAnimationTrackName,
                            StringComparison.OrdinalIgnoreCase));

                if (targetTrack != null &&
                    targetTrack.GetClips().Any())
                {
                    valid = false;
                    LogTimelineIssue(
                        logWarnings,
                        timeline,
                        $"{segment}: 공격 Timeline에 고정 Target Animation Clip이 있습니다. " +
                        "타깃은 런타임에 달라지므로 Hit Event가 현재 타깃의 Hit 상태를 재생해야 합니다.");
                }
            }
            else
            {
                int unexpectedHitCount = tracks
                    .OfType<SkillCutsceneEventTrack>()
                    .SelectMany(track => track.GetClips())
                    .Count(clip =>
                        clip.asset is SkillCutsceneEventClip asset &&
                        asset.EventType == SkillCutsceneEventType.Hit);

                if (unexpectedHitCount > 0)
                {
                    valid = false;
                    LogTimelineIssue(
                        logWarnings,
                        timeline,
                        $"{segment}: 피해 없는 스킬에 Hit Event가 있습니다. Actual={unexpectedHitCount}");
                }
            }
        }

        SkillVfxTimelineClip[] vfxClips = tracks
            .OfType<SkillVfxTimelineTrack>()
            .SelectMany(track => track.GetClips())
            .Select(clip => clip.asset as SkillVfxTimelineClip)
            .Where(asset => asset != null)
            .ToArray();

        foreach (SkillVfxTimelineClip vfxClip in vfxClips)
        {
            if (vfxClip.Definition != null)
                continue;

            valid = false;
            LogTimelineIssue(
                logWarnings,
                timeline,
                $"{segment}: VFX Clip에 BattleVfxDefinition이 없습니다.");
        }

        SkillShaderTimelineClip[] shaderClips = tracks
            .OfType<SkillShaderTimelineTrack>()
            .SelectMany(track => track.GetClips())
            .Select(clip => clip.asset as SkillShaderTimelineClip)
            .Where(asset => asset != null)
            .ToArray();

        foreach (SkillShaderTimelineClip shaderClip in shaderClips)
        {
            if (shaderClip.Definition != null)
                continue;

            valid = false;
            LogTimelineIssue(
                logWarnings,
                timeline,
                $"{segment}: Shader FX Clip에 SkillShaderEffectDefinition이 없습니다.");
        }

        if (definition.UseExplicitVisualFxTracks)
        {
            int legacyVfxEventCount = tracks
                .OfType<SkillCutsceneEventTrack>()
                .SelectMany(track => track.GetClips())
                .Count(clip =>
                    clip.asset is SkillCutsceneEventClip asset &&
                    asset.EventType == SkillCutsceneEventType.Vfx);

            if (legacyVfxEventCount > 0)
            {
                valid = false;
                LogTimelineIssue(
                    logWarnings,
                    timeline,
                    $"{segment}: Explicit Visual FX가 활성화됐지만 Legacy Vfx Event가 {legacyVfxEventCount}개 남아 있습니다. " +
                    "Visual FX Migration으로 VFX Track Clip으로 변환하세요.");
            }
        }

        return valid;
    }

    private static void LogTimelineIssue(
        bool logWarnings,
        UnityEngine.Object context,
        string message)
    {
        if (!logWarnings)
            return;

        Debug.LogError(
            $"[BATTLE VISUAL VALIDATION] Timeline-only 정책 위반: {message}",
            context);
    }

    private static string BuildCollisionIdentity(
        BattleVfxCue cue)
    {
        if (cue == null)
            return string.Empty;

        return
            cue.CueKey + "|" +
            cue.Timing + "|" +
            cue.RepeatMode + "|" +
            (cue.UseHitIndexFilter
                ? "FILTER:" + cue.HitIndex
                : "NO_FILTER") + "|" +
            (cue.RequiredAttackerData != null
                ? cue.RequiredAttackerData.GetInstanceID().ToString()
                : "ANY_ATTACKER") + "|" +
            (cue.RequiredSkillNameContains ?? string.Empty) + "|" +
            cue.RequirePositiveDamage + "|" +
            cue.RequirePositiveResolvedDamage;
    }

    private static bool CanAutoDistributeDuplicateHitCue(
        SkillVisualDefinition visual,
        BattleVfxCue cue,
        int cueIndex)
    {
        if (visual?.VfxCues == null ||
            cue == null ||
            cueIndex < 0 ||
            cueIndex >= visual.VfxCues.Count)
        {
            return false;
        }

        for (int i = 0; i < cueIndex; i++)
        {
            BattleVfxCue previous = visual.VfxCues[i];

            if (cue.CanAutoDistributeByHitIndexWith(previous))
                return true;
        }

        return false;
    }

    public static bool ValidateProfile(
        SkillVisualProfile profile,
        UnityEngine.Object context = null,
        bool logWarnings = true)
    {
        if (profile == null)
            return true;

        bool valid = true;

        foreach (SkillVisualDefinition visual in profile.EnumerateDefinitions())
        {
            if (visual == null)
                continue;

            if (!visual.AllowAsProfileFallback)
            {
                valid = false;

                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[BATTLE VISUAL VALIDATION] 캐릭터 전용 Visual이 기본 Profile에 연결됨 / Visual={visual.name}",
                        context != null ? context : profile);
                }
            }

            if (visual.VfxCues != null)
            {
                foreach (BattleVfxCue cue in visual.VfxCues)
                {
                    if (cue?.RequiredAttackerData == null)
                        continue;

                    valid = false;

                    if (logWarnings)
                    {
                        Debug.LogWarning(
                            $"[BATTLE VISUAL VALIDATION] 특정 CharacterData 필터를 가진 Visual이 기본 Profile에 연결됨 / " +
                            $"Visual={visual.name}, Character={cue.RequiredAttackerData.CharacterName}",
                            context != null ? context : profile);
                    }
                }
            }

            // Timeline-only 런타임은 Profile fallback을 사용하지 않는다.
            // Profile은 이전 데이터 확인용이므로 Timeline 필수 검증 대상에서 제외한다.
        }

        return valid;
    }
}