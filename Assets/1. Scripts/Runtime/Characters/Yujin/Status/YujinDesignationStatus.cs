using UnityEngine;

/// <summary>
/// 0922 유진 전용 수치형 키워드 「지정」.
/// NumericTimed N/T independent Entry이며, 실제 표식 보너스 계산은 YujinMechanic에서 수행한다.
/// </summary>
public sealed class YujinDesignationStatus : NumericTimedStatus, IUniqueKeywordStatus
{
    public const string KeywordId = "yujin.designation";
    public string UniqueKeywordId => KeywordId;

    /// <summary>
    /// 구 호출부 호환: 단일 int는 Duration으로 해석하고 N=1을 사용한다.
    /// </summary>
    public YujinDesignationStatus(int turns = 3)
        : this(1, turns)
    {
    }

    /// <summary>
    /// 0922 canonical N/T 생성자.
    /// </summary>
    public YujinDesignationStatus(
        int value,
        int duration)
        : base(
            "지정",
            Mathf.Max(0, value),
            duration)
    {
    }
}
