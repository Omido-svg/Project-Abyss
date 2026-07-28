#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>
/// Project Abyss의 모든 전투 SkillDefinition을 Timeline-only Authoring 구조로 변환한다.
///
/// 정책:
/// - SkillDefinition마다 전용 SkillVisualDefinition을 소유한다.
/// - SkillVisualDefinition마다 전용 SkillCutsceneDefinition과 5개 Segment를 소유한다.
/// - 공격 타이밍은 Timeline Battle Events Track의 Hit/Vfx/Camera 이벤트만 사용한다.
/// - AnimationEvent_HitFrame / EffectFrame / End와 AnimationEventRelay는 제거한다.
/// - Animator는 Idle / Hit / Dead / 상태 표현만 담당한다.
/// </summary>
public static class ProjectAbyssTimelineOnlyMigration
{
    private const string DataRoot =
        "Assets/2. Data";

    private const string ReportFolder =
        DataRoot + "/Timeline Migration";

    private const string ReportPath =
        ReportFolder + "/Timeline_Only_Migration_Report.md";

    private const string HitSyncReportPath =
        ReportFolder + "/Timeline_Hit_Sync_Repair_Report.md";

    private const string MenuRoot =
        "Tools/Project Abyss/Migration/";

    private static readonly string[]
        RemovedAnimationEventFunctions =
        {
            "AnimationEvent_HitFrame",
            "AnimationEvent_EffectFrame",
            "AnimationEvent_End"
        };

