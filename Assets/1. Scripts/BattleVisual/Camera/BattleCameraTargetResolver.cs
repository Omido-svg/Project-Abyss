using UnityEngine;

public static class BattleCameraTargetResolver
{
    public static CharacterView GetView(Character character)
    {
        if (character == null)
            return null;

        CharacterView view =
            character.GetComponent<CharacterView>();

        if (view != null)
            return view;

        return character.GetComponentInChildren<CharacterView>(true);
    }

    public static Transform GetLookAtTarget(Character character)
    {
        CharacterView view =
            GetView(character);

        return GetLookAtTarget(
            character,
            view);
    }

    public static Transform GetLookAtTarget(
        Character character,
        CharacterView resolvedView)
    {
        CharacterView view = resolvedView;

        if (view != null &&
            view.LookAtPoint != null)
        {
            return view.LookAtPoint;
        }

        return character != null
            ? character.transform
            : null;
    }

    public static Transform GetAttackCameraPoint(Character character)
    {
        CharacterView view =
            GetView(character);

        return GetAttackCameraPoint(
            character,
            view);
    }

    public static Transform GetAttackCameraPoint(
        Character character,
        CharacterView resolvedView)
    {
        CharacterView view = resolvedView;

        if (view != null &&
            view.AttackCameraPoint != null)
        {
            return view.AttackCameraPoint;
        }

        return GetLookAtTarget(
            character,
            view);
    }

    public static Transform GetHitCameraPoint(Character character)
    {
        CharacterView view =
            GetView(character);

        return GetHitCameraPoint(
            character,
            view);
    }

    public static Transform GetHitCameraPoint(
        Character character,
        CharacterView resolvedView)
    {
        CharacterView view = resolvedView;

        if (view != null &&
            view.HitCameraPoint != null)
        {
            return view.HitCameraPoint;
        }

        return GetLookAtTarget(
            character,
            view);
    }

    public static Transform GetTargetPartAnchor(
        Character character,
        BodyPart part)
    {
        CharacterView view =
            GetView(character);

        return GetTargetPartAnchor(
            character,
            part,
            view);
    }

    public static Transform GetTargetPartAnchor(
        Character character,
        BodyPart part,
        CharacterView resolvedView)
    {
        CharacterView view = resolvedView;

        if (view == null)
        {
            return character != null
                ? character.transform
                : null;
        }

        if (part == null)
        {
            return GetLookAtTarget(
                character,
                view);
        }

        Transform anchor =
            view.GetBodyPartAnchor(part);

        return anchor != null
            ? anchor
            : GetLookAtTarget(
                character,
                view);
    }

    public static Transform ResolveCameraPoint(
        BattleVisualRequest request,
        SkillCameraPointReference pointReference)
    {
        if (request == null ||
            pointReference == null)
        {
            return null;
        }

        Character owner =
            pointReference.Owner == SkillCameraPointOwner.Attacker
                ? request.Attacker
                : request.Target;

        if (owner == null)
            return null;

        CharacterCameraPointSet pointSet =
            owner.GetComponentInChildren<CharacterCameraPointSet>(true);

        if (pointSet == null)
            return null;

        return pointSet.GetPoint(
            pointReference.Key);
    }
}
