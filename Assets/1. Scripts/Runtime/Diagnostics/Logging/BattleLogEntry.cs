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

    public BattleLogCategory Category =
        BattleLogCategory.Combat;

    public BattleLogLevel Level =
        BattleLogLevel.Info;

    public int TurnNumber;
    public int Sequence;

    public bool IsWinner;

    public int MyPower;
    public int EnemyPower;

    public int Damage;
    public int PrestigeGain;

    public int TargetHPBefore;
    public int TargetHPAfter;

    public bool TargetPartBroken;
    public bool TargetPartWasBrokenBeforeDamage;
    public bool TargetDead;

    public string Message;

    //--------------------------------
    // BattleAction 변경과 무관한 스냅샷
    //--------------------------------

    public long ActionId;
    public int ActionIndex;

    public string OwnerName;
    public string OwnerPartName;

    public string TargetName;
    public string TargetPartName;

    public string SkillName;
    public string ActionTypeName;

    public int Speed;
    public int PurePower;
    public int ClashPower;
    public int SpeedModifier;
    public int MomentumModifier;
    public int PreparationModifier;
    public bool Critical;

    private bool snapshotCaptured;

    public static BattleLogBuilder Create(
        BattleAction action)
    {
        return new BattleLogBuilder(
            action);
    }

    public void CaptureActionSnapshot()
    {
        if (snapshotCaptured)
            return;

        snapshotCaptured = true;

        if (Action == null)
        {
            OwnerName = "NULL";
            OwnerPartName = "NONE";
            TargetName = "NULL";
            TargetPartName = "NONE";
            SkillName = "NULL";
            ActionTypeName = "NULL";
            return;
        }

        ActionId =
            Action.ActionId;

        ActionIndex =
            Action.ActionIndex;

        OwnerName =
            GetCharacterName(
                Action.Owner);

        OwnerPartName =
            GetOwnerPartName(
                Action.OwnerPart);

        TargetName =
            GetCharacterName(
                Action.Target);

        TargetPartName =
            GetTargetPartName(
                Action.Target,
                Action.TargetPart);

        SkillName =
            Action.Skill?.SkillName ??
            "NULL";

        ActionTypeName =
            Action.Skill == null
                ? "NULL"
                : Action.ActionType.ToString();

        Speed =
            Action.Speed;

        PurePower =
            Action.RolledPower;

        ClashPower =
            Action.ClashPower;

        SpeedModifier =
            Action.SpeedModifier;

        MomentumModifier =
            Action.MomentumModifier;

        PreparationModifier =
            Action.PreparationModifier;

        Critical =
            Action.Critical;
    }

    public override string ToString()
    {
        CaptureActionSnapshot();

        StringBuilder builder =
            new();

        builder.AppendLine(
            "====================================");

        builder.AppendLine(
            $"[{BattleDebugLog.GetCategoryLabel(Category)}] " +
            $"Seq={Sequence}, Turn={TurnNumber}, " +
            $"ActionId={ActionId}, Index={ActionIndex}");

        builder.AppendLine(
            $"{OwnerName} ({OwnerPartName})");

        builder.AppendLine(
            $" -> {TargetName} ({TargetPartName})");

        builder.AppendLine(
            $"Skill : {SkillName}");

        builder.AppendLine(
            $"Type  : {ActionTypeName}");

        builder.AppendLine(
            $"Speed : {Speed}");

        builder.AppendLine(
            $"Power : {PurePower}");

        builder.AppendLine(
            $"Critical : {(Critical ? "YES" : "NO")}");

        if (Type == BattleLogType.Clash)
        {
            builder.AppendLine(
                $"Clash : {MyPower} vs {EnemyPower}");

            builder.AppendLine(
                $"Clash Breakdown : " +
                $"{PurePower} " +
                $"{FormatSigned("Speed", SpeedModifier)} " +
                $"{FormatSigned("Momentum", MomentumModifier)} " +
                $"{FormatSigned("Preparation", PreparationModifier)} " +
                $"= {ClashPower}");

            builder.AppendLine(
                IsWinner
                    ? "WIN"
                    : "LOSE");
        }

        if (Damage > 0)
        {
            builder.AppendLine(
                $"Damage : {Damage}");

            if (TargetPartWasBrokenBeforeDamage)
            {
                builder.AppendLine(
                    "Damage Route : Direct HP " +
                    "(target part was already broken)");
            }
            else if (TargetHPBefore !=
                     TargetHPAfter)
            {
                builder.AppendLine(
                    $"HP : {TargetHPBefore} " +
                    $"-> {TargetHPAfter}");
            }
        }

        if (PrestigeGain > 0)
        {
            builder.AppendLine(
                $"Prestige : +{PrestigeGain}");
        }

        if (TargetPartBroken)
            builder.AppendLine("Part Broken");

        if (TargetDead)
            builder.AppendLine("Target Dead");

        if (!string.IsNullOrWhiteSpace(Message))
            builder.AppendLine(Message);

        return builder.ToString();
    }

    private static string FormatSigned(
        string label,
        int value)
    {
        return value >= 0
            ? $"+ {label} {value}"
            : $"- {label} {-value}";
    }

    private static string GetCharacterName(
        Character character)
    {
        if (character == null)
            return "NULL";

        return character.Data?.CharacterName ??
               character.name ??
               "NULL";
    }

    private static string GetOwnerPartName(
        BodyPart part)
    {
        return part == null
            ? "NONE"
            : part.Type.ToString();
    }

    private static string GetTargetPartName(
        Character target,
        BodyPart part)
    {
        if (part != null)
            return part.Type.ToString();

        return target == null
            ? "NONE"
            : "SINGLE_HP";
    }
}