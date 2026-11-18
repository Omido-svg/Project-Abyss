/// <summary>
/// 굴림이 없는 공격 스킬(예: 히후미 평타③ 동귀어진). 합을 받지 않으며(CanClash = false),
/// 해결 순서가 오면 굴림 · 교환 · 히트 없이 이 메서드로 효과만 처리한다.
/// ClashManager의 일방 해결 경로(일반 · Stepwise 공통)에서 한 번 호출된다.
/// </summary>
public interface IRollessResolutionSkill
{
    void ResolveWithoutRolls(BattleAction action, BattleContext battleContext);
}
