#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BattleNormalAutoPlan0918HotfixVerification
{
    private const string ServicePath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanService.cs";

    private const string PanelPath =
        "Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleAutoPlanButtonPanel.cs";

    [MenuItem("Game System Verification/Battle Auto Plan/Verify 0918 Normal Battle AutoPlan Hotfix")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new();
        List<string> fail = new();
        List<string> details = new();

        string service = Read(ServicePath);
        string panel = Read(PanelPath);

        bool serviceMarker =
            service.Contains(
                "[0918_NORMAL_AUTOPLAN_HOTFIX:ALLOW_ZERO_ENEMY_SLOTS]",
                StringComparison.Ordinal);

        bool noHardAbort =
            !service.Contains(
                "\"현재 턴에 계획된 적 행동이 없습니다.\";\n            return result;",
                StringComparison.Ordinal);

        bool serviceContinuesFill =
            service.Contains(
                "적 ActionSlot이 0개이므로 합 매칭 없이",
                StringComparison.Ordinal) &&
            service.Contains(
                "FindBestFillCandidate(",
                StringComparison.Ordinal);

        if (serviceMarker && noHardAbort && serviceContinuesFill)
        {
            pass.Add(
                "PlayerAutoPlanService: enemySlots=0에서도 living enemy 대상 Fill 자동계획 허용");
        }
        else
        {
            fail.Add(
                "PlayerAutoPlanService zero-enemy-slot hard gate remains");
        }

        bool panelMarker =
            panel.Contains(
                "[0918_NORMAL_AUTOPLAN_HOTFIX:LIVING_ENEMY_GATE]",
                StringComparison.Ordinal);

        bool panelLivingEnemyGate =
            panel.Contains(
                "IReadOnlyList<Character> enemies =",
                StringComparison.Ordinal) &&
            panel.Contains(
                "if (enemy != null && !enemy.IsDead)",
                StringComparison.Ordinal);

        string canApplyBlock =
            ExtractMethodBlock(
                panel,
                "private bool CanApplyPlan()",
                "private void SynchronizeModeWithCurrentSlots()");

        bool panelNoSlotRequirement =
            !canApplyBlock.Contains(
                "foreach (ActionSlot slot",
                StringComparison.Ordinal) &&
            !canApplyBlock.Contains(
                ".Enemies.Contains(owner)",
                StringComparison.Ordinal);

        if (panelMarker &&
            panelLivingEnemyGate &&
            panelNoSlotRequirement)
        {
            pass.Add(
                "BattleAutoPlanButtonPanel: 버튼 활성 조건을 living enemy 기준으로 변경");
        }
        else
        {
            fail.Add(
                "BattleAutoPlanButtonPanel still depends on enemy ActionSlots");
        }

        if (Application.isPlaying)
        {
            BattleManager manager =
                UnityEngine.Object.FindFirstObjectByType<BattleManager>(
                    FindObjectsInactive.Include);

            int livingEnemies = 0;
            int enemySlots = 0;

            if (manager?.BattleContext?.Enemies != null)
            {
                foreach (Character enemy in manager.BattleContext.Enemies)
                {
                    if (enemy != null && !enemy.IsDead)
                        livingEnemies++;
                }
            }

            if (manager?.ActionManager?.Slots != null &&
                manager?.BattleContext?.Enemies != null)
            {
                foreach (ActionSlot slot in manager.ActionManager.Slots)
                {
                    if (slot?.Owner != null &&
                        manager.BattleContext.Enemies.Contains(slot.Owner))
                    {
                        enemySlots++;
                    }
                }
            }

            details.Add(
                $"PlayMode snapshot: livingEnemies={livingEnemies}, enemySlots={enemySlots}");
        }

        string summary =
            "[0918 Normal Battle AutoPlan Hotfix Verification]\n" +
            $"PASS={pass.Count} FAIL={fail.Count}\n\n" +
            "PASS\n- " + string.Join("\n- ", pass) +
            "\n\nDETAILS\n- " + string.Join("\n- ", details) +
            "\n\nFAIL\n- " + string.Join("\n- ", fail);

        if (fail.Count > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);
    }

    private static string Read(string path) =>
        File.Exists(path)
            ? File.ReadAllText(path).Replace("\r\n", "\n")
            : string.Empty;

    private static string ExtractMethodBlock(
        string source,
        string startToken,
        string endToken)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;

        int end = source.IndexOf(endToken, start, StringComparison.Ordinal);
        if (end < 0)
            end = source.Length;

        return source.Substring(start, end - start);
    }
}
#endif
