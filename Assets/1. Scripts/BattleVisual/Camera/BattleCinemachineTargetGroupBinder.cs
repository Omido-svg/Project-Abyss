using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public sealed class BattleCinemachineTargetGroupBinder : IDisposable
{
    private readonly CinemachineTargetGroup targetGroup;
    private readonly List<Transform> boundMembers = new();

    public bool IsAvailable => targetGroup != null;
    public int BoundMemberCount => boundMembers.Count;

    public BattleCinemachineTargetGroupBinder(
        CinemachineTargetGroup targetGroup)
    {
        this.targetGroup = targetGroup;
    }

    public bool BindTwoTargets(
        Transform first,
        Transform second,
        float firstWeight,
        float secondWeight,
        float firstRadius,
        float secondRadius)
    {
        Clear();

        if (targetGroup == null)
            return false;

        if (first == null && second == null)
            return false;

        if (first == second)
        {
            return BindOneTarget(
                first,
                Mathf.Max(firstWeight, secondWeight),
                Mathf.Max(firstRadius, secondRadius));
        }

        Add(first, firstWeight, firstRadius);
        Add(second, secondWeight, secondRadius);
        RefreshGroup();

        return boundMembers.Count > 0;
    }

    public bool BindOneTarget(
        Transform target,
        float weight,
        float radius)
    {
        Clear();

        if (targetGroup == null || target == null)
            return false;

        Add(target, weight, radius);
        RefreshGroup();

        return boundMembers.Count == 1;
    }

    public void Clear()
    {
        if (targetGroup == null)
        {
            boundMembers.Clear();
            return;
        }

        for (int i = boundMembers.Count - 1; i >= 0; i--)
        {
            Transform member = boundMembers[i];

            if (member == null)
                continue;

            if (targetGroup.FindMember(member) >= 0)
                targetGroup.RemoveMember(member);
        }

        boundMembers.Clear();
        RefreshGroup();
    }

    public void Dispose()
    {
        Clear();
    }

    private void Add(
        Transform target,
        float weight,
        float radius)
    {
        if (target == null || targetGroup == null)
            return;

        if (targetGroup.FindMember(target) >= 0)
            targetGroup.RemoveMember(target);

        targetGroup.AddMember(
            target,
            Mathf.Max(0f, weight),
            Mathf.Max(0.01f, radius));

        boundMembers.Add(target);
    }

    private void RefreshGroup()
    {
        if (targetGroup != null)
            targetGroup.DoUpdate();
    }
}
