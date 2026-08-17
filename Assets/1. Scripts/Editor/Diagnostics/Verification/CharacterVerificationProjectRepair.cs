#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Character Verification 실행 전에 Bundle, Prefab, Animator, Presentation 참조를
/// 실제 프로젝트 Asset 기준으로 복구한다. 검증 결과를 억지로 PASS 처리하지 않고,
/// 누락된 직렬화 참조와 테스트 Animator 단절만 수리한다.
/// </summary>
public static class CharacterVerificationProjectRepair
{
    private const string TestControllerPath =
        "Assets/2. Data/TestEncounters/Animation/TestCharacter.controller";

    private const string TestPresentationPath =
        "Assets/2. Data/TestEncounters/Animation/TestCharacterPresentation.asset";

    [MenuItem(
        "Tools/Project Abyss/Character Verification/Repair All Verification Dependencies")]
    public static void RepairAllMenu()
    {
        CharacterVerificationRepairSummary summary =
            RepairAll(logResult: true);

        CharacterVerificationProfileBuilder
            .CreateOrRefreshAllProfiles();

        EditorUtility.DisplayDialog(
            "Character Verification 복구",
            summary.BuildSummary(),
            "확인");
    }

    public static CharacterVerificationRepairSummary RepairAll(
        bool logResult)
    {
        CharacterVerificationRepairSummary summary =
            new CharacterVerificationRepairSummary();

        string[] bundleGuids =
            AssetDatabase.FindAssets(
                "t:CharacterAuthoringBundle");

        for (int i = 0; i < bundleGuids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    bundleGuids[i]);

            CharacterAuthoringBundle bundle =
                AssetDatabase.LoadAssetAtPath<
                    CharacterAuthoringBundle>(path);

            if (bundle == null)
                continue;

            summary.CheckedBundles++;

            if (RepairBundle(
                    bundle,
                    out CharacterVerificationBundleRepairResult result,
                    saveAssets: false))
            {
                if (result.Changed)
                    summary.RepairedBundles++;
                else
                    summary.AlreadyValidBundles++;
            }
            else
            {
                summary.UnresolvedBundles++;
            }

            summary.Results.Add(result);

            if (!logResult)
                continue;

            if (result.Success)
            {
                Debug.Log(
                    "[CharacterVerification] Dependency 복구 / " +
                    result.BuildLine(),
                    bundle);
            }
            else
            {
                Debug.LogWarning(
                    "[CharacterVerification] Dependency 복구 실패 / " +
                    result.BuildLine(),
                    bundle);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (logResult)
        {
            Debug.Log(
                "[CharacterVerification] 전체 Dependency 복구 완료 / " +
                summary.BuildSummary());
        }

        return summary;
    }

    public static bool RepairBundle(
        CharacterAuthoringBundle bundle,
        out CharacterVerificationBundleRepairResult result,
        bool saveAssets = true)
    {
        result = new CharacterVerificationBundleRepairResult
        {
            BundleName = bundle != null
                ? bundle.name
                : "NULL"
        };

        if (bundle == null)
        {
            result.Detail = "Bundle이 없습니다.";
            return false;
        }

        bool changed = false;

        Character beforePrefab =
            bundle.CharacterPrefab;

        if (!CharacterVerificationPrefabResolver.TryRepair(
                bundle,
                out Character prefab,
                out string prefabDetail,
                saveAssets: false))
        {
            result.Detail = prefabDetail;
            return false;
        }

        if (beforePrefab != prefab)
        {
            changed = true;
            result.PrefabRepaired = true;
        }

        string prefabPath =
            AssetDatabase.GetAssetPath(prefab);

        if (string.IsNullOrWhiteSpace(prefabPath) ||
            !prefabPath.EndsWith(
                ".prefab",
                StringComparison.OrdinalIgnoreCase))
        {
            result.Detail =
                "연결된 Character가 Prefab Asset이 아닙니다: " +
                prefabPath;
            return false;
        }

        GameObject prefabAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);

        Animator prefabAnimator =
            prefabAsset != null
                ? prefabAsset.GetComponentInChildren<Animator>(true)
                : null;

        CharacterView prefabView =
            prefabAsset != null
                ? prefabAsset.GetComponentInChildren<CharacterView>(true)
                : null;

        RuntimeAnimatorController controller =
            bundle.AnimatorController ??
            prefabAnimator?.runtimeAnimatorController ??
            ResolveFallbackController(bundle);

        CharacterPresentationProfile presentation =
            bundle.PresentationProfile ??
            prefabView?.PresentationProfile ??
            ResolveFallbackPresentation(bundle);

        Avatar avatar =
            bundle.Avatar ??
            prefabAnimator?.avatar;

        if (controller == null)
        {
            result.Detail =
                "RuntimeAnimatorController를 찾지 못했습니다. " +
                $"Prefab={prefabPath}";
            return false;
        }

        SerializedObject bundleSerialized =
            new SerializedObject(bundle);

        bundleSerialized.Update();

        changed |= SetObject(
            bundleSerialized,
            "animatorController",
            controller);

        changed |= SetBool(
            bundleSerialized,
            "overrideAnimatorController",
            true);

        if (avatar != null)
        {
            changed |= SetObject(
                bundleSerialized,
                "avatar",
                avatar);

            changed |= SetBool(
                bundleSerialized,
                "overrideAvatar",
                true);
        }

        if (presentation != null &&
            bundle.PresentationProfile != presentation)
        {
            bundle.ConfigurePresentationProfile(
                presentation);
            changed = true;
            result.PresentationRepaired = true;
        }

        bundleSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bundle);

