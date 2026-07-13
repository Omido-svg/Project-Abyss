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

        if (view != null &&
            view.AttackCameraPoint != null)
        {
            return view.AttackCameraPoint;
        }

        return GetLookAtTarget(character);
    }

    public static Transform GetHitCameraPoint(Character character)
    {
        CharacterView view =
            GetView(character);

        if (view != null &&
            view.HitCameraPoint != null)
        {
            return view.HitCameraPoint;
        }

        return GetLookAtTarget(character);
    }

    public static Transform GetTargetPartAnchor(
        Character character,
        BodyPart part)
    {
        CharacterView view =
            GetView(character);

        if (view == null)
            return GetLookAtTarget(character);

        if (part == null)
            return view.LookAtPoint;

        Transform anchor =
            view.GetBodyPartAnchor(part);

        return anchor != null
            ? anchor
            : view.LookAtPoint;
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