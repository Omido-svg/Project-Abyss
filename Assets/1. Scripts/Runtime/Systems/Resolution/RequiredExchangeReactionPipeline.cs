using System.Collections.Generic;

/// <summary>
/// 개별 교환에서 반드시 적용되어야 하는 CombatMechanic 규칙을
/// OnExchangeResolved observer 통보보다 먼저 실행한다.
///
/// 현재는 StaggerGaugeMechanic이 이 계약을 사용한다.
/// 전투 참가자/메커닉 순서대로 동기 실행하며, 필수 규칙 예외는 삼키지 않는다.
/// </summary>
public sealed class RequiredExchangeReactionPipeline
{
    private readonly BattleContext battleContext;

    public RequiredExchangeReactionPipeline(
        BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    public void Apply(
        ClashExchangeResult exchange)
    {
        if (exchange == null ||
            battleContext == null)
        {
            return;
        }

        Character player =
            battleContext.Player;

        ApplyCharacter(
            player,
            exchange);

        IReadOnlyList<Character> enemies =
            battleContext.Enemies;

        if (enemies == null)
            return;

        foreach (Character enemy in enemies)
        {
            if (enemy == null ||
                ReferenceEquals(enemy, player))
            {
                continue;
            }

            ApplyCharacter(
                enemy,
                exchange);
        }
    }

    private static void ApplyCharacter(
        Character character,
        ClashExchangeResult exchange)
    {
        IReadOnlyList<CombatMechanic> mechanics =
            character?.Mechanics;

        if (mechanics == null)
            return;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IRequiredExchangeReaction reaction)
            {
                reaction.ApplyRequiredExchangeReaction(
                    exchange);
            }
        }
    }
}
