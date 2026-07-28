#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

public static class ProjectAbyssVisualFxTrackMigration
{
    private const string EnsureMenu =
        "Tools/Project Abyss/Migration/Ensure Visual FX Tracks";
    private const string ConvertMenu =
        "Tools/Project Abyss/Migration/Convert Legacy VFX Events To VFX Tracks";

    [MenuItem(EnsureMenu, false, 2300)]
    public static void EnsureAllTracks()
    {
        int skillCount = 0;
        int timelineCount = 0;

        foreach (SkillDefinition skill in LoadAll<SkillDefinition>())
        {
            SkillCutsceneDefinition definition =
                skill?.VisualDefinition?.CutsceneDefinition;

            if (definition == null)
                continue;

            skillCount++;

            foreach (SkillCutsceneSegment segment in
                     (SkillCutsceneSegment[])Enum.GetValues(
                         typeof(SkillCutsceneSegment)))
            {
                TimelineAsset timeline = definition.GetTimeline(segment);

                if (timeline == null)
                    continue;

                SkillCutsceneAssetBuilder.EnsureTimelineStructure(
                    definition,
                    timeline,
                    addDefaultClips: false);
                timelineCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Project Abyss Visual FX Migration",
            $"Visual FX Track 확인 완료\nSkills={skillCount}\nTimelines={timelineCount}",
            "확인");
    }

    [MenuItem(ConvertMenu, false, 2301)]
    public static void ConvertLegacyVfxEvents()
    {
        int convertedClips = 0;
        int removedEvents = 0;
        int convertedSkills = 0;
        List<string> warnings = new();

        foreach (SkillDefinition skill in LoadAll<SkillDefinition>())
        {
            SkillVisualDefinition visual = skill?.VisualDefinition;
            SkillCutsceneDefinition definition = visual?.CutsceneDefinition;

            if (visual == null || definition == null || visual.VfxCues == null)
                continue;

            int before = convertedClips;

            foreach (SkillCutsceneSegment segment in
                     (SkillCutsceneSegment[])Enum.GetValues(
                         typeof(SkillCutsceneSegment)))
            {
                TimelineAsset timeline = definition.GetTimeline(segment);

                if (timeline == null)
                    continue;

                SkillCutsceneAssetBuilder.EnsureTimelineStructure(
                    definition,
                    timeline,
                    addDefaultClips: false);

                List<TimelineClip> legacyEvents =
                    SkillTimelineTrackUtility
                        .EnumerateAllTracks(timeline)
                        .OfType<SkillCutsceneEventTrack>()
                        .SelectMany(track => track.GetClips())
                        .Where(clip =>
                            clip.asset is SkillCutsceneEventClip asset &&
                            asset.EventType == SkillCutsceneEventType.Vfx)
                        .ToList();

                foreach (TimelineClip eventTimelineClip in legacyEvents)
                {
                    SkillCutsceneEventClip eventAsset =
                        eventTimelineClip.asset as SkillCutsceneEventClip;

                    if (eventAsset == null)
                        continue;

                    List<BattleVfxCue> matching = visual.VfxCues
                        .Where(cue => cue?.Vfx != null &&
                                      cue.Timing == eventAsset.VfxTiming &&
                                      (!cue.UseHitIndexFilter ||
                                       eventAsset.HitIndex < 0 ||
                                       cue.HitIndex == eventAsset.HitIndex))
                        .ToList();

                    foreach (BattleVfxCue cue in matching)
                    {
                        TimelineClip created =
                            SkillCutsceneAssetBuilder.AddVfxClip(
                                definition,
                                timeline,
                                eventTimelineClip.start + cue.Delay,
                                cue.Vfx);

                        if (created?.asset is not SkillVfxTimelineClip vfxClip)
                            continue;

                        ConfigureFromLegacyCue(vfxClip, cue, eventAsset.HitIndex);
                        convertedClips++;
                    }

                    timeline.DeleteClip(eventTimelineClip);
                    removedEvents++;
                    EditorUtility.SetDirty(timeline);
                }

                // 과거 Hit 자동 VFX만 있고 Vfx Event가 없는 Timeline도 복구한다.
                if (legacyEvents.Count == 0)
                {
                    ConvertHitBoundCues(
                        definition,
                        timeline,
                        visual,
                        ref convertedClips);
                }
            }

            if (convertedClips > before)
            {
                definition.UseExplicitVisualFxTracks = true;
                EditorUtility.SetDirty(definition);
                convertedSkills++;
            }
            else if (visual.VfxCues.Count > 0)
            {
                warnings.Add(
                    $"{skill.name}: VFX Cue는 있지만 변환할 Timeline Event/Hit를 찾지 못했습니다.");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string warningText = warnings.Count == 0
            ? "없음"
            : string.Join("\n", warnings.Take(10));

        EditorUtility.DisplayDialog(
            "Project Abyss Visual FX Migration",
            $"Legacy VFX → Timeline VFX 변환 완료\n" +
            $"Skills={convertedSkills}\n" +
            $"Created VFX Clips={convertedClips}\n" +
            $"Removed Vfx Events={removedEvents}\n\n" +
            $"Warnings\n{warningText}",
            "확인");
    }

    private static void ConvertHitBoundCues(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillVisualDefinition visual,
        ref int convertedClips)
    {
        List<TimelineClip> hits =
            SkillTimelineTrackUtility
                .EnumerateAllTracks(timeline)
                .OfType<SkillCutsceneEventTrack>()
                .SelectMany(track => track.GetClips())
                .Where(clip =>
                    clip.asset is SkillCutsceneEventClip asset &&
                    asset.EventType == SkillCutsceneEventType.Hit)
                .OrderBy(clip => clip.start)
                .ToList();

        foreach (TimelineClip hit in hits)
        {
            SkillCutsceneEventClip hitAsset =
                hit.asset as SkillCutsceneEventClip;
            int hitIndex = hitAsset?.HitIndex ?? -1;

            foreach (BattleVfxCue cue in visual.VfxCues)
            {
                if (cue?.Vfx == null)
                    continue;

                bool timingMatches =
                    cue.Timing == BattleVfxTiming.OnHitFrame ||
                    cue.Timing == BattleVfxTiming.OnCritical;

                if (!timingMatches)
                    continue;

                if (cue.UseHitIndexFilter &&
                    hitIndex >= 0 &&
                    cue.HitIndex != hitIndex)
                {
                    continue;
                }

                TimelineClip created =
                    SkillCutsceneAssetBuilder.AddVfxClip(
                        definition,
                        timeline,
                        hit.start + cue.Delay,
                        cue.Vfx);

                if (created?.asset is not SkillVfxTimelineClip vfxClip)
                    continue;

                ConfigureFromLegacyCue(vfxClip, cue, hitIndex);
                vfxClip.RequireCritical =
                    cue.Timing == BattleVfxTiming.OnCritical;
                convertedClips++;
            }
        }
    }

    private static void ConfigureFromLegacyCue(
        SkillVfxTimelineClip clip,
        BattleVfxCue cue,
        int eventHitIndex)
    {
        clip.Binding = MapBinding(cue.AnchorType);
        clip.AnchorKey = MapAnchorKey(cue.AnchorType);
        clip.FollowMode = cue.Vfx.FollowMode == BattleVfxFollowMode.FollowAnchor
            ? SkillTimelineVfxFollowMode.FollowBinding
            : SkillTimelineVfxFollowMode.SpawnWorldFixed;
        clip.StartPosition = cue.Vfx.PositionOffset;
        clip.StartEuler = cue.Vfx.RotationOffset;
        clip.StartScale = cue.Vfx.Scale;
        clip.UseClipDurationAsLifetime = false;
        clip.LifetimeOverride = cue.Vfx.Lifetime;
        clip.PlaybackMode = SkillTimelineVfxPlaybackMode.OneShot;
        clip.ReleaseAtClipEnd = false;
        clip.UseHitIndexFilter = cue.UseHitIndexFilter || eventHitIndex >= 0;
        clip.HitIndex = cue.UseHitIndexFilter
            ? cue.HitIndex
            : Mathf.Max(0, eventHitIndex);
        clip.RequirePositiveDamage = cue.RequirePositiveDamage;
        clip.RequirePositiveResolvedDamage = cue.RequirePositiveResolvedDamage;
        clip.RequiredAttackerData = cue.RequiredAttackerData;
        clip.RequiredSkillNameContains = cue.RequiredSkillNameContains;
        clip.RequireCritical = cue.Timing == BattleVfxTiming.OnCritical;
        clip.RequireKill = cue.Timing == BattleVfxTiming.OnKill ||
                           cue.Timing == BattleVfxTiming.OnDeath;
        clip.RequirePartBreak = cue.Timing == BattleVfxTiming.OnPartBroken;
        EditorUtility.SetDirty(clip);
    }

    private static SkillVisualFxBinding MapBinding(BattleVfxAnchorType anchor)
    {
        return anchor switch
        {
            BattleVfxAnchorType.AttackerRoot =>
                SkillVisualFxBinding.AttackerRoot,
            BattleVfxAnchorType.TargetRoot =>
                SkillVisualFxBinding.TargetRoot,
            BattleVfxAnchorType.AttackerBodyPart =>
                SkillVisualFxBinding.AttackerBodyPart,
            BattleVfxAnchorType.TargetBodyPart =>
                SkillVisualFxBinding.TargetBodyPart,
            BattleVfxAnchorType.AttackerLookAt =>
                SkillVisualFxBinding.AttackerAnchor,
            BattleVfxAnchorType.TargetLookAt =>
                SkillVisualFxBinding.TargetAnchor,
            BattleVfxAnchorType.WorldPosition =>
                SkillVisualFxBinding.CombatFrame,
            _ =>
                SkillVisualFxBinding.CombatFrame
        };
    }

    private static string MapAnchorKey(BattleVfxAnchorType anchor)
    {
        return anchor switch
        {
            BattleVfxAnchorType.AttackerLookAt => "LookAtPoint",
            BattleVfxAnchorType.TargetLookAt => "LookAtPoint",
            BattleVfxAnchorType.AttackerBodyPart => "Center",
            BattleVfxAnchorType.TargetBodyPart => "Center",
            _ => "Center"
        };
    }

    private static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object
    {
        string filter = "t:" + typeof(T).Name;

        foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets/2. Data" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                yield return asset;
        }
    }
}
#endif
