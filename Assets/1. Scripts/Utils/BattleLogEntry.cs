using System.Text;

public enum BattleLogType
{
    Normal,
    Clash,
    Ambush,
    Prestige
}

public class BattleLogEntry
{
    public BattleAction Action;

    public BattleLogType ResultType;

    // Clash 전용
    public bool IsWinner;
    public int MyPower;
    public int EnemyPower;

    // 결과값
    public int Damage;
    public int PrestigeGain;

    public string Message;

    //--------------------------------
    // Builder
    //--------------------------------

    public static BattleLogBuilder Create(BattleAction action)
    {
        return new BattleLogBuilder(action);
    }

    //--------------------------------
    // Print
    //--------------------------------

    public override string ToString()
    {
        StringBuilder sb = new();

        sb.AppendLine($"{Action.Owner.Data.CharacterName} ({Action.OwnerPart.Type})");

        sb.AppendLine($"Skill : {Action.Skill.SkillName}");
        sb.AppendLine($"Type  : {Action.ActionType}");
        sb.AppendLine($"Speed : {Action.Speed}");
        sb.AppendLine($"Power : {Action.RolledPower}");

        sb.AppendLine($"Result: {ResultType}");

        // Clash 정보
        if (ResultType == BattleLogType.Clash)
        {
            sb.AppendLine($"Clash : {MyPower} vs {EnemyPower}");
            sb.AppendLine(IsWinner ? "WIN" : "LOSE");
        }

        if (Damage > 0)
            sb.AppendLine($"Damage : {Damage}");

        if (PrestigeGain > 0)
            sb.AppendLine($"Prestige +{PrestigeGain}");

        if (!string.IsNullOrEmpty(Message))
            sb.AppendLine(Message);

        return sb.ToString();
    }
}