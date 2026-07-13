using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BodyPartTargetModel : ICombatTargetModel
{
    public bool UsesBodyParts => true;
    public bool RequiresTargetPart => true;

    public int CalculateInitialHp(Character owner)
    {
        if (owner?.BodyParts == null)
            return 0;

        int hp = 0;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null || part.IsBroken)
                continue;

            hp += Mathf.Max(
                0,
                Mathf.RoundToInt(part.PartHP));
        }

        return hp;
    }

    public int GetMaxHp(Character owner)
    {
        if (owner?.BodyParts == null)
            return 0;

        int hp = 0;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            hp += Mathf.Max(
                0,
                Mathf.RoundToInt(part.MaxPartHP));
        }

        return hp;
    }

    public bool IsValidTargetPart(
        Character owner,
        BodyPart part,
        bool allowBrokenPart)
    {
        if (owner == null || part == null)
            return false;

        if (part.Owner != null &&
            part.Owner != owner)
        {
            return false;
        }

        if (!allowBrokenPart &&
            part.IsBroken)
        {
            return false;
        }

        return true;
    }

    public bool IsStructureDestroyed(Character owner)
    {
        if (owner?.BodyParts == null ||
            owner.BodyParts.Count == 0)
        {
            return false;
        }

        bool foundPart = false;

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            foundPart = true;

            if (!part.IsBroken)
                return false;
        }

        return foundPart;
    }

    public IReadOnlyList<TargetPoint> GetTargetPoints(
        Character owner,
        bool includeBrokenParts)
    {
        if (owner?.BodyParts == null)
            return Array.Empty<TargetPoint>();

        List<TargetPoint> result = new();

        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null)
                continue;

            if (!includeBrokenParts &&
                part.IsBroken)
            {
                continue;
            }

            result.Add(
                TargetPoint.ForBodyPart(
                    owner,
                    part));
        }

        return result;
    }
}
