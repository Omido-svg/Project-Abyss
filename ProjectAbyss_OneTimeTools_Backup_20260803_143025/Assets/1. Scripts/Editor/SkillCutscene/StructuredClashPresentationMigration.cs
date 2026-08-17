#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// 기존 Timeline-only 스킬 연출을 다음 책임 구조로 마이그레이션합니다.
///
/// - 합 접근/대치/승패/재정렬: CharacterPresentationProfile
/// - 합 승리 후 실제 공격: 기존 ClashAttack Timeline
/// - 타깃 피격: 타깃 자신의 CharacterPresentationProfile
///
/// 기존 PartBreak / Kill / Return Timeline 참조는 데이터 손실 방지를 위해 보존하지만
/// v8 런타임에서는 재생하지 않습니다. 고정 Target Animation Track은 모두 제거합니다.
/// </summary>
public static class StructuredClashPresentationMigration
{
    private const string ApplyMenu =
        "Tools/Project Abyss/Migration/Apply Structured Clash Presentation";

    private const string ValidateMenu =
        "Tools/Project Abyss/Migration/Validate Structured Clash Presentation";

    [MenuItem(ApplyMenu, false, 2100)]
    public static void Apply()
    {
        MigrationStats stats =
            new MigrationStats();

        try
        {
            HashSet<SkillVisualDefinition> profileFallbackDefinitions =
                CollectProfileFallbackDefinitions();

            // Character Prefab이 아직 조립되지 않은 프로젝트도 지원해야 하므로
            // Bundle 자체에서 Presentation Profile을 먼저 생성한다.
            ProcessCharacterBundles(stats);
            ProcessRemainingCharacterPrefabs(stats);
            ProcessLoadedSceneCharacterViews(stats);
            ProcessSkillVisualDefinitions(
                stats,
                profileFallbackDefinitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceUpdate);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            stats.Errors++;
        }

        Debug.Log(
            stats.BuildReport(
                "STRUCTURED CLASH MIGRATION"));
    }

    [MenuItem(ValidateMenu, false, 2101)]
    public static void Validate()
    {
        MigrationStats stats =
            new MigrationStats();

        HashSet<SkillVisualDefinition> profileFallbackDefinitions =
            CollectProfileFallbackDefinitions();

        ValidateCharacterBundles(stats);
        ValidateCharacterPrefabs(stats);
        ValidateLoadedSceneCharacterViews(stats);
        ValidateSkillVisualDefinitions(
            stats,
            profileFallbackDefinitions);

        Debug.Log(
            stats.BuildReport(
                "STRUCTURED CLASH VALIDATION"));
    }

    private static void ValidateCharacterBundles(
        MigrationStats stats)
    {
        string[] bundleGuids =
            AssetDatabase.FindAssets(
                "t:CharacterAuthoringBundle",
                new[] { "Assets/2. Data" });

        foreach (string guid in bundleGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<
                    CharacterAuthoringBundle>(path);

            if (bundle == null)
                continue;

            stats.BundlesScanned++;

            if (bundle.PresentationProfile != null)
                continue;

            stats.Errors++;
            Debug.LogError(
                "[Structured Clash Validation] " +
                $"CharacterAuthoringBundle에 PresentationProfile이 없습니다: {path}",
                bundle);
        }
    }

