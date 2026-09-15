/// <summary>
/// 0915 C-09: 한 Action의 남은 paired/one-sided roll을 계속할 수 있는지 판단한다.
/// 핵심은 "이번 Action 도중 target이 사망/파괴되었는가"이며, 처음부터 파괴된 부위를
/// 조준한 별도 Action은 1.5 규칙대로 전체 HP 직접 일방타격을 수행할 수 있어야 한다.
/// </summary>
public static class ClashContinuationPolicy
{
    public static bool CanContinue(BattleAction action)
    {
        if (action == null || action.Owner == null || action.Skill == null)
            return false;

        if (action.Owner.IsDead)
            return false;

        if (action.Target != null && action.Target.IsDead)
            return false;

        if (action.OwnerPart != null && action.OwnerPart.IsBroken)
            return false;

        // C-09: primary target을 이번 Action의 앞선 피해가 종료시켰다면
        // 같은 슬롯을 향한 남은 굴림은 즉시 소멸한다.
        if (action.PrimaryTargetTerminatedDuringResolution)
            return false;

        return true;
    }
}
