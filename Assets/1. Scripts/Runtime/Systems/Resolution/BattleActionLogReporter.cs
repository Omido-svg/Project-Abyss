/// <summary>
/// ActionResolver의 전투 해석과 로그 출력 책임을 분리한다.
/// </summary>
public interface IBattleActionReporter
{
    void ReportStandaloneAction(BattleAction action);
}

public sealed class BattleActionLogReporter : IBattleActionReporter
{
    private readonly BattleLogger logger;

    public BattleActionLogReporter(BattleLogger logger)
    {
        this.logger = logger;
    }

    public void ReportStandaloneAction(BattleAction action)
    {
        if (action == null || logger == null)
            return;

        BattleLogType type =
            action.Phase switch
            {
                ActionPhase.PRETURN => BattleLogType.Prestige,
                ActionPhase.FORESIGHT => BattleLogType.Preparation,
                _ => BattleLogType.Normal
            };

        if (action.HasDamageLog)
        {
            logger.LogDamage(
                action,
                type,
                action.LoggedDamage,
                action.LoggedBeforeHP,
                action.LoggedAfterHP);
            return;
        }

        logger.LogAction(action, type);
    }
}