        GameObject root =
            PrefabUtility.LoadPrefabContents(
                prefabPath);

        try
        {
            Character rootCharacter =
                root.GetComponent<Character>() ??
                root.GetComponentInChildren<Character>(true);

            Animator rootAnimator =
                root.GetComponentInChildren<Animator>(true);

            CharacterView rootView =
                root.GetComponentInChildren<CharacterView>(true);

            CharacterAuthoringLink rootLink =
                root.GetComponentInChildren<CharacterAuthoringLink>(true);

            if (rootCharacter == null)
            {
                result.Detail =
                    "Prefab 내부에서 Character Component를 찾지 못했습니다.";
                return false;
            }

            if (rootAnimator == null)
            {
                result.Detail =
                    "Prefab 내부에서 Animator를 찾지 못했습니다.";
                return false;
            }

            if (rootAnimator.runtimeAnimatorController != controller)
            {
                rootAnimator.runtimeAnimatorController = controller;
                changed = true;
                result.AnimatorRepaired = true;
            }

            if (avatar != null &&
                rootAnimator.avatar != avatar)
            {
                rootAnimator.avatar = avatar;
                changed = true;
            }

            if (rootLink != null &&
                rootLink.Bundle != bundle)
            {
                rootLink.Configure(bundle);
                changed = true;
            }

            if (rootView != null &&
                presentation != null &&
                rootView.PresentationProfile != presentation)
            {
                rootView.ConfigurePresentationProfile(
                    presentation);
                changed = true;
                result.PresentationRepaired = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        GameObject refreshedRoot =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);

        Character refreshedCharacter =
            refreshedRoot != null
                ? refreshedRoot.GetComponentInChildren<Character>(true)
                : null;

        if (refreshedCharacter != null &&
            bundle.CharacterPrefab != refreshedCharacter)
        {
            bundle.ConfigurePrefab(refreshedCharacter);
            EditorUtility.SetDirty(bundle);
            changed = true;
            result.PrefabRepaired = true;
        }

        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        result.Success = true;
        result.Changed = changed;
        result.PrefabPath = prefabPath;
        result.ControllerPath =
            AssetDatabase.GetAssetPath(controller);
        result.Detail = prefabDetail;
        return true;
    }

