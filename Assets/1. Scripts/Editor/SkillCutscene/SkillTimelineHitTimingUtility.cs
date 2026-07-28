#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Legacy Animation Event 또는 검증된 Core 타이밍을 Timeline Hit Event 시간으로 변환한다.
/// 공격 AnimationClip의 실제 접촉 프레임이 타격 타이밍의 유일한 마이그레이션 근거다.
/// </summary>
public static class SkillTimelineHitTimingUtility
{
    private const string LegacyHitFunction =
        "AnimationEvent_HitFrame";

    // 2026-07-28 Core Export에서 복구한 원래 Animation Event 시간.
    // v6.0 마이그레이션이 Animation Event를 이미 제거한 프로젝트를 복구하기 위한
    // 1회성 안전망이며, 신규 Animation은 Timeline에서 직접 Hit을 Authoring해야 한다.
    private static readonly Dictionary<string, double[]>
        KnownCoreHitTimes =
            new Dictionary<string, double[]>(
                StringComparer.OrdinalIgnoreCase)
            {
                {
                    "Olaf_NormalAttack",
                    new[] { 0.8666667d }
                },
                {
                    "EliteEnemy_NormalAttack",
                    new[] { 0.8666667d }
                },
                {
                    "Olaf_Duel",
                    new[] { 0.9d, 1.6666666d }
                },
                {
                    "EliteEnemy_Duel",
                    new[] { 0.9d, 1.6666666d }
                },
                {
                    "Olaf_Preparation",
                    new[] { 1.3333334d }
                },
                {
                    "Olaf_Prestige",
                    new[] { 1.6666666d }
                },
                {
                    "EliteEnemy_Prestige",
                    new[] { 1.6666666d }
                }
            };

