using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SingleHpTargetModel : ICombatTargetModel
{
    private int maxHp;

    public SingleHpTargetModel(int maxHp)
    {
        this.maxHp =
            Mathf.Max(1, maxHp);
    }

    public bool UsesBodyParts => false;
    public bool RequiresTargetPart => false;

    public int CalculateInitialHp(Character owner)
    {
        return maxHp;
    }

    public int GetMaxHp(Character owner)
    {
        return maxHp;
    }

    public bool SetMaxHpForDebug(int value)
    {
        maxHp =
            Mathf.Max(
                1,
                value);

        return true;
    }

    public bool IsValidTargetPart(
        Character owner,
        BodyPart part,
        bool allowBrokenPart)
    {
        return
            owner != null &&
            part == null;
    }

    public bool IsStructureDestroyed(Character owner)
    {
        return false;
    }

    public IReadOnlyList<TargetPoint> GetTargetPoints(
        Character owner,
        bool includeBrokenParts)
    {
        if (owner == null)
            return Array.Empty<TargetPoint>();

        return new[]
        {
            TargetPoint.ForCharacter(owner)
        };
    }
}