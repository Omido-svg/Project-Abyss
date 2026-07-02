using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class BattleLogger
{
    private readonly List<string> logs = new();

    public IReadOnlyList<string> Logs => logs;

    //------------------------------------------------

    public void Clear()
    {
        logs.Clear();
    }

    //------------------------------------------------
    // PRETURN / FORESIGHT / 단순 행동 로그
    //------------------------------------------------

    public void LogAction(
        BattleAction action,
        BattleLogType type)
    {
        string log =
            BattleLogEntry.Create(action)
                .SetType(type)
                .Build()
                .ToString();

        logs.Add(log);
    }

    //------------------------------------------------
    // 일방공격
    //------------------------------------------------

    public void LogOneSideResult(
        BattleAction action,
        int damage,
        int beforeHP,
        int afterHP,
        bool targetPartWasBrokenBeforeDamage = false)
    {
        if (action == null)
            return;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine("====================================");

        builder.AppendLine(
            $"{action.Owner.Data.CharacterName} ({action.OwnerPart.Type})");

        builder.AppendLine(
            $" -> {action.Target.Data.CharacterName} ({action.TargetPart.Type})");

        builder.AppendLine($"Skill : {action.Skill.SkillName}");
        builder.AppendLine($"Type  : {action.ActionType}");
        builder.AppendLine($"Speed : {action.Speed}");
        builder.AppendLine($"Power : {action.RolledPower}");

        AppendDamageLog(
            builder,
            damage,
            beforeHP,
            afterHP,
            targetPartWasBrokenBeforeDamage);

        builder.AppendLine();
        builder.AppendLine("----------------------------------");

        logs.Add(builder.ToString());
    }

    //------------------------------------------------
    // 일반 데미지
    //------------------------------------------------

    public void LogDamage(
        BattleAction action,
        BattleLogType type,
        int damage,
        int beforeHP,
        int afterHP)
    {
        string log =
            BattleLogEntry.Create(action)
                .SetType(type)
                .SetDamage(damage, beforeHP, afterHP)
                .SetBroken(action != null &&
                           action.TargetPart != null &&
                           action.TargetPart.IsBroken)
                .SetDead(action != null &&
                         action.Target != null &&
                         action.Target.IsDead)
                .Build()
                .ToString();

        logs.Add(log);
    }

    //------------------------------------------------
    // 합 결과
    //------------------------------------------------

    public void LogClashResult(
        BattleAction action,
        bool isWin,
        int myClash,
        int enemyClash,
        int damage = 0,
        int prestigeGain = 0,
        int beforeHP = 0,
        int afterHP = 0,
        bool targetPartWasBrokenBeforeDamage = false)
    {
        if (action == null)
            return;

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine("====================================");

        builder.AppendLine(
            $"{action.Owner.Data.CharacterName} ({action.OwnerPart.Type})");

        builder.AppendLine(
            $" -> {action.Target.Data.CharacterName} ({action.TargetPart.Type})");

        builder.AppendLine($"Skill : {action.Skill.SkillName}");
        builder.AppendLine($"Type  : {action.ActionType}");
        builder.AppendLine($"Speed : {action.Speed}");
        builder.AppendLine($"Power : {action.RolledPower}");

        builder.AppendLine($"Clash : {myClash} vs {enemyClash}");
        builder.AppendLine(isWin ? "WIN" : "LOSE");

        AppendDamageLog(
            builder,
            damage,
            beforeHP,
            afterHP,
            targetPartWasBrokenBeforeDamage);

        if (prestigeGain > 0)
        {
            builder.AppendLine($"Prestige : +{prestigeGain}");
        }

        builder.AppendLine();
        builder.AppendLine("----------------------------------");

        logs.Add(builder.ToString());
    }

    //------------------------------------------------

    public void PrintTurn(int turn)
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine("==================================");
        sb.AppendLine($"TURN {turn} RESULT");
        sb.AppendLine("==================================");

        foreach (string log in logs)
        {
            if (string.IsNullOrEmpty(log))
                continue;

            sb.AppendLine(log);
        }

        sb.AppendLine("==================================");

        Debug.Log(sb.ToString());

        logs.Clear();
    }

    //------------------------------------------------

    private void AppendDamageLog(
        StringBuilder builder,
        int damage,
        int beforeHP,
        int afterHP,
        bool targetPartWasBrokenBeforeDamage)
    {
        if (builder == null)
            return;

        if (damage <= 0)
            return;

        if (targetPartWasBrokenBeforeDamage)
        {
            builder.AppendLine("Target Part : Broken");
            builder.AppendLine($"Damage : {damage}");
            builder.AppendLine("Damage Type : Direct");
            return;
        }

        builder.AppendLine($"Damage : {damage}");
        builder.AppendLine($"HP : {beforeHP} -> {afterHP}");
    }
}