using UnityEngine;

/// <summary>
/// 올라프 전용 「공포」. 적 위력 -1, 비누적, 3턴. 재부여 시 지속만 max 갱신한다.
/// </summary>
public sealed class OlafFearStatus : PresenceTimedStatus, IUniqueKeywordStatus
{
    public const string KeywordId = "olaf.fear";
    public const int DefaultDuration = 3;

    public string UniqueKeywordId => KeywordId;

    public OlafFearStatus(int duration = DefaultDuration)
        : base("공포", duration)
    {
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner)
            return roll;

        return Mathf.Max(1, roll - 1);
    }
}
