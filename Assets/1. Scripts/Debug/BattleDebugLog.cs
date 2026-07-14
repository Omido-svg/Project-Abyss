using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum BattleLogCategory
{
    None = 0,

    Combat = 1 << 0,
    UI = 1 << 1,
    Visual = 1 << 2,
    VFX = 1 << 3,
    AI = 1 << 4,
    Event = 1 << 5,
    Damage = 1 << 6,
    Status = 1 << 7,

    All =
        Combat |
        UI |
        Visual |
        VFX |
        AI |
        Event |
        Damage |
        Status
}

public enum BattleLogLevel
{
    Trace = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4
}

[Serializable]
public sealed class BattleDebugMessage
{
    public BattleLogCategory Category;
    public BattleLogLevel Level;
    public string Message;
    public int Frame;
    public float Realtime;
    public UnityEngine.Object Context;

    public string CategoryLabel =>
        BattleDebugLog.GetCategoryLabel(Category);
}

public static class BattleDebugLog
{
    private const int DefaultRecentCapacity = 250;

    private static readonly List<BattleDebugMessage>
        recentMessages = new();

    private static BattleLogCategory enabledCategories =
        BattleLogCategory.Combat |
        BattleLogCategory.Damage |
        BattleLogCategory.Status |
        BattleLogCategory.Event;

    private static BattleLogLevel minimumLevel =
        BattleLogLevel.Info;

    private static bool includeRealtime;
    private static bool includeFrame = true;
    private static bool captureRecentMessages = true;
    private static int recentCapacity =
        DefaultRecentCapacity;

    public static event Action<BattleDebugMessage>
        MessageEmitted;

    public static BattleLogCategory EnabledCategories =>
        enabledCategories;

    public static BattleLogLevel MinimumLevel =>
        minimumLevel;

    public static IReadOnlyList<BattleDebugMessage>
        RecentMessages =>
            recentMessages;

    //------------------------------------------------
    // 기존 호출부 호환 프로퍼티
    //------------------------------------------------

    public static bool ShowSpeedRoll =>
        IsEnabled(
            BattleLogCategory.Combat,
            BattleLogLevel.Trace);

    public static bool ShowActionSlot =>
        IsEnabled(
            BattleLogCategory.Combat,
            BattleLogLevel.Trace);

    public static bool ShowClashBuild =>
        IsEnabled(
            BattleLogCategory.Combat,
            BattleLogLevel.Trace);

    public static bool ShowUIInput =>
        IsEnabled(
            BattleLogCategory.UI,
            BattleLogLevel.Trace);

    public static bool ShowSkillPanel =>
        IsEnabled(
            BattleLogCategory.UI,
            BattleLogLevel.Trace);

    public static bool ShowBattleFlow =>
        IsEnabled(
            BattleLogCategory.Combat,
            BattleLogLevel.Info);

    public static bool ShowCombatResult =>
        IsEnabled(
            BattleLogCategory.Combat,
            BattleLogLevel.Info);

    public static bool ShowCombatEffect =>
        IsEnabled(
            BattleLogCategory.Combat,
            BattleLogLevel.Info);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        enabledCategories =
            BattleLogCategory.Combat |
            BattleLogCategory.Damage |
            BattleLogCategory.Status |
            BattleLogCategory.Event;

        minimumLevel =
            BattleLogLevel.Info;

        includeRealtime = false;
        includeFrame = true;
        captureRecentMessages = true;
        recentCapacity =
            DefaultRecentCapacity;

