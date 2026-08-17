using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

public sealed class CharacterVerificationContext
{
    public CharacterVerificationProfile Profile { get; }
    public CharacterVerificationCaseDefinition Definition { get; }
    public CharacterAuthoringBundle Bundle { get; }
    public Character Character { get; }
    public BattleContext BattleContext { get; }
    public bool IsLiveScene { get; }

    public Character OpponentCharacter
    {
        get
        {
            if (BattleContext?.Enemies == null)
                return null;

            for (int i = 0;
                 i < BattleContext.Enemies.Count;
                 i++)
            {
                Character candidate =
                    BattleContext.Enemies[i];

                if (candidate != null &&
                    candidate != Character)
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    private readonly List<string> eventLog =
        new List<string>();

    public IReadOnlyList<string> EventLog =>
        eventLog;

    public CharacterVerificationContext(
        CharacterVerificationProfile profile,
        CharacterVerificationCaseDefinition definition,
        Character character,
        BattleContext battleContext,
        bool isLiveScene)
    {
        Profile = profile;
        Definition = definition;
        Bundle = profile?.Bundle;
        Character = character;
        BattleContext = battleContext;
        IsLiveScene = isLiveScene;
    }

    public void Log(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            eventLog.Add(message);
    }

    public CharacterVerificationCaseResult Pass(
        string expected,
        string actual,
        string details = null)
    {
        return CreateResult(
            CharacterVerificationStatus.Pass,
            expected,
            actual,
            details);
    }

    public CharacterVerificationCaseResult Fail(
        string expected,
        string actual,
        string details = null)
    {
        return CreateResult(
            CharacterVerificationStatus.Fail,
            expected,
            actual,
            details);
    }

    public CharacterVerificationCaseResult Skip(
        string reason)
    {
        return CreateResult(
            CharacterVerificationStatus.Skip,
            "검증 실행",
            "건너뜀",
            reason);
    }

    public CharacterVerificationCaseResult Error(
        Exception exception)
    {
        return CreateResult(
            CharacterVerificationStatus.Error,
            "예외 없이 완료",
            exception?.GetType().Name ?? "UnknownException",
            exception?.ToString());
    }

    private CharacterVerificationCaseResult CreateResult(
        CharacterVerificationStatus status,
        string expected,
        string actual,
        string details)
    {
        string eventText =
            eventLog.Count == 0
                ? string.Empty
                : "\n\nEvent Log\n- " +
                  string.Join("\n- ", eventLog);

        return new CharacterVerificationCaseResult
        {
            CaseId = Definition?.CaseId ?? "UNKNOWN",
            DisplayName =
                Definition?.DisplayName ??
                Definition?.CaseId ??
                "Unknown Verification",
            Category =
                Definition?.Category ??
                CharacterVerificationCategory.Data,
            ExecutionMode =
                Definition?.ExecutionMode ??
                CharacterVerificationExecutionMode.DataOnly,
            Status = status,
            Expected = expected ?? string.Empty,
            Actual = actual ?? string.Empty,
            Details =
                (details ?? string.Empty) +
                eventText
        };
    }

    public static string DescribeSnapshot(
        Character character)
    {
        if (character == null)
            return "Character=NULL";

        CharacterRuntimeSnapshot snapshot =
            character.CaptureRuntimeSnapshot();

        if (snapshot == null)
            return "Snapshot=NULL";

        StringBuilder builder =
            new StringBuilder();

        builder.Append(
            $"{snapshot.CharacterName} / " +
            $"Initialized={character.IsInitialized} / " +
            $"HP={snapshot.CurrentHP}/{snapshot.MaxHP} / " +
            $"Block={snapshot.CurrentBlock} / " +
            $"Prestige={snapshot.CurrentPrestige}");

        if (snapshot.CustomResources != null &&
            snapshot.CustomResources.Count > 0)
        {
            builder.Append(" / Resources=");

            bool first = true;

            foreach (KeyValuePair<string, int> pair
                     in snapshot.CustomResources)
            {
                if (!first)
                    builder.Append(", ");

                builder.Append(pair.Key);
                builder.Append(':');
                builder.Append(pair.Value);
                first = false;
            }
        }

        if (snapshot.BodyParts != null &&
            snapshot.BodyParts.Count > 0)
        {
            builder.Append(" / Parts=[");

            for (int i = 0;
                 i < snapshot.BodyParts.Count;
                 i++)
            {
                if (i > 0)
                    builder.Append(", ");

                BodyPartRuntimeSnapshot part =
                    snapshot.BodyParts[i];

                builder.Append(
                    $"{part.PartType} " +
                    $"{part.CurrentHP}/{part.MaxHP} " +
                    $"{part.State}");
            }

            builder.Append(']');
        }

        return builder.ToString();
    }
}

public static class CharacterVerificationReflection
{
    private const BindingFlags Flags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    public static bool TrySetField(
        object target,
        string fieldName,
        object value)
    {
        if (target == null ||
            string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        FieldInfo field =
            FindField(
                target.GetType(),
                fieldName);

        if (field == null)
            return false;

        field.SetValue(
            target,
            value);

        return true;
    }

    public static bool TryGetField<T>(
        object target,
        string fieldName,
        out T value)
    {
        value = default;

        if (target == null ||
            string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        FieldInfo field =
            FindField(
                target.GetType(),
                fieldName);

        if (field == null)
            return false;

        object raw =
            field.GetValue(target);

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    public static bool TryInvoke(
        object target,
        string methodName,
        object[] arguments,
        out object result)
    {
        result = null;

        if (target == null ||
            string.IsNullOrWhiteSpace(methodName))
        {
            return false;
        }

        MethodInfo[] methods =
            target.GetType().GetMethods(Flags);

        int argumentCount =
            arguments?.Length ?? 0;

        for (int i = 0;
             i < methods.Length;
             i++)
        {
            MethodInfo method =
                methods[i];

            if (!string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal) ||
                method.GetParameters().Length !=
                argumentCount)
            {
                continue;
            }

            result =
                method.Invoke(
                    target,
                    arguments);

            return true;
        }

        return false;
    }

    private static FieldInfo FindField(
        Type type,
        string fieldName)
    {
        Type current = type;

        while (current != null)
        {
            FieldInfo field =
                current.GetField(
                    fieldName,
                    Flags);

            if (field != null)
                return field;

            current =
                current.BaseType;
        }

        return null;
    }
}