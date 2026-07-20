using UnityEngine;

/// <summary>
/// 도사림 효과의 공통 source-part 보정.
/// 현재 확정 규칙은 약화된 다리에서 사용한 도사림의 flat 효과를 낮춘다.
/// </summary>
public static class PreparationEffectUtility
{
    public static bool IsWeakenedLegSource(
        SkillEffectContext context)
    {
        BodyPart sourcePart =
            context?.Action?.OwnerPart;

        return sourcePart != null &&
               sourcePart.Type == PartType.LEGS &&
               sourcePart.IsWeakened;
    }

    public static int ApplyPositiveFlatPenalty(
        SkillEffectContext context,
        int value,
        int weakenedLegPenalty)
    {
        int safeValue = Mathf.Max(0, value);

        if (!IsWeakenedLegSource(context))
            return safeValue;

        return Mathf.Max(
            0,
            safeValue - Mathf.Max(0, weakenedLegPenalty));
    }
}
