using UnityEngine;

public static class BattleCameraTargetResolver
{
    public static CharacterView GetView(Character character)
    {
        if (character == null)
            return null;

        CharacterView view = character.GetComponent<CharacterView>();

        if (view != null)
            return view;

        return character.GetComponentInChildren<CharacterView>(true);
    }

    public static CharacterCameraPointSet GetCameraPointSet(Character character)
    {
        if (character == null)
            return null;

        return character.GetComponentInChildren<CharacterCameraPointSet>(true);
    }

    public static Transform GetLookAtTarget(Character character)
    {
        return GetLookAtTarget(character, GetView(character));
    }

    public static Transform GetLookAtTarget(
        Character character,
        CharacterView resolvedView)
    {
        if (resolvedView != null && resolvedView.LookAtPoint != null)
            return resolvedView.LookAtPoint;

        return character != null
            ? character.transform
            : null;
    }

    public static Transform GetAttackCameraPoint(Character character)
    {
        return GetAttackCameraPoint(character, GetView(character));
    }

    public static Transform GetAttackCameraPoint(
        Character character,
        CharacterView resolvedView)
    {
        if (resolvedView != null && resolvedView.AttackCameraPoint != null)
            return resolvedView.AttackCameraPoint;

        return GetLookAtTarget(character, resolvedView);
    }

    public static Transform GetHitCameraPoint(Character character)
    {
        return GetHitCameraPoint(character, GetView(character));
    }

    public static Transform GetHitCameraPoint(
        Character character,
        CharacterView resolvedView)
    {
        if (resolvedView != null && resolvedView.HitCameraPoint != null)
            return resolvedView.HitCameraPoint;

        return GetLookAtTarget(character, resolvedView);
    }

    public static Transform GetTargetPartAnchor(
        Character character,
        BodyPart part)
    {
        return GetTargetPartAnchor(character, part, GetView(character));
    }

    public static Transform GetTargetPartAnchor(
        Character character,
        BodyPart part,
        CharacterView resolvedView)
    {
        if (resolvedView == null)
        {
            return character != null
                ? character.transform
                : null;
        }

        if (part == null)
            return GetLookAtTarget(character, resolvedView);

        Transform anchor = resolvedView.GetBodyPartAnchor(part);

        return anchor != null
            ? anchor
            : GetLookAtTarget(character, resolvedView);
    }

    public static Transform ResolveCameraPoint(
        BattleVisualRequest request,
        SkillCameraPointReference pointReference)
    {
        return TryResolveCameraPoint(
            request,
            pointReference,
            out Transform point,
            out _)
                ? point
                : null;
    }

    public static bool TryResolveCameraPoint(
        BattleVisualRequest request,
        SkillCameraPointReference pointReference,
        out Transform point,
        out string failureReason)
    {
        point = null;
        failureReason = string.Empty;

        if (request == null)
        {
            failureReason = "BattleVisualRequest가 null입니다.";
            return false;
        }

        if (pointReference == null)
        {
            failureReason = "SkillCameraPointReference가 null입니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(pointReference.Key))
        {
            failureReason = "CameraPoint Key가 비어 있습니다.";
            return false;
        }

        Character owner = ResolveOwner(request, pointReference.Owner);

        if (owner == null)
        {
            failureReason = $"CameraPoint Owner를 찾지 못했습니다. OwnerType={pointReference.Owner}";
            return false;
        }

        CharacterCameraPointSet pointSet = GetCameraPointSet(owner);

        if (pointSet != null &&
            pointSet.TryGetPoint(pointReference.Key, out point))
        {
            return point != null;
        }

        if (TryResolveBuiltInPoint(owner, pointReference.Key, out point))
            return point != null;

        failureReason =
            $"CharacterCameraPointSet에서 Key를 찾지 못했습니다. " +
            $"Character={GetCharacterName(owner)}, Key={pointReference.Key}";

        return false;
    }

    public static Character ResolveOwner(
        BattleVisualRequest request,
        SkillCameraPointOwner ownerType)
    {
        if (request == null)
            return null;

        return ownerType == SkillCameraPointOwner.Attacker
            ? request.Attacker
            : request.Target;
    }

    private static bool TryResolveBuiltInPoint(
        Character owner,
        string key,
        out Transform point)
    {
        point = null;

        if (owner == null || string.IsNullOrEmpty(key))
            return false;

        switch (key)
        {
            case "LookAt":
            case "LookAtPoint":
                point = GetLookAtTarget(owner);
                return point != null;

            case "Attack":
            case "AttackCameraPoint":
                point = GetAttackCameraPoint(owner);
                return point != null;

            case "Hit":
            case "HitCameraPoint":
                point = GetHitCameraPoint(owner);
                return point != null;
        }

        return false;
    }

    private static string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data != null
            ? character.Data.CharacterName
            : character.name;
    }
}
