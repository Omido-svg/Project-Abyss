using UnityEngine;

/// <summary>
/// 0916 유진 전용 키워드 「지정」.
/// 3턴 지속이며, 같은 anchor에 표식이 적립될 때 YujinMechanic이 +2를 가산한다.
/// </summary>
public sealed class YujinDesignationStatus : StatusEffect, IUniqueKeywordStatus
{
    public const string KeywordId = "yujin.designation";
    public string UniqueKeywordId => KeywordId;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.TurnEnd;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.RefreshDuration;

    public YujinDesignationStatus(int turns = 3)
    {
        Name = "지정";
        Duration = Mathf.Max(1, turns);
        Stack = 1;
    }

    public override bool CanMergeWith(StatusEffect other) =>
        other is YujinDesignationStatus;

    public override void Merge(StatusEffect other)
    {
        if (other is not YujinDesignationStatus designation)
            return;

        Duration = Mathf.Max(Duration, Mathf.Max(1, designation.Duration));
        Stack = 1;
    }
}
