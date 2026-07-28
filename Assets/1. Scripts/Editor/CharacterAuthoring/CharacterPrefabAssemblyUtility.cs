#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bundle을 Timeline-only Character Prefab의 표준 Component/Hierarchy/Reference로 조립한다.
/// 공격 Animation Event Relay는 생성하지 않으며 Animator는 상태 표현용으로만 유지한다.
/// </summary>
public static class CharacterPrefabAssemblyUtility
{
    private static readonly string[] CameraPointNames =
    {
        // 새 Timeline Cutscene에서 선택적으로 사용하는 의미 기반 Binding Point.
        // 실제 Camera 위치는 Skill 전용 CameraRig Prefab 안에 자유롭게 배치한다.
        "Root",
        "Center",
        "Head",
        "Chest",
        "LeftHand",
        "RightHand",
        "WeaponMain",
        "WeaponSub",
        "Feet",

        // 기존 SkillCameraDefinition / CharacterView 연출 호환 Point.
        "LookAtPoint",
        "CloseCameraPoint",
        "OverShoulderCameraPoint",
        "HitImpactCameraPoint",
        "SideCameraPoint",
        "ClashRollCameraPoint",
        "DetailCameraPoint"
    };

    public static CharacterPrefabAssemblyReport Assemble(
        CharacterAuthoringBundle bundle,
        bool ensureStandardComponents,
        bool createStandardHierarchy)
    {
        CharacterPrefabAssemblyReport report = new();

        if (bundle?.CharacterPrefab == null)
        {
            report.Error("Character Prefab이 없습니다.");
            return report;
        }

        string path = AssetDatabase.GetAssetPath(bundle.CharacterPrefab);

        if (string.IsNullOrWhiteSpace(path) ||
            !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            report.Error("Project Prefab Asset이 아닙니다.");
            return report;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        if (root == null)
        {
            report.Error("Prefab Contents를 열지 못했습니다.");
            return report;
        }

        try
        {
            Character character =
                root.GetComponent<Character>() ??
                root.GetComponentInChildren<Character>(true);

            if (character == null)
            {
                report.Error("Character Component를 찾지 못했습니다.");
                return report;
            }

            ApplyCharacterReferences(character, bundle);

            Transform visualRoot = ResolveVisualRoot(character);
            Animator animator = character.GetComponentInChildren<Animator>(true);

            Transform anchorsRoot = createStandardHierarchy
                ? EnsureChild(visualRoot, "Anchors", report)
                : FindDescendant(visualRoot, "Anchors");

            Transform cameraRoot = createStandardHierarchy
                ? EnsureChild(visualRoot, "CameraPoints", report)
                : FindDescendant(visualRoot, "CameraPoints");

            Dictionary<PartType, Transform> anchors =
                createStandardHierarchy
                    ? EnsureBodyAnchors(anchorsRoot, report)
                    : FindBodyAnchors(anchorsRoot);

            Dictionary<string, Transform> points =
                createStandardHierarchy
                    ? EnsureCameraPoints(cameraRoot, report)
                    : FindCameraPoints(cameraRoot);

            CharacterAuthoringLink link =
                EnsureComponent<CharacterAuthoringLink>(
                    character.gameObject,
                    report);

            ConfigureLink(link, bundle, character, animator);

            if (ensureStandardComponents)
            {
                CharacterView view =
                    EnsureComponent<CharacterView>(
                        character.gameObject,
                        report);

                CharacterViewEventBinder binder =
                    EnsureComponent<CharacterViewEventBinder>(
                        character.gameObject,
                        report);

                CharacterFacingController facing =
                    EnsureComponent<CharacterFacingController>(
                        character.gameObject,
                        report);

                CharacterActionMover mover =
                    EnsureComponent<CharacterActionMover>(
                        character.gameObject,
                        report);

                BattleCharacterWorldClickInspector click =
                    EnsureComponent<BattleCharacterWorldClickInspector>(
                        character.gameObject,
                        report);

                OutlineController outline =
                    EnsureComponent<OutlineController>(
                        character.gameObject,
                        report);

                EnsureComponent<CharacterRandomDebugOverride>(
                    character.gameObject,
                    report);

                CharacterCameraPointSet pointSet =
                    EnsureComponent<CharacterCameraPointSet>(
                        visualRoot.gameObject,
                        report);

                CharacterPersistentVfxController persistentVfx =
                    EnsureComponent<CharacterPersistentVfxController>(
                        visualRoot.gameObject,
                        report);

                ConfigureView(
                    view,
                    character,
                    animator,
                    anchors,
                    points);

                SetObject(binder, "character", character);
                SetObject(binder, "characterView", view);
                SetObject(facing, "rotateRoot", visualRoot);
                SetObject(mover, "visualRoot", visualRoot);
                SetObject(click, "character", character);
                SetArray(
                    outline,
                    "targetRenderers",
                    visualRoot.GetComponentsInChildren<Renderer>(true));
                SetObject(pointSet, "cameraPointsRoot", cameraRoot);
                SetObject(persistentVfx, "character", character);
                SetObject(persistentVfx, "characterView", view);
                pointSet.RebuildLookup();
            }

            ApplyAnimator(animator, bundle);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            report.SavedPrefabPath = path;
        }
        catch (Exception exception)
        {
            report.Error(exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Character refreshed = asset?.GetComponentInChildren<Character>(true);

        if (refreshed != null)
        {
            bundle.ConfigurePrefab(refreshed);
            EditorUtility.SetDirty(bundle);
        }

        AssetDatabase.SaveAssets();
        return report;
    }

    public static List<string> ValidatePrefab(CharacterAuthoringBundle bundle)
    {
        List<string> result = new();
        Character prefab = bundle?.CharacterPrefab;

        if (prefab == null)
        {
            result.Add("Character Prefab이 없습니다.");
            return result;
        }

        Require<CharacterAuthoringLink>(prefab, result);
        Require<CharacterView>(prefab, result);
        Require<CharacterViewEventBinder>(prefab, result);
        Require<CharacterFacingController>(prefab, result);
        Require<CharacterActionMover>(prefab, result);
        Require<BattleCharacterWorldClickInspector>(prefab, result);
        Require<OutlineController>(prefab, result);
        Require<CharacterRandomDebugOverride>(prefab, result);

        Animator animator = prefab.GetComponentInChildren<Animator>(true);

        if (animator == null)
            result.Add("Animator가 없습니다.");

        Transform visualRoot = ResolveVisualRoot(prefab);

        if (visualRoot.GetComponent<CharacterCameraPointSet>() == null)
            result.Add("Visual Root에 CharacterCameraPointSet이 없습니다.");

        if (visualRoot.GetComponent<CharacterPersistentVfxController>() == null)
            result.Add("Visual Root에 CharacterPersistentVfxController가 없습니다.");

        if (FindDescendant(visualRoot, "Anchors") == null)
            result.Add("Anchors Root가 없습니다.");

        if (FindDescendant(visualRoot, "CameraPoints") == null)
            result.Add("CameraPoints Root가 없습니다.");

        return result;
    }

    private static void ApplyCharacterReferences(
        Character character,
        CharacterAuthoringBundle bundle)
    {
        SerializedObject so = new(character);
        so.Update();
        SetObject(so, "data", bundle.CharacterData);

        if (bundle.SkillSet != null)
            SetObject(so, "skillSet", bundle.SkillSet);

        if (bundle.OverrideLoadout)
        {
            SetArray(so, "equippedItems", bundle.EquippedItems);
            SetArray(so, "equippedAugments", bundle.EquippedAugments);
        }

        if (character is NormalEnemy && bundle.OverrideNormalEnemySingleHp)
            SetInt(so, "singleMaxHP", bundle.NormalEnemySingleMaxHp);

        if (character is EliteEnemy)
            SetBool(so, "usePostureRotation", bundle.UseElitePostureRotation);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static void ConfigureLink(
        CharacterAuthoringLink link,
        CharacterAuthoringBundle bundle,
        Character character,
        Animator animator)
    {
        SerializedObject so = new(link);
        so.Update();
        SetObject(so, "bundle", bundle);
        SetObject(so, "targetCharacter", character);
        SetObject(so, "targetAnimator", animator);
        SetBool(so, "applyOnAwake", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(link);
    }

    private static void ConfigureView(
        CharacterView view,
        Character character,
        Animator animator,
        IReadOnlyDictionary<PartType, Transform> anchors,
        IReadOnlyDictionary<string, Transform> points)
    {
        SerializedObject so = new(view);
        so.Update();
        SetObject(so, "character", character);
        SetObject(so, "animator", animator);

        SerializedProperty anchorList = so.FindProperty("bodyPartAnchors");

        if (anchorList != null && anchorList.isArray)
        {
            anchorList.arraySize = anchors?.Count ?? 0;
            int index = 0;

            if (anchors != null)
            {
                foreach (KeyValuePair<PartType, Transform> pair
                         in anchors.OrderBy(item => (int)item.Key))
                {
                    SerializedProperty element =
                        anchorList.GetArrayElementAtIndex(index++);
                    SerializedProperty type =
                        element.FindPropertyRelative("PartType");
                    SerializedProperty anchor =
                        element.FindPropertyRelative("Anchor");

                    if (type != null) type.enumValueIndex = (int)pair.Key;
                    if (anchor != null) anchor.objectReferenceValue = pair.Value;
                }
            }
        }

        SetObject(so, "lookAtPoint", GetPoint(points, "LookAtPoint"));
        SetObject(
            so,
            "attackCameraPoint",
            GetPoint(points, "OverShoulderCameraPoint") ??
            GetPoint(points, "CloseCameraPoint"));
        SetObject(so, "hitCameraPoint", GetPoint(points, "HitImpactCameraPoint"));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
    }

    private static void ApplyAnimator(
        Animator animator,
        CharacterAuthoringBundle bundle)
    {
        if (animator == null)
            return;

        if (bundle.OverrideAnimatorController && bundle.AnimatorController != null)
            animator.runtimeAnimatorController = bundle.AnimatorController;

        if (bundle.OverrideAvatar && bundle.Avatar != null)
            animator.avatar = bundle.Avatar;

        EditorUtility.SetDirty(animator);
    }

    private static Transform ResolveVisualRoot(Character character)
    {
        Transform named = FindDescendant(character.transform, character.name + "_View");
        if (named != null) return named;

        named = FindDescendant(character.transform, "VisualRoot");
        if (named != null) return named;

        Animator animator = character.GetComponentInChildren<Animator>(true);
        if (animator == null) return character.transform;

        Transform current = animator.transform;

        while (current.parent != null && current.parent != character.transform)
            current = current.parent;

        return current;
    }

    private static Dictionary<PartType, Transform> EnsureBodyAnchors(
        Transform root,
        CharacterPrefabAssemblyReport report)
    {
        Dictionary<PartType, Transform> result = new();
        if (root == null) return result;

        result[PartType.HEAD] = EnsureChild(root, "HEAD_Anchor", report);
        result[PartType.LEFT_HAND] = EnsureChild(root, "LEFT_HAND_Anchor", report);
        result[PartType.RIGHT_HAND] = EnsureChild(root, "RIGHT_HAND_Anchor", report);
        result[PartType.LEGS] = EnsureChild(root, "LEGS_Anchor", report);
        return result;
    }

    private static Dictionary<PartType, Transform> FindBodyAnchors(Transform root)
    {
        Dictionary<PartType, Transform> result = new();
        if (root == null) return result;

        AddIfFound(result, PartType.HEAD, FindDescendant(root, "HEAD_Anchor"));
        AddIfFound(result, PartType.LEFT_HAND, FindDescendant(root, "LEFT_HAND_Anchor"));
        AddIfFound(result, PartType.RIGHT_HAND, FindDescendant(root, "RIGHT_HAND_Anchor"));
        AddIfFound(result, PartType.LEGS, FindDescendant(root, "LEGS_Anchor"));
        return result;
    }

    private static Dictionary<string, Transform> EnsureCameraPoints(
        Transform root,
        CharacterPrefabAssemblyReport report)
    {
        Dictionary<string, Transform> result =
            new(StringComparer.Ordinal);

        if (root == null) return result;

        foreach (string name in CameraPointNames)
            result[name] = EnsureChild(root, name, report);

        return result;
    }

    private static Dictionary<string, Transform> FindCameraPoints(Transform root)
    {
        Dictionary<string, Transform> result =
            new(StringComparer.Ordinal);

        if (root == null) return result;

        foreach (string name in CameraPointNames)
        {
            Transform point = FindDescendant(root, name);
            if (point != null) result[name] = point;
        }

        return result;
    }

    private static void AddIfFound(
        IDictionary<PartType, Transform> values,
        PartType type,
        Transform target)
    {
        if (target != null) values[type] = target;
    }

    private static Transform GetPoint(
        IReadOnlyDictionary<string, Transform> values,
        string key)
    {
        return values != null && values.TryGetValue(key, out Transform value)
            ? value
            : null;
    }

    private static Transform EnsureChild(
        Transform parent,
        string name,
        CharacterPrefabAssemblyReport report)
    {
        if (parent == null) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;
        }

        GameObject created = new(name);
        created.transform.SetParent(parent, false);
        report.Created(PathOf(created.transform));
        return created.transform;
    }

    private static T EnsureComponent<T>(
        GameObject target,
        CharacterPrefabAssemblyReport report)
        where T : Component
    {
        T component = target.GetComponent<T>();

        if (component != null)
            return component;

        component = target.AddComponent<T>();
        report.Added(typeof(T).Name, PathOf(target.transform));
        return component;
    }

    private static void Require<T>(Character prefab, ICollection<string> result)
        where T : Component
    {
        if (prefab.GetComponent<T>() == null)
            result.Add($"{typeof(T).Name}이 없습니다.");
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null) return null;

        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
        {
            if (item.name == name) return item;
        }

        return null;
    }

    private static string PathOf(Transform target)
    {
        if (target == null) return "NULL";

        string path = target.name;

        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private static void SetObject(Component target, string name, UnityEngine.Object value)
    {
        if (target == null) return;

        SerializedObject so = new(target);
        so.Update();
        SetObject(so, name, value);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetObject(
        SerializedObject so,
        string name,
        UnityEngine.Object value)
    {
        SerializedProperty property = so?.FindProperty(name);

        if (property != null &&
            property.propertyType == SerializedPropertyType.ObjectReference)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetArray<T>(
        Component target,
        string name,
        IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        if (target == null) return;

        SerializedObject so = new(target);
        so.Update();
        SetArray(so, name, values);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetArray<T>(
        SerializedObject so,
        string name,
        IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        SerializedProperty property = so?.FindProperty(name);

        if (property == null || !property.isArray)
            return;

        property.arraySize = values?.Count ?? 0;

        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetArray<T>(Component target, string name, T[] values)
        where T : UnityEngine.Object
    {
        SetArray(target, name, (IReadOnlyList<T>)values);
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so?.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so?.FindProperty(name);
        if (property != null) property.intValue = value;
    }
}

public sealed class CharacterPrefabAssemblyReport
{
    private readonly List<string> messages = new();

    public IReadOnlyList<string> Messages => messages;
    public string SavedPrefabPath { get; set; }
    public int AddedComponentCount { get; private set; }
    public int CreatedHierarchyCount { get; private set; }
    public int ErrorCount { get; private set; }
    public bool Success => ErrorCount == 0 &&
                           !string.IsNullOrWhiteSpace(SavedPrefabPath);

    public void Added(string component, string path)
    {
        AddedComponentCount++;
        messages.Add($"Component 추가: {component} @ {path}");
    }

    public void Created(string path)
    {
        CreatedHierarchyCount++;
        messages.Add($"Hierarchy 생성: {path}");
    }

    public void Error(string message)
    {
        ErrorCount++;
        messages.Add("ERROR: " + message);
    }

    public string BuildSummary() =>
        $"Success={Success}\n" +
        $"AddedComponents={AddedComponentCount}\n" +
        $"CreatedHierarchy={CreatedHierarchyCount}\n" +
        $"Errors={ErrorCount}\n" +
        $"Prefab={SavedPrefabPath ?? "NULL"}";
}
#endif