    private static void ValidateCharacterPrefabs(
        MigrationStats stats)
    {
        string[] prefabGuids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets" });

        foreach (string guid in prefabGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);

            if (prefab == null)
                continue;

            CharacterView[] views =
                prefab.GetComponentsInChildren<CharacterView>(
                    true);

            foreach (CharacterView view in views)
            {
                ValidateCharacterView(
                    view,
                    path,
                    stats,
                    isLoadedSceneView: false);
            }
        }
    }

    private static void ValidateLoadedSceneCharacterViews(
        MigrationStats stats)
    {
        foreach (CharacterView view in
                 EnumerateLoadedSceneCharacterViews())
        {
            string scenePath =
                view.gameObject.scene.path;

            string context =
                string.IsNullOrWhiteSpace(scenePath)
                    ? $"Unsaved Scene/{GetHierarchyPath(view.transform)}"
                    : scenePath + "/" +
                      GetHierarchyPath(view.transform);

            ValidateCharacterView(
                view,
                context,
                stats,
                isLoadedSceneView: true);
        }
    }

    private static void ValidateCharacterView(
        CharacterView view,
        string context,
        MigrationStats stats,
        bool isLoadedSceneView)
    {
        if (view == null)
            return;

        stats.CharacterViewsScanned++;

        if (isLoadedSceneView)
            stats.LoadedSceneViewsScanned++;

        CharacterPresentationProfile profile =
            ResolveEffectiveProfile(view);

        if (profile != null)
            return;

        stats.Errors++;
        Debug.LogError(
            "[Structured Clash Validation] " +
            $"CharacterPresentationProfile 없음: {context}",
            view);
    }

    private static void ValidateSkillVisualDefinitions(
        MigrationStats stats,
        HashSet<SkillVisualDefinition> profileFallbackDefinitions)
    {
        string[] visualGuids =
            AssetDatabase.FindAssets(
                "t:SkillVisualDefinition",
                new[] { "Assets/2. Data" });

        foreach (string guid in visualGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            SkillVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<
                    SkillVisualDefinition>(path);

            if (visual == null)
                continue;

            stats.VisualDefinitionsScanned++;

            bool isReferencedByProfile =
                profileFallbackDefinitions != null &&
                profileFallbackDefinitions.Contains(visual);

            if (isReferencedByProfile &&
                !visual.AllowAsProfileFallback)
            {
                stats.Errors++;
                Debug.LogError(
                    "[Structured Clash Validation] " +
                    $"SkillVisualProfile에서 폴백으로 참조하지만 AllowAsProfileFallback이 꺼져 있습니다: {path}",
                    visual);
            }

            bool isPureFallbackTemplate =
                IsPureFallbackTemplate(
                    visual,
                    profileFallbackDefinitions);

            if (isPureFallbackTemplate)
            {
                // Duel Visual / Normal Attack Visual 같은 공통 Profile Fallback은
                // 카메라나 Timeline을 직접 소유하지 않는 설정 템플릿이다.
                stats.FallbackVisualTemplatesSkipped++;
            }
            else if (!visual.HasCompleteTimelineSet)
            {
                stats.Errors++;
                Debug.LogError(
                    "[Structured Clash Validation] " +
                    $"Action/ClashAttack Timeline 또는 Camera Rig가 없습니다: {path}",
                    visual);
            }

            if (visual.HasHitFrameDamage &&
                visual.TargetReaction == HitReactionKey.None)
            {
                stats.Errors++;
                Debug.LogError(
                    "[Structured Clash Validation] " +
                    $"피해 스킬의 TargetReaction이 None입니다: {path}",
                    visual);
            }

            foreach (TimelineAsset timeline in
                     EnumerateAllTimelines(visual))
            {
                int targetTrackCount =
                    CountTargetAnimationTracks(
                        timeline);

                if (targetTrackCount <= 0)
                    continue;

                stats.Errors += targetTrackCount;

                Debug.LogError(
                    "[Structured Clash Validation] " +
                    $"고정 Target Animation Track이 남아 있습니다: {AssetDatabase.GetAssetPath(timeline)}",
                    timeline);
            }
        }
    }

    private static bool IsPureFallbackTemplate(
        SkillVisualDefinition visual,
        HashSet<SkillVisualDefinition> profileFallbackDefinitions)
    {
        if (visual == null)
            return false;

        bool isDeclaredProfileFallback =
            visual.AllowAsProfileFallback ||
            (profileFallbackDefinitions != null &&
             profileFallbackDefinitions.Contains(visual));

        if (!isDeclaredProfileFallback)
            return false;

        bool hasAnyActiveCutsceneReference =
            visual.ActionTimeline != null ||
            visual.ClashAttackTimeline != null ||
            visual.CameraRigPrefab != null;

        return !hasAnyActiveCutsceneReference;
    }

    private static HashSet<SkillVisualDefinition>
        CollectProfileFallbackDefinitions()
    {
        HashSet<SkillVisualDefinition> result =
            new HashSet<SkillVisualDefinition>();

        string[] profileGuids =
            AssetDatabase.FindAssets(
                "t:SkillVisualProfile",
                new[] { "Assets/2. Data" });

        foreach (string guid in profileGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            SkillVisualProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    SkillVisualProfile>(path);

            if (profile == null)
                continue;

            foreach (SkillVisualDefinition definition in
                     profile.EnumerateDefinitions())
            {
                if (definition != null)
                    result.Add(definition);
            }
        }

        return result;
    }

    private static void ProcessCharacterBundles(
        MigrationStats stats)
    {
        string[] bundleGuids =
            AssetDatabase.FindAssets(
                "t:CharacterAuthoringBundle",
                new[] { "Assets/2. Data" });

        foreach (string guid in bundleGuids)
        {
            string bundlePath =
                AssetDatabase.GUIDToAssetPath(guid);

            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<
                    CharacterAuthoringBundle>(bundlePath);

            if (bundle == null)
                continue;

            stats.BundlesScanned++;

            CharacterPresentationProfile profile =
                EnsureProfileForBundle(
                    bundlePath,
                    bundle,
                    stats);

            if (profile == null)
            {
                stats.Errors++;
                Debug.LogError(
                    "[Structured Clash Migration] " +
                    $"Bundle PresentationProfile을 만들 수 없습니다: {bundlePath}",
                    bundle);
                continue;
            }

            if (bundle.PresentationProfile != profile)
            {
                bundle.ConfigurePresentationProfile(
                    profile);

                EditorUtility.SetDirty(bundle);
                stats.BundlesUpdated++;
            }

            if (bundle.CharacterPrefab == null)
                continue;

            string prefabPath =
                AssetDatabase.GetAssetPath(
                    bundle.CharacterPrefab.gameObject);

            EnsureProfileForPrefab(
                prefabPath,
                stats,
                profile);
        }
    }

    private static CharacterPresentationProfile
        EnsureProfileForBundle(
            string bundlePath,
            CharacterAuthoringBundle bundle,
            MigrationStats stats)
    {
        if (bundle == null)
            return null;

        CharacterPresentationProfile profile =
            bundle.PresentationProfile;

        if (profile == null)
        {
            string characterRootFolder =
                ResolveCharacterRootFolder(
                    bundlePath);

            string baseName =
                ResolveBundleProfileBaseName(
                    bundle);

            profile =
                FindOrCreateProfileAtFolder(
                    characterRootFolder,
                    baseName,
                    stats);
        }

        PopulateProfileFromController(
            profile,
            bundle.AnimatorController);

        return profile;
    }

    private static string ResolveCharacterRootFolder(
        string bundlePath)
    {
        string bundleFolder =
            Path.GetDirectoryName(bundlePath)?
                .Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(bundleFolder))
            return "Assets/2. Data/Characters";

        string leaf =
            Path.GetFileName(bundleFolder);

        if (!string.Equals(
                leaf,
                "Authoring",
                StringComparison.OrdinalIgnoreCase))
        {
            return bundleFolder;
        }

        string parent =
            Path.GetDirectoryName(bundleFolder)?
                .Replace('\\', '/');

        return string.IsNullOrWhiteSpace(parent)
            ? bundleFolder
            : parent;
    }

    private static string ResolveBundleProfileBaseName(
        CharacterAuthoringBundle bundle)
    {
        string value =
            !string.IsNullOrWhiteSpace(bundle?.DisplayName)
                ? bundle.DisplayName
                : bundle?.name;

        if (string.IsNullOrWhiteSpace(value))
            return "Character";

        string[] suffixes =
        {
            " Character Bundle",
            "Character Bundle",
            " Bundle"
        };

        foreach (string suffix in suffixes)
        {
            if (!value.EndsWith(
                    suffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value =
                value.Substring(
                    0,
                    value.Length - suffix.Length)
                    .Trim();
            break;
        }

        return value;
    }

    private static void ProcessRemainingCharacterPrefabs(
        MigrationStats stats)
    {
        string[] prefabGuids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets" });

        foreach (string guid in prefabGuids)
        {
            string prefabPath =
                AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);

            if (prefab == null ||
                prefab.GetComponentInChildren<CharacterView>(
                    true) == null)
            {
                continue;
            }

            EnsureProfileForPrefab(
                prefabPath,
                stats,
                preferredProfile: null);
        }
    }

    private static CharacterPresentationProfile
        EnsureProfileForPrefab(
            string prefabPath,
            MigrationStats stats,
            CharacterPresentationProfile preferredProfile)
    {
        if (string.IsNullOrWhiteSpace(prefabPath) ||
            !prefabPath.EndsWith(
                ".prefab",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        GameObject root =
            PrefabUtility.LoadPrefabContents(
                prefabPath);

        if (root == null)
            return null;

        CharacterPresentationProfile result =
            null;

        bool changed = false;

        try
        {
            CharacterView[] views =
                root.GetComponentsInChildren<CharacterView>(
                    true);

            if (views.Length == 0)
                return null;

            foreach (CharacterView view in views)
            {
                stats.CharacterViewsScanned++;

                CharacterPresentationProfile profile =
                    view.PresentationProfile ??
                    ResolveLinkedBundleProfile(view) ??
                    preferredProfile;

                if (profile == null)
                {
                    profile =
                        FindOrCreateProfileForPrefab(
                            prefabPath,
                            root.name,
                            stats);
                }

                if (profile == null)
                    continue;

                PopulateProfileFromAnimator(
                    profile,
                    view.Animator);

                if (view.PresentationProfile != profile)
                {
                    view.ConfigurePresentationProfile(
                        profile);

                    EditorUtility.SetDirty(view);
                    changed = true;
                    stats.CharacterViewsUpdated++;
                }

                result ??= profile;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);

                stats.PrefabsUpdated++;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(
                root);
        }

        return result;
    }

    private static void ProcessLoadedSceneCharacterViews(
        MigrationStats stats)
    {
        HashSet<int> dirtiedSceneHandles =
            new HashSet<int>();

        foreach (CharacterView view in
                 EnumerateLoadedSceneCharacterViews())
        {
            stats.CharacterViewsScanned++;
            stats.LoadedSceneViewsScanned++;

            CharacterPresentationProfile profile =
                ResolveEffectiveProfile(view);

            if (profile == null)
            {
                profile =
                    FindOrCreateProfileForSceneView(
                        view,
                        stats);
            }

            if (profile == null)
            {
                stats.Errors++;
                Debug.LogError(
                    "[Structured Clash Migration] " +
                    $"Scene CharacterView의 PresentationProfile을 만들 수 없습니다: {GetHierarchyPath(view.transform)}",
                    view);
                continue;
            }

            PopulateProfileFromAnimator(
                profile,
                view.Animator);

            if (view.PresentationProfile == profile)
                continue;

            view.ConfigurePresentationProfile(
                profile);

            EditorUtility.SetDirty(view);
            stats.CharacterViewsUpdated++;

            Scene scene =
                view.gameObject.scene;

            if (!scene.IsValid() ||
                !scene.isLoaded)
            {
                continue;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            if (dirtiedSceneHandles.Add(scene.handle))
                stats.ScenesMarkedDirty++;
        }
    }

    private static IEnumerable<CharacterView>
        EnumerateLoadedSceneCharacterViews()
    {
        return Resources
            .FindObjectsOfTypeAll<CharacterView>()
            .Where(view =>
                view != null &&
                !EditorUtility.IsPersistent(view) &&
                view.gameObject.scene.IsValid() &&
                view.gameObject.scene.isLoaded)
            .OrderBy(view =>
                view.gameObject.scene.path,
                StringComparer.Ordinal)
            .ThenBy(view =>
                GetHierarchyPath(view.transform),
                StringComparer.Ordinal);
    }

    private static CharacterPresentationProfile
        ResolveEffectiveProfile(
            CharacterView view)
    {
        if (view == null)
            return null;

        return view.PresentationProfile ??
               ResolveLinkedBundleProfile(view);
    }

    private static CharacterPresentationProfile
        ResolveLinkedBundleProfile(
            CharacterView view)
    {
        if (view == null)
            return null;

        CharacterAuthoringLink link =
            view.GetComponent<CharacterAuthoringLink>() ??
            view.GetComponentInParent<CharacterAuthoringLink>();

        return link?.Bundle?.PresentationProfile;
    }

    private static CharacterPresentationProfile
        FindOrCreateProfileForSceneView(
            CharacterView view,
            MigrationStats stats)
    {
        if (view == null)
            return null;

        Scene scene =
            view.gameObject.scene;

        string scenePath =
            scene.path;

        string sceneFolder =
            Path.GetDirectoryName(scenePath)?
                .Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(sceneFolder))
            sceneFolder = "Assets/2. Data/Characters";

        return FindOrCreateProfileAtFolder(
            sceneFolder,
            view.name,
            stats);
    }

    private static CharacterPresentationProfile
        FindOrCreateProfileForPrefab(
            string prefabPath,
            string prefabName,
            MigrationStats stats)
    {
        string prefabFolder =
            Path.GetDirectoryName(prefabPath)?
                .Replace('\\', '/');

        return FindOrCreateProfileAtFolder(
            prefabFolder,
            prefabName,
            stats);
    }

    private static CharacterPresentationProfile
        FindOrCreateProfileAtFolder(
            string ownerFolder,
            string baseName,
            MigrationStats stats)
    {
        if (string.IsNullOrWhiteSpace(ownerFolder))
            return null;

        string presentationFolder =
            ownerFolder + "/Presentation";

        EnsureFolder(
            presentationFolder);

        string safeName =
            SanitizeFileName(baseName);

        string profilePath =
            presentationFolder + "/" +
            safeName +
            "_Presentation.asset";

        CharacterPresentationProfile profile =
            AssetDatabase.LoadAssetAtPath<
                CharacterPresentationProfile>(
                    profilePath);

        if (profile != null)
            return profile;

        profile =
            ScriptableObject.CreateInstance<
                CharacterPresentationProfile>();

        profile.name =
            safeName +
            "_Presentation";

        AssetDatabase.CreateAsset(
            profile,
            profilePath);

        stats.ProfilesCreated++;

        return profile;
    }

    private static void PopulateProfileFromAnimator(
        CharacterPresentationProfile profile,
        Animator animator)
    {
        PopulateProfileFromController(
            profile,
            animator?.runtimeAnimatorController);
    }

    private static void PopulateProfileFromController(
        CharacterPresentationProfile profile,
        RuntimeAnimatorController controller)
    {
        if (profile == null ||
            controller == null)
        {
            return;
        }

        string controllerPath =
            AssetDatabase.GetAssetPath(
                controller);

        if (string.IsNullOrWhiteSpace(controllerPath))
            return;

        List<AnimationClip> clips =
            AssetDatabase.GetDependencies(
                    controllerPath,
                    recursive: true)
                .SelectMany(path =>
                    AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<AnimationClip>())
                .Where(clip => clip != null)
                .Distinct()
                .ToList();

        AnimationClip idle =
            FindClip(
                clips,
                "_Idle",
                "Idle");

        AnimationClip hit =
            FindClip(
                clips,
                "HitMotion",
                "_Hit",
                "Hit");

        AnimationClip broken =
            FindClip(
                clips,
                "Broken",
                "Break");

        AnimationClip death =
            FindClip(
                clips,
                "Death",
                "Dead",
                "Die");

        bool changed = false;

        changed |= AssignIfNull(
            ref profile.ClashEnter,
            idle);

        changed |= AssignIfNull(
            ref profile.ClashContest,
            idle);

        changed |= AssignIfNull(
            ref profile.ClashAdvantage,
            idle);

        changed |= AssignIfNull(
            ref profile.ClashDisadvantage,
            hit);

        changed |= AssignIfNull(
            ref profile.ClashTie,
            hit);

        changed |= AssignIfNull(
            ref profile.ClashReengage,
            idle);

        changed |= AssignIfNull(
            ref profile.ClashExit,
            idle);

        changed |= AssignIfNull(
            ref profile.LightHit,
            hit);

        changed |= AssignIfNull(
            ref profile.HeavyHit,
            hit);

        changed |= AssignIfNull(
            ref profile.PartBreak,
            broken ?? hit);

        changed |= AssignIfNull(
            ref profile.Death,
            death);

        if (!changed)
            return;

        EditorUtility.SetDirty(profile);
    }

    private static bool AssignIfNull(
        ref AnimationClip target,
        AnimationClip value)
    {
        if (target != null ||
            value == null)
        {
            return false;
        }

        target = value;
        return true;
    }

    private static AnimationClip FindClip(
        IReadOnlyList<AnimationClip> clips,
        params string[] tokens)
    {
        if (clips == null ||
            tokens == null)
        {
            return null;
        }

        foreach (string token in tokens)
        {
            AnimationClip exact =
                clips.FirstOrDefault(clip =>
                    string.Equals(
                        clip.name,
                        token,
                        StringComparison.OrdinalIgnoreCase));

            if (exact != null)
                return exact;
        }

        foreach (string token in tokens)
        {
            AnimationClip partial =
                clips.FirstOrDefault(clip =>
                    clip.name.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0);

            if (partial != null)
                return partial;
        }

        return null;
    }

    private static void ProcessSkillVisualDefinitions(
        MigrationStats stats,
        HashSet<SkillVisualDefinition> profileFallbackDefinitions)
    {
        string[] visualGuids =
            AssetDatabase.FindAssets(
                "t:SkillVisualDefinition",
                new[] { "Assets/2. Data" });

        foreach (string guid in visualGuids)
        {
            string visualPath =
                AssetDatabase.GUIDToAssetPath(guid);

            SkillVisualDefinition visual =
                AssetDatabase.LoadAssetAtPath<
                    SkillVisualDefinition>(
                        visualPath);

            if (visual == null)
                continue;

            stats.VisualDefinitionsScanned++;

            bool isReferencedByProfile =
                profileFallbackDefinitions != null &&
                profileFallbackDefinitions.Contains(visual);

            if (isReferencedByProfile &&
                !visual.AllowAsProfileFallback)
            {
                visual.AllowAsProfileFallback = true;
                EditorUtility.SetDirty(visual);
                stats.ProfileFallbackFlagsEnabled++;
                stats.VisualDefinitionsUpdated++;
            }

            if (IsPureFallbackTemplate(
                    visual,
                    profileFallbackDefinitions))
            {
                stats.FallbackVisualTemplatesSkipped++;
            }

            HitReactionKey reaction =
                GuessReaction(visual);

            if (visual.TargetReaction != reaction)
            {
                visual.TargetReaction = reaction;
                EditorUtility.SetDirty(visual);
                stats.VisualDefinitionsUpdated++;
            }

            SerializedObject serialized =
                new SerializedObject(visual);

            serialized.Update();

            SerializedProperty legacyTarget =
                serialized.FindProperty(
                    "TargetAnimation");

            if (legacyTarget != null &&
                legacyTarget.objectReferenceValue != null)
            {
                legacyTarget.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                stats.LegacyTargetReferencesRemoved++;
            }

            foreach (TimelineAsset timeline in
                     EnumerateAllTimelines(visual))
            {
                stats.TargetAnimationTracksRemoved +=
                    RemoveTargetAnimationTracks(
                        timeline);
            }

            visual.MarkPresentationSchemaVersion(
                8);
        }
    }

    private static HitReactionKey GuessReaction(
        SkillVisualDefinition visual)
    {
        if (visual == null ||
            !visual.HasHitFrameDamage)
        {
            return HitReactionKey.None;
        }

        string name =
            visual.name ??
            string.Empty;

        if (name.IndexOf(
                "일반공격",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf(
                "Normal",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return HitReactionKey.LightHit;
        }

        return HitReactionKey.HeavyHit;
    }

    private static IEnumerable<TimelineAsset>
        EnumerateAllTimelines(
            SkillVisualDefinition visual)
    {
        if (visual == null)
            yield break;

        HashSet<TimelineAsset> visited =
            new HashSet<TimelineAsset>();

        foreach (SkillCutsceneSegment segment in
                 SkillCutsceneSegmentUtility
                     .EnumerateActiveAttackerSegments())
        {
            TimelineAsset timeline =
                visual.GetTimeline(segment);

            if (timeline != null &&
                visited.Add(timeline))
            {
                yield return timeline;
            }
        }

        foreach (TimelineAsset timeline in
                 visual.EnumerateLegacyTimelines())
        {
            if (timeline != null &&
                visited.Add(timeline))
            {
                yield return timeline;
            }
        }
    }

    private static int RemoveTargetAnimationTracks(
        TimelineAsset timeline)
    {
        if (timeline == null)
            return 0;

        AnimationTrack[] tracks =
            SkillTimelineTrackUtility
                .EnumerateAllTracks(timeline)
                .OfType<AnimationTrack>()
                .Where(track =>
                    string.Equals(
                        track.name,
                        SkillCutsceneTimelineBinder
                            .TargetAnimationTrackName,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        foreach (AnimationTrack track in tracks)
        {
            timeline.DeleteTrack(track);
        }

        if (tracks.Length > 0)
            EditorUtility.SetDirty(timeline);

        return tracks.Length;
    }

    private static int CountTargetAnimationTracks(
        TimelineAsset timeline)
    {
        if (timeline == null)
            return 0;

        return SkillTimelineTrackUtility
            .EnumerateAllTracks(timeline)
            .OfType<AnimationTrack>()
            .Count(track =>
                string.Equals(
                    track.name,
                    SkillCutsceneTimelineBinder
                        .TargetAnimationTrackName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string GetHierarchyPath(
        Transform transform)
    {
        if (transform == null)
            return "NULL";

        Stack<string> names =
            new Stack<string>();

        Transform current =
            transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static void EnsureFolder(
        string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent =
            Path.GetDirectoryName(folder)?
                .Replace('\\', '/');

        string name =
            Path.GetFileName(folder);

        if (string.IsNullOrWhiteSpace(parent) ||
            string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static string SanitizeFileName(
        string value)
    {
        string result =
            string.IsNullOrWhiteSpace(value)
                ? "Character"
                : value.Trim();

        foreach (char invalid in
                 Path.GetInvalidFileNameChars())
        {
            result = result.Replace(
                invalid,
                '_');
        }

        return result;
    }

    private sealed class MigrationStats
    {
        public int BundlesScanned;
        public int CharacterViewsScanned;
        public int LoadedSceneViewsScanned;
        public int CharacterViewsUpdated;
        public int PrefabsUpdated;
        public int ScenesMarkedDirty;
        public int ProfilesCreated;
        public int BundlesUpdated;
        public int VisualDefinitionsScanned;
        public int VisualDefinitionsUpdated;
        public int FallbackVisualTemplatesSkipped;
        public int ProfileFallbackFlagsEnabled;
        public int LegacyTargetReferencesRemoved;
        public int TargetAnimationTracksRemoved;
        public int Errors;

        public string BuildReport(
            string title)
        {
            return
                $"=== {title} ===\n" +
                $"Character Bundles Scanned: {BundlesScanned}\n" +
                $"Character Views Scanned: {CharacterViewsScanned}\n" +
                $"Loaded Scene Views Scanned: {LoadedSceneViewsScanned}\n" +
                $"Character Views Updated: {CharacterViewsUpdated}\n" +
                $"Prefabs Updated: {PrefabsUpdated}\n" +
                $"Scenes Marked Dirty: {ScenesMarkedDirty}\n" +
                $"Presentation Profiles Created: {ProfilesCreated}\n" +
                $"Bundles Updated: {BundlesUpdated}\n" +
                $"Skill Visuals Scanned: {VisualDefinitionsScanned}\n" +
                $"Skill Visuals Updated: {VisualDefinitionsUpdated}\n" +
                $"Fallback Visual Templates Skipped: {FallbackVisualTemplatesSkipped}\n" +
                $"Profile Fallback Flags Enabled: {ProfileFallbackFlagsEnabled}\n" +
                $"Legacy Target References Removed: {LegacyTargetReferencesRemoved}\n" +
                $"Target Animation Tracks Removed: {TargetAnimationTracksRemoved}\n" +
                $"Errors: {Errors}\n" +
                (Errors == 0
                    ? "RESULT: READY"
                    : "RESULT: INCOMPLETE");
        }
    }
}
#endif
