/// <summary>
/// 공격자의 캐릭터 고유 규칙이 한 타격에 추가 흐트러짐 피해를 더할 때 사용하는 계약.
/// 반환값은 내성 적용 전 raw stagger damage다.
/// </summary>
public interface IAdditionalStaggerDamageProvider
{
    int GetAdditionalStaggerDamage(
        BattleAction action,
        Character target);
}
