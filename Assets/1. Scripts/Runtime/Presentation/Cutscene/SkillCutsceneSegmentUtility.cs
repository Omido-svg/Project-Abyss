using System.Collections.Generic;

/// <summary>
/// v8에서 스킬이 직접 소유하는 공격자 중심 Timeline Segment 집합입니다.
/// PartBreak / Kill / Return 값은 기존 직렬화 호환을 위해 enum에만 남아 있습니다.
/// </summary>
public static class SkillCutsceneSegmentUtility
{
    public static bool IsActiveAttackerSegment(
        SkillCutsceneSegment segment)
    {
        return segment == SkillCutsceneSegment.Action ||
               segment == SkillCutsceneSegment.ClashAttack;
    }

    public static IEnumerable<SkillCutsceneSegment>
        EnumerateActiveAttackerSegments()
    {
        yield return SkillCutsceneSegment.Action;
        yield return SkillCutsceneSegment.ClashAttack;
    }
}