    [MenuItem(
        MenuRoot +
        "Convert All Combat Skills To Timeline",
        false,
        2100)]
    public static void ConvertAllCombatSkills()
    {
        bool proceed =
            EditorUtility.DisplayDialog(
                "Timeline-only 전투 스킬 마이그레이션",
                "Assets/2. Data의 모든 SkillDefinition을 다음 정책으로 변환합니다.\n\n" +
                "• 스킬별 전용 Visual / Cutscene 생성\n" +
                "• Action / ClashAttack / PartBreak / Kill / Return Timeline 생성\n" +
                "• 실제 Animation 접촉 시간으로 Timeline Hit Event 동기화\n" +
                "• .anim의 기존 Animation Event 제거\n" +
                "• Prefab 및 열린 Scene의 AnimationEventRelay 제거\n\n" +
                "Git 커밋 또는 백업 후 실행하는 것을 권장합니다.",
                "변환",
                "취소");

        if (!proceed)
            return;

        if (!EditorSceneManager
            .SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        MigrationReport report =
            new MigrationReport();

        try
        {
            ExecuteMigration(report);
            WriteReport(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            EditorUtility.DisplayDialog(
                report.HasErrors
                    ? "Timeline-only 변환 완료 — 확인 필요"
                    : "Timeline-only 변환 완료",
                report.BuildDialogSummary(),
                "확인");

            Debug.Log(
                "[TimelineOnlyMigration] " +
                report.BuildDialogSummary() +
                "\nReport: " + ReportPath);
        }
        catch (Exception exception)
        {
            report.Errors.Add(
                exception.ToString());

            Debug.LogException(exception);

            try
            {
                WriteReport(report);
            }
            catch (Exception reportException)
            {
                Debug.LogException(
                    reportException);
            }

            EditorUtility.DisplayDialog(
                "Timeline-only 변환 실패",
                exception.ToString(),
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }


    [MenuItem(
        MenuRoot +
        "Repair Existing Timeline Hit Sync",
        false,
        2101)]
    public static void RepairExistingTimelineHitSync()
    {
        bool proceed =
            EditorUtility.DisplayDialog(
                "Timeline Hit 싱크 복구",
                "현재 생성된 Action / ClashAttack Timeline의 Hit Event를 " +
                "공격 AnimationClip의 실제 접촉 시간으로 재배치합니다.\n\n" +
                "v6.0의 55%~82% 추정 배치를 사용한 기존 Timeline을 복구하며, " +
                "NormalEnemy처럼 공격 모션이 없는 스킬은 건너뜁니다.",
                "Hit 싱크 복구",
                "취소");

        if (!proceed)
            return;

        List<SkillDefinition> skills =
            LoadAllSkills();

        int retimed = 0;
        int removedRedundant = 0;
        int warningCount = 0;
        int errorCount = 0;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "# Project Abyss Timeline Hit Sync Repair Report");
        builder.AppendLine();
        builder.AppendLine(
            $"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine(
            $"- Unity: {Application.unityVersion}");
        builder.AppendLine(
            $"- Skills scanned: {skills.Count}");
        builder.AppendLine();

        try
        {
            for (int index = 0;
                 index < skills.Count;
                 index++)
            {
                SkillDefinition skill =
                    skills[index];

                EditorUtility.DisplayProgressBar(
                    "Timeline Hit 싱크 복구",
                    skill != null
                        ? skill.name
                        : "NULL",
                    skills.Count > 0
                        ? (float)index / skills.Count
                        : 1f);

                SkillMigrationResult result =
                    new SkillMigrationResult(skill);

                SkillVisualDefinition visual =
                    skill?.VisualDefinition;

                SkillCutsceneDefinition definition =
                    visual?.CutsceneDefinition;

                if (visual == null ||
                    definition == null)
                {
                    result.Warnings.Add(
                        "Visual 또는 CutsceneDefinition이 없어 건너뜁니다.");
                }
                else
                {
                    TimelineAsset[] attackTimelines =
                    {
                        definition.ActionTimeline,
                        definition.ClashAttackTimeline
                    };

                    foreach (TimelineAsset timeline in
                             attackTimelines)
                    {
                        if (timeline == null)
                        {
                            result.Warnings.Add(
                                "Action/ClashAttack Timeline이 없어 일부 복구를 건너뜁니다.");
                            continue;
                        }

                        NormalizeHitEvents(
                            skill,
                            visual,
                            definition,
                            timeline,
                            result);
                    }
                }

                retimed +=
                    result.RetimedHitEventCount;

                removedRedundant +=
                    result.RemovedDuplicateEventCount;

                warningCount +=
                    result.Warnings.Count;

                errorCount +=
                    result.Errors.Count;

                builder.AppendLine(
                    $"## {result.DisplayName}");
                builder.AppendLine(
                    $"- Retimed Hit events: {result.RetimedHitEventCount}");
                builder.AppendLine(
                    $"- Removed redundant events: {result.RemovedDuplicateEventCount}");
                builder.AppendLine(
                    $"- Timing source: {result.HitTimingSource}");

                foreach (string warning in
                         result.Warnings)
                {
                    builder.AppendLine(
                        $"- Warning: {warning}");
                }

                foreach (string error in
                         result.Errors)
                {
                    builder.AppendLine(
                        $"- Error: {error}");
                }

                builder.AppendLine();
            }

            EnsureFolder(
                ReportFolder);

            File.WriteAllText(
                HitSyncReportPath,
                builder.ToString(),
                new UTF8Encoding(false));

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                HitSyncReportPath,
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            string summary =
                $"Retimed Hit Events: {retimed}\n" +
                $"Removed Redundant Events: {removedRedundant}\n" +
                $"Warnings: {warningCount}\n" +
                $"Errors: {errorCount}\n" +
                $"Report: {HitSyncReportPath}";

            EditorUtility.DisplayDialog(
                errorCount > 0
                    ? "Timeline Hit 싱크 복구 완료 — 확인 필요"
                    : "Timeline Hit 싱크 복구 완료",
                summary,
                "확인");

            Debug.Log(
                "[TimelineHitSyncRepair] " +
                summary);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(
        MenuRoot +
        "Validate Timeline Only Combat Skills",
        false,
        2102)]
    public static void ValidateAllCombatSkills()
    {
        MigrationReport report =
            new MigrationReport();

        try
        {
            List<SkillDefinition> skills =
                LoadAllSkills();

            Dictionary<SkillVisualDefinition, int>
                visualUseCount =
                    BuildVisualUseCount(
                        skills);

            for (int index = 0;
                 index < skills.Count;
                 index++)
            {
                SkillDefinition skill =
                    skills[index];

                EditorUtility.DisplayProgressBar(
                    "Timeline-only 검증",
                    skill != null
                        ? skill.name
                        : "NULL",
                    skills.Count > 0
                        ? (float)index / skills.Count
                        : 1f);

                SkillMigrationResult validation =
                    ValidateSkill(skill);

                if (skill?.VisualDefinition != null &&
                    visualUseCount.TryGetValue(
                        skill.VisualDefinition,
                        out int useCount) &&
                    useCount > 1)
                {
                    validation.Errors.Add(
                        $"SkillVisualDefinition이 {useCount}개 스킬에 공유되어 있습니다. " +
                        "Timeline-only 정책에서는 스킬별 전용 Visual이 필요합니다.");
                    validation.Converted = false;
                }

                report.SkillResults.Add(
                    validation);
            }

            report.AnimationEventScan =
                ScanLegacyAnimationEvents(
                    remove: false);

            report.RelayScan =
                ScanAnimationEventRelays(
                    remove: false);

            WriteReport(report);

            EditorUtility.DisplayDialog(
                report.HasErrors
                    ? "Timeline-only 검증 실패"
                    : "Timeline-only 검증 성공",
                report.BuildDialogSummary(),
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void ExecuteMigration(
        MigrationReport report)
    {
        List<SkillDefinition> skills =
            LoadAllSkills();

        report.SkillCount =
            skills.Count;

        Dictionary<SkillVisualDefinition, int>
            visualUseCount =
                BuildVisualUseCount(
                    skills);

        for (int index = 0;
             index < skills.Count;
             index++)
        {
            SkillDefinition skill =
                skills[index];

            EditorUtility.DisplayProgressBar(
                "Timeline-only 전투 스킬 변환",
                skill != null
                    ? skill.name
                    : "NULL",
                skills.Count > 0
                    ? (float)index / skills.Count
                    : 1f);

            SkillMigrationResult result =
                ConvertSkill(
                    skill,
                    visualUseCount);

            report.SkillResults.Add(
                result);
        }

        report.AnimationEventScan =
            ScanLegacyAnimationEvents(
                remove: true);

        report.RelayScan =
            ScanAnimationEventRelays(
                remove: true);

        if (report.RelayScan.ModifiedSceneCount > 0)
        {
            EditorSceneManager
                .SaveOpenScenes();
        }
    }

    private static SkillMigrationResult ConvertSkill(
        SkillDefinition skill,
        IReadOnlyDictionary<
            SkillVisualDefinition,
            int> visualUseCount)
    {
        SkillMigrationResult result =
            new SkillMigrationResult(skill);

        if (skill == null)
        {
            result.Errors.Add(
                "SkillDefinition 참조가 NULL입니다.");
            return result;
        }

        string skillPath =
            AssetDatabase.GetAssetPath(
                skill);

        if (string.IsNullOrWhiteSpace(
                skillPath))
        {
            result.Errors.Add(
                "영구 에셋 경로가 없습니다.");
            return result;
        }

        SkillVisualDefinition originalVisual =
            skill.VisualDefinition;

        AnimationClip resolvedAnimation =
            ResolveAttackerAnimation(
                skill,
                originalVisual?
                    .CutsceneDefinition?
                    .AttackerAnimation,
                result);

        SkillVisualDefinition visual =
            EnsureExclusiveVisual(
                skill,
                originalVisual,
                visualUseCount,
                result);

        if (visual == null)
        {
            result.Errors.Add(
                "전용 SkillVisualDefinition 생성에 실패했습니다.");
            return result;
        }

        ApplyActionTypeDefaults(
            skill,
            visual,
            originalVisual == null);

        EditorUtility.SetDirty(
            visual);

        bool cutsceneWasMissing =
            visual.CutsceneDefinition == null;

        SkillCutsceneDefinition definition =
            SkillCutsceneAssetBuilder
                .EnsureForSkill(
                    skill,
                    null,
                    null);

        if (definition == null)
        {
            result.Errors.Add(
                "SkillCutsceneDefinition 생성에 실패했습니다.");
            return result;
        }

        if (resolvedAnimation != null)
        {
            definition.AttackerAnimation =
                resolvedAnimation;

            int durationFrames =
                Mathf.Max(
                    30,
                    Mathf.CeilToInt(
                        resolvedAnimation.length *
                        (float)definition.FrameRate));

            definition.DefaultDurationFrames =
                Mathf.Max(
                    definition.DefaultDurationFrames,
                    durationFrames);
        }
        else
        {
            result.Warnings.Add(
                "공격 AnimationClip을 자동 선택하지 못했습니다. " +
                "Action/ClashAttack Timeline에 직접 배치해야 합니다.");
        }

        definition.PrepareFacing =
            visual.FaceEachOther;

        definition.PrepareMovement =
            visual.MovesToTarget;

        definition.RestoreMovement =
            visual.ReturnPositionAfterAction;

        definition.RestoreFacing =
            visual.ReturnFacingAfterAction;

        EditorUtility.SetDirty(
            definition);

        foreach (SkillCutsceneSegment segment in
                 (SkillCutsceneSegment[])
                 Enum.GetValues(
                     typeof(
                         SkillCutsceneSegment)))
        {
            TimelineAsset timeline =
                definition.GetTimeline(
                    segment) ??
                SkillCutsceneAssetBuilder
                    .EnsureSegment(
                        definition,
                        segment);

            if (timeline == null)
            {
                result.Errors.Add(
                    $"{segment} Timeline을 생성하지 못했습니다.");
                continue;
            }

            bool isAttackSegment =
                segment ==
                    SkillCutsceneSegment.Action ||
                segment ==
                    SkillCutsceneSegment.ClashAttack;

            SkillCutsceneAssetBuilder
                .EnsureTimelineStructure(
                    definition,
                    timeline,
                    addDefaultClips:
                        isAttackSegment);

            if (isAttackSegment)
            {
                EnsureAttackerAnimation(
                    definition,
                    timeline,
                    resolvedAnimation,
                    result);

                NormalizeDynamicTargetTrack(
                    timeline,
                    result);

                NormalizeHitEvents(
                    skill,
                    visual,
                    definition,
                    timeline,
                    result);

                if (cutsceneWasMissing)
                {
                    MigrateLegacyCameraShots(
                        visual,
                        definition,
                        timeline,
                        segment,
                        result);
                }
            }

            EnsureVfxEvents(
                visual,
                definition,
                timeline,
                segment,
                result);

            EnsureCameraImpactEvents(
                visual,
                definition,
                timeline,
                segment,
                result);

            if (segment ==
                SkillCutsceneSegment.Return)
            {
                EnsureEvent(
                    definition,
                    timeline,
                    SkillCutsceneEventType
                        .ReturnOverview,
                    Math.Max(
                        0d,
                        timeline.fixedDuration -
                        1d /
                        definition.FrameRate),
                    "ReturnOverview");
            }

            EditorUtility.SetDirty(
                timeline);
        }

        EditorUtility.SetDirty(
            skill);

        EditorUtility.SetDirty(
            visual);

        EditorUtility.SetDirty(
            definition);

        AssetDatabase.SaveAssets();

        SkillMigrationResult validation =
            ValidateSkill(
                skill);

        result.Errors.AddRange(
            validation.Errors);

        result.Warnings.AddRange(
            validation.Warnings);

        result.VisualPath =
            AssetDatabase.GetAssetPath(
                visual);

        result.CutscenePath =
            AssetDatabase.GetAssetPath(
                definition);

        result.AnimationPath =
            resolvedAnimation != null
                ? AssetDatabase.GetAssetPath(
                    resolvedAnimation)
                : string.Empty;

        result.Converted =
            result.Errors.Count == 0;

        return result;
    }

    private static SkillVisualDefinition
        EnsureExclusiveVisual(
            SkillDefinition skill,
            SkillVisualDefinition source,
            IReadOnlyDictionary<
                SkillVisualDefinition,
                int> visualUseCount,
            SkillMigrationResult result)
    {
        bool isShared =
            source != null &&
            visualUseCount != null &&
            visualUseCount.TryGetValue(
                source,
                out int count) &&
            count > 1;

        bool mustClone =
            source == null ||
            isShared ||
            source.AllowAsProfileFallback;

        if (!mustClone)
            return source;

        string folder =
            BuildSkillCutsceneFolder(
                skill);

        EnsureFolder(
            folder);

        SkillVisualDefinition visual =
            ScriptableObject.CreateInstance<
                SkillVisualDefinition>();

        if (source != null)
        {
            EditorUtility.CopySerialized(
                source,
                visual);
        }

        visual.name =
            Sanitize(
                string.IsNullOrWhiteSpace(
                    skill.SkillName)
                    ? skill.name
                    : skill.SkillName) +
            "_Timeline_Visual";

        visual.AllowAsProfileFallback =
            false;

        visual.ApplyDamageIfNoHitFrame =
            false;

        // 공유 Cutscene을 그대로 참조하면 한 스킬의 편집이 다른 스킬을 바꾼다.
        visual.CutsceneDefinition =
            null;

        string visualPath =
            AssetDatabase
                .GenerateUniqueAssetPath(
                    $"{folder}/{visual.name}.asset");

        AssetDatabase.CreateAsset(
            visual,
            visualPath);

        if (visual.CameraDefinition != null)
        {
            visual.CameraDefinition =
                CloneCameraDefinition(
                    visual.CameraDefinition,
                    folder,
                    skill,
                    result);
        }

        skill.VisualDefinition =
            visual;

        EditorUtility.SetDirty(
            skill);

        result.CreatedExclusiveVisual =
            true;

        if (isShared)
        {
            result.Warnings.Add(
                "공유 SkillVisualDefinition을 스킬 전용 복사본으로 분리했습니다.");
        }
        else if (source != null &&
                 source.AllowAsProfileFallback)
        {
            result.Warnings.Add(
                "Profile Fallback Visual을 스킬 전용 복사본으로 분리했습니다.");
        }
        else
        {
            result.Warnings.Add(
                "SkillVisualDefinition이 없어 새 전용 Visual을 생성했습니다.");
        }

        return visual;
    }

    private static SkillCameraDefinition
        CloneCameraDefinition(
            SkillCameraDefinition source,
            string folder,
            SkillDefinition skill,
            SkillMigrationResult result)
    {
        if (source == null)
            return null;

        SkillCameraDefinition clone =
            ScriptableObject.CreateInstance<
                SkillCameraDefinition>();

        EditorUtility.CopySerialized(
            source,
            clone);

        clone.name =
            Sanitize(
                string.IsNullOrWhiteSpace(
                    skill?.SkillName)
                    ? skill?.name
                    : skill.SkillName) +
            "_Timeline_Impact";

        string path =
            AssetDatabase.GenerateUniqueAssetPath(
                $"{folder}/{clone.name}.asset");

        AssetDatabase.CreateAsset(
            clone,
            path);

        EditorUtility.SetDirty(
            clone);

        result.Warnings.Add(
            "공유 CameraDefinition을 스킬 전용 Migration/Impact 데이터로 분리했습니다.");

        return clone;
    }

    private static void ApplyActionTypeDefaults(
        SkillDefinition skill,
        SkillVisualDefinition visual,
        bool visualWasMissing)
    {
        if (skill == null || visual == null)
            return;

        visual.AllowAsProfileFallback = false;
        visual.ApplyDamageIfNoHitFrame = false;

        if (!visualWasMissing)
            return;

        bool isPreparation =
            skill.ActionType == ActionType.Preparation;

        visual.HasHitFrameDamage =
            !isPreparation;
        visual.ExpectedHitFrameCount = 1;
        visual.DistributeDamageByHitCount =
            !isPreparation;
        visual.MovesToTarget =
            !isPreparation;
        visual.ReturnPositionAfterAction =
            !isPreparation;
        visual.FaceEachOther =
            !isPreparation;
        visual.ReturnFacingAfterAction =
            !isPreparation;

        if (visual.MoveSettings != null)
        {
            visual.MoveSettings.UseMove =
                !isPreparation;
        }
    }

    private static void MigrateLegacyCameraShots(
        SkillVisualDefinition visual,
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneSegment segment,
        SkillMigrationResult result)
    {
        SkillCameraDefinition cameraDefinition =
            visual?.CameraDefinition;

        if (cameraDefinition?.Shots == null ||
            cameraDefinition.Shots.Count == 0 ||
            definition == null ||
            timeline == null)
        {
            return;
        }

        SkillCameraTimelineTrack track =
            FindTrack<SkillCameraTimelineTrack>(
                timeline,
                "Skill Camera") ??
            timeline.CreateTrack<SkillCameraTimelineTrack>(
                null,
                "Skill Camera");

        List<SkillCameraShot> applicable =
            cameraDefinition.Shots
                .Where(shot =>
                    shot != null &&
                    IsShotApplicable(
                        shot.Timing,
                        segment))
                .ToList();

        if (applicable.Count == 0)
            return;

        foreach (TimelineClip existing in
                 track.GetClips().ToArray())
        {
            timeline.DeleteClip(
                existing);
        }

        TimelineClip firstHit =
            timeline.GetOutputTracks()
                .OfType<SkillCutsceneEventTrack>()
                .SelectMany(eventTrack =>
                    eventTrack.GetClips())
                .Where(clip =>
                    clip.asset is SkillCutsceneEventClip asset &&
                    asset.EventType == SkillCutsceneEventType.Hit)
                .OrderBy(clip => clip.start)
                .FirstOrDefault();

        Dictionary<SkillCameraShotTiming, int> ordinalByTiming =
            new Dictionary<SkillCameraShotTiming, int>();

        foreach (SkillCameraShot shot in applicable)
        {
            ordinalByTiming.TryGetValue(
                shot.Timing,
                out int ordinal);

            ordinalByTiming[shot.Timing] =
                ordinal + 1;

            double start =
                ResolveLegacyShotStart(
                    shot,
                    timeline.fixedDuration,
                    definition.FrameRate,
                    firstHit?.start,
                    ordinal);

            TimelineClip created =
                SkillCutsceneAssetBuilder.AddCameraClip(
                    definition,
                    timeline,
                    start,
                    ResolveCameraKey(
                        shot.ShotType));

            if (created == null)
                continue;

            created.duration =
                Math.Max(
                    1d / definition.FrameRate,
                    shot.Duration > 0f
                        ? shot.Duration
                        : 0.35f);

            created.displayName =
                $"{shot.Timing} — {shot.ShotType}";

            if (created.asset is
                SkillCameraTimelineClip asset)
            {
                ConfigureCameraClipFromLegacyShot(
                    asset,
                    shot,
                    definition.FrameRate);

                EditorUtility.SetDirty(
                    asset);
            }

            result.MigratedCameraShotCount++;
        }

        if (!track.GetClips().Any())
        {
            SkillCutsceneAssetBuilder.AddCameraClip(
                definition,
                timeline,
                0d,
                "CM_Overview");
        }

        EditorUtility.SetDirty(
            timeline);
    }

    private static bool IsShotApplicable(
        SkillCameraShotTiming timing,
        SkillCutsceneSegment segment)
    {
        if (segment != SkillCutsceneSegment.Action &&
            segment != SkillCutsceneSegment.ClashAttack)
        {
            return false;
        }

        if (timing == SkillCameraShotTiming.OnClashRoll)
        {
            return segment ==
                SkillCutsceneSegment.ClashAttack;
        }

        return true;
    }

    private static double ResolveLegacyShotStart(
        SkillCameraShot shot,
        double duration,
        double frameRate,
        double? firstHitTime,
        int ordinal)
    {
        double safeDuration =
            Math.Max(
                1d / frameRate,
                duration);

        double baseTime =
            shot.Timing switch
            {
                SkillCameraShotTiming.OnActionStart => 0d,
                SkillCameraShotTiming.OnClashRoll => 0d,
                SkillCameraShotTiming.BeforeAttackAnimation =>
                    Math.Min(
                        safeDuration,
                        3d / frameRate),
                SkillCameraShotTiming.OnHitFrame =>
                    firstHitTime ?? safeDuration * 0.62d,
                SkillCameraShotTiming.AfterAction =>
                    Math.Max(
                        0d,
                        safeDuration -
                        Math.Max(
                            shot.Duration,
                            1f / (float)frameRate)),
                _ => 0d
            };

        return Math.Max(
            0d,
            baseTime +
            ordinal *
            Math.Max(
                1d / frameRate,
                shot.BlendWaitTime));
    }

    private static string ResolveCameraKey(
        SkillCameraShotType shotType)
    {
        return shotType switch
        {
            SkillCameraShotType.HitImpact => "CM_Impact",
            SkillCameraShotType.TargetClose => "CM_Impact",
            SkillCameraShotType.AttackerClose => "CM_Follow",
            SkillCameraShotType.AttackerOverShoulder => "CM_Follow",
            SkillCameraShotType.TargetOverShoulder => "CM_Follow",
            SkillCameraShotType.ReturnOverview => "CM_Overview",
            SkillCameraShotType.ClashWide => "CM_Overview",
            _ => "CM_Overview"
        };
    }

    private static void ConfigureCameraClipFromLegacyShot(
        SkillCameraTimelineClip clip,
        SkillCameraShot shot,
        double frameRate)
    {
        if (clip == null || shot == null)
            return;

        clip.CameraKey =
            ResolveCameraKey(
                shot.ShotType);

        clip.CapturedPosition =
            shot.PositionOffset;

        clip.AimOffset =
            shot.LookAtOffset;

        clip.FallbackBlendStyle =
            shot.BlendStyle;

        clip.FallbackBlendFrames =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    shot.BlendTime *
                    (float)frameRate));

        switch (shot.ShotType)
        {
            case SkillCameraShotType.AttackerClose:
                clip.PositionBinding =
                    SkillCameraPositionBinding.AttackerVisualRoot;
                clip.AimBinding =
                    SkillCameraAimBinding.AttackerVisualRoot;
                break;

            case SkillCameraShotType.TargetClose:
                clip.PositionBinding =
                    SkillCameraPositionBinding.TargetVisualRoot;
                clip.AimBinding =
                    SkillCameraAimBinding.TargetVisualRoot;
                break;

            case SkillCameraShotType.AttackerOverShoulder:
                clip.PositionBinding =
                    SkillCameraPositionBinding.AttackerVisualRoot;
                clip.AimBinding =
                    SkillCameraAimBinding.TargetVisualRoot;
                break;

            case SkillCameraShotType.TargetOverShoulder:
                clip.PositionBinding =
                    SkillCameraPositionBinding.TargetVisualRoot;
                clip.AimBinding =
                    SkillCameraAimBinding.AttackerVisualRoot;
                break;

            case SkillCameraShotType.HitImpact:
                clip.PositionBinding =
                    SkillCameraPositionBinding.CombatFrameFollow;
                clip.AimBinding =
                    SkillCameraAimBinding.TargetVisualRoot;
                break;

            case SkillCameraShotType.FocusBetween:
            case SkillCameraShotType.ClashWide:
            case SkillCameraShotType.ReturnOverview:
            default:
                clip.PositionBinding =
                    SkillCameraPositionBinding.RigRootFixed;
                clip.AimBinding =
                    SkillCameraAimBinding.AttackerTargetMidpoint;
                break;
        }
    }

    private static void EnsureCameraImpactEvents(
        SkillVisualDefinition visual,
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneSegment segment,
        SkillMigrationResult result)
    {
        if (visual?.CameraDefinition?.ImpactPulses == null ||
            definition == null ||
            timeline == null)
        {
            return;
        }

        foreach (SkillCameraImpactPulse pulse in
                 visual.CameraDefinition.ImpactPulses)
        {
            if (pulse == null || !pulse.Enabled)
                continue;

            if (pulse.Timing ==
                SkillCameraImpactTiming.OnHitFrame)
            {
                // Hit별 Event는 NormalizeHitEvents에서 생성한다.
                continue;
            }

            if (pulse.Timing ==
                SkillCameraImpactTiming.OnClashFinalResult)
            {
                string warning =
                    "Legacy OnClashFinalResult Pulse는 개별 Skill Timeline으로 " +
                    "정확히 표현할 수 없어 자동 배치하지 않았습니다. " +
                    "합 전체 결과 UI 연출로 별도 설계하세요.";

                if (!result.Warnings.Contains(warning))
                    result.Warnings.Add(warning);

                continue;
            }

            if (!TryGetImpactPlacement(
                    pulse.Timing,
                    segment,
                    timeline.fixedDuration,
                    definition.FrameRate,
                    out double time))
            {
                continue;
            }

            SkillCutsceneEventTrack track =
                FindTrack<SkillCutsceneEventTrack>(
                    timeline,
                    "Battle Events") ??
                timeline.CreateTrack<SkillCutsceneEventTrack>(
                    null,
                    "Battle Events");

            bool exists =
                track.GetClips().Any(clip =>
                    clip.asset is SkillCutsceneEventClip asset &&
                    asset.EventType ==
                        SkillCutsceneEventType.CameraImpactPulse &&
                    asset.CameraImpactTiming == pulse.Timing);

            if (exists)
                continue;

            TimelineClip created =
                EnsureEvent(
                    definition,
                    timeline,
                    SkillCutsceneEventType.CameraImpactPulse,
                    time,
                    "CameraImpact — " + pulse.Timing,
                    allowDuplicateType: true);

            if (created?.asset is
                SkillCutsceneEventClip asset)
            {
                asset.CameraImpactTiming =
                    pulse.Timing;
                asset.HitIndex =
                    pulse.UseHitIndexFilter
                        ? pulse.HitIndex
                        : -1;

                EditorUtility.SetDirty(
                    asset);
            }

            if (created != null)
                result.CreatedCameraImpactEventCount++;
        }
    }

    private static bool TryGetImpactPlacement(
        SkillCameraImpactTiming timing,
        SkillCutsceneSegment segment,
        double duration,
        double frameRate,
        out double time)
    {
        double safeDuration =
            Math.Max(
                1d / frameRate,
                duration);

        time = 0d;

        switch (timing)
        {
            case SkillCameraImpactTiming.OnActionStart:
                if (segment == SkillCutsceneSegment.Action ||
                    segment == SkillCutsceneSegment.ClashAttack)
                {
                    return true;
                }
                break;

            case SkillCameraImpactTiming.OnClashRoll:
                if (segment == SkillCutsceneSegment.ClashAttack)
                {
                    return true;
                }
                break;

            case SkillCameraImpactTiming.AfterAction:
                if (segment == SkillCutsceneSegment.Action ||
                    segment == SkillCutsceneSegment.ClashAttack)
                {
                    time = Math.Max(
                        0d,
                        safeDuration -
                        1d / frameRate);
                    return true;
                }
                break;
        }

        return false;
    }

    private static void EnsureAttackerAnimation(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        AnimationClip animation,
        SkillMigrationResult result)
    {
        if (definition == null ||
            timeline == null)
        {
            return;
        }

        AnimationTrack track =
            FindTrack<AnimationTrack>(
                timeline,
                SkillCutsceneTimelineBinder
                    .AttackerAnimationTrackName) ??
            timeline.CreateTrack<
                AnimationTrack>(
                    null,
                    SkillCutsceneTimelineBinder
                        .AttackerAnimationTrackName);

        TimelineClip[] clips =
            track.GetClips()
                .ToArray();

        if (clips.Length > 0)
        {
            timeline.fixedDuration =
                Math.Max(
                    timeline.fixedDuration,
                    clips.Max(
                        clip =>
                            clip.end));
            return;
        }

        if (animation == null)
            return;

        TimelineClip created =
            track.CreateClip(
                animation);

        created.start =
            0d;

        created.duration =
            Math.Max(
                1d /
                definition.FrameRate,
                animation.length);

        created.displayName =
            animation.name;

        timeline.fixedDuration =
            Math.Max(
                timeline.fixedDuration,
                created.end);

        result.CreatedAnimationClipCount++;
    }


    private static void NormalizeDynamicTargetTrack(
        TimelineAsset timeline,
        SkillMigrationResult result)
    {
        if (timeline == null)
            return;

        AnimationTrack targetTrack =
            FindTrack<AnimationTrack>(
                timeline,
                SkillCutsceneTimelineBinder
                    .TargetAnimationTrackName);

        if (targetTrack == null)
            return;

        // 공격 Timeline은 어떤 캐릭터가 맞을지 미리 알 수 없다.
        // 특정 EliteEnemy/Olaf Hit Clip을 고정하면 다른 타깃에서 Avatar와 모션이 틀어지고,
        // Timeline 출력이 타깃의 상태 Animator를 덮는다.
        foreach (TimelineClip clip in
                 targetTrack.GetClips()
                    .ToArray())
        {
            timeline.DeleteClip(
                clip);
            result.RemovedFixedTargetAnimationClipCount++;
        }

        EditorUtility.SetDirty(
            targetTrack);
        EditorUtility.SetDirty(
            timeline);
    }

    private static void NormalizeHitEvents(
        SkillDefinition skill,
        SkillVisualDefinition visual,
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillMigrationResult result)
    {
        SkillCutsceneEventTrack eventTrack =
            FindTrack<SkillCutsceneEventTrack>(
                timeline,
                "Battle Events") ??
            timeline.CreateTrack<
                SkillCutsceneEventTrack>(
                    null,
                    "Battle Events");

        List<TimelineClip> hits =
            eventTrack.GetClips()
                .Where(
                    clip =>
                        clip.asset is
                            SkillCutsceneEventClip asset &&
                        asset.EventType ==
                            SkillCutsceneEventType.Hit)
                .OrderBy(
                    clip => clip.start)
                .ToList();

        if (!visual.HasHitFrameDamage)
        {
            foreach (TimelineClip hit in hits)
            {
                timeline.DeleteClip(hit);
                result.RemovedDuplicateEventCount++;
            }

            result.RemovedDuplicateEventCount +=
                SkillTimelineHitTimingUtility
                    .RemoveRedundantHitSideEffects(
                        timeline,
                        Array.Empty<TimelineClip>(),
                        definition.FrameRate);

            result.HitTimingSource =
                "No damage";

            EditorUtility.SetDirty(timeline);
            return;
        }

        int expected =
            Mathf.Max(
                1,
                visual.ExpectedHitFrameCount);

        bool hasResolvedTiming =
            SkillTimelineHitTimingUtility
                .TryResolveTimelineHitTimes(
                    timeline,
                    expected,
                    definition.FrameRate,
                    out List<double> resolvedTimes,
                    out string timingSource,
                    out string timingError);

        if (hasResolvedTiming)
        {
            while (hits.Count < expected)
            {
                int index =
                    hits.Count;

                TimelineClip created =
                    SkillCutsceneAssetBuilder
                        .AddEventClip(
                            definition,
                            timeline,
                            resolvedTimes[index],
                            SkillCutsceneEventType.Hit);

                if (created == null)
                    break;

                hits.Add(created);
                result.CreatedHitEventCount++;
            }

            while (hits.Count > expected)
            {
                TimelineClip extra =
                    hits[hits.Count - 1];

                timeline.DeleteClip(extra);
                hits.RemoveAt(hits.Count - 1);
                result.RemovedDuplicateEventCount++;
            }

            hits =
                hits.OrderBy(clip => clip.start)
                    .ToList();

            double tolerance =
                0.25d /
                Math.Max(
                    1d,
                    definition.FrameRate);

            for (int index = 0;
                 index < hits.Count &&
                 index < resolvedTimes.Count;
                 index++)
            {
                TimelineClip hit =
                    hits[index];

                if (Math.Abs(
                        hit.start -
                        resolvedTimes[index]) >
                    tolerance)
                {
                    hit.start =
                        resolvedTimes[index];
                    result.RetimedHitEventCount++;
                }

                hit.duration =
                    1d /
                    definition.FrameRate;

                hit.displayName =
                    $"Hit {index}";

                if (hit.asset is
                    SkillCutsceneEventClip hitAsset)
                {
                    hitAsset.HitIndex =
                        index;
                    EditorUtility.SetDirty(
                        hitAsset);
                }
            }

            result.HitTimingSource =
                timingSource;
        }
        else
        {
            // 기존에 수동 배치된 Hit이 충분하면 그 위치는 보존한다.
            // 단, 추정 위치를 새로 만들지는 않는다.
            if (hits.Count < expected)
            {
                result.Errors.Add(
                    $"Hit 타이밍 동기화 실패: {timingError}");
            }
            else
            {
                result.Warnings.Add(
                    $"Hit 타이밍 자동 검증 불가 — 기존 Timeline 위치를 보존합니다. " +
                    timingError);

                for (int index = 0;
                     index < hits.Count;
                     index++)
                {
                    TimelineClip hit =
                        hits[index];

                    hit.duration =
                        1d /
                        definition.FrameRate;

                    hit.displayName =
                        $"Hit {index}";

                    if (hit.asset is
                        SkillCutsceneEventClip hitAsset)
                    {
                        hitAsset.HitIndex =
                            index;
                        EditorUtility.SetDirty(
                            hitAsset);
                    }
                }
            }

            result.HitTimingSource =
                "Existing Timeline (unverified)";
        }

        hits =
            eventTrack.GetClips()
                .Where(
                    clip =>
                        clip.asset is
                            SkillCutsceneEventClip asset &&
                        asset.EventType ==
                            SkillCutsceneEventType.Hit)
                .OrderBy(
                    clip => clip.start)
                .ToList();

        result.RemovedDuplicateEventCount +=
            SkillTimelineHitTimingUtility
                .RemoveRedundantHitSideEffects(
                    timeline,
                    hits,
                    definition.FrameRate);

        EditorUtility.SetDirty(timeline);
    }

    private static void EnsureVfxEvents(
        SkillVisualDefinition visual,
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneSegment segment,
        SkillMigrationResult result)
    {
        if (visual?.VfxCues == null ||
            visual.VfxCues.Count == 0 ||
            definition == null ||
            timeline == null)
        {
            return;
        }

        HashSet<BattleVfxTiming> timings =
            new HashSet<BattleVfxTiming>(
                visual.VfxCues
                    .Where(
                        cue =>
                            cue != null)
                    .Select(
                        cue =>
                            cue.Timing));

        foreach (BattleVfxTiming timing in
                 timings)
        {
            if (timing ==
                    BattleVfxTiming.OnHitFrame ||
                timing ==
                    BattleVfxTiming.OnCritical ||
                IsStatusOnlyTiming(
                    timing))
            {
                continue;
            }

            if (!TryGetVfxPlacement(
                    segment,
                    timing,
                    timeline.fixedDuration,
                    definition.FrameRate,
                    out double time))
            {
                continue;
            }

            TimelineClip clip =
                EnsureVfxEvent(
                    definition,
                    timeline,
                    timing,
                    time);

            if (clip != null)
                result.CreatedVfxEventCount++;
        }
    }

    private static bool TryGetVfxPlacement(
        SkillCutsceneSegment segment,
        BattleVfxTiming timing,
        double duration,
        double frameRate,
        out double time)
    {
        double safeDuration =
            Math.Max(
                1d / frameRate,
                duration);

        time =
            0d;

        switch (segment)
        {
            case SkillCutsceneSegment.Action:
            case SkillCutsceneSegment.ClashAttack:
                switch (timing)
                {
                    case BattleVfxTiming
                        .OnActionStart:
                        time = 0d;
                        return true;

                    case BattleVfxTiming
                        .OnClashRoll:
                        if (segment !=
                            SkillCutsceneSegment
                                .ClashAttack)
                        {
                            return false;
                        }

                        time = 0d;
                        return true;

                    case BattleVfxTiming
                        .BeforeAttackAnimation:
                        time =
                            Math.Min(
                                safeDuration,
                                1d / frameRate);
                        return true;

                    case BattleVfxTiming
                        .OnEffectFrame:
                        time =
                            safeDuration * 0.5d;
                        return true;

                    case BattleVfxTiming
                        .AfterAction:
                        time =
                            Math.Max(
                                0d,
                                safeDuration -
                                1d / frameRate);
                        return true;
                }
                break;

            case SkillCutsceneSegment.PartBreak:
                if (timing ==
                        BattleVfxTiming
                            .OnPartBroken)
                {
                    time =
                        safeDuration * 0.4d;
                    return true;
                }
                break;

            case SkillCutsceneSegment.Kill:
                if (timing ==
                        BattleVfxTiming.OnKill ||
                    timing ==
                        BattleVfxTiming.OnDeath)
                {
                    time =
                        safeDuration * 0.4d;
                    return true;
                }
                break;
        }

        return false;
    }

    private static bool IsStatusOnlyTiming(
        BattleVfxTiming timing)
    {
        return timing ==
                   BattleVfxTiming
                       .OnStatusApplied ||
               timing ==
                   BattleVfxTiming
                       .OnStatusRefreshed ||
               timing ==
                   BattleVfxTiming
                       .OnStatusStacked ||
               timing ==
                   BattleVfxTiming
                       .OnStatusTickDamage ||
               timing ==
                   BattleVfxTiming
                       .OnStatusRemoved ||
               timing ==
                   BattleVfxTiming
                       .OnStatusExpired ||
               timing ==
                   BattleVfxTiming
                       .OnPartWeakened ||
               timing ==
                   BattleVfxTiming
                       .OnPartRecovered;
    }

    private static TimelineClip EnsureVfxEvent(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        BattleVfxTiming timing,
        double startTime)
    {
        SkillCutsceneEventTrack track =
            FindTrack<SkillCutsceneEventTrack>(
                timeline,
                "Battle Events") ??
            timeline.CreateTrack<
                SkillCutsceneEventTrack>(
                    null,
                    "Battle Events");

        TimelineClip existing =
            track.GetClips()
                .FirstOrDefault(
                    clip =>
                        clip.asset is
                            SkillCutsceneEventClip asset &&
                        asset.EventType ==
                            SkillCutsceneEventType.Vfx &&
                        asset.VfxTiming ==
                            timing);

        if (existing != null)
            return null;

        TimelineClip created =
            SkillCutsceneAssetBuilder
                .AddEventClip(
                    definition,
                    timeline,
                    Math.Max(
                        0d,
                        startTime),
                    SkillCutsceneEventType.Vfx);

        if (created?.asset is
            SkillCutsceneEventClip createdAsset)
        {
            createdAsset.VfxTiming =
                timing;

            created.displayName =
                "VFX — " + timing;

            EditorUtility.SetDirty(
                createdAsset);
        }

        return created;
    }

    private static TimelineClip EnsureEventNear(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneEventType type,
        double startTime,
        string displayName)
    {
        double tolerance =
            0.5d /
            definition.FrameRate;

        SkillCutsceneEventTrack track =
            FindTrack<SkillCutsceneEventTrack>(
                timeline,
                "Battle Events") ??
            timeline.CreateTrack<
                SkillCutsceneEventTrack>(
                    null,
                    "Battle Events");

        TimelineClip existing =
            track.GetClips()
                .FirstOrDefault(
                    clip =>
                        clip.asset is
                            SkillCutsceneEventClip asset &&
                        asset.EventType ==
                            type &&
                        Math.Abs(
                            clip.start -
                            startTime) <=
                        tolerance);

        if (existing != null)
            return existing;

        return EnsureEvent(
            definition,
            timeline,
            type,
            startTime,
            displayName,
            allowDuplicateType: true);
    }

    private static TimelineClip EnsureEvent(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneEventType type,
        double startTime,
        string displayName,
        bool allowDuplicateType = false)
    {
        SkillCutsceneEventTrack track =
            FindTrack<SkillCutsceneEventTrack>(
                timeline,
                "Battle Events") ??
            timeline.CreateTrack<
                SkillCutsceneEventTrack>(
                    null,
                    "Battle Events");

        if (!allowDuplicateType)
        {
            TimelineClip existing =
                track.GetClips()
                    .FirstOrDefault(
                        clip =>
                            clip.asset is
                                SkillCutsceneEventClip asset &&
                            asset.EventType ==
                                type);

            if (existing != null)
                return existing;
        }

        TimelineClip created =
            SkillCutsceneAssetBuilder
                .AddEventClip(
                    definition,
                    timeline,
                    Math.Max(
                        0d,
                        startTime),
                    type);

        if (created != null)
        {
            created.displayName =
                displayName;
        }

        return created;
    }

    private static AnimationClip ResolveAttackerAnimation(
        SkillDefinition skill,
        AnimationClip current,
        SkillMigrationResult result)
    {
        if (current != null)
            return current;

        string skillPath =
            AssetDatabase.GetAssetPath(
                skill);

        string characterRoot =
            ResolveCharacterRoot(
                skillPath);

        if (string.IsNullOrWhiteSpace(
                characterRoot) ||
            !AssetDatabase.IsValidFolder(
                characterRoot))
        {
            return null;
        }

        List<AnimationClipCandidate> candidates =
            new List<AnimationClipCandidate>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:AnimationClip",
                new[]
                {
                    characterRoot
                });

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            foreach (AnimationClip clip in
                     AssetDatabase.LoadAllAssetsAtPath(
                             path)
                         .OfType<AnimationClip>())
            {
                if (clip == null ||
                    clip.name.StartsWith(
                        "__preview__",
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    continue;
                }

                int score =
                    ScoreAnimationClip(
                        clip,
                        path,
                        skill.ActionType);

                if (score <= 0)
                    continue;

                candidates.Add(
                    new AnimationClipCandidate(
                        clip,
                        path,
                        score));
            }
        }

        AnimationClipCandidate best =
            candidates
                .OrderByDescending(
                    candidate =>
                        candidate.Score)
                .ThenBy(
                    candidate =>
                        candidate.Path,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ThenBy(
                    candidate =>
                        candidate.Clip.name,
                    StringComparer
                        .OrdinalIgnoreCase)
                .FirstOrDefault();

        if (best == null)
            return null;

        if (candidates.Count > 1 &&
            candidates
                .OrderByDescending(
                    candidate =>
                        candidate.Score)
                .Skip(1)
                .Any(
                    candidate =>
                        candidate.Score ==
                        best.Score))
        {
            result.Warnings.Add(
                "동일 점수 AnimationClip 후보가 여러 개라 경로 순으로 선택했습니다: " +
                best.Clip.name);
        }

        return best.Clip;
    }

    private static int ScoreAnimationClip(
        AnimationClip clip,
        string path,
        ActionType actionType)
    {
        if (clip == null)
            return 0;

        string value =
            (clip.name + " " + path)
                .ToLowerInvariant();

        string[] excluded =
        {
            "idle",
            "hit",
            "death",
            "dead",
            "broken",
            "weak",
            "피격",
            "사망",
            "camera",
            "motion"
        };

        if (excluded.Any(
                token =>
                    value.Contains(token)))
        {
            return 0;
        }

        string[] preferred =
            actionType switch
            {
                ActionType.NormalAttack =>
                    new[]
                    {
                        "normalattack",
                        "normal attack",
                        "attack",
                        "공격",
                        "참격",
                        "bite",
                        "물어"
                    },

                ActionType.Duel =>
                    new[]
                    {
                        "duel",
                        "clash",
                        "결투",
                        "압박"
                    },

                ActionType.Preparation =>
                    new[]
                    {
                        "preparation",
                        "prepare",
                        "도사림",
                        "태세"
                    },

                ActionType.Prestige =>
                    new[]
                    {
                        "prestige",
                        "ultimate",
                        "위세",
                        "처형",
                        "광란",
                        "포식"
                    },

                _ => Array.Empty<string>()
            };

        int score =
            0;

        for (int index = 0;
             index < preferred.Length;
             index++)
        {
            if (!value.Contains(
                    preferred[index]))
            {
                continue;
            }

            score +=
                100 - index * 5;
        }

        if (path.EndsWith(
                ".anim",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            score += 10;
        }

        return score;
    }

    private static SkillMigrationResult ValidateSkill(
        SkillDefinition skill)
    {
        SkillMigrationResult result =
            new SkillMigrationResult(
                skill);

        if (skill == null)
        {
            result.Errors.Add(
                "SkillDefinition 참조가 NULL입니다.");
            return result;
        }

        SkillVisualDefinition visual =
            skill.VisualDefinition;

        if (visual == null)
        {
            result.Errors.Add(
                "SkillVisualDefinition이 없습니다.");
            return result;
        }

        SkillCutsceneDefinition definition =
            visual.CutsceneDefinition;

        if (definition == null)
        {
            result.Errors.Add(
                "SkillCutsceneDefinition이 없습니다.");
            return result;
        }

        if (!definition.HasCompleteTimelineSet)
        {
            result.Errors.Add(
                "완전한 5-Segment Timeline 세트가 아닙니다. Missing=" +
                string.Join(
                    ", ",
                    definition.GetMissingRequirements()));
        }

        foreach (SkillCutsceneSegment segment in
                 (SkillCutsceneSegment[])
                 Enum.GetValues(
                     typeof(
                         SkillCutsceneSegment)))
        {
            TimelineAsset timeline =
                definition.GetTimeline(
                    segment);

            if (timeline == null)
                continue;

            ValidateTimeline(
                skill,
                visual,
                definition,
                timeline,
                segment,
                result);
        }

        bool runtimeValid =
            BattleVisualValidator
                .ValidateDefinition(
                    visual,
                    actor: null,
                    logWarnings: false);

        if (!runtimeValid &&
            result.Errors.Count == 0)
        {
            result.Errors.Add(
                "BattleVisualValidator 검증에 실패했습니다.");
        }

        result.VisualPath =
            AssetDatabase.GetAssetPath(
                visual);

        result.CutscenePath =
            AssetDatabase.GetAssetPath(
                definition);

        result.AnimationPath =
            definition.AttackerAnimation != null
                ? AssetDatabase.GetAssetPath(
                    definition.AttackerAnimation)
                : string.Empty;

        result.Converted =
            result.Errors.Count == 0;

        return result;
    }

    private static void ValidateTimeline(
        SkillDefinition skill,
        SkillVisualDefinition visual,
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneSegment segment,
        SkillMigrationResult result)
    {
        TrackAsset[] tracks =
            timeline.GetOutputTracks()
                .ToArray();

        bool hasCamera =
            tracks.OfType<
                    SkillCameraTimelineTrack>()
                .SelectMany(
                    track =>
                        track.GetClips())
                .Any();

        if (!hasCamera)
        {
            result.Errors.Add(
                $"{segment}: Camera Clip이 없습니다.");
        }

        bool attackSegment =
            segment ==
                SkillCutsceneSegment.Action ||
            segment ==
                SkillCutsceneSegment.ClashAttack;

        if (attackSegment)
        {
            bool hasAnimation =
                tracks.OfType<AnimationTrack>()
                    .Where(
                        track =>
                            string.Equals(
                                track.name,
                                SkillCutsceneTimelineBinder
                                    .AttackerAnimationTrackName,
                                StringComparison
                                    .OrdinalIgnoreCase))
                    .SelectMany(
                        track =>
                            track.GetClips())
                    .Any();

            if (!hasAnimation)
            {
                result.Errors.Add(
                    $"{segment}: Attacker Animation Clip이 없습니다.");
            }

            int hitCount =
                CountEvents(
                    timeline,
                    SkillCutsceneEventType.Hit);

            int expected =
                visual.HasHitFrameDamage
                    ? Mathf.Max(
                        1,
                        visual.ExpectedHitFrameCount)
                    : 0;

            if (hitCount != expected)
            {
                result.Errors.Add(
                    $"{segment}: Hit Event 수 불일치. Expected={expected}, Actual={hitCount}");
            }

            if (!visual.HasHitFrameDamage &&
                hitCount > 0)
            {
                result.Errors.Add(
                    $"{segment}: 피해 없는 스킬에 Hit Event가 있습니다.");
            }

            AnimationTrack targetTrack =
                tracks.OfType<AnimationTrack>()
                    .FirstOrDefault(
                        track =>
                            string.Equals(
                                track.name,
                                SkillCutsceneTimelineBinder
                                    .TargetAnimationTrackName,
                                StringComparison
                                    .OrdinalIgnoreCase));

            if (targetTrack != null &&
                targetTrack.GetClips().Any())
            {
                result.Errors.Add(
                    $"{segment}: 공격 Timeline에 고정 Target Animation Clip이 있습니다.");
            }
        }

        if (segment ==
                SkillCutsceneSegment.Return &&
            CountEvents(
                timeline,
                SkillCutsceneEventType
                    .ReturnOverview) == 0)
        {
            result.Errors.Add(
                "Return: ReturnOverview Event가 없습니다.");
        }
    }

    private static int CountEvents(
        TimelineAsset timeline,
        SkillCutsceneEventType type)
    {
        if (timeline == null)
            return 0;

        return timeline.GetOutputTracks()
            .OfType<
                SkillCutsceneEventTrack>()
            .SelectMany(
                track =>
                    track.GetClips())
            .Count(
                clip =>
                    clip.asset is
                        SkillCutsceneEventClip asset &&
                    asset.EventType ==
                        type);
    }

    private static LegacyAnimationEventScan
        ScanLegacyAnimationEvents(
            bool remove)
    {
        LegacyAnimationEventScan result =
            new LegacyAnimationEventScan();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:AnimationClip",
                new[]
                {
                    DataRoot
                });

        HashSet<string> visitedPaths =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (!visitedPaths.Add(path))
                continue;

            if (!path.EndsWith(
                    ".anim",
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (AnimationClip clip in
                     AssetDatabase.LoadAllAssetsAtPath(
                             path)
                         .OfType<AnimationClip>())
            {
                result.ScannedClipCount++;

                AnimationEvent[] events =
                    AnimationUtility
                        .GetAnimationEvents(
                            clip);

                AnimationEvent[] kept =
                    events.Where(
                            animationEvent =>
                                animationEvent == null ||
                                !RemovedAnimationEventFunctions
                                    .Contains(
                                        animationEvent
                                            .functionName))
                        .ToArray();

                int removedCount =
                    events.Length -
                    kept.Length;

                if (removedCount <= 0)
                    continue;

                result.FoundEventCount +=
                    removedCount;

                result.AffectedClips.Add(
                    $"{path} :: {clip.name} ({removedCount})");

                if (!remove)
                    continue;

                try
                {
                    AnimationUtility
                        .SetAnimationEvents(
                            clip,
                            kept);

                    EditorUtility.SetDirty(
                        clip);

                    result.RemovedEventCount +=
                        removedCount;
                }
                catch (Exception exception)
                {
                    result.Errors.Add(
                        $"{path} :: {clip.name} — {exception.Message}");
                }
            }
        }

        return result;
    }

    private static RelayScanResult
        ScanAnimationEventRelays(
            bool remove)
    {
        RelayScanResult result =
            new RelayScanResult();

        string[] prefabGuids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[]
                {
                    DataRoot
                });

        foreach (string guid in
                 prefabGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            GameObject root =
                null;

            try
            {
                root =
                    PrefabUtility
                        .LoadPrefabContents(
                            path);

                List<MonoBehaviour> relays =
                    FindRelays(
                        root);

                if (relays.Count == 0)
                    continue;

                result.FoundRelayCount +=
                    relays.Count;

                result.AffectedPrefabs.Add(
                    $"{path} ({relays.Count})");

                if (!remove)
                    continue;

                foreach (MonoBehaviour relay in
                         relays)
                {
                    if (relay != null)
                    {
                        Object.DestroyImmediate(
                            relay);
                    }
                }

                PrefabUtility
                    .SaveAsPrefabAsset(
                        root,
                        path);

                result.RemovedRelayCount +=
                    relays.Count;

                result.ModifiedPrefabCount++;
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    $"{path} — {exception.Message}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility
                        .UnloadPrefabContents(
                            root);
                }
            }
        }

        for (int sceneIndex = 0;
             sceneIndex < SceneManager.sceneCount;
             sceneIndex++)
        {
            Scene scene =
                SceneManager.GetSceneAt(
                    sceneIndex);

            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                continue;
            }

            List<MonoBehaviour> relays =
                scene.GetRootGameObjects()
                    .SelectMany(
                        FindRelays)
                    .Where(
                        relay =>
                            relay != null)
                    .Distinct()
                    .ToList();

            if (relays.Count == 0)
                continue;

            result.FoundRelayCount +=
                relays.Count;

            result.AffectedScenes.Add(
                $"{scene.path} ({relays.Count})");

            if (!remove)
                continue;

            foreach (MonoBehaviour relay in
                     relays)
            {
                if (relay != null)
                {
                    Object.DestroyImmediate(
                        relay);
                }
            }

            EditorSceneManager
                .MarkSceneDirty(
                    scene);

            result.RemovedRelayCount +=
                relays.Count;

            result.ModifiedSceneCount++;
        }

        return result;
    }

    private static List<MonoBehaviour> FindRelays(
        GameObject root)
    {
        if (root == null)
            return new List<MonoBehaviour>();

        return root
            .GetComponentsInChildren<
                MonoBehaviour>(
                    true)
            .Where(
                component =>
                    component != null &&
                    string.Equals(
                        component.GetType().Name,
                        "AnimationEventRelay",
                        StringComparison
                            .Ordinal))
            .ToList();
    }

    private static Dictionary<
        SkillVisualDefinition,
        int> BuildVisualUseCount(
            IEnumerable<SkillDefinition> skills)
    {
        Dictionary<
            SkillVisualDefinition,
            int> result =
                new Dictionary<
                    SkillVisualDefinition,
                    int>();

        foreach (SkillDefinition skill in
                 skills ??
                 Enumerable.Empty<
                     SkillDefinition>())
        {
            SkillVisualDefinition visual =
                skill?.VisualDefinition;

            if (visual == null)
                continue;

            result.TryGetValue(
                visual,
                out int count);

            result[visual] =
                count + 1;
        }

        return result;
    }

    private static List<SkillDefinition>
        LoadAllSkills()
    {
        return AssetDatabase
            .FindAssets(
                "t:SkillDefinition",
                new[]
                {
                    DataRoot
                })
            .Select(
                AssetDatabase
                    .GUIDToAssetPath)
            .Select(
                path =>
                    AssetDatabase
                        .LoadAssetAtPath<
                            SkillDefinition>(
                                path))
            .Where(
                skill =>
                    skill != null)
            .Distinct()
            .OrderBy(
                skill =>
                    AssetDatabase
                        .GetAssetPath(
                            skill),
                StringComparer
                    .OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveCharacterRoot(
        string skillPath)
    {
        if (string.IsNullOrWhiteSpace(
                skillPath))
        {
            return null;
        }

        int skillsIndex =
            skillPath.IndexOf(
                "/Skills/",
                StringComparison
                    .OrdinalIgnoreCase);

        if (skillsIndex > 0)
        {
            return skillPath.Substring(
                0,
                skillsIndex);
        }

        string directory =
            Path.GetDirectoryName(
                    skillPath)
                ?.Replace(
                    '\\',
                    '/');

        return directory;
    }

    private static string BuildSkillCutsceneFolder(
        SkillDefinition skill)
    {
        string skillPath =
            AssetDatabase.GetAssetPath(
                skill);

        string parent =
            Path.GetDirectoryName(
                    skillPath)
                ?.Replace(
                    '\\',
                    '/');

        if (string.IsNullOrWhiteSpace(
                parent))
        {
            parent =
                DataRoot;
        }

        return $"{parent}/Cutscenes/" +
               Sanitize(
                   skill?.name);
    }

    private static T FindTrack<T>(
        TimelineAsset timeline,
        string trackName)
        where T : TrackAsset
    {
        if (timeline == null)
            return null;

        return timeline.GetOutputTracks()
            .OfType<T>()
            .FirstOrDefault(
                track =>
                    string.Equals(
                        track.name,
                        trackName,
                        StringComparison
                            .OrdinalIgnoreCase));
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(
                path))
        {
            return;
        }

        string[] parts =
            path.Split(
                '/',
                StringSplitOptions
                    .RemoveEmptyEntries);

        if (parts.Length == 0)
            return;

        string current =
            parts[0];

        for (int index = 1;
             index < parts.Length;
             index++)
        {
            string next =
                current + "/" +
                parts[index];

            if (!AssetDatabase.IsValidFolder(
                    next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[index]);
            }

            current =
                next;
        }
    }

    private static string Sanitize(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "UnnamedSkill";
        }

        char[] invalid =
            Path.GetInvalidFileNameChars();

        return new string(
            value.Select(
                    character =>
                        invalid.Contains(
                            character) ||
                        character == '/' ||
                        character == '\\'
                            ? '_'
                            : character)
                .ToArray());
    }

    private static void WriteReport(
        MigrationReport report)
    {
        EnsureFolder(
            ReportFolder);

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "# Project Abyss Timeline-only Migration Report");
        builder.AppendLine();
        builder.AppendLine(
            $"- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine(
            $"- Unity: {Application.unityVersion}");
        builder.AppendLine(
            $"- SkillDefinition: {report.SkillResults.Count}");
        builder.AppendLine(
            $"- Passed: {report.PassedSkillCount}");
        builder.AppendLine(
            $"- Failed: {report.FailedSkillCount}");
        builder.AppendLine();

        builder.AppendLine(
            "## Policy");
        builder.AppendLine();
        builder.AppendLine(
            "- 모든 전투 스킬은 SkillVisualDefinition과 SkillCutsceneDefinition을 필수로 소유한다.");
        builder.AppendLine(
            "- Action / ClashAttack / PartBreak / Kill / Return 5개 Timeline을 필수로 소유한다.");
        builder.AppendLine(
            "- 타격 타이밍은 Timeline Hit Event Clip만 사용한다.");
        builder.AppendLine(
            "- Animator는 Idle / Hit / Dead / 상태 표현만 담당한다.");
        builder.AppendLine();

        builder.AppendLine(
            "## Skills");
        builder.AppendLine();

        foreach (SkillMigrationResult result in
                 report.SkillResults)
        {
            builder.AppendLine(
                $"### {(result.Converted ? "PASS" : "FAIL")} — {result.DisplayName}");
            builder.AppendLine();
            builder.AppendLine(
                $"- Skill: `{result.SkillPath}`");
            builder.AppendLine(
                $"- Visual: `{result.VisualPath}`");
            builder.AppendLine(
                $"- Cutscene: `{result.CutscenePath}`");
            builder.AppendLine(
                $"- Animation: `{result.AnimationPath}`");
            builder.AppendLine(
                $"- Created exclusive visual: {result.CreatedExclusiveVisual}");
            builder.AppendLine(
                $"- Created animation clips: {result.CreatedAnimationClipCount}");
            builder.AppendLine(
                $"- Created Hit events: {result.CreatedHitEventCount}");
            builder.AppendLine(
                $"- Retimed Hit events: {result.RetimedHitEventCount}");
            builder.AppendLine(
                $"- Hit timing source: {result.HitTimingSource}");
            builder.AppendLine(
                $"- Created VFX events: {result.CreatedVfxEventCount}");
            builder.AppendLine(
                $"- Migrated camera shots: {result.MigratedCameraShotCount}");
            builder.AppendLine(
                $"- Created camera impact events: {result.CreatedCameraImpactEventCount}");
            builder.AppendLine(
                $"- Removed duplicate events: {result.RemovedDuplicateEventCount}");
            builder.AppendLine(
                $"- Removed fixed target animation clips: {result.RemovedFixedTargetAnimationClipCount}");

            foreach (string warning in
                     result.Warnings)
            {
                builder.AppendLine(
                    $"- Warning: {warning}");
            }

            foreach (string error in
                     result.Errors)
            {
                builder.AppendLine(
                    $"- Error: {error}");
            }

            builder.AppendLine();
        }

        builder.AppendLine(
            "## Legacy Animation Events");
        builder.AppendLine();
        builder.AppendLine(
            $"- Scanned .anim clips: {report.AnimationEventScan.ScannedClipCount}");
        builder.AppendLine(
            $"- Found legacy events: {report.AnimationEventScan.FoundEventCount}");
        builder.AppendLine(
            $"- Removed legacy events: {report.AnimationEventScan.RemovedEventCount}");

        foreach (string item in
                 report.AnimationEventScan.AffectedClips)
        {
            builder.AppendLine(
                $"- `{item}`");
        }

        foreach (string error in
                 report.AnimationEventScan.Errors)
        {
            builder.AppendLine(
                $"- Error: {error}");
        }

        builder.AppendLine();
        builder.AppendLine(
            "## AnimationEventRelay");
        builder.AppendLine();
        builder.AppendLine(
            $"- Found: {report.RelayScan.FoundRelayCount}");
        builder.AppendLine(
            $"- Removed: {report.RelayScan.RemovedRelayCount}");
        builder.AppendLine(
            $"- Modified prefabs: {report.RelayScan.ModifiedPrefabCount}");
        builder.AppendLine(
            $"- Modified open scenes: {report.RelayScan.ModifiedSceneCount}");

        foreach (string item in
                 report.RelayScan.AffectedPrefabs)
        {
            builder.AppendLine(
                $"- Prefab: `{item}`");
        }

        foreach (string item in
                 report.RelayScan.AffectedScenes)
        {
            builder.AppendLine(
                $"- Scene: `{item}`");
        }

        foreach (string error in
                 report.RelayScan.Errors)
        {
            builder.AppendLine(
                $"- Error: {error}");
        }

        if (report.Errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(
                "## Fatal Errors");
            builder.AppendLine();

            foreach (string error in
                     report.Errors)
            {
                builder.AppendLine(
                    "```text");
                builder.AppendLine(
                    error);
                builder.AppendLine(
                    "```");
            }
        }

        File.WriteAllText(
            ReportPath,
            builder.ToString(),
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier:
                    false));

        AssetDatabase.ImportAsset(
            ReportPath,
            ImportAssetOptions
                .ForceSynchronousImport);
    }

    private sealed class AnimationClipCandidate
    {
        public AnimationClipCandidate(
            AnimationClip clip,
            string path,
            int score)
        {
            Clip = clip;
            Path = path;
            Score = score;
        }

        public AnimationClip Clip { get; }
        public string Path { get; }
        public int Score { get; }
    }

    private sealed class MigrationReport
    {
        public int SkillCount;
        public readonly List<SkillMigrationResult>
            SkillResults =
                new List<SkillMigrationResult>();
        public LegacyAnimationEventScan
            AnimationEventScan =
                new LegacyAnimationEventScan();
        public RelayScanResult RelayScan =
            new RelayScanResult();
        public readonly List<string> Errors =
            new List<string>();

        public int PassedSkillCount =>
            SkillResults.Count(
                result =>
                    result.Converted);

        public int FailedSkillCount =>
            SkillResults.Count -
            PassedSkillCount;

        public bool HasErrors =>
            FailedSkillCount > 0 ||
            Errors.Count > 0 ||
            AnimationEventScan.Errors.Count > 0 ||
            RelayScan.Errors.Count > 0;

        public string BuildDialogSummary()
        {
            return
                $"Skills: {SkillResults.Count}\n" +
                $"Passed: {PassedSkillCount}\n" +
                $"Failed: {FailedSkillCount}\n" +
                $"Animation Events Removed: {AnimationEventScan.RemovedEventCount}\n" +
                $"AnimationEventRelay Removed: {RelayScan.RemovedRelayCount}\n" +
                $"Report: {ReportPath}";
        }
    }

    private sealed class SkillMigrationResult
    {
        public SkillMigrationResult(
            SkillDefinition skill)
        {
            Skill = skill;
            SkillPath = skill != null
                ? AssetDatabase.GetAssetPath(
                    skill)
                : string.Empty;
        }

        public SkillDefinition Skill;
        public string SkillPath;
        public string VisualPath;
        public string CutscenePath;
        public string AnimationPath;
        public bool CreatedExclusiveVisual;
        public int CreatedAnimationClipCount;
        public int CreatedHitEventCount;
        public int RetimedHitEventCount;
        public string HitTimingSource = string.Empty;
        public int CreatedVfxEventCount;
        public int MigratedCameraShotCount;
        public int CreatedCameraImpactEventCount;
        public int RemovedDuplicateEventCount;
        public int RemovedFixedTargetAnimationClipCount;
        public bool Converted;
        public readonly List<string> Warnings =
            new List<string>();
        public readonly List<string> Errors =
            new List<string>();

        public string DisplayName =>
            Skill == null
                ? "NULL"
                : string.IsNullOrWhiteSpace(
                    Skill.SkillName)
                    ? Skill.name
                    : Skill.SkillName;
    }

    private sealed class LegacyAnimationEventScan
    {
        public int ScannedClipCount;
        public int FoundEventCount;
        public int RemovedEventCount;
        public readonly List<string> AffectedClips =
            new List<string>();
        public readonly List<string> Errors =
            new List<string>();
    }

    private sealed class RelayScanResult
    {
        public int FoundRelayCount;
        public int RemovedRelayCount;
        public int ModifiedPrefabCount;
        public int ModifiedSceneCount;
        public readonly List<string> AffectedPrefabs =
            new List<string>();
        public readonly List<string> AffectedScenes =
            new List<string>();
        public readonly List<string> Errors =
            new List<string>();
    }
}
#endif