    public static bool TryResolveTimelineHitTimes(
        TimelineAsset timeline,
        int expectedHitCount,
        double frameRate,
        out List<double> timelineTimes,
        out string timingSource,
        out string error)
    {
        timelineTimes =
            new List<double>();
        timingSource =
            string.Empty;
        error =
            string.Empty;

        if (timeline == null)
        {
            error = "Timeline이 없습니다.";
            return false;
        }

        if (expectedHitCount <= 0)
        {
            timingSource = "No damage";
            return true;
        }

        if (!TryGetAttackerAnimationClip(
                timeline,
                out TimelineClip attackerTimelineClip,
                out AnimationClip animation))
        {
            error =
                "[Abyss] Attacker Animation Clip을 찾지 못했습니다.";
            return false;
        }

        List<double> sourceTimes =
            ReadLegacyAnimationEventTimes(
                animation);

        if (sourceTimes.Count > 0)
        {
            timingSource =
                "AnimationEvent_HitFrame";
        }
        else if (KnownCoreHitTimes.TryGetValue(
                     animation.name,
                     out double[] knownTimes))
        {
            sourceTimes =
                knownTimes
                    .OrderBy(time => time)
                    .ToList();

            timingSource =
                "Recovered Core timing catalog";
        }

        if (sourceTimes.Count == 0)
        {
            error =
                $"{animation.name}: 실제 Hit 타이밍 근거가 없습니다. " +
                "Timeline Battle Events Track에서 Hit Clip을 직접 배치하세요.";
            return false;
        }

        if (sourceTimes.Count < expectedHitCount)
        {
            error =
                $"{animation.name}: 실제 접촉 프레임은 {sourceTimes.Count}개인데 " +
                $"스킬은 {expectedHitCount} Hit을 요구합니다. " +
                "현재 AnimationClip으로는 모든 타격을 물리적으로 동기화할 수 없습니다.";
            return false;
        }

        double safeFrameRate =
            Math.Max(
                1d,
                frameRate);

        for (int index = 0;
             index < expectedHitCount;
             index++)
        {
            double sourceTime =
                sourceTimes[index];

            double speed =
                Math.Max(
                    0.0001d,
                    attackerTimelineClip.timeScale);

            double converted =
                attackerTimelineClip.start +
                (sourceTime -
                 attackerTimelineClip.clipIn) /
                speed;

            double minimum =
                attackerTimelineClip.start;

            double maximum =
                Math.Max(
                    minimum,
                    attackerTimelineClip.end -
                    0.5d /
                    safeFrameRate);

            timelineTimes.Add(
                Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        converted)));
        }

        return true;
    }

    public static int RemoveRedundantHitSideEffects(
        TimelineAsset timeline,
        IReadOnlyList<TimelineClip> hits,
        double frameRate)
    {
        if (timeline == null)
            return 0;

        SkillCutsceneEventTrack eventTrack =
            timeline.GetOutputTracks()
                .OfType<SkillCutsceneEventTrack>()
                .FirstOrDefault();

        if (eventTrack == null)
            return 0;

        double proximity =
            2d /
            Math.Max(
                1d,
                frameRate);

        int removed = 0;

        foreach (TimelineClip clip in
                 eventTrack.GetClips()
                    .ToArray())
        {
            if (clip.asset is not
                SkillCutsceneEventClip asset)
            {
                continue;
            }

            bool nearHit =
                hits != null &&
                hits.Any(
                    hit =>
                        Math.Abs(
                            hit.start -
                            clip.start) <=
                        proximity);

            bool redundant =
                asset.EventType ==
                    SkillCutsceneEventType.Vfx &&
                (asset.VfxTiming ==
                     BattleVfxTiming.OnHitFrame ||
                 asset.VfxTiming ==
                     BattleVfxTiming.OnCritical);

            redundant |=
                asset.EventType ==
                    SkillCutsceneEventType.CameraShake &&
                (asset.HitIndex >= 0 || nearHit);

            redundant |=
                asset.EventType ==
                    SkillCutsceneEventType.CameraImpactPulse &&
                asset.CameraImpactTiming ==
                    SkillCameraImpactTiming.OnHitFrame &&
                (asset.HitIndex >= 0 || nearHit);

            redundant |=
                asset.EventType ==
                    SkillCutsceneEventType.TargetHitReaction &&
                nearHit;

            if (!redundant)
                continue;

            timeline.DeleteClip(
                clip);
            removed++;
        }

        if (removed > 0)
            EditorUtility.SetDirty(timeline);

        return removed;
    }

    private static bool TryGetAttackerAnimationClip(
        TimelineAsset timeline,
        out TimelineClip attackerTimelineClip,
        out AnimationClip animation)
    {
        attackerTimelineClip =
            null;
        animation =
            null;

        AnimationTrack track =
            timeline.GetOutputTracks()
                .OfType<AnimationTrack>()
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.name,
                            SkillCutsceneTimelineBinder
                                .AttackerAnimationTrackName,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (track == null)
            return false;

        foreach (TimelineClip clip in
                 track.GetClips()
                    .OrderBy(item => item.start))
        {
            if (clip.asset is not
                AnimationPlayableAsset playable ||
                playable.clip == null)
            {
                continue;
            }

            attackerTimelineClip =
                clip;
            animation =
                playable.clip;
            return true;
        }

        return false;
    }

    private static List<double>
        ReadLegacyAnimationEventTimes(
            AnimationClip animation)
    {
        if (animation == null)
            return new List<double>();

        try
        {
            return AnimationUtility
                .GetAnimationEvents(animation)
                .Where(
                    animationEvent =>
                        animationEvent != null &&
                        string.Equals(
                            animationEvent.functionName,
                            LegacyHitFunction,
                            StringComparison.Ordinal))
                .Select(
                    animationEvent =>
                        (double)animationEvent.time)
                .Distinct()
                .OrderBy(time => time)
                .ToList();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[SkillTimelineHitTimingUtility] " +
                $"{animation.name} Animation Event 읽기 실패: " +
                exception.Message,
                animation);

            return new List<double>();
        }
    }
}
#endif
