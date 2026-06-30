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

    public bool IsWinner;

    public int MyPower;
    public int EnemyPower;

    public int Damage;
    public int PrestigeGain;

    public int TargetHPBefore;
    public int TargetHPAfter;

    public bool TargetPartBroken;
    public bool TargetDead;

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

        sb.AppendLine(
            $"{GetCharacterName(Action?.Owner)} " +
            $"({GetPartName(Action?.OwnerPart)})");

        sb.AppendLine(
            $" -> {GetCharacterName(Action?.Target)} " +
            $"({GetPartName(Action?.TargetPart)})");

        sb.AppendLine($"Skill : {GetSkillName(Action?.Skill)}");
        sb.AppendLine($"Type  : {GetActionType()}");

        sb.AppendLine($"Speed : {GetSpeed()}");
        sb.AppendLine($"Power : {GetPower()}");

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

    //--------------------------------

    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }

    private string GetPartName(BodyPart part)
    {
        if (part == null)
            return "NULL";

        return part.Type.ToString();
    }

    private string GetSkillName(Skill skill)
    {
        if (skill == null)
            return "NULL";

        return skill.SkillName;
    }

    private string GetActionType()
    {
        if (Action == null)
            return "NULL";

        if (Action.Skill == null)
            return "NULL";

        return Action.ActionType.ToString();
    }

    private int GetSpeed()
    {
        if (Action == null)
            return 0;

        return Action.Speed;
    }

    private int GetPower()
    {
        if (Action == null)
            return 0;

        if (Action.finalPower != 0)
            return Action.finalPower;

        if (Action.HasRolled)
            return Action.RolledPower;

        return 0;
    }
}