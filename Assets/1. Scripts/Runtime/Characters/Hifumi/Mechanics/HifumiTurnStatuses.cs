using UnityEngine;

/// <summary>히후미의 다음 턴 속도 -1. 다음 TurnStart에 부여되고 그 TurnEnd에 제거된다.</summary>
public sealed class HifumiSpeedPenaltyStatus : StatusEffect
{
    public override StatusEffectDurationPolicy DurationPolicy => StatusEffectDurationPolicy.TurnEnd;
    public override StatusEffectStackPolicy StackPolicy => StatusEffectStackPolicy.RefreshDuration;

    public HifumiSpeedPenaltyStatus()
    {
        Name = "판단 후유증";
        Stack = 1;
        Duration = 1;
    }

    public override int ModifySpeed(BodyPart part, int speed) => Mathf.Max(0, speed - 1);
}
