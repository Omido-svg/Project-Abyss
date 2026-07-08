using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterCameraPointSet : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Transform cameraPointsRoot;

    [Header("Collected Points")]
    [SerializeField] private List<CharacterCameraPointEntry> points = new();

    public Transform GetPoint(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        foreach (CharacterCameraPointEntry entry in points)
        {
            if (entry == null)
                continue;

            if (entry.Point == null)
                continue;

            if (entry.Key == key)
                return entry.Point;
        }

        return null;
    }

#if UNITY_EDITOR
    [ContextMenu("Collect Child Points")]
    private void CollectChildPoints()
    {
        points.Clear();

        if (cameraPointsRoot == null)
            cameraPointsRoot = FindCameraPointsRoot();

        if (cameraPointsRoot == null)
        {
            Debug.LogWarning(
                $"[CharacterCameraPointSet] CameraPoints 루트를 찾지 못했습니다. / Object={name}");

            return;
        }

        foreach (Transform child in cameraPointsRoot)
        {
            if (child == null)
                continue;

            points.Add(
                new CharacterCameraPointEntry
                {
                    Key = child.name,
                    Point = child
                });
        }

        Debug.Log(
            $"[CharacterCameraPointSet] CameraPoints 수집 완료 / Object={name}, Count={points.Count}");
    }

    [ContextMenu("Collect Child Points Recursive")]
    private void CollectChildPointsRecursive()
    {
        points.Clear();

        if (cameraPointsRoot == null)
            cameraPointsRoot = FindCameraPointsRoot();

        if (cameraPointsRoot == null)
        {
            Debug.LogWarning(
                $"[CharacterCameraPointSet] CameraPoints 루트를 찾지 못했습니다. / Object={name}");

            return;
        }

        foreach (Transform child in cameraPointsRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == cameraPointsRoot)
                continue;

            points.Add(
                new CharacterCameraPointEntry
                {
                    Key = child.name,
                    Point = child
                });
        }

        Debug.Log(
            $"[CharacterCameraPointSet] CameraPoints 재귀 수집 완료 / Object={name}, Count={points.Count}");
    }

    private Transform FindCameraPointsRoot()
    {
        Transform found =
            transform.Find("CameraPoints");

        if (found != null)
            return found;

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "CameraPoints")
                return child;
        }

        return null;
    }
#endif
}

[Serializable]
public class CharacterCameraPointEntry
{
    public string Key;
    public Transform Point;
}