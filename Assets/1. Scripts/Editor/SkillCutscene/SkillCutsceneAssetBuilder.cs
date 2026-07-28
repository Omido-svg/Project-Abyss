#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class SkillCutsceneAssetBuilder
{
    public const string AttackerTrackName =
        SkillCutsceneTimelineBinder
            .AttackerAnimationTrackName;

    public const string TargetTrackName =
        SkillCutsceneTimelineBinder
            .TargetAnimationTrackName;

    public const string VisualFxGroupName =
        "Visual FX";

    public const string VfxTrackName =
        "[Abyss FX] VFX";

    public const string ShaderTrackName =
        "[Abyss FX] Shader";

    public static SkillCutsceneDefinition EnsureForSkill(
        SkillDefinition skill,
        Character attackerPrefab,
        Character targetPrefab)
    {
        if (skill == null)
            return null;

        string skillPath =
            AssetDatabase.GetAssetPath(
                skill);

        string baseFolder =
            BuildCutsceneFolder(
                skillPath,
                skill.name);

        EnsureFolder(
            baseFolder);

        SkillVisualDefinition visual =
            skill.VisualDefinition;

        if (visual == null)
        {
            visual =
                ScriptableObject
                    .CreateInstance<
                        SkillVisualDefinition>();

            visual.name =
                skill.name +
                "_Visual";

            string visualPath =
                AssetDatabase
                    .GenerateUniqueAssetPath(
                        $"{baseFolder}/" +
                        $"{visual.name}.asset");

            AssetDatabase.CreateAsset(
                visual,
                visualPath);

            skill.VisualDefinition =
                visual;

            EditorUtility.SetDirty(
                skill);
        }

        SkillCutsceneDefinition definition =
            visual.CutsceneDefinition;

        if (definition == null)
        {
            definition =
                ScriptableObject
                    .CreateInstance<
                        SkillCutsceneDefinition>();

            definition.name =
                skill.name +
                "_Cutscene";

            string definitionPath =
                AssetDatabase
                    .GenerateUniqueAssetPath(
                        $"{baseFolder}/" +
                        $"{definition.name}.asset");

            AssetDatabase.CreateAsset(
                definition,
                definitionPath);

            definition.PrepareFacing =
                visual.FaceEachOther;
            definition.PrepareMovement =
                visual.MovesToTarget;
            definition.RestoreMovement =
                visual.ReturnPositionAfterAction;
            definition.RestoreFacing =
                visual.ReturnFacingAfterAction;
            definition.UseExplicitVisualFxTracks = true;

            visual.CutsceneDefinition =
                definition;
            visual.AllowAsProfileFallback =
                false;
            visual.ApplyDamageIfNoHitFrame =
                false;

            EditorUtility.SetDirty(
                definition);
            EditorUtility.SetDirty(
                visual);
        }

        if (definition.ActionTimeline ==
            null)
        {
            definition.ActionTimeline =
                CreateTimeline(
                    definition,
                    SkillCutsceneSegment.Action,
                    baseFolder);
        }

        if (definition.CameraRigPrefab ==
            null)
        {
            definition.CameraRigPrefab =
                CreateCameraRigPrefab(
                    definition,
                    baseFolder);
        }

        EnsureTimelineStructure(
            definition,
            definition.ActionTimeline,
            addDefaultClips: true);

        foreach (SkillCutsceneSegment segment in
                 (SkillCutsceneSegment[])Enum.GetValues(
                     typeof(SkillCutsceneSegment)))
        {
            TimelineAsset timeline =
                segment == SkillCutsceneSegment.Action
                    ? definition.ActionTimeline
                    : EnsureSegment(definition, segment);

            EnsureTimelineStructure(
                definition,
                timeline,
                addDefaultClips:
                    segment == SkillCutsceneSegment.Action ||
                    segment == SkillCutsceneSegment.ClashAttack);

            EnsureSegmentDefaults(
                definition,
                timeline,
                segment);
        }

        EditorUtility.SetDirty(
            definition);

        EditorUtility.SetDirty(
            visual);

        EditorUtility.SetDirty(
            skill);

        AssetDatabase.SaveAssets();

        return definition;
    }

    public static TimelineAsset EnsureSegment(
        SkillCutsceneDefinition definition,
        SkillCutsceneSegment segment)
    {
        if (definition == null)
            return null;

        TimelineAsset existing =
            definition.GetTimeline(
                segment);

        if (segment ==
                SkillCutsceneSegment
                    .ClashAttack &&
            definition.ClashAttackTimeline ==
            null)
        {
            existing =
                null;
        }

        if (existing != null)
            return existing;

        string definitionPath =
            AssetDatabase.GetAssetPath(
                definition);

        string folder =
            Path.GetDirectoryName(
                definitionPath)
                ?.Replace(
                    '\\',
                    '/');

        if (string.IsNullOrWhiteSpace(
                folder))
        {
            folder =
                "Assets/2. Data/SkillCutscenes";
        }

        EnsureFolder(
            folder);

        TimelineAsset timeline =
            CreateTimeline(
                definition,
                segment,
                folder);

        switch (segment)
        {
            case SkillCutsceneSegment
                .ClashAttack:
                definition
                    .ClashAttackTimeline =
                        timeline;
                break;

            case SkillCutsceneSegment
                .PartBreak:
                definition
                    .PartBreakTimeline =
                        timeline;
                break;

            case SkillCutsceneSegment.Kill:
                definition.KillTimeline =
                    timeline;
                break;

            case SkillCutsceneSegment.Return:
                definition.ReturnTimeline =
                    timeline;
                break;

            default:
                definition.ActionTimeline =
                    timeline;
                break;
        }

        EnsureTimelineStructure(
            definition,
            timeline,
            addDefaultClips:
                segment == SkillCutsceneSegment.Action ||
                segment == SkillCutsceneSegment.ClashAttack);

        EnsureSegmentDefaults(
            definition,
            timeline,
            segment);

        EditorUtility.SetDirty(
            definition);

        AssetDatabase.SaveAssets();

        return timeline;
    }

    public static void EnsureTimelineStructure(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        bool addDefaultClips)
    {
        if (definition == null ||
            timeline == null)
        {
            return;
        }

        timeline.editorSettings.frameRate =
            definition.FrameRate;

        timeline.durationMode =
            TimelineAsset.DurationMode
                .FixedLength;

        timeline.fixedDuration =
            Math.Max(
                definition
                    .DefaultDurationSeconds,
                1d /
                definition.FrameRate);

        AnimationTrack attackerTrack =
            FindTrack<AnimationTrack>(
                timeline,
                AttackerTrackName) ??
            timeline.CreateTrack<
                AnimationTrack>(
                    null,
                    AttackerTrackName);

        AnimationTrack targetTrack =
            FindTrack<AnimationTrack>(
                timeline,
                TargetTrackName) ??
            timeline.CreateTrack<
                AnimationTrack>(
                    null,
                    TargetTrackName);

        SkillCameraTimelineTrack
            cameraTrack =
                FindTrack<
                    SkillCameraTimelineTrack>(
                        timeline,
                        "Skill Camera") ??
                timeline.CreateTrack<
                    SkillCameraTimelineTrack>(
                        null,
                        "Skill Camera");

        SkillCutsceneEventTrack
            eventTrack =
                FindTrack<
                    SkillCutsceneEventTrack>(
                        timeline,
                        "Battle Events") ??
                timeline.CreateTrack<
                    SkillCutsceneEventTrack>(
                        null,
                        "Battle Events");

        GroupTrack visualFxGroup =
            FindTrack<GroupTrack>(
                timeline,
                VisualFxGroupName) ??
            timeline.CreateTrack<GroupTrack>(
                null,
                VisualFxGroupName);

        SkillVfxTimelineTrack vfxTrack =
            FindTrack<SkillVfxTimelineTrack>(
                timeline,
                VfxTrackName) ??
            timeline.CreateTrack<SkillVfxTimelineTrack>(
                visualFxGroup,
                VfxTrackName);

        SkillShaderTimelineTrack shaderTrack =
            FindTrack<SkillShaderTimelineTrack>(
                timeline,
                ShaderTrackName) ??
            timeline.CreateTrack<SkillShaderTimelineTrack>(
                visualFxGroup,
                ShaderTrackName);

        if (!addDefaultClips)
        {
            EditorUtility.SetDirty(
                timeline);

            return;
        }

        if (!attackerTrack.GetClips()
            .Any() &&
            definition.AttackerAnimation !=
            null)
        {
            TimelineClip animationClip =
                attackerTrack.CreateClip(
                    definition
                        .AttackerAnimation);

            animationClip.start =
                0d;

            animationClip.duration =
                definition
                    .AttackerAnimation
                    .length;

            timeline.fixedDuration =
                Math.Max(
                    timeline.fixedDuration,
                    animationClip.duration);
        }

        if (!targetTrack.GetClips()
            .Any() &&
            definition.TargetAnimation !=
            null)
        {
            TimelineClip targetClip =
                targetTrack.CreateClip(
                    definition
                        .TargetAnimation);

            targetClip.start =
                0d;

            targetClip.duration =
                definition
                    .TargetAnimation
                    .length;

            timeline.fixedDuration =
                Math.Max(
                    timeline.fixedDuration,
                    targetClip.duration);
        }

        if (!cameraTrack.GetClips()
            .Any())
        {
            CreateDefaultCameraClips(
                definition,
                cameraTrack);
        }

        if (!eventTrack.GetClips().Any() &&
            ShouldCreateDefaultHit(
                definition))
        {
            CreateHitEventClip(
                definition,
                eventTrack,
                definition
                    .DefaultDurationFrames *
                0.58d /
                definition.FrameRate);
        }

        EditorUtility.SetDirty(
            timeline);
    }

    public static string CreatePreviewScene(
        SkillDefinition skill,
        SkillCutsceneDefinition definition,
        Character attackerPrefab,
        Character targetPrefab)
    {
        if (skill == null ||
            definition == null ||
            attackerPrefab == null ||
            targetPrefab == null)
        {
            throw new InvalidOperationException(
                "Skill, Cutscene Definition, Attacker Prefab, Target Prefab이 모두 필요합니다.");
        }

        if (!EditorSceneManager
            .SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return null;
        }

        string definitionPath =
            AssetDatabase.GetAssetPath(
                definition);

        string folder =
            Path.GetDirectoryName(
                definitionPath)
                ?.Replace(
                    '\\',
                    '/');

        EnsureFolder(
            folder);

        string scenePath =
            $"{folder}/" +
            $"{Sanitize(skill.name)}_Preview.unity";

        GameObject attackerPrefabRoot =
            ResolvePersistentCharacterPrefabRoot(
                attackerPrefab,
                "attacker");

        GameObject targetPrefabRoot =
            ResolvePersistentCharacterPrefabRoot(
                targetPrefab,
                "target");

        Scene scene =
            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

        GameObject mainCameraObject =
            new GameObject(
                "Main Camera");

        Camera mainCamera =
            mainCameraObject.AddComponent<
                Camera>();

        mainCameraObject.tag =
            "MainCamera";

        mainCameraObject.transform
            .SetPositionAndRotation(
                new Vector3(
                    0f,
                    2.4f,
                    -7f),
                Quaternion.LookRotation(
                    new Vector3(
                        0f,
                        1.2f,
                        0f) -
                    new Vector3(
                        0f,
                        2.4f,
                        -7f)));

        mainCameraObject.AddComponent<
            CinemachineBrain>();

        GameObject lightObject =
            new GameObject(
                "Preview Directional Light");

        Light light =
            lightObject.AddComponent<
                Light>();

        light.type =
            LightType.Directional;

        light.intensity =
            1.2f;

        lightObject.transform.rotation =
            Quaternion.Euler(
                48f,
                -35f,
                0f);

        GameObject attackerObject =
            PrefabUtility
                .InstantiatePrefab(
                    attackerPrefabRoot,
                    scene)
            as GameObject;

        GameObject targetObject =
            PrefabUtility
                .InstantiatePrefab(
                    targetPrefabRoot,
                    scene)
            as GameObject;

        if (attackerObject == null ||
            targetObject == null)
        {
            throw new InvalidOperationException(
                "Preview Character Prefab 인스턴스 생성에 실패했습니다.");
        }

        attackerObject.name =
            "Preview_Attacker";

        targetObject.name =
            "Preview_Target";

        attackerObject.transform.position =
            new Vector3(
                -1.8f,
                0f,
                0f);

        targetObject.transform.position =
            new Vector3(
                1.8f,
                0f,
                0f);

        FaceEachOther(
            attackerObject.transform,
            targetObject.transform);

        GameObject rigObject =
            PrefabUtility
                .InstantiatePrefab(
                    definition
                        .CameraRigPrefab,
                    scene)
            as GameObject;

        rigObject.name =
            definition
                .CameraRigPrefab
                .name;

        GameObject host =
            new GameObject(
                "SkillCutscene_PreviewHost");

        PlayableDirector director =
            host.AddComponent<
                PlayableDirector>();

        SkillCutsceneRuntimeContext
            context =
                host.AddComponent<
                    SkillCutsceneRuntimeContext>();

        ProjectAbyssSkillCutscenePreviewBinder
            binder =
                host.AddComponent<
                    ProjectAbyssSkillCutscenePreviewBinder>();

        binder.Configure(
            skill,
            definition,
            attackerObject
                .GetComponentInChildren<
                    Character>(true),
            targetObject
                .GetComponentInChildren<
                    Character>(true),
            rigObject
                .GetComponentInChildren<
                    SkillCutsceneCameraRig>(
                        true),
            director,
            context);

        binder.BindNow();

        bool sceneSaved =
            EditorSceneManager.SaveScene(
                scene,
                scenePath);

        if (!sceneSaved)
        {
            Debug.LogWarning(
                $"[SkillCutsceneAssetBuilder] Preview Scene 저장 실패: {scenePath}");
            return null;
        }

        // SaveScene 또는 에셋 import 과정에서 기존 ScriptableObject wrapper가
        // 파괴될 수 있다. 저장 전에 얻어 둔 persistent path로 다시 로드한 뒤
        // PreviewScenePath를 기록해야 MissingReferenceException이 발생하지 않는다.
        SkillCutsceneDefinition persistentDefinition =
            AssetDatabase.LoadAssetAtPath<SkillCutsceneDefinition>(
                definitionPath);

        if (persistentDefinition != null)
        {
            persistentDefinition.SetPreviewScenePath(
                scenePath);
        }
        else
        {
            Debug.LogWarning(
                "[SkillCutsceneAssetBuilder] 저장 후 Cutscene Definition을 " +
                $"다시 로드하지 못했습니다. Path={definitionPath}");
        }

        // SaveScene은 .unity 에셋을 이미 등록한다. 여기서 Refresh까지 호출하면
        // Timeline/VFX/Scene import와 창 repaint가 같은 tick에 중첩될 수 있다.
        AssetDatabase.SaveAssets();

        Selection.activeGameObject =
            host;

        EditorApplication
            .ExecuteMenuItem(
                "Window/Sequencing/Timeline");

        return scenePath;
    }

    private static void EnsureSegmentDefaults(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        SkillCutsceneSegment segment)
    {
        if (definition == null || timeline == null)
            return;

        SkillCameraTimelineTrack cameraTrack =
            FindTrack<SkillCameraTimelineTrack>(
                timeline,
                "Skill Camera") ??
            timeline.CreateTrack<SkillCameraTimelineTrack>(
                null,
                "Skill Camera");

        if (!cameraTrack.GetClips().Any())
        {
            TimelineClip camera =
                cameraTrack.CreateClip<SkillCameraTimelineClip>();

            camera.start = 0d;
            camera.duration = Math.Max(
                1d / definition.FrameRate,
                timeline.fixedDuration);
            camera.displayName =
                segment == SkillCutsceneSegment.Return
                    ? "CM_Overview"
                    : segment == SkillCutsceneSegment.Kill ||
                      segment == SkillCutsceneSegment.PartBreak
                        ? "CM_Impact"
                        : "CM_Overview";

            if (camera.asset is SkillCameraTimelineClip cameraAsset)
            {
                cameraAsset.CameraKey = camera.displayName;
                cameraAsset.PositionBinding =
                    camera.displayName == "CM_Overview"
                        ? SkillCameraPositionBinding.RigRootFixed
                        : SkillCameraPositionBinding.CombatFrameFollow;
                cameraAsset.AimBinding =
                    SkillCameraAimBinding.AttackerTargetMidpoint;
                EditorUtility.SetDirty(cameraAsset);
            }
        }

        SkillCutsceneEventTrack eventTrack =
            FindTrack<SkillCutsceneEventTrack>(
                timeline,
                "Battle Events") ??
            timeline.CreateTrack<SkillCutsceneEventTrack>(
                null,
                "Battle Events");

        if (segment == SkillCutsceneSegment.Return &&
            !eventTrack.GetClips().Any(clip =>
                clip.asset is SkillCutsceneEventClip asset &&
                asset.EventType == SkillCutsceneEventType.ReturnOverview))
        {
            TimelineClip returnClip =
                eventTrack.CreateClip<SkillCutsceneEventClip>();
            returnClip.start = Math.Max(
                0d,
                timeline.fixedDuration - 1d / definition.FrameRate);
            returnClip.duration = 1d / definition.FrameRate;
            returnClip.displayName = "ReturnOverview";

            if (returnClip.asset is SkillCutsceneEventClip returnAsset)
            {
                returnAsset.EventType =
                    SkillCutsceneEventType.ReturnOverview;
                EditorUtility.SetDirty(returnAsset);
            }
        }

        EditorUtility.SetDirty(timeline);
    }

    public static TimelineClip AddCameraClip(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        double startTime,
        string cameraKey)
    {
        if (definition == null ||
            timeline == null)
        {
            return null;
        }

        SkillCameraTimelineTrack track =
            FindTrack<
                SkillCameraTimelineTrack>(
                    timeline,
                    "Skill Camera") ??
            timeline.CreateTrack<
                SkillCameraTimelineTrack>(
                    null,
                    "Skill Camera");

        TimelineClip clip =
            track.CreateClip<
                SkillCameraTimelineClip>();

        clip.start =
            Math.Max(
                0d,
                startTime);

        clip.duration =
            30d /
            definition.FrameRate;

        clip.displayName =
            string.IsNullOrWhiteSpace(
                cameraKey)
                ? "Camera Shot"
                : cameraKey;

        SkillCameraTimelineClip asset =
            clip.asset as
                SkillCameraTimelineClip;

        if (asset != null)
        {
            asset.CameraKey =
                string.IsNullOrWhiteSpace(
                    cameraKey)
                    ? "CM_Overview"
                    : cameraKey;

            EditorUtility.SetDirty(
                asset);
        }

        EditorUtility.SetDirty(
            timeline);

        AssetDatabase.SaveAssets();

        return clip;
    }

    public static AnimationTrack
        EnsureCameraMotionTrack(
            TimelineAsset timeline,
            string cameraKey)
    {
        if (timeline == null ||
            string.IsNullOrWhiteSpace(
                cameraKey))
        {
            return null;
        }

        string trackName =
            SkillCutsceneTimelineBinder
                .RigAnimationPrefix +
            cameraKey.Trim();

        AnimationTrack track =
            timeline
                .GetOutputTracks()
                .OfType<
                    AnimationTrack>()
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.name,
                            trackName,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (track == null)
        {
            track =
                timeline.CreateTrack<
                    AnimationTrack>(
                        null,
                        trackName);
        }

        EditorUtility.SetDirty(
            timeline);

        AssetDatabase.SaveAssets();

        return track;
    }

    public static TimelineClip AddVfxClip(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        double startTime,
        BattleVfxDefinition vfxDefinition)
    {
        if (definition == null || timeline == null)
            return null;

        GroupTrack group =
            FindTrack<GroupTrack>(timeline, VisualFxGroupName) ??
            timeline.CreateTrack<GroupTrack>(null, VisualFxGroupName);

        SkillVfxTimelineTrack track =
            FindTrack<SkillVfxTimelineTrack>(timeline, VfxTrackName) ??
            timeline.CreateTrack<SkillVfxTimelineTrack>(group, VfxTrackName);

        TimelineClip clip = track.CreateClip<SkillVfxTimelineClip>();
        clip.start = Math.Max(0d, startTime);
        clip.duration = 30d / definition.FrameRate;
        clip.displayName = vfxDefinition != null
            ? vfxDefinition.name
            : "VFX Clip";

        if (clip.asset is SkillVfxTimelineClip asset)
        {
            asset.Definition = vfxDefinition;
            EditorUtility.SetDirty(asset);
        }

        definition.UseExplicitVisualFxTracks = true;
        EditorUtility.SetDirty(definition);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return clip;
    }

    public static TimelineClip AddShaderFxClip(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        double startTime,
        SkillShaderEffectDefinition shaderDefinition)
    {
        if (definition == null || timeline == null)
            return null;

        GroupTrack group =
            FindTrack<GroupTrack>(timeline, VisualFxGroupName) ??
            timeline.CreateTrack<GroupTrack>(null, VisualFxGroupName);

        SkillShaderTimelineTrack track =
            FindTrack<SkillShaderTimelineTrack>(timeline, ShaderTrackName) ??
            timeline.CreateTrack<SkillShaderTimelineTrack>(group, ShaderTrackName);

        TimelineClip clip = track.CreateClip<SkillShaderTimelineClip>();
        clip.start = Math.Max(0d, startTime);
        clip.duration = 30d / definition.FrameRate;
        clip.displayName = shaderDefinition != null
            ? shaderDefinition.name
            : "Shader FX Clip";

        if (clip.asset is SkillShaderTimelineClip asset)
        {
            asset.Definition = shaderDefinition;
            EditorUtility.SetDirty(asset);
        }

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        return clip;
    }

    public static TimelineClip AddEventClip(
        SkillCutsceneDefinition definition,
        TimelineAsset timeline,
        double startTime,
        SkillCutsceneEventType eventType)
    {
        if (definition == null ||
            timeline == null)
        {
            return null;
        }

        SkillCutsceneEventTrack track =
            FindTrack<
                SkillCutsceneEventTrack>(
                    timeline,
                    "Battle Events") ??
            timeline.CreateTrack<
                SkillCutsceneEventTrack>(
                    null,
                    "Battle Events");

        TimelineClip clip =
            track.CreateClip<
                SkillCutsceneEventClip>();

        clip.start =
            Math.Max(
                0d,
                startTime);

        clip.duration =
            1d /
            definition.FrameRate;

        clip.displayName =
            eventType.ToString();

        SkillCutsceneEventClip asset =
            clip.asset as
                SkillCutsceneEventClip;

        if (asset != null)
        {
            asset.EventType =
                eventType;

            EditorUtility.SetDirty(
                asset);
        }

        EditorUtility.SetDirty(
            timeline);

        AssetDatabase.SaveAssets();

        return clip;
    }

    public static CinemachineCamera AddCameraToRig(
        SkillCutsceneDefinition definition,
        string desiredName)
    {
        if (definition?.CameraRigPrefab ==
            null)
        {
            return null;
        }

        string requestedName =
            string.IsNullOrWhiteSpace(
                desiredName)
                ? "CM_NewShot"
                : desiredName.Trim();

        string prefabPath =
            AssetDatabase.GetAssetPath(
                definition
                    .CameraRigPrefab);

        GameObject root =
            PrefabUtility
                .LoadPrefabContents(
                    prefabPath);

        try
        {
            Transform camerasRoot =
                root.transform.Find(
                    "Cameras");

            if (camerasRoot == null)
            {
                GameObject created =
                    new GameObject(
                        "Cameras");

                created.transform.SetParent(
                    root.transform,
                    false);

                camerasRoot =
                    created.transform;
            }

            string cameraName =
                MakeUniqueCameraName(
                    root,
                    requestedName);

            GameObject bindingObject =
                new GameObject(
                    cameraName +
                    "_Binding");

            bindingObject.transform.SetParent(
                camerasRoot,
                false);

            bindingObject.AddComponent<
                SkillCutsceneCameraBindingRoot>();

            bindingObject.transform
                .SetLocalPositionAndRotation(
                    new Vector3(
                        0f,
                        2f,
                        -5f),
                    Quaternion.identity);

            GameObject cameraObject =
                new GameObject(
                    cameraName);

            cameraObject.transform.SetParent(
                bindingObject.transform,
                false);

            CinemachineCamera camera =
                cameraObject.AddComponent<
                    CinemachineCamera>();

            camera.Priority =
                -10;

            cameraObject.AddComponent<
                Animator>();

            PrefabUtility.SaveAsPrefabAsset(
                root,
                prefabPath);
        }
        finally
        {
            PrefabUtility
                .UnloadPrefabContents(
                    root);
        }

        AssetDatabase.ImportAsset(
            prefabPath,
            ImportAssetOptions
                .ForceUpdate);

        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<
                GameObject>(
                    prefabPath);

        return prefab?
            .GetComponentsInChildren<
                CinemachineCamera>(true)
            .FirstOrDefault(
                camera =>
                    camera.name.StartsWith(
                        requestedName,
                        StringComparison
                            .OrdinalIgnoreCase));
    }

    private static TimelineAsset CreateTimeline(
        SkillCutsceneDefinition definition,
        SkillCutsceneSegment segment,
        string folder)
    {
        TimelineAsset timeline =
            ScriptableObject
                .CreateInstance<
                    TimelineAsset>();

        timeline.name =
            $"{definition.name}_" +
            $"{segment}_Timeline";

        string path =
            AssetDatabase
                .GenerateUniqueAssetPath(
                    $"{folder}/" +
                    $"{timeline.name}.playable");

        AssetDatabase.CreateAsset(
            timeline,
            path);

        return timeline;
    }

    private static GameObject CreateCameraRigPrefab(
        SkillCutsceneDefinition definition,
        string folder)
    {
        GameObject root =
            new GameObject(
                $"{definition.name}_" +
                "CameraRig");

        root.AddComponent<
            SkillCutsceneCameraRig>();

        GameObject cameras =
            new GameObject(
                "Cameras");

        cameras.transform.SetParent(
            root.transform,
            false);

        CreateCamera(
            cameras.transform,
            "CM_Overview",
            new Vector3(
                0f,
                2.5f,
                -7f));

        CreateCamera(
            cameras.transform,
            "CM_Follow",
            new Vector3(
                2.2f,
                1.8f,
                -4.2f));

        CreateCamera(
            cameras.transform,
            "CM_Impact",
            new Vector3(
                0.8f,
                1.6f,
                -2.4f));

        string prefabPath =
            AssetDatabase
                .GenerateUniqueAssetPath(
                    $"{folder}/" +
                    $"{root.name}.prefab");

        GameObject prefab =
            PrefabUtility
                .SaveAsPrefabAsset(
                    root,
                    prefabPath);

        UnityEngine.Object
            .DestroyImmediate(
                root);

        return prefab;
    }

    private static void CreateCamera(
        Transform parent,
        string name,
        Vector3 localPosition)
    {
        GameObject bindingObject =
            new GameObject(
                name +
                "_Binding");

        bindingObject.transform.SetParent(
            parent,
            false);

        bindingObject.AddComponent<
            SkillCutsceneCameraBindingRoot>();

        bindingObject.transform
            .localPosition =
                localPosition;

        bindingObject.transform
            .localRotation =
                Quaternion.LookRotation(
                    -localPosition
                        .normalized,
                    Vector3.up);

        GameObject cameraObject =
            new GameObject(
                name);

        cameraObject.transform.SetParent(
            bindingObject.transform,
            false);

        CinemachineCamera camera =
            cameraObject.AddComponent<
                CinemachineCamera>();

        camera.Priority =
            -10;

        cameraObject.AddComponent<
            Animator>();
    }

    private static void CreateDefaultCameraClips(
        SkillCutsceneDefinition definition,
        SkillCameraTimelineTrack track)
    {
        double fps =
            definition.FrameRate;

        TimelineClip overview =
            track.CreateClip<
                SkillCameraTimelineClip>();

        overview.start =
            0d;

        overview.duration =
            40d /
            fps;

        overview.displayName =
            "CM_Overview";

        SkillCameraTimelineClip
            overviewAsset =
                overview.asset as
                    SkillCameraTimelineClip;

        overviewAsset.CameraKey =
            "CM_Overview";

        overviewAsset.PositionBinding =
            SkillCameraPositionBinding
                .RigRootFixed;

        overviewAsset.AimBinding =
            SkillCameraAimBinding
                .AttackerTargetMidpoint;

        overviewAsset.CapturedPosition =
            new Vector3(
                0f,
                2.5f,
                -7f);

        TimelineClip follow =
            track.CreateClip<
                SkillCameraTimelineClip>();

        follow.start =
            34d /
            fps;

        follow.duration =
            50d /
            fps;

        follow.displayName =
            "CM_Follow";

        SkillCameraTimelineClip
            followAsset =
                follow.asset as
                    SkillCameraTimelineClip;

        followAsset.CameraKey =
            "CM_Follow";

        followAsset.PositionBinding =
            SkillCameraPositionBinding
                .AttackerVisualRoot;

        followAsset.AimBinding =
            SkillCameraAimBinding
                .TargetVisualRoot;

        followAsset.CapturedPosition =
            new Vector3(
                2.2f,
                1.8f,
                -4.2f);

        followAsset.PositionDamping =
            0.12f;

        followAsset.RotationDamping =
            0.08f;

        TimelineClip impact =
            track.CreateClip<
                SkillCameraTimelineClip>();

        impact.start =
            78d /
            fps;

        impact.duration =
            42d /
            fps;

        impact.displayName =
            "CM_Impact";

        SkillCameraTimelineClip
            impactAsset =
                impact.asset as
                    SkillCameraTimelineClip;

        impactAsset.CameraKey =
            "CM_Impact";

        impactAsset.PositionBinding =
            SkillCameraPositionBinding
                .CombatFrameFollow;

        impactAsset.AimBinding =
            SkillCameraAimBinding
                .TargetVisualRoot;

        impactAsset.CapturedPosition =
            new Vector3(
                0.8f,
                1.6f,
                -2.4f);

        EditorUtility.SetDirty(
            overviewAsset);

        EditorUtility.SetDirty(
            followAsset);

        EditorUtility.SetDirty(
            impactAsset);
    }

    private static bool ShouldCreateDefaultHit(
        SkillCutsceneDefinition definition)
    {
        if (definition == null)
            return true;

        string[] guids =
            AssetDatabase.FindAssets(
                "t:SkillDefinition");

        foreach (string guid in guids)
        {
            SkillDefinition skill =
                AssetDatabase.LoadAssetAtPath<
                    SkillDefinition>(
                        AssetDatabase.GUIDToAssetPath(
                            guid));

            if (skill?.VisualDefinition?
                    .CutsceneDefinition !=
                definition)
            {
                continue;
            }

            return skill.VisualDefinition
                .HasHitFrameDamage;
        }

        return true;
    }

    private static void CreateHitEventClip(
        SkillCutsceneDefinition definition,
        SkillCutsceneEventTrack track,
        double startTime)
    {
        TimelineClip clip =
            track.CreateClip<
                SkillCutsceneEventClip>();

        clip.start =
            startTime;

        clip.duration =
            1d /
            definition.FrameRate;

        clip.displayName =
            "Hit";

        SkillCutsceneEventClip asset =
            clip.asset as
                SkillCutsceneEventClip;

        asset.EventType =
            SkillCutsceneEventType.Hit;

        asset.HitIndex =
            -1;

        EditorUtility.SetDirty(
            asset);
    }

    private static T FindTrack<T>(
        TimelineAsset timeline,
        string name)
        where T : TrackAsset
    {
        if (timeline == null)
            return null;

        return SkillTimelineTrackUtility
            .EnumerateAllTracks(timeline)
            .OfType<T>()
            .FirstOrDefault(
                track =>
                    string.Equals(
                        track.name,
                        name,
                        StringComparison
                            .OrdinalIgnoreCase));
    }

    private static GameObject
        ResolvePersistentCharacterPrefabRoot(
            Character character,
            string role)
    {
        if (character == null)
        {
            throw new ArgumentNullException(
                role,
                $"Preview {role} Character 참조가 없거나 이미 파괴되었습니다.");
        }

        string path =
            AssetDatabase.GetAssetPath(
                character);

        if (string.IsNullOrWhiteSpace(path))
        {
            path =
                AssetDatabase.GetAssetPath(
                    character.gameObject);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                $"Preview {role}는 저장된 Character Prefab이어야 합니다. " +
                $"Character={character.name}");
        }

        GameObject prefabRoot =
            AssetDatabase.LoadAssetAtPath<
                GameObject>(
                    path);

        if (prefabRoot == null)
        {
            throw new InvalidOperationException(
                $"Preview {role} Prefab을 다시 읽지 못했습니다: {path}");
        }

        Character reloadedCharacter =
            prefabRoot.GetComponentInChildren<
                Character>(
                    true);

        if (reloadedCharacter == null)
        {
            throw new InvalidOperationException(
                $"Preview {role} Prefab에 Character 컴포넌트가 없습니다: {path}");
        }

        return prefabRoot;
    }

    private static void FaceEachOther(
        Transform attacker,
        Transform target)
    {
        if (attacker == null ||
            target == null)
        {
            return;
        }

        Vector3 direction =
            target.position -
            attacker.position;

        direction.y =
            0f;

        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return;
        }

        attacker.rotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);

        target.rotation =
            Quaternion.LookRotation(
                -direction.normalized,
                Vector3.up);
    }

    private static string BuildCutsceneFolder(
        string skillPath,
        string skillName)
    {
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
                "Assets/2. Data";
        }

        return
            $"{parent}/Cutscenes/" +
            $"{Sanitize(skillName)}";
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

        string current =
            parts[0];

        for (int index = 1;
             index < parts.Length;
             index++)
        {
            string next =
                $"{current}/" +
                $"{parts[index]}";

            if (!AssetDatabase
                .IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[index]);
            }

            current =
                next;
        }
    }

    private static string MakeUniqueCameraName(
        GameObject root,
        string baseName)
    {
        string candidate =
            baseName;

        int suffix =
            1;

        CinemachineCamera[] cameras =
            root != null
                ? root.GetComponentsInChildren<
                    CinemachineCamera>(
                        true)
                : Array.Empty<
                    CinemachineCamera>();

        while (cameras.Any(
                   camera =>
                       camera != null &&
                       string.Equals(
                           camera.name,
                           candidate,
                           StringComparison
                               .OrdinalIgnoreCase)))
        {
            candidate =
                $"{baseName}_{suffix++:00}";
        }

        return candidate;
    }

    private static string Sanitize(
        string value)
    {
        string safe =
            string.IsNullOrWhiteSpace(
                value)
                ? "Skill"
                : value.Trim();

        foreach (char invalid
                 in Path
                     .GetInvalidFileNameChars())
        {
            safe =
                safe.Replace(
                    invalid,
                    '_');
        }

        return safe
            .Replace(
                '/',
                '_')
            .Replace(
                '\\',
                '_');
    }
}
#endif