    private static RuntimeAnimatorController ResolveFallbackController(
        CharacterAuthoringBundle bundle)
    {
        string preferredPath =
            bundle.Kind switch
            {
                CharacterAuthoringKind.Olaf =>
                    "Assets/2. Data/Characters/Olaf/AnimatorControllers/OlafAnimatorController.controller",

                CharacterAuthoringKind.EliteEnemy =>
                    "Assets/2. Data/Characters/Enemies/EliteEnemy/AnimationControllers/EliteEnemyAnimatorController.controller",

                CharacterAuthoringKind.Yujin =>
                    TestControllerPath,

                CharacterAuthoringKind.Hifumi =>
                    TestControllerPath,

                CharacterAuthoringKind.NormalEnemy =>
                    TestControllerPath,

                _ =>
                    TestControllerPath
            };

        RuntimeAnimatorController preferred =
            AssetDatabase.LoadAssetAtPath<
                RuntimeAnimatorController>(
                    preferredPath);

        if (preferred != null)
            return preferred;

        string[] controllerGuids =
            AssetDatabase.FindAssets(
                "t:AnimatorController");

        string displayName =
            bundle.DisplayName ??
            bundle.name;

        RuntimeAnimatorController fallback = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < controllerGuids.Length; i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    controllerGuids[i]);

            RuntimeAnimatorController candidate =
                AssetDatabase.LoadAssetAtPath<
                    RuntimeAnimatorController>(path);

            if (candidate == null)
                continue;

            int score = 0;

            if (Contains(path, displayName))
                score += 200;

            if (Contains(path, bundle.Kind.ToString()))
                score += 120;

            if (Contains(path, "TestCharacter"))
                score += 50;

            if (score <= bestScore)
                continue;

            bestScore = score;
            fallback = candidate;
        }

        return fallback;
    }

    private static CharacterPresentationProfile ResolveFallbackPresentation(
        CharacterAuthoringBundle bundle)
    {
        if (bundle.Kind == CharacterAuthoringKind.Yujin ||
            bundle.Kind == CharacterAuthoringKind.Hifumi ||
            bundle.Kind == CharacterAuthoringKind.NormalEnemy)
        {
            return AssetDatabase.LoadAssetAtPath<
                CharacterPresentationProfile>(
                    TestPresentationPath);
        }

        return null;
    }

    private static bool SetObject(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null ||
            property.objectReferenceValue == value)
        {
            return false;
        }

        property.objectReferenceValue = value;
        return true;
    }

    private static bool SetBool(
        SerializedObject serialized,
        string propertyName,
        bool value)
    {
        SerializedProperty property =
            serialized.FindProperty(propertyName);

        if (property == null ||
            property.boolValue == value)
        {
            return false;
        }

        property.boolValue = value;
        return true;
    }

    private static bool Contains(
        string source,
        string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               !string.IsNullOrWhiteSpace(value) &&
               source.IndexOf(
                   value,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

[Serializable]
public sealed class CharacterVerificationBundleRepairResult
{
    public string BundleName;
    public bool Success;
    public bool Changed;
    public bool PrefabRepaired;
    public bool AnimatorRepaired;
    public bool PresentationRepaired;
    public string PrefabPath;
    public string ControllerPath;
    public string Detail;

    public string BuildLine()
    {
        return
            $"Bundle={BundleName}, Success={Success}, Changed={Changed}, " +
            $"Prefab={PrefabPath}, Controller={ControllerPath}, Detail={Detail}";
    }
}

[Serializable]
public sealed class CharacterVerificationRepairSummary
{
    public int CheckedBundles;
    public int RepairedBundles;
    public int AlreadyValidBundles;
    public int UnresolvedBundles;
    public List<CharacterVerificationBundleRepairResult> Results = new();

    public string BuildSummary()
    {
        return
            $"Checked={CheckedBundles}\n" +
            $"Repaired={RepairedBundles}\n" +
            $"Already Valid={AlreadyValidBundles}\n" +
            $"Unresolved={UnresolvedBundles}";
    }
}
#endif
