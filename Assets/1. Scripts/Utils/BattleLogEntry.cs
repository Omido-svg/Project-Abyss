using System.Text;

public enum BattleLogType
{
    Normal,
    Clash,
    Preparation,
    Prestige
}

public class BattleLogEntry
{
    public BattleAction Action;

    public BattleLogType Type;

    // 결과
    public bool IsWinner;

    // 수치
    public int MyPower;
    public int EnemyPower;

    public int Damage;
    public int PrestigeGain;

    // HP
    public int TargetHPBefore;
    public int TargetHPAfter;

    // 상태
    public bool TargetPartBroken;
    public bool TargetDead;

    // 기타
    public string Message;

    //--------------------------------

    public static BattleLogBuilder Create(BattleAction action)
    {
        return new BattleLogBuilder(action);
    }

    //--------------------------------

    public override string ToString()
    {
        StringBuilder sb = new();

        sb.AppendLine("====================================");

        sb.AppendLine($"{Action.Owner.Data.CharacterName} ({Action.OwnerPart.Type})");
        sb.AppendLine($" -> {Action.Target.Data.CharacterName} ({Action.TargetPart.Type})");

        sb.AppendLine($"Skill : {Action.Skill.SkillName}");
        sb.AppendLine($"Type  : {Action.ActionType}");

        sb.AppendLine($"Speed : {Action.Speed}");
        sb.AppendLine($"Power : {Action.finalPower}");

        if (Type == BattleLogType.Clash)
        {
            sb.AppendLine($"Clash : {MyPower} vs {EnemyPower}");
            sb.AppendLine(IsWinner ? "WIN" : "LOSE");
        }

        if (Damage > 0)
            sb.AppendLine($"Damage : {Damage}");

        if (PrestigeGain > 0)
            sb.AppendLine($"Prestige : +{PrestigeGain}");

        if (TargetHPBefore != TargetHPAfter)
            sb.AppendLine($"HP : {TargetHPBefore} -> {TargetHPAfter}");

        if (TargetPartBroken)
            sb.AppendLine("Part Broken");

        if (TargetDead)
            sb.AppendLine("Target Dead");

        if (!string.IsNullOrEmpty(Message))
            sb.AppendLine(Message);

        return sb.ToString();
    }
}