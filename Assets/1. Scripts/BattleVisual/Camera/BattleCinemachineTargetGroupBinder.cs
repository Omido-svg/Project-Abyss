using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class BattleCinemachineTargetGroupBinder
{
    private readonly CinemachineTargetGroup targetGroup;
    private readonly List<Transform> boundMembers = new();

    public BattleCinemachineTargetGroupBinder(
        CinemachineTargetGroup targetGroup)
    {
        this.targetGroup = targetGroup;
    }

    public void BindTwoTargets(
        Transform first,
        Transform second,
        float firstWeight,
        float secondWeight,
        float firstRadius,
        float secondRadius)
    {
        Clear();

        if (targetGroup == null)
            return;

        Add(
            first,
            firstWeight,
            firstRadius);

        Add(
            second,
            secondWeight,
            secondRadius);

        targetGroup.DoUpdate();
    }

    public void BindOneTarget(
        Transform target,
        float weight,
        float radius)
    {
        Clear();

        if (targetGroup == null)
            return;

        Add(
            target,
            weight,
            radius);

        targetGroup.DoUpdate();
    }

    public void Clear()
    {
        if (targetGroup == null)
            return;

        foreach (Transform member in boundMembers)
        {
            if (member == null)
                continue;

            targetGroup.RemoveMember(
                member);
        }

        boundMembers.Clear();

        targetGroup.DoUpdate();
    }

    private void Add(
        Transform target,
        float weight,
        float radius)
    {
        if (target == null)
            return;

        if (targetGroup.FindMember(target) >= 0)
            return;

        targetGroup.AddMember(
            target,
            Mathf.Max(0f, weight),
            Mathf.Max(0.01f, radius));

        boundMembers.Add(target);
    }
}