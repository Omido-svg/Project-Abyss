/// <summary>
/// 스킬 컷씬 Timeline Segment의 직렬화 호환 enum.
///
/// v8 런타임에서 활성 Segment는 Action / ClashAttack 두 개뿐입니다.
/// PartBreak / Kill / Return 값은 기존 에셋의 정수 직렬화 호환을 위해 남겨 둡니다.
/// </summary>
public enum SkillCutsceneSegment
{
    Action = 0,
    ClashAttack = 1,
    PartBreak = 2,
    Kill = 3,
    Return = 4
}