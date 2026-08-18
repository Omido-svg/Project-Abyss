/// <summary>
/// Presentation 계층이 SkillDefinition의 불투명 presentation extension을
/// 구체 연출 타입으로 해석하는 단방향 adapter.
/// </summary>
public static class SkillPresentationAccess
{
    public static SkillVisualDefinition Get(
        SkillDefinition definition)
    {
        return definition?.PresentationAsset
            as SkillVisualDefinition;
    }

    public static void Set(
        SkillDefinition definition,
        SkillVisualDefinition visual)
    {
        definition?.SetPresentationAsset(visual);
    }
}
