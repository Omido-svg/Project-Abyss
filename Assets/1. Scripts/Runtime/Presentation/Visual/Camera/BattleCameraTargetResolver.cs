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

}