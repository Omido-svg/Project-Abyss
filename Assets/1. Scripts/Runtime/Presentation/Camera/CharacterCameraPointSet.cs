using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterCameraPointSet : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Transform cameraPointsRoot;

    [Header("Collected Points")]
    [SerializeField] private List<CharacterCameraPointEntry> points = new();

    [Header("Runtime")]
    [SerializeField] private bool warnDuplicateKeys = true;

    private readonly Dictionary<string, Transform> lookup =
        new(StringComparer.Ordinal);

    private readonly List<string> duplicateKeys = new();
    private bool lookupReady;

    public Transform CameraPointsRoot => cameraPointsRoot;
    public IReadOnlyList<CharacterCameraPointEntry> Points => points;
    public IReadOnlyList<string> DuplicateKeys => duplicateKeys;

    private void Awake()
    {
        RebuildLookup();
    }

    private void OnEnable()
    {
        if (!lookupReady)
            RebuildLookup();
    }

    private void OnValidate()
    {
        if (cameraPointsRoot == null)
            cameraPointsRoot = FindCameraPointsRoot();

        lookupReady = false;
    }

    public Transform GetPoint(string key)
    {
        return TryGetPoint(key, out Transform point)
            ? point
            : null;
    }

    public bool TryGetPoint(string key, out Transform point)
    {
        point = null;

        if (string.IsNullOrEmpty(key))
            return false;

        EnsureLookup();

        if (lookup.TryGetValue(key, out point))
        {
            if (point != null)
                return true;

            lookup.Remove(key);
            point = null;
        }

        if (!TryFindHierarchyPoint(
                key,
                out point))
        {
            return false;
        }

        AddLookupEntry(
            key,
            point);

        return true;
    }

    public bool ContainsKey(string key)
    {
        return TryGetPoint(key, out _);
    }

    public IReadOnlyList<string> GetKeys()
    {
        EnsureLookup();
        return new List<string>(lookup.Keys);
    }

    public void RebuildLookup()
    {
        lookup.Clear();
        duplicateKeys.Clear();

        if (cameraPointsRoot == null)
            cameraPointsRoot = FindCameraPointsRoot();

        if (points != null)
        {
            foreach (CharacterCameraPointEntry entry in points)
            {
                if (entry == null)
                    continue;

                AddLookupEntry(
                    entry.Key,
                    entry.Point);
            }
        }

        AddHierarchyPointsToLookup();

        lookupReady = true;

        if (warnDuplicateKeys && duplicateKeys.Count > 0)
        {
            Debug.LogWarning(
                $"[CharacterCameraPointSet] 중복 CameraPoint Key가 있습니다. " +
                $"Object={name}, Keys={string.Join(", ", duplicateKeys)}",
                this);
        }
    }

    public bool ValidatePoints(bool log)
    {
        RebuildLookup();

        bool valid = true;

        if (cameraPointsRoot == null)
        {
            valid = false;

            if (log)
            {
                Debug.LogWarning(
                    $"[CharacterCameraPointSet] CameraPoints Root가 없습니다. Object={name}",
                    this);
            }
        }

        if (duplicateKeys.Count > 0)
            valid = false;

        if (lookup.Count == 0)
        {
            valid = false;

            if (log)
            {
                Debug.LogWarning(
                    $"[CharacterCameraPointSet] 사용할 수 있는 CameraPoint가 없습니다. Object={name}",
                    this);
            }
        }

        return valid;
    }

    [ContextMenu("Collect Child Points")]
    private void CollectChildPoints()
    {
        CollectPoints(false);
    }

    [ContextMenu("Collect Child Points Recursive")]
    private void CollectChildPointsRecursive()
    {
        CollectPoints(true);
    }

    [ContextMenu("Validate Camera Points")]
    private void ValidateCameraPointsContextMenu()
    {
        bool valid = ValidatePoints(true);

        Debug.Log(
            $"[CharacterCameraPointSet] 검증 완료 / " +
            $"Object={name}, Valid={valid}, Count={lookup.Count}",
            this);
    }

    private void CollectPoints(bool recursive)
    {
        points ??= new List<CharacterCameraPointEntry>();
        points.Clear();

        if (cameraPointsRoot == null)
            cameraPointsRoot = FindCameraPointsRoot();

        if (cameraPointsRoot == null)
        {
            Debug.LogWarning(
                $"[CharacterCameraPointSet] CameraPoints 루트를 찾지 못했습니다. Object={name}",
                this);
            return;
        }

        if (recursive)
        {
            Transform[] children =
                cameraPointsRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child == null || child == cameraPointsRoot)
                    continue;

                AddEntry(child);
            }
        }
        else
        {
            foreach (Transform child in cameraPointsRoot)
            {
                if (child != null)
                    AddEntry(child);
            }
        }

        RebuildLookup();

        Debug.Log(
            $"[CharacterCameraPointSet] CameraPoints 수집 완료 / " +
            $"Object={name}, Recursive={recursive}, Count={points.Count}",
            this);
    }

    private void AddEntry(Transform point)
    {
        points.Add(
            new CharacterCameraPointEntry
            {
                Key = point.name,
                Point = point
            });
    }

    private void AddHierarchyPointsToLookup()
    {
        if (cameraPointsRoot == null)
            return;

        Transform[] children =
            cameraPointsRoot.GetComponentsInChildren<Transform>(
                true);

        foreach (Transform child in children)
        {
            if (child == null ||
                child == cameraPointsRoot)
            {
                continue;
            }

            AddLookupEntry(
                child.name,
                child);
        }
    }

    private void AddLookupEntry(
        string key,
        Transform point)
    {
        if (string.IsNullOrEmpty(key) ||
            point == null)
        {
            return;
        }

        if (lookup.TryGetValue(
                key,
                out Transform existing))
        {
            // Inspector 등록과 자동 탐색이 같은 Transform을 가리키면
            // 정상적인 중복 등록으로 보고 경고하지 않는다.
            if (existing == point)
                return;

            if (!duplicateKeys.Contains(key))
                duplicateKeys.Add(key);

            return;
        }

        lookup.Add(
            key,
            point);
    }

    private bool TryFindHierarchyPoint(
        string key,
        out Transform point)
    {
        point = null;

        if (string.IsNullOrEmpty(key))
            return false;

        if (cameraPointsRoot == null)
            cameraPointsRoot = FindCameraPointsRoot();

        if (cameraPointsRoot == null)
            return false;

        Transform[] children =
            cameraPointsRoot.GetComponentsInChildren<Transform>(
                true);

        foreach (Transform child in children)
        {
            if (child == null ||
                child == cameraPointsRoot)
            {
                continue;
            }

            if (!string.Equals(
                    child.name,
                    key,
                    StringComparison.Ordinal))
            {
                continue;
            }

            point = child;
            return true;
        }

        return false;
    }

    private Transform FindCameraPointsRoot()
    {
        Transform direct = transform.Find("CameraPoints");

        if (direct != null)
            return direct;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == "CameraPoints")
                return child;
        }

        return null;
    }

    private void EnsureLookup()
    {
        if (!lookupReady)
            RebuildLookup();
    }
}

[Serializable]
public class CharacterCameraPointEntry
{
    public string Key;
    public Transform Point;
}