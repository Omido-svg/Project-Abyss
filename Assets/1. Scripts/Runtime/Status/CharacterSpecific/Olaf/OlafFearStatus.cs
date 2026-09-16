using UnityEngine;

/// <summary>
/// 올라프 전용 「공포」. 적 위력 -1, 비누적, 3턴. 재부여 시 지속만 갱신한다.
/// </summary>
public sealed class OlafFearStatus : StatusEffect, IUniqueKeywordStatus
{
    public const string KeywordId = "olaf.fear";
    public const int DefaultDuration = 3;

    public string UniqueKeywordId => KeywordId;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    public OlafFearStatus(int duration = DefaultDuration)
    {
        Name = "공포";
        Stack = 1;
        Duration = Mathf.Max(1, duration);
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner)
            return roll;

        return Mathf.Max(1, roll - 1);
    }
}
