#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

/// <summary>
/// v6.3의 완성형 올라프 샘플을 기반으로 Project Abyss Timeline Authoring 기능을
/// 한 번에 확인할 수 있는 Showcase를 생성한다.
///
/// 포함 기능:
/// - 5개 Segment(Action / ClashAttack / PartBreak / Kill / Return)
/// - 공격 Animation, Camera Shot, Camera Motion, Hit, Camera Shake,
///   Camera Impact Pulse, Time Scale, Custom Event, Return Overview
/// - 명시적 Skill VFX Track: 임의 위치, Anchor, Camera, CombatFrame,
///   Transform Motion, Scale Motion, Playback Speed, Hit/Kill/PartBreak Filter
/// - Skill Shader FX Track: Attacker/Target Renderer, Material Slot,
///   Float/Color Property, Strength Curve, Clip Blend, 자동 원상 복구
///
/// 기존 올라프 원본은 수정하지 않고 Generated Complete 아래만 다시 만든다.
/// </summary>
public static class OlafTimelineFeatureShowcaseBuilder
{
    private const string GeneratedRoot =
        "Assets/2. Data/Characters/Olaf/Generated Complete";

    private const string ShowcaseRoot =
        GeneratedRoot + "/Timeline Feature Showcase";

    private const string VfxPrefabFolder =
        ShowcaseRoot + "/VFX Prefabs";

    private const string VfxDefinitionFolder =
        ShowcaseRoot + "/VFX Definitions";

    private const string ShaderDefinitionFolder =
        ShowcaseRoot + "/Shader FX";

    private const string MaterialFolder =
        ShowcaseRoot + "/Materials";

    private const string ReportPath =
        ShowcaseRoot + "/Olaf_Timeline_Feature_Showcase_Report.md";

    private const string BundlePath =
        GeneratedRoot + "/Olaf_Complete_CharacterBundle.asset";

    private const string AttackerPrefabPath =
        GeneratedRoot + "/Prefabs/Olaf_Complete.prefab";

    private const string TargetPrefabPath =
        GeneratedRoot + "/Prefabs/EliteEnemy_CutscenePreview.prefab";

    private const string NormalSkillPath =
        GeneratedRoot + "/Skills/Olaf_BloodStrike_Complete.asset";

    private const string DuelSkillPath =
        GeneratedRoot + "/Skills/Olaf_BerserkerDuel_Complete.asset";

    private const string PreparationSkillPath =
        GeneratedRoot + "/Skills/Olaf_MadnessFlurry_Complete.asset";

    private const string PrestigeSkillPath =
        GeneratedRoot + "/Skills/Olaf_ImmortalFrenzy_Complete.asset";

    [MenuItem(
        "Tools/Project Abyss/Samples/Build Olaf Timeline Feature Showcase (All Features)",
        false,
        2035)]
    public static void BuildFromMenu()
    {
        bool proceed =
            !AssetDatabase.IsValidFolder(GeneratedRoot) ||
            EditorUtility.DisplayDialog(
                "올라프 Timeline Showcase 다시 생성",
                "Generated Complete 폴더를 다시 만든 뒤 모든 Timeline 기능을 사용하는 " +
                "올라프 Showcase를 생성합니다.\n\n기존 원본 올라프 데이터는 수정하지 않습니다.",
                "다시 생성",
                "취소");

        if (!proceed)
            return;

        try
        {
            OlafCompleteBuildResult baseResult =
                OlafCompleteCharacterBuilder.BuildCompleteOlaf();

            if (!baseResult.Success)
            {
                EditorUtility.DisplayDialog(
                    "기본 올라프 생성 실패",
                    baseResult.Summary,
                    "확인");
                return;
            }

            OlafTimelineShowcaseBuildResult showcase =
                BuildShowcase();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            if (!showcase.Success)
            {
                EditorUtility.DisplayDialog(
                    "Timeline Showcase 생성 실패",
                    showcase.Summary,
                    "확인");
                return;
            }

            Debug.Log(
                "[OlafTimelineFeatureShowcaseBuilder] " +
                showcase.Summary,
                showcase.PrestigeSkill);

            bool openPreview =
                EditorUtility.DisplayDialog(
                    "올라프 Timeline Showcase 생성 완료",
                    showcase.Summary +
                    "\n\n불사의 광란 Preview Scene과 Studio를 열까요?",
                    "Preview와 Studio 열기",
                    "에셋만 확인");

            if (openPreview)
            {
                QueueOpenShowcasePreview();
            }
            else
            {
                Selection.activeObject = showcase.PrestigeSkill;
                EditorGUIUtility.PingObject(showcase.PrestigeSkill);
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Timeline Showcase 생성 중 예외",
                exception.ToString(),
                "확인");
        }
    }

