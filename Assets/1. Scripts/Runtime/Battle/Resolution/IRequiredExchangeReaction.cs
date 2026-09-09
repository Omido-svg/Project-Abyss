/// <summary>
/// 한 교환의 "완료 사실"을 BattleEvent로 발행하기 전에 반드시 적용되어야 하는
/// 게임 규칙용 계약.
///
/// Presentation/로그 관찰자가 결과를 받은 뒤 결과를 완성하는 구조를 막기 위해 사용한다.
/// 구현은 이미 생성된 ClashExchangeResult에 필수 결과를 기록할 수 있으며,
/// 이 단계가 끝난 뒤에만 OnExchangeResolved가 발행된다.
/// </summary>
public interface IRequiredExchangeReaction
{
    void ApplyRequiredExchangeReaction(
        ClashExchangeResult exchange);
}