        recentMessages.Clear();
        MessageEmitted = null;
    }

    public static void Configure(
        BattleLogCategory categories,
        BattleLogLevel level,
        bool showRealtime,
        bool showFrame,
        bool captureRecent,
        int maxRecentMessages)
    {
        enabledCategories =
            categories & BattleLogCategory.All;

        minimumLevel =
            level;

        includeRealtime =
            showRealtime;

        includeFrame =
            showFrame;

        captureRecentMessages =
            captureRecent;

        recentCapacity =
            Mathf.Max(
                0,
                maxRecentMessages);

        TrimRecentMessages();
    }

    public static void SetCategoryEnabled(
        BattleLogCategory category,
        bool enabled)
    {
        category &=
            BattleLogCategory.All;

        if (enabled)
        {
            enabledCategories |=
                category;
        }
        else
        {
            enabledCategories &=
                ~category;
        }
    }

    public static void SetMinimumLevel(
        BattleLogLevel level)
    {
        minimumLevel =
            level;
    }

    public static bool IsEnabled(
        BattleLogCategory category,
        BattleLogLevel level =
            BattleLogLevel.Info)
    {
        if (level == BattleLogLevel.None)
            return false;

        // 오류는 필터 설정과 관계없이 남겨야
        // 전투 시뮬레이션 실패를 놓치지 않는다.
        if (level >= BattleLogLevel.Error)
            return true;

        if (level < minimumLevel)
            return false;

        category &=
            BattleLogCategory.All;

        if (category == BattleLogCategory.None)
            category = BattleLogCategory.Combat;

        return
            (enabledCategories & category) != 0;
    }

    public static void ClearRecentMessages()
    {
        recentMessages.Clear();
    }

    public static List<BattleDebugMessage>
        GetRecentMessages(
            BattleLogCategory categories,
            BattleLogLevel level,
            int maximumCount)
    {
        List<BattleDebugMessage> result =
            new();

        if (maximumCount <= 0)
            return result;

        categories &=
            BattleLogCategory.All;

        if (categories == BattleLogCategory.None)
            categories = BattleLogCategory.All;

        for (int i = recentMessages.Count - 1;
             i >= 0;
             i--)
        {
            BattleDebugMessage message =
                recentMessages[i];

            if (message == null)
                continue;

            if (message.Level < level)
                continue;

            if ((message.Category & categories) == 0)
                continue;

            result.Add(message);

            if (result.Count >= maximumCount)
                break;
        }

        result.Reverse();
        return result;
    }

    //------------------------------------------------
    // 표준 로그 API
    //------------------------------------------------

    public static void Log(
        BattleLogCategory category,
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (!IsEnabled(category, level))
            return;

        BattleDebugMessage debugMessage =
            new BattleDebugMessage
            {
                Category =
                    NormalizeCategory(category),
                Level = level,
                Message = message,
                Frame = Time.frameCount,
                Realtime =
                    Time.realtimeSinceStartup,
                Context = context
            };

        AddRecentMessage(debugMessage);

        string formatted =
            FormatMessage(debugMessage);

        switch (level)
        {
            case BattleLogLevel.Warning:
                Debug.LogWarning(
                    formatted,
                    context);
                break;

            case BattleLogLevel.Error:
                Debug.LogError(
                    formatted,
                    context);
                break;

            default:
                Debug.Log(
                    formatted,
                    context);
                break;
        }

        InvokeMessageEmitted(
            debugMessage);
    }

    public static void Warning(
        BattleLogCategory category,
        string message,
        UnityEngine.Object context = null)
    {
        Log(
            category,
            message,
            BattleLogLevel.Warning,
            context);
    }

    public static void Error(
        BattleLogCategory category,
        string message,
        UnityEngine.Object context = null)
    {
        Log(
            category,
            message,
            BattleLogLevel.Error,
            context);
    }

    public static void Exception(
        BattleLogCategory category,
        Exception exception,
        string message = null,
        UnityEngine.Object context = null)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Error(
                category,
                message,
                context);
        }

        if (exception == null)
            return;

        Debug.LogException(
            exception,
            context);
    }

    //------------------------------------------------
    // 신규 카테고리 단축 API
    //------------------------------------------------

    public static void Combat(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.Combat,
            message,
            level,
            context);
    }

    public static void UI(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.UI,
            message,
            level,
            context);
    }

    public static void Visual(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.Visual,
            message,
            level,
            context);
    }

    public static void Vfx(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.VFX,
            message,
            level,
            context);
    }

    // 대문자 이름도 제공해 호출부 표기를 통일할 수 있다.
    public static void VFX(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Vfx(
            message,
            level,
            context);
    }

    public static void AI(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.AI,
            message,
            level,
            context);
    }

    public static void Event(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.Event,
            message,
            level,
            context);
    }

    public static void Damage(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.Damage,
            message,
            level,
            context);
    }

    public static void Status(
        string message,
        BattleLogLevel level =
            BattleLogLevel.Info,
        UnityEngine.Object context = null)
    {
        Log(
            BattleLogCategory.Status,
            message,
            level,
            context);
    }

    //------------------------------------------------
    // 기존 단축 API
    //------------------------------------------------

    public static void Speed(string message)
    {
        Combat(
            message,
            BattleLogLevel.Trace);
    }

    public static void ActionSlot(string message)
    {
        Combat(
            message,
            BattleLogLevel.Trace);
    }

    public static void ClashBuild(string message)
    {
        Combat(
            message,
            BattleLogLevel.Trace);
    }

    public static void UIInput(string message)
    {
        UI(
            message,
            BattleLogLevel.Trace);
    }

    public static void SkillPanel(string message)
    {
        UI(
            message,
            BattleLogLevel.Trace);
    }

    public static void BattleFlow(string message)
    {
        Combat(
            message,
            BattleLogLevel.Info);
    }

    public static void CombatResult(string message)
    {
        Combat(
            message,
            BattleLogLevel.Info);
    }

    public static void CombatEffect(string message)
    {
        Combat(
            message,
            BattleLogLevel.Info);
    }

    public static string GetCategoryLabel(
        BattleLogCategory category)
    {
        category =
            NormalizeCategory(category);

        return category
            .ToString()
            .Replace(
                ", ",
                "|");
    }

    private static BattleLogCategory NormalizeCategory(
        BattleLogCategory category)
    {
        category &=
            BattleLogCategory.All;

        return category == BattleLogCategory.None
            ? BattleLogCategory.Combat
            : category;
    }

    private static string FormatMessage(
        BattleDebugMessage message)
    {
        string prefix =
            $"[{message.CategoryLabel}]";

        if (message.Level != BattleLogLevel.Info)
        {
            prefix +=
                $"[{message.Level}]";
        }

        if (includeFrame)
        {
            prefix +=
                $"[F{message.Frame}]";
        }

        if (includeRealtime)
        {
            prefix +=
                $"[T{message.Realtime:0.000}]";
        }

        return
            $"{prefix} {message.Message}";
    }

    private static void AddRecentMessage(
        BattleDebugMessage message)
    {
        if (!captureRecentMessages ||
            recentCapacity <= 0 ||
            message == null)
        {
            return;
        }

        recentMessages.Add(message);
        TrimRecentMessages();
    }

    private static void TrimRecentMessages()
    {
        if (recentCapacity <= 0)
        {
            recentMessages.Clear();
            return;
        }

        int removeCount =
            recentMessages.Count -
            recentCapacity;

        if (removeCount <= 0)
            return;

        recentMessages.RemoveRange(
            0,
            removeCount);
    }

    private static void InvokeMessageEmitted(
        BattleDebugMessage message)
    {
        Delegate[] listeners =
            MessageEmitted?
                .GetInvocationList();

        if (listeners == null)
            return;

        foreach (Delegate listener in listeners)
        {
            try
            {
                ((Action<BattleDebugMessage>)listener)
                    .Invoke(message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