    [MenuItem(
        "Tools/Project Abyss/Samples/Open Olaf Timeline Feature Showcase",
        false,
        2036)]
    public static void OpenShowcasePreview()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueOpenShowcasePreview();
            return;
        }

        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(BundlePath);
        SkillDefinition prestige =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(PrestigeSkillPath);
        Character attacker =
            LoadPrefabCharacter<Character>(AttackerPrefabPath);
        Character target =
            LoadPrefabCharacter<Character>(TargetPrefabPath) ?? attacker;

        if (bundle == null || prestige == null || attacker == null || target == null)
        {
            EditorUtility.DisplayDialog(
                "Showcase 없음",
                "먼저 Build Olaf Timeline Feature Showcase를 실행하세요.",
                "확인");
            return;
        }

        SkillCutsceneDefinition definition =
            prestige.VisualDefinition?.CutsceneDefinition;

        if (definition != null)
        {
            SkillCutsceneAssetBuilder.CreatePreviewScene(
                prestige,
                definition,
                attacker,
                target);
        }

        // Scene 생성/저장 직후 같은 Editor tick에서 여러 EditorWindow를 열면
        // Asset import, Timeline graph 재구성과 Scene repaint가 한꺼번에 겹친다.
        // 다음 tick에서 경로로 다시 로드해 파괴된 UnityEngine.Object 참조와
        // Editor TempJob 경고 가능성을 줄인다.
        QueueOpenShowcaseStudios();
    }

    private static void QueueOpenShowcasePreview()
    {
        EditorApplication.delayCall -= OpenShowcasePreview;
        EditorApplication.delayCall += OpenShowcasePreview;
    }

    private static void QueueOpenShowcaseStudios()
    {
        EditorApplication.delayCall -= OpenShowcaseStudios;
        EditorApplication.delayCall += OpenShowcaseStudios;
    }

    private static void OpenShowcaseStudios()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueOpenShowcaseStudios();
            return;
        }

        CharacterAuthoringBundle reloadedBundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(BundlePath);
        SkillDefinition reloadedPrestige =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(PrestigeSkillPath);

        if (reloadedBundle == null || reloadedPrestige == null)
        {
            Debug.LogWarning(
                "[OlafTimelineFeatureShowcaseBuilder] " +
                "Preview 생성 후 Studio 에셋을 다시 로드하지 못했습니다.");
            return;
        }

        ProjectAbyssCharacterStudio.Open(reloadedBundle);
        ProjectAbyssSkillCutsceneStudio.Open(
            reloadedPrestige,
            reloadedBundle);
    }

    [MenuItem(
        "Tools/Project Abyss/Samples/Validate Olaf Timeline Feature Showcase",
        false,
        2037)]
    public static void ValidateFromMenu()
    {
        List<string> issues = ValidateShowcase();
        string message = issues.Count == 0
            ? "올라프 Timeline Feature Showcase 검증 PASS"
            : string.Join("\n", issues);

        Debug.Log(
            "[OlafTimelineFeatureShowcaseBuilder] " + message);

        EditorUtility.DisplayDialog(
            issues.Count == 0 ? "검증 PASS" : "검증 이슈",
            message,
            "확인");
    }

    public static OlafTimelineShowcaseBuildResult BuildShowcase()
    {
        EnsureFolder(ShowcaseRoot);
        EnsureFolder(VfxPrefabFolder);
        EnsureFolder(VfxDefinitionFolder);
        EnsureFolder(ShaderDefinitionFolder);
        EnsureFolder(MaterialFolder);

        CharacterAuthoringBundle bundle =
            AssetDatabase.LoadAssetAtPath<CharacterAuthoringBundle>(BundlePath);

        SkillDefinition normal =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(NormalSkillPath);
        SkillDefinition duel =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(DuelSkillPath);
        SkillDefinition preparation =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(PreparationSkillPath);
        SkillDefinition prestige =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(PrestigeSkillPath);

        if (bundle == null || normal == null || duel == null ||
            preparation == null || prestige == null)
        {
            return OlafTimelineShowcaseBuildResult.Fail(
                "완성형 올라프 기본 에셋을 찾지 못했습니다. " +
                "Build Complete Olaf 단계가 정상 완료됐는지 확인하세요.");
        }

        Material particleMaterial = CreateParticleMaterial();
        GameObject genericParticlePrefab =
            CreateGenericParticlePrefab(particleMaterial);

        ShowcaseVfxSet vfx = CreateVfxDefinitions(genericParticlePrefab);
        ShowcaseShaderSet shader = CreateShaderDefinitions();

        SkillDefinition[] skills =
        {
            normal,
            duel,
            preparation,
            prestige
        };

        List<string> log = new();

        foreach (SkillDefinition skill in skills)
        {
            ConfigureSkillShowcase(
                skill,
                vfx,
                shader,
                log);
        }

        RegisterBundleAssets(
            bundle,
            particleMaterial,
            genericParticlePrefab,
            vfx,
            shader,
            skills);

        List<string> issues = ValidateShowcase();
        WriteReport(skills, vfx, shader, log, issues);

        AssetDatabase.SaveAssets();

        string summary =
            "올라프 Prefab/Bundle과 4개 스킬의 5개 Timeline Segment에 " +
            "Animation, Camera, Camera Motion, Hit, Time Scale, " +
            "명시적 VFX Track, Shader FX Track, PartBreak/Kill/Return 분기를 구성했습니다.\n" +
            $"VFX Definitions: {vfx.All.Count}, " +
            $"Shader Definitions: {shader.All.Count}, " +
            $"Validation Issues: {issues.Count}\n" +
            $"Report: {ReportPath}";

        if (issues.Count > 0)
            summary += "\n\n" + string.Join("\n", issues);

        return issues.Count == 0
            ? OlafTimelineShowcaseBuildResult.Ok(prestige, summary)
            : OlafTimelineShowcaseBuildResult.Warning(prestige, summary);
    }

    private static void ConfigureSkillShowcase(
        SkillDefinition skill,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader,
        ICollection<string> log)
    {
        SkillCutsceneDefinition definition =
            skill?.VisualDefinition?.CutsceneDefinition;

        if (definition == null)
        {
            log.Add($"[SKIP] {skill?.name ?? "NULL"}: CutsceneDefinition 없음");
            return;
        }

        definition.UseExplicitVisualFxTracks = true;
        skill.VisualDefinition.ApplyDamageIfNoHitFrame = false;
        skill.VisualDefinition.AllowAsProfileFallback = false;

        if (skill.VisualDefinition.VfxCues != null)
            skill.VisualDefinition.VfxCues.Clear();

        EditorUtility.SetDirty(skill.VisualDefinition);
        EditorUtility.SetDirty(definition);

        foreach (SkillCutsceneSegment segment in
                 (SkillCutsceneSegment[])Enum.GetValues(typeof(SkillCutsceneSegment)))
        {
            TimelineAsset timeline = definition.GetTimeline(segment);

            if (timeline == null)
                continue;

            SkillCutsceneAssetBuilder.EnsureTimelineStructure(
                definition,
                timeline,
                addDefaultClips:
                    segment == SkillCutsceneSegment.Action ||
                    segment == SkillCutsceneSegment.ClashAttack);

            ClearVisualFxClips(timeline);
            RemoveLegacyVfxEvents(timeline);

            switch (segment)
            {
                case SkillCutsceneSegment.Action:
                    ConfigureActionVisualFx(definition, timeline, skill, vfx, shader, false);
                    break;

                case SkillCutsceneSegment.ClashAttack:
                    ConfigureActionVisualFx(definition, timeline, skill, vfx, shader, true);
                    break;

                case SkillCutsceneSegment.PartBreak:
                    ConfigurePartBreakVisualFx(definition, timeline, vfx, shader);
                    break;

                case SkillCutsceneSegment.Kill:
                    ConfigureKillVisualFx(definition, timeline, vfx, shader);
                    break;

                case SkillCutsceneSegment.Return:
                    ConfigureReturnVisualFx(definition, timeline, vfx, shader);
                    break;
            }

            EditorUtility.SetDirty(timeline);
        }

        EditorUtility.SetDirty(skill);
        log.Add($"[OK] {skill.name}: 5개 Segment Visual FX Showcase 구성");
    }

    private static void ConfigureActionVisualFx(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillDefinition skill,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader,
        bool isClash)
    {
        double frameRate = definition.FrameRate;
        int hitCount = Math.Max(
            1,
            skill.VisualDefinition?.ExpectedHitFrameCount ?? 1);

        List<int> hitFrames = GetHitFrames(timeline, frameRate);

        if (hitFrames.Count == 0)
        {
            for (int i = 0; i < hitCount; i++)
                hitFrames.Add(24 + i * 18);
        }

        int firstHit = hitFrames[0];
        int lastHit = hitFrames[hitFrames.Count - 1];
        int durationFrames = Math.Max(
            definition.DefaultDurationFrames,
            Mathf.RoundToInt((float)(timeline.fixedDuration * frameRate)));

        // 1) 공격자 WeaponMain을 따라가는 충전 오라.
        AddVfx(
            definition,
            timeline,
            vfx.RageEmbers,
            displayName: isClash ? "Clash Weapon Rage — Follow Anchor" : "Weapon Rage — Follow Anchor",
            startFrame: 6,
            durationFrames: Math.Max(20, firstHit - 8),
            binding: SkillVisualFxBinding.AttackerAnchor,
            anchorKey: "WeaponMain",
            follow: SkillTimelineVfxFollowMode.FollowBinding,
            playbackMode: SkillTimelineVfxPlaybackMode.LoopDuringClip,
            playbackSpeed: isClash ? 1.35f : 0.8f,
            startPosition: Vector3.zero,
            startEuler: Vector3.zero,
            startScale: new Vector3(0.25f, 0.25f, 0.25f),
            animateTransform: true,
            endPosition: new Vector3(0f, 0f, 0.2f),
            endEuler: new Vector3(0f, 360f, 0f),
            endScale: new Vector3(1.1f, 1.1f, 1.1f),
            positionCurve: EaseInOut(),
            rotationCurve: Linear(),
            scaleCurve: EaseOut());

        // 2) CombatFrame의 임의 위치에서 대상 방향으로 이동하는 검기.
        AddVfx(
            definition,
            timeline,
            vfx.SlashTrail,
            displayName: "Moving Slash — CombatFrame Motion",
            startFrame: Math.Max(0, firstHit - 9),
            durationFrames: isClash ? 15 : 12,
            binding: SkillVisualFxBinding.CombatFrame,
            anchorKey: "Center",
            follow: SkillTimelineVfxFollowMode.SpawnWorldFixed,
            playbackMode: SkillTimelineVfxPlaybackMode.ClipControlled,
            playbackSpeed: isClash ? 1.8f : 1.25f,
            startPosition: new Vector3(-1.15f, 1.15f, -0.2f),
            startEuler: new Vector3(0f, 35f, -25f),
            startScale: new Vector3(0.35f, 0.15f, 0.8f),
            animateTransform: true,
            endPosition: new Vector3(1.2f, 1.35f, 0.25f),
            endEuler: new Vector3(0f, 135f, 25f),
            endScale: new Vector3(1.4f, 0.5f, 1.8f),
            positionCurve: EaseIn(),
            rotationCurve: EaseInOut(),
            scaleCurve: EaseOut());

        // 3) 각 HitIndex에 대응하는 타깃 부위 피격 VFX.
        for (int index = 0; index < hitFrames.Count; index++)
        {
            int hitFrame = hitFrames[index];

            AddVfx(
                definition,
                timeline,
                vfx.HitBurst,
                displayName: $"Hit Burst #{index + 1} — Target Body Part",
                startFrame: hitFrame,
                durationFrames: 12,
                binding: SkillVisualFxBinding.TargetBodyPart,
                anchorKey: "Chest",
                follow: SkillTimelineVfxFollowMode.SpawnWorldFixed,
                playbackMode: SkillTimelineVfxPlaybackMode.OneShot,
                playbackSpeed: 1f + index * 0.18f,
                startPosition: Vector3.zero,
                startEuler: new Vector3(0f, 180f, 0f),
                startScale: Vector3.one * (0.7f + index * 0.12f),
                animateTransform: true,
                endPosition: new Vector3(0f, 0.15f, -0.18f),
                endEuler: new Vector3(10f, 220f, 10f),
                endScale: Vector3.one * (1.35f + index * 0.15f),
                positionCurve: EaseOut(),
                rotationCurve: EaseOut(),
                scaleCurve: EaseOut(),
                useHitIndex: true,
                hitIndex: index,
                requireResolvedDamage: true);

            AddShader(
                definition,
                timeline,
                shader.HitFlash,
                displayName: $"Target Hit Flash #{index + 1}",
                startFrame: Math.Max(0, hitFrame - 1),
                durationFrames: 6,
                target: SkillShaderTargetCharacter.Target,
                rendererBinding: SkillShaderRendererBinding.AllCharacterRenderers,
                anchorKey: "Chest",
                rendererName: string.Empty,
                materialSlot: -1,
                playbackSpeed: 1f,
                strengthCurve: FlashCurve());
        }

        // 4) Critical 결과일 때만 나타나는 조건부 강조 VFX.
        AddVfx(
            definition,
            timeline,
            vfx.HitBurst,
            displayName: "Critical Accent — Conditional Filter",
            startFrame: firstHit,
            durationFrames: 10,
            binding: SkillVisualFxBinding.TargetRoot,
            anchorKey: "Center",
            follow: SkillTimelineVfxFollowMode.SpawnWorldFixed,
            playbackMode: SkillTimelineVfxPlaybackMode.OneShot,
            playbackSpeed: 2.1f,
            startPosition: new Vector3(0f, 1.1f, 0f),
            startEuler: Vector3.zero,
            startScale: Vector3.one * 0.9f,
            animateTransform: true,
            endPosition: new Vector3(0f, 1.55f, 0f),
            endEuler: new Vector3(0f, 360f, 0f),
            endScale: Vector3.one * 2.4f,
            positionCurve: EaseOut(),
            rotationCurve: EaseOut(),
            scaleCurve: EaseOut(),
            requireDamage: true,
            requireCritical: true);

        // 5) 공격자의 전체 메시를 Timeline 구간 동안 광기색으로 변화.
        AddShader(
            definition,
            timeline,
            shader.RageGlow,
            displayName: isClash ? "Attacker Clash Rage Shader" : "Attacker Rage Shader",
            startFrame: 4,
            durationFrames: Math.Max(20, lastHit + 16),
            target: SkillShaderTargetCharacter.Attacker,
            rendererBinding: SkillShaderRendererBinding.AllCharacterRenderers,
            anchorKey: "WeaponMain",
            rendererName: string.Empty,
            materialSlot: -1,
            playbackSpeed: isClash ? 1.25f : 0.85f,
            strengthCurve: HoldCurve());

        // 6) Camera-local 전경 불티. Camera binding 예제.
        AddVfx(
            definition,
            timeline,
            vfx.ForegroundEmbers,
            displayName: "Foreground Embers — Camera Local",
            startFrame: Math.Max(0, firstHit - 4),
            durationFrames: Math.Min(28, Math.Max(12, durationFrames - firstHit)),
            binding: SkillVisualFxBinding.Camera,
            anchorKey: string.Empty,
            follow: SkillTimelineVfxFollowMode.FollowBinding,
            playbackMode: SkillTimelineVfxPlaybackMode.LoopDuringClip,
            playbackSpeed: 0.55f,
            startPosition: new Vector3(0f, -0.35f, 1.4f),
            startEuler: new Vector3(0f, 180f, 0f),
            startScale: new Vector3(0.35f, 0.35f, 0.35f),
            animateTransform: true,
            endPosition: new Vector3(0.2f, 0.35f, 1.1f),
            endEuler: new Vector3(0f, 260f, 0f),
            endScale: new Vector3(0.7f, 0.7f, 0.7f),
            positionCurve: Linear(),
            rotationCurve: Linear(),
            scaleCurve: EaseInOut());
    }

    private static void ConfigurePartBreakVisualFx(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader)
    {
        AddVfx(
            definition,
            timeline,
            vfx.PartBreakBurst,
            "Part Break Burst — Conditional",
            8,
            24,
            SkillVisualFxBinding.TargetBodyPart,
            "Chest",
            SkillTimelineVfxFollowMode.SpawnWorldFixed,
            SkillTimelineVfxPlaybackMode.ClipControlled,
            0.75f,
            Vector3.zero,
            new Vector3(0f, 180f, 0f),
            Vector3.one * 0.6f,
            true,
            new Vector3(0f, 0.3f, 0f),
            new Vector3(25f, 260f, 20f),
            Vector3.one * 2.2f,
            EaseOut(),
            EaseOut(),
            EaseOut(),
            requirePartBreak: true);

        AddShader(
            definition,
            timeline,
            shader.BreakTint,
            "Target Break Tint",
            5,
            30,
            SkillShaderTargetCharacter.Target,
            SkillShaderRendererBinding.AllCharacterRenderers,
            "Chest",
            string.Empty,
            -1,
            0.8f,
            FlashThenHoldCurve());
    }

    private static void ConfigureKillVisualFx(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader)
    {
        AddVfx(
            definition,
            timeline,
            vfx.KillExplosion,
            "Kill Explosion — Target Root",
            10,
            34,
            SkillVisualFxBinding.TargetRoot,
            "Center",
            SkillTimelineVfxFollowMode.SpawnWorldFixed,
            SkillTimelineVfxPlaybackMode.ClipControlled,
            0.65f,
            new Vector3(0f, 1f, 0f),
            Vector3.zero,
            Vector3.one * 0.7f,
            true,
            new Vector3(0f, 1.65f, 0.15f),
            new Vector3(0f, 360f, 0f),
            Vector3.one * 3.2f,
            EaseOut(),
            EaseOut(),
            EaseOut(),
            requireKill: true);

        AddVfx(
            definition,
            timeline,
            vfx.ForegroundEmbers,
            "Kill Camera Embers",
            6,
            42,
            SkillVisualFxBinding.Camera,
            string.Empty,
            SkillTimelineVfxFollowMode.FollowBinding,
            SkillTimelineVfxPlaybackMode.LoopDuringClip,
            0.45f,
            new Vector3(-0.25f, -0.45f, 1.3f),
            new Vector3(0f, 180f, 0f),
            Vector3.one * 0.45f,
            true,
            new Vector3(0.35f, 0.5f, 1.15f),
            new Vector3(0f, 310f, 0f),
            Vector3.one,
            Linear(),
            Linear(),
            EaseOut(),
            requireKill: true);

        AddShader(
            definition,
            timeline,
            shader.DeathDarken,
            "Target Death Darken",
            4,
            52,
            SkillShaderTargetCharacter.Target,
            SkillShaderRendererBinding.AllCharacterRenderers,
            "Center",
            string.Empty,
            -1,
            0.65f,
            DeathCurve());
    }

    private static void ConfigureReturnVisualFx(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader)
    {
        AddVfx(
            definition,
            timeline,
            vfx.RageEmbers,
            "Return Trail — Attacker Root",
            0,
            24,
            SkillVisualFxBinding.AttackerRoot,
            "Root",
            SkillTimelineVfxFollowMode.FollowBinding,
            SkillTimelineVfxPlaybackMode.ClipControlled,
            1.6f,
            new Vector3(0f, 0.8f, -0.25f),
            Vector3.zero,
            Vector3.one * 0.5f,
            true,
            new Vector3(0f, 0.15f, -0.8f),
            new Vector3(0f, 180f, 0f),
            Vector3.one * 0.12f,
            EaseIn(),
            Linear(),
            EaseIn());

        AddShader(
            definition,
            timeline,
            shader.RageGlow,
            "Return Rage Fade",
            0,
            18,
            SkillShaderTargetCharacter.Attacker,
            SkillShaderRendererBinding.AllCharacterRenderers,
            "Center",
            string.Empty,
            -1,
            1.7f,
            FadeOutCurve());
    }

    private static TimelineClip AddVfx(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        BattleVfxDefinition vfxDefinition,
        string displayName,
        int startFrame,
        int durationFrames,
        SkillVisualFxBinding binding,
        string anchorKey,
        SkillTimelineVfxFollowMode follow,
        SkillTimelineVfxPlaybackMode playbackMode,
        float playbackSpeed,
        Vector3 startPosition,
        Vector3 startEuler,
        Vector3 startScale,
        bool animateTransform,
        Vector3 endPosition,
        Vector3 endEuler,
        Vector3 endScale,
        AnimationCurve positionCurve,
        AnimationCurve rotationCurve,
        AnimationCurve scaleCurve,
        bool useHitIndex = false,
        int hitIndex = 0,
        bool requireDamage = false,
        bool requireResolvedDamage = false,
        bool requireCritical = false,
        bool requireKill = false,
        bool requirePartBreak = false)
    {
        TimelineClip clip = SkillCutsceneAssetBuilder.AddVfxClip(
            definition,
            timeline,
            Math.Max(0, startFrame) / definition.FrameRate,
            vfxDefinition);

        if (clip?.asset is not SkillVfxTimelineClip asset)
            return clip;

        clip.duration = Math.Max(1, durationFrames) / definition.FrameRate;
        clip.displayName = displayName;

        asset.Definition = vfxDefinition;
        asset.Binding = binding;
        asset.AnchorKey = anchorKey ?? string.Empty;
        asset.FollowMode = follow;
        asset.PlaybackMode = playbackMode;
        asset.PlaybackSpeed = Mathf.Max(0.01f, playbackSpeed);
        asset.UseClipDurationAsLifetime = true;
        asset.ReleaseAtClipEnd = true;
        asset.StartPosition = startPosition;
        asset.StartEuler = startEuler;
        asset.StartScale = NonZeroScale(startScale);
        asset.AnimateTransform = animateTransform;
        asset.EndPosition = endPosition;
        asset.EndEuler = endEuler;
        asset.EndScale = NonZeroScale(endScale);
        asset.PositionCurve = positionCurve ?? Linear();
        asset.RotationCurve = rotationCurve ?? Linear();
        asset.ScaleCurve = scaleCurve ?? Linear();
        asset.UseHitIndexFilter = useHitIndex;
        asset.HitIndex = Math.Max(0, hitIndex);
        asset.RequirePositiveDamage = requireDamage;
        asset.RequirePositiveResolvedDamage = requireResolvedDamage;
        asset.RequireCritical = requireCritical;
        asset.RequireKill = requireKill;
        asset.RequirePartBreak = requirePartBreak;
        asset.PreviewInEditMode = true;

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(timeline);
        return clip;
    }

    private static TimelineClip AddShader(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillShaderEffectDefinition shaderDefinition,
        string displayName,
        int startFrame,
        int durationFrames,
        SkillShaderTargetCharacter target,
        SkillShaderRendererBinding rendererBinding,
        string anchorKey,
        string rendererName,
        int materialSlot,
        float playbackSpeed,
        AnimationCurve strengthCurve)
    {
        TimelineClip clip = SkillCutsceneAssetBuilder.AddShaderFxClip(
            definition,
            timeline,
            Math.Max(0, startFrame) / definition.FrameRate,
            shaderDefinition);

        if (clip?.asset is not SkillShaderTimelineClip asset)
            return clip;

        clip.duration = Math.Max(1, durationFrames) / definition.FrameRate;
        clip.displayName = displayName;

        asset.Definition = shaderDefinition;
        asset.TargetCharacter = target;
        asset.RendererBinding = rendererBinding;
        asset.AnchorKey = anchorKey ?? string.Empty;
        asset.RendererName = rendererName ?? string.Empty;
        asset.MaterialSlot = materialSlot;
        asset.IncludeInactive = true;
        asset.PlaybackSpeed = Mathf.Max(0.01f, playbackSpeed);
        asset.StrengthCurve = strengthCurve ?? HoldCurve();

        EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(timeline);
        return clip;
    }

    private static void ClearVisualFxClips(TimelineAsset timeline)
    {
        foreach (TrackAsset track in
                 SkillTimelineTrackUtility.EnumerateAllTracks(timeline).ToArray())
        {
            if (track is not SkillVfxTimelineTrack &&
                track is not SkillShaderTimelineTrack)
            {
                continue;
            }

            foreach (TimelineClip clip in track.GetClips().ToArray())
                timeline.DeleteClip(clip);
        }
    }

    private static void RemoveLegacyVfxEvents(TimelineAsset timeline)
    {
        foreach (SkillCutsceneEventTrack track in
                 SkillTimelineTrackUtility.EnumerateAllTracks(timeline)
                     .OfType<SkillCutsceneEventTrack>())
        {
            foreach (TimelineClip clip in track.GetClips().ToArray())
            {
                if (clip.asset is SkillCutsceneEventClip asset &&
                    asset.EventType == SkillCutsceneEventType.Vfx)
                {
                    timeline.DeleteClip(clip);
                }
            }
        }
    }

    private static List<int> GetHitFrames(
        TimelineAsset timeline,
        double frameRate)
    {
        return SkillTimelineTrackUtility.EnumerateAllTracks(timeline)
            .OfType<SkillCutsceneEventTrack>()
            .SelectMany(track => track.GetClips())
            .Where(clip =>
                clip.asset is SkillCutsceneEventClip asset &&
                asset.EventType == SkillCutsceneEventType.Hit)
            .OrderBy(clip => clip.start)
            .Select(clip => Mathf.RoundToInt((float)(clip.start * frameRate)))
            .ToList();
    }

    private static ShowcaseVfxSet CreateVfxDefinitions(
        GameObject genericParticlePrefab)
    {
        return new ShowcaseVfxSet
        {
            RageEmbers = CreateVfxDefinition(
                "Olaf_Showcase_RageEmbers",
                genericParticlePrefab,
                new Color(1f, 0.18f, 0.015f, 1f),
                0.8f,
                1.0f,
                12),

            SlashTrail = CreateVfxDefinition(
                "Olaf_Showcase_SlashTrail",
                genericParticlePrefab,
                new Color(0.95f, 0.03f, 0.02f, 1f),
                0.55f,
                1.35f,
                12),

            HitBurst = CreateVfxDefinition(
                "Olaf_Showcase_HitBurst",
                genericParticlePrefab,
                new Color(1f, 0.72f, 0.12f, 1f),
                0.45f,
                1.2f,
                20),

            PartBreakBurst = CreateVfxDefinition(
                "Olaf_Showcase_PartBreakBurst",
                genericParticlePrefab,
                new Color(0.9f, 0.02f, 0.06f, 1f),
                1.0f,
                1.8f,
                8),

            KillExplosion = CreateVfxDefinition(
                "Olaf_Showcase_KillExplosion",
                genericParticlePrefab,
                new Color(0.45f, 0.005f, 0.015f, 1f),
                1.5f,
                2.5f,
                6),

            ForegroundEmbers = CreateVfxDefinition(
                "Olaf_Showcase_ForegroundEmbers",
                genericParticlePrefab,
                new Color(1f, 0.35f, 0.04f, 0.65f),
                1.2f,
                0.65f,
                10)
        };
    }

    private static BattleVfxDefinition CreateVfxDefinition(
        string name,
        GameObject prefab,
        Color color,
        float lifetime,
        float radius,
        int maxPoolSize)
    {
        string path = VfxDefinitionFolder + "/" + name + ".asset";
        BattleVfxDefinition asset =
            AssetDatabase.LoadAssetAtPath<BattleVfxDefinition>(path);

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<BattleVfxDefinition>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.EffectPrefab = prefab;
        asset.Lifetime = Mathf.Max(0.05f, lifetime);
        asset.DestroyAfterLifetime = true;
        asset.UsePooling = true;
        asset.PrewarmCount = Math.Min(2, maxPoolSize);
        asset.MaxPoolSize = Math.Max(1, maxPoolSize);
        asset.Color = color;
        asset.Intensity = 1f;
        asset.Radius = Mathf.Max(0.1f, radius);
        asset.PlayEventName = "OnPlay";
        asset.LogSpawn = false;
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static ShowcaseShaderSet CreateShaderDefinitions()
    {
        return new ShowcaseShaderSet
        {
            RageGlow = CreateShaderDefinition(
                "Olaf_Showcase_RageGlow",
                new Color(1f, 0.16f, 0.035f, 1f),
                new Color(2.8f, 0.18f, 0.025f, 1f),
                smoothness: 0.85f),

            HitFlash = CreateShaderDefinition(
                "Olaf_Showcase_HitFlash",
                Color.white,
                new Color(4f, 4f, 4f, 1f),
                smoothness: 1f),

            BreakTint = CreateShaderDefinition(
                "Olaf_Showcase_BreakTint",
                new Color(0.42f, 0.015f, 0.025f, 1f),
                new Color(1.6f, 0.01f, 0.02f, 1f),
                smoothness: 0.25f),

            DeathDarken = CreateShaderDefinition(
                "Olaf_Showcase_DeathDarken",
                new Color(0.035f, 0.005f, 0.008f, 1f),
                Color.black,
                smoothness: 0f)
        };
    }

    private static SkillShaderEffectDefinition CreateShaderDefinition(
        string name,
        Color baseColor,
        Color emissionColor,
        float smoothness)
    {
        string path = ShaderDefinitionFolder + "/" + name + ".asset";
        SkillShaderEffectDefinition definition =
            AssetDatabase.LoadAssetAtPath<SkillShaderEffectDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<SkillShaderEffectDefinition>();
            definition.name = name;
            AssetDatabase.CreateAsset(definition, path);
        }

        definition.IgnoreMissingProperties = true;
        definition.FloatProperties = new List<SkillShaderFloatProperty>
        {
            new SkillShaderFloatProperty
            {
                PropertyName = "_Smoothness",
                TargetValue = Mathf.Clamp01(smoothness),
                BlendMode = SkillShaderPropertyBlendMode.Override
            },
            new SkillShaderFloatProperty
            {
                PropertyName = "_Glossiness",
                TargetValue = Mathf.Clamp01(smoothness),
                BlendMode = SkillShaderPropertyBlendMode.Override
            }
        };

        definition.ColorProperties = new List<SkillShaderColorProperty>
        {
            new SkillShaderColorProperty
            {
                PropertyName = "_BaseColor",
                TargetValue = baseColor,
                BlendMode = SkillShaderPropertyBlendMode.Override
            },
            new SkillShaderColorProperty
            {
                PropertyName = "_Color",
                TargetValue = baseColor,
                BlendMode = SkillShaderPropertyBlendMode.Override
            },
            new SkillShaderColorProperty
            {
                PropertyName = "_EmissionColor",
                TargetValue = emissionColor,
                BlendMode = SkillShaderPropertyBlendMode.Add
            }
        };

        definition.VectorProperties = new List<SkillShaderVectorProperty>();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static Material CreateParticleMaterial()
    {
        string path = MaterialFolder + "/Olaf_Showcase_Particle.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (existing != null)
            return existing;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Unlit/Color");

        if (shader == null)
            throw new InvalidOperationException("Particle Material용 Shader를 찾지 못했습니다.");

        Material material = new(shader)
        {
            name = "Olaf_Showcase_Particle"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateGenericParticlePrefab(Material material)
    {
        string path = VfxPrefabFolder + "/Olaf_Showcase_GenericParticle.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (existing != null)
            return existing;

        GameObject root = new("Olaf_Showcase_GenericParticle");

        try
        {
            ParticleSystem particle = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = true;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
            main.maxParticles = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f;

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.02f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particle.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.25f),
                    new Keyframe(0.22f, 1f),
                    new Keyframe(1f, 0f)));

            ParticleSystemRenderer renderer =
                root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;

            root.AddComponent<BattleVfxInstance>();
            root.AddComponent<ScriptedParticleEffectPlayer>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void RegisterBundleAssets(
        CharacterAuthoringBundle bundle,
        Material particleMaterial,
        GameObject particlePrefab,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader,
        IEnumerable<SkillDefinition> skills)
    {
        if (bundle == null)
            return;

        bundle.RegisterSupportingAsset(particleMaterial);
        bundle.RegisterSupportingAsset(particlePrefab);

        foreach (BattleVfxDefinition definition in vfx.All)
            bundle.RegisterIncludedAsset(definition);

        foreach (SkillShaderEffectDefinition definition in shader.All)
            bundle.RegisterIncludedAsset(definition);

        foreach (SkillDefinition skill in skills)
        {
            if (skill == null)
                continue;

            bundle.RegisterIncludedAsset(skill);
            bundle.RegisterIncludedAsset(skill.VisualDefinition);
            bundle.RegisterIncludedAsset(skill.VisualDefinition?.CutsceneDefinition);

            SkillCutsceneDefinition cutscene =
                skill.VisualDefinition?.CutsceneDefinition;

            if (cutscene == null)
                continue;

            foreach (SkillCutsceneSegment segment in
                     (SkillCutsceneSegment[])Enum.GetValues(typeof(SkillCutsceneSegment)))
            {
                bundle.RegisterSupportingAsset(cutscene.GetTimeline(segment));
            }

            bundle.RegisterSupportingAsset(cutscene.CameraRigPrefab);
        }

        EditorUtility.SetDirty(bundle);
    }

    private static List<string> ValidateShowcase()
    {
        List<string> issues = new();
        string[] paths =
        {
            NormalSkillPath,
            DuelSkillPath,
            PreparationSkillPath,
            PrestigeSkillPath
        };

        foreach (string path in paths)
        {
            SkillDefinition skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (skill == null)
            {
                issues.Add($"[ERROR] Skill 없음: {path}");
                continue;
            }

            SkillCutsceneDefinition definition =
                skill.VisualDefinition?.CutsceneDefinition;

            if (definition == null)
            {
                issues.Add($"[ERROR] {skill.name}: CutsceneDefinition 없음");
                continue;
            }

            if (!definition.UseExplicitVisualFxTracks)
                issues.Add($"[ERROR] {skill.name}: UseExplicitVisualFxTracks=false");

            foreach (SkillCutsceneSegment segment in
                     (SkillCutsceneSegment[])Enum.GetValues(typeof(SkillCutsceneSegment)))
            {
                TimelineAsset timeline = definition.GetTimeline(segment);

                if (timeline == null)
                {
                    issues.Add($"[ERROR] {skill.name}/{segment}: Timeline 없음");
                    continue;
                }

                List<TrackAsset> tracks =
                    SkillTimelineTrackUtility.EnumerateAllTracks(timeline).ToList();

                int vfxCount = tracks.OfType<SkillVfxTimelineTrack>()
                    .Sum(track => track.GetClips().Count());
                int shaderCount = tracks.OfType<SkillShaderTimelineTrack>()
                    .Sum(track => track.GetClips().Count());
                int legacyVfxCount = tracks.OfType<SkillCutsceneEventTrack>()
                    .SelectMany(track => track.GetClips())
                    .Count(clip =>
                        clip.asset is SkillCutsceneEventClip asset &&
                        asset.EventType == SkillCutsceneEventType.Vfx);

                if (vfxCount == 0)
                    issues.Add($"[ERROR] {skill.name}/{segment}: VFX Clip 없음");

                if (shaderCount == 0)
                    issues.Add($"[ERROR] {skill.name}/{segment}: Shader FX Clip 없음");

                if (legacyVfxCount > 0)
                    issues.Add($"[ERROR] {skill.name}/{segment}: Legacy Vfx Event {legacyVfxCount}개 남음");
            }
        }

        SkillDefinition prestige =
            AssetDatabase.LoadAssetAtPath<SkillDefinition>(PrestigeSkillPath);
        SkillCutsceneDefinition prestigeCutscene =
            prestige?.VisualDefinition?.CutsceneDefinition;

        if (prestigeCutscene != null)
        {
            TimelineAsset action = prestigeCutscene.ActionTimeline;
            List<SkillVfxTimelineClip> vfxAssets = action == null
                ? new List<SkillVfxTimelineClip>()
                : SkillTimelineTrackUtility.EnumerateAllTracks(action)
                    .OfType<SkillVfxTimelineTrack>()
                    .SelectMany(track => track.GetClips())
                    .Select(clip => clip.asset as SkillVfxTimelineClip)
                    .Where(asset => asset != null)
                    .ToList();

            if (!vfxAssets.Any(asset => asset.AnimateTransform))
                issues.Add("[ERROR] Prestige Action: 이동 VFX 예제가 없음");

            if (!vfxAssets.Any(asset => asset.PlaybackSpeed > 1.01f || asset.PlaybackSpeed < 0.99f))
                issues.Add("[ERROR] Prestige Action: VFX PlaybackSpeed 예제가 없음");

            if (!vfxAssets.Any(asset => asset.Binding == SkillVisualFxBinding.Camera))
                issues.Add("[ERROR] Prestige Action: Camera Binding VFX 예제가 없음");

            if (!vfxAssets.Any(asset => asset.Binding == SkillVisualFxBinding.CombatFrame))
                issues.Add("[ERROR] Prestige Action: CombatFrame VFX 예제가 없음");
        }

        return issues;
    }

    private static void WriteReport(
        IReadOnlyList<SkillDefinition> skills,
        ShowcaseVfxSet vfx,
        ShowcaseShaderSet shader,
        IEnumerable<string> log,
        IReadOnlyList<string> issues)
    {
        List<string> lines = new()
        {
            "# Olaf Timeline Feature Showcase Build Report",
            string.Empty,
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty,
            "## Feature Matrix",
            string.Empty,
            "| 기능 | 생성 위치 |",
            "|---|---|",
            "| 공격 Animation | Action / ClashAttack Attacker Track |",
            "| 피격 타이밍 | Battle Events / Hit |",
            "| Camera Shot Blend | Skill Camera Track |",
            "| Camera Local Motion | [AbyssRig] Camera Motion |",
            "| Time Scale | SetTimeScale / RestoreTimeScale |",
            "| 임의 공간 VFX | CombatFrame VFX Clip |",
            "| Anchor Follow VFX | AttackerAnchor / WeaponMain |",
            "| 타깃 부위 VFX | TargetBodyPart + HitIndex |",
            "| Camera Local VFX | Camera Binding |",
            "| VFX 이동·회전·크기 | Start/End Transform + Curve |",
            "| VFX 재생 속도 | PlaybackSpeed |",
            "| Shader FX | Attacker/Target Renderer Property Curve |",
            "| 부위 파괴 분기 | PartBreak Timeline + RequirePartBreak |",
            "| 사망 분기 | Kill Timeline + RequireKill |",
            "| 복귀 | Return Timeline + ReturnOverview |",
            string.Empty,
            "## Skills",
            string.Empty
        };

        foreach (SkillDefinition skill in skills)
        {
            if (skill == null)
                continue;

            lines.Add($"### {skill.name}");
            SkillCutsceneDefinition cutscene =
                skill.VisualDefinition?.CutsceneDefinition;

            foreach (SkillCutsceneSegment segment in
                     (SkillCutsceneSegment[])Enum.GetValues(typeof(SkillCutsceneSegment)))
            {
                TimelineAsset timeline = cutscene?.GetTimeline(segment);
                int vfxCount = timeline == null ? 0 :
                    SkillTimelineTrackUtility.EnumerateAllTracks(timeline)
                        .OfType<SkillVfxTimelineTrack>()
                        .Sum(track => track.GetClips().Count());
                int shaderCount = timeline == null ? 0 :
                    SkillTimelineTrackUtility.EnumerateAllTracks(timeline)
                        .OfType<SkillShaderTimelineTrack>()
                        .Sum(track => track.GetClips().Count());

                lines.Add($"- {segment}: VFX={vfxCount}, ShaderFX={shaderCount}");
            }

            lines.Add(string.Empty);
        }

        lines.Add("## Generated VFX Definitions");
        lines.AddRange(vfx.All.Select(item => "- " + AssetDatabase.GetAssetPath(item)));
        lines.Add(string.Empty);
        lines.Add("## Generated Shader Definitions");
        lines.AddRange(shader.All.Select(item => "- " + AssetDatabase.GetAssetPath(item)));
        lines.Add(string.Empty);
        lines.Add("## Build Log");
        lines.AddRange(log.Select(item => "- " + item));
        lines.Add(string.Empty);
        lines.Add("## Validation");

        if (issues.Count == 0)
            lines.Add("- PASS");
        else
            lines.AddRange(issues.Select(item => "- " + item));

        File.WriteAllLines(ReportPath, lines);
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
    }

    private static T LoadPrefabCharacter<T>(string path)
        where T : Character
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null
            ? prefab.GetComponentInChildren<T>(true)
            : null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string normalized = path.Replace('\\', '/');
        string parent = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        string name = Path.GetFileName(normalized);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            return;

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(normalized))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Vector3 NonZeroScale(Vector3 value)
    {
        return value == Vector3.zero ? Vector3.one : value;
    }

    private static AnimationCurve Linear() =>
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private static AnimationCurve EaseIn() =>
        new(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2f, 0f));

    private static AnimationCurve EaseOut() =>
        new(
            new Keyframe(0f, 0f, 2f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

    private static AnimationCurve EaseInOut() =>
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private static AnimationCurve HoldCurve() =>
        new(
            new Keyframe(0f, 0f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.82f, 1f),
            new Keyframe(1f, 0f));

    private static AnimationCurve FlashCurve() =>
        new(
            new Keyframe(0f, 0f),
            new Keyframe(0.08f, 1f),
            new Keyframe(0.28f, 0.72f),
            new Keyframe(1f, 0f));

    private static AnimationCurve FlashThenHoldCurve() =>
        new(
            new Keyframe(0f, 0f),
            new Keyframe(0.08f, 1f),
            new Keyframe(0.25f, 0.55f),
            new Keyframe(0.82f, 0.55f),
            new Keyframe(1f, 0f));

    private static AnimationCurve DeathCurve() =>
        new(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 0.35f),
            new Keyframe(0.75f, 1f),
            new Keyframe(1f, 1f));

    private static AnimationCurve FadeOutCurve() =>
        new(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));

    private sealed class ShowcaseVfxSet
    {
        public BattleVfxDefinition RageEmbers;
        public BattleVfxDefinition SlashTrail;
        public BattleVfxDefinition HitBurst;
        public BattleVfxDefinition PartBreakBurst;
        public BattleVfxDefinition KillExplosion;
        public BattleVfxDefinition ForegroundEmbers;

        public IReadOnlyList<BattleVfxDefinition> All =>
            new[]
            {
                RageEmbers,
                SlashTrail,
                HitBurst,
                PartBreakBurst,
                KillExplosion,
                ForegroundEmbers
            };
    }

    private sealed class ShowcaseShaderSet
    {
        public SkillShaderEffectDefinition RageGlow;
        public SkillShaderEffectDefinition HitFlash;
        public SkillShaderEffectDefinition BreakTint;
        public SkillShaderEffectDefinition DeathDarken;

        public IReadOnlyList<SkillShaderEffectDefinition> All =>
            new[]
            {
                RageGlow,
                HitFlash,
                BreakTint,
                DeathDarken
            };
    }
}

public sealed class OlafTimelineShowcaseBuildResult
{
    public bool Success { get; private set; }
    public bool HasWarnings { get; private set; }
    public SkillDefinition PrestigeSkill { get; private set; }
    public string Summary { get; private set; }

    public static OlafTimelineShowcaseBuildResult Ok(
        SkillDefinition prestigeSkill,
        string summary)
    {
        return new OlafTimelineShowcaseBuildResult
        {
            Success = true,
            HasWarnings = false,
            PrestigeSkill = prestigeSkill,
            Summary = summary
        };
    }

    public static OlafTimelineShowcaseBuildResult Warning(
        SkillDefinition prestigeSkill,
        string summary)
    {
        return new OlafTimelineShowcaseBuildResult
        {
            Success = true,
            HasWarnings = true,
            PrestigeSkill = prestigeSkill,
            Summary = summary
        };
    }

    public static OlafTimelineShowcaseBuildResult Fail(string summary)
    {
        return new OlafTimelineShowcaseBuildResult
        {
            Success = false,
            HasWarnings = false,
            Summary = summary
        };
    }
}
#endif
