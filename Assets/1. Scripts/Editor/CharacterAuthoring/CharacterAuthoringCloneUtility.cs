#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기존 Character Prefab이 참조하는 Project Abyss ScriptableObject 그래프를
/// CharacterAuthoringBundle의 Sub-Asset 복사본으로 안전하게 패킹한다.
/// 원본 에셋은 삭제하거나 이동하지 않는다.
/// </summary>
public sealed class CharacterAuthoringCloneUtility
{
    private readonly CharacterAuthoringBundle bundle;

    private readonly Dictionary<Object, Object>
        cloneMap = new();

    private readonly List<ScriptableObject>
        clonedAssets = new();

    public CharacterAuthoringCloneUtility(
        CharacterAuthoringBundle bundle)
    {
        this.bundle = bundle;
    }

    public IReadOnlyDictionary<Object, Object> CloneMap =>
        cloneMap;

    public void CapturePrefab(
        GameObject prefabRoot)
    {
        if (prefabRoot == null)
            return;

        MonoBehaviour[] behaviours =
            prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            CaptureSerializedReferences(behaviour);
        }
    }

    public T CloneRoot<T>(T source)
        where T : ScriptableObject
    {
        return CloneAsset(source) as T;
    }

    public T GetClone<T>(T source)
        where T : Object
    {
        if (source == null)
            return null;

        return cloneMap.TryGetValue(
                source,
                out Object clone)
            ? clone as T
            : null;
    }

    public void FinalizeClones()
    {
        for (int i = 0; i < clonedAssets.Count; i++)
        {
            RemapSerializedReferences(
                clonedAssets[i]);
        }
    }

    public void RemapPrefab(
        GameObject prefabRoot)
    {
        if (prefabRoot == null || cloneMap.Count == 0)
            return;

        MonoBehaviour[] behaviours =
            prefabRoot.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            RemapSerializedReferences(behaviour);
        }
    }

    private void CaptureSerializedReferences(
        Object sourceObject)
    {
        if (sourceObject == null)
            return;

        SerializedObject serialized =
            new SerializedObject(sourceObject);

        SerializedProperty property =
            serialized.GetIterator();

        bool enterChildren = true;

        while (property.Next(enterChildren))
        {
            enterChildren = true;

            if (property.propertyType !=
                SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            if (property.objectReferenceValue is not
                ScriptableObject referenced)
            {
                continue;
            }

            CloneAsset(referenced);
        }
    }

    private ScriptableObject CloneAsset(
        ScriptableObject source)
    {
        if (!ShouldClone(source))
            return source;

        if (cloneMap.TryGetValue(
                source,
                out Object existing))
        {
            return existing as ScriptableObject;
        }

        ScriptableObject clone =
            ScriptableObject.CreateInstance(
                source.GetType());

        clone.name = source.name;

        // 순환 참조를 안전하게 처리하기 위해 먼저 Map에 등록한다.
        cloneMap[source] = clone;

        EditorUtility.CopySerialized(
            source,
            clone);

        clone.name = source.name;

        AssetDatabase.AddObjectToAsset(
            clone,
            bundle);

        bundle.RegisterIncludedAsset(clone);
        clonedAssets.Add(clone);

        CaptureSerializedReferences(source);
        EditorUtility.SetDirty(clone);

        return clone;
    }

    private void RemapSerializedReferences(
        Object target)
    {
        if (target == null)
            return;

        SerializedObject serialized =
            new SerializedObject(target);

        SerializedProperty property =
            serialized.GetIterator();

        bool changed = false;
        bool enterChildren = true;

        while (property.Next(enterChildren))
        {
            enterChildren = true;

            if (property.propertyType !=
                SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            Object current =
                property.objectReferenceValue;

            if (current == null ||
                !cloneMap.TryGetValue(
                    current,
                    out Object replacement) ||
                replacement == null)
            {
                continue;
            }

            property.objectReferenceValue = replacement;
            changed = true;
        }

        if (!changed)
            return;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static bool ShouldClone(
        ScriptableObject source)
    {
        if (source == null ||
            source is CharacterAuthoringBundle)
        {
            return false;
        }

        string assetPath =
            AssetDatabase.GetAssetPath(source);

        if (string.IsNullOrWhiteSpace(assetPath) ||
            !assetPath.StartsWith("Assets/"))
        {
            return false;
        }

        MonoScript script =
            MonoScript.FromScriptableObject(source);

        if (script == null)
            return false;

        string scriptPath =
            AssetDatabase.GetAssetPath(script);

        return !string.IsNullOrWhiteSpace(scriptPath) &&
               scriptPath.StartsWith("Assets/1. Scripts/");
    }
}
#endif
