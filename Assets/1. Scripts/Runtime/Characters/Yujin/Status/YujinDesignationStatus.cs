using UnityEngine;

/// <summary>
/// 0922 유진 전용 수치형 키워드 「지정」.
/// Phase 2에서는 independent NumericTimed 저장만 적용하고, 실제 N authoring/표식 공식은 Phase 6에서 확정한다.
/// </summary>
public sealed class YujinDesignationStatus : NumericTimedStatus, IUniqueKeywordStatus
{
    public const string KeywordId = "yujin.designation";
    public string UniqueKeywordId => KeywordId;

    // 기존 호출부는 단일 int를 duration으로 넘기므로 Phase 6 전까지 시그니처를 보존한다.
    public YujinDesignationStatus(int turns = 3)
        : base("지정", 1, turns)
    {
    }
}
