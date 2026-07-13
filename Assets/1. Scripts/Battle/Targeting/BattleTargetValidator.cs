using System;
using System.Collections.Generic;
using UnityEngine;

public static class BattleTargetValidator
{
    public static bool IsValid(
        Character target,
        BodyPart targetPart,
        TargetSelectionRule rule)
    {
        if (target == null)
            return false;

        if (!rule.AllowDeadCharacters &&
            target.IsDead)
        {
            return false;
        }

        ICombatTargetModel model =
            target.TargetModel;

        if (model == null)
        {
            bool hasParts =
                target.BodyParts != null &&
                target.BodyParts.Count > 0;

            model = hasParts
                ? new BodyPartTargetModel()
                : new SingleHpTargetModel(
                    Mathf.Max(1, target.CurrentHP));
        }

        return model.IsValidTargetPart(
            target,
            targetPart,
            rule.AllowBrokenParts);
    }

    public static IReadOnlyList<TargetPoint> GetTargetPoints(
        Character target,
        TargetSelectionRule rule)
    {
        if (target == null)
            return Array.Empty<TargetPoint>();

        if (!rule.AllowDeadCharacters &&
            target.IsDead)
        {
            return Array.Empty<TargetPoint>();
        }

        ICombatTargetModel model =
            target.TargetModel;

        if (model == null)
        {
            bool hasParts =
                target.BodyParts != null &&
                target.BodyParts.Count > 0;

            model = hasParts
                ? new BodyPartTargetModel()
                : new SingleHpTargetModel(
                    Mathf.Max(1, target.CurrentHP));
        }

        return model.GetTargetPoints(
            target,
            rule.AllowBrokenParts);
    }

    public static TargetPoint ChooseWeightedTargetPoint(
        Character target,
        TargetSelectionRule rule,
        float brokenPartWeight = 0.7f)
    {
        IReadOnlyList<TargetPoint> points =
            GetTargetPoints(
                target,
                rule);

        if (points == null ||
            points.Count == 0)
        {
            return default;
        }

        if (points.Count == 1)
            return points[0];

        List<TargetPoint> broken = new();
        List<TargetPoint> living = new();

        foreach (TargetPoint point in points)
        {
            if (!point.IsValid)
                continue;

            if (point.Part != null &&
                point.Part.IsBroken)
            {
                broken.Add(point);
            }
            else
            {
                living.Add(point);
            }
        }

        if (broken.Count == 0)
        {
            return living.Count == 0
                ? default
                : living[UnityEngine.Random.Range(
                    0,
                    living.Count)];
        }

        if (living.Count == 0)
        {
            return broken[UnityEngine.Random.Range(
                0,
                broken.Count)];
        }

        bool chooseBroken =
            UnityEngine.Random.value <
            Mathf.Clamp01(brokenPartWeight);

        List<TargetPoint> pool =
            chooseBroken
                ? broken
                : living;

        return pool[UnityEngine.Random.Range(
            0,
            pool.Count)];
    }

    public static bool IsSameTarget(
        Character firstCharacter,
        BodyPart firstPart,
        Character secondCharacter,
        BodyPart secondPart)
    {
        if (firstCharacter != secondCharacter)
            return false;

        if (firstPart == null ||
            secondPart == null)
        {
            return
                firstPart == null &&
                secondPart == null;
        }

        return
            firstPart == secondPart ||
            firstPart.Type ==
            secondPart.Type;
    }
}
