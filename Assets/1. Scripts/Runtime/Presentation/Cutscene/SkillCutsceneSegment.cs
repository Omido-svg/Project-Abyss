/// <summary>
/// 스킬 컷씬을 구성하는 독립 Timeline Segment.
///
/// 이 파일이 SkillCutsceneSegment의 유일한 영구 정의다.
/// Legacy Cutscene SO 또는 SkillVisualDefinition에 같은 enum을 다시 선언하지 않는다.
/// </summary>
public enum SkillCutsceneSegment
{
    Action = 0,
    ClashAttack = 1,
    PartBreak = 2,
    Kill = 3,
    Return = 4
}
