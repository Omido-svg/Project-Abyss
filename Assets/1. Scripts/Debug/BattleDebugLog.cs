using UnityEngine;

public static class BattleDebugLog
{
    //------------------------------------------------
    // 개발용 상세 로그 스위치
    //------------------------------------------------

    public const bool ShowSpeedRoll = false;
    public const bool ShowActionSlot = false;
    public const bool ShowClashBuild = false;
    public const bool ShowUIInput = false;
    public const bool ShowSkillPanel = false;

    //------------------------------------------------
    // 실제 전투 결과 로그는 유지
    //------------------------------------------------

    public const bool ShowBattleFlow = true;
    public const bool ShowCombatResult = true;
    public const bool ShowCombatEffect = true;

    //------------------------------------------------

    public static void Speed(string message)
    {
        if (!ShowSpeedRoll)
            return;

        Debug.Log(message);
    }

    public static void ActionSlot(string message)
    {
        if (!ShowActionSlot)
            return;

        Debug.Log(message);
    }

    public static void ClashBuild(string message)
    {
        if (!ShowClashBuild)
            return;

        Debug.Log(message);
    }

    public static void UIInput(string message)
    {
        if (!ShowUIInput)
            return;

        Debug.Log(message);
    }

    public static void SkillPanel(string message)
    {
        if (!ShowSkillPanel)
            return;

        Debug.Log(message);
    }

    public static void BattleFlow(string message)
    {
        if (!ShowBattleFlow)
            return;

        Debug.Log(message);
    }

    public static void CombatResult(string message)
    {
        if (!ShowCombatResult)
            return;

        Debug.Log(message);
    }

    public static void CombatEffect(string message)
    {
        if (!ShowCombatEffect)
            return;

        Debug.Log(message);
    }
}