#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0918 Normal Battle AutoPlan hotfix.
/// Baseline: Default-Battle-Test @ 835870ec4a52d7a2e5ee93d3ec317baad33ff023 ("0918_1").
///
/// Root cause:
/// BattleAutoPlanButtonPanel.CanApplyPlan() and PlayerAutoPlanService.BuildAndApply()
/// both required at least one enemy ActionSlot to exist before allowing auto-plan.
/// NormalBattle uses single-HP NormalEnemy targets. Even when living normal enemies are
/// valid attack targets, a turn can temporarily/legitimately have zero enemy ActionSlots
/// in the live ActionManager, making the WinRate/Damage buttons refuse to auto-assign.
///
/// The planner's fill stage already supports Character targets with BodyPart == null,
/// so the correct gate is "at least one living enemy", not "at least one enemy slot".
/// </summary>
public static class BattleNormalAutoPlan0918HotfixMigration
{
    private const string ServicePath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanService.cs";

    private const string PanelPath =
        "Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleAutoPlanButtonPanel.cs";

    private const string ServiceMarker =
        "[0918_NORMAL_AUTOPLAN_HOTFIX:ALLOW_ZERO_ENEMY_SLOTS]";

    private const string PanelMarker =
        "[0918_NORMAL_AUTOPLAN_HOTFIX:LIVING_ENEMY_GATE]";

    [MenuItem("Game System Verification/Battle Auto Plan/Apply 0918 Normal Battle AutoPlan Hotfix")]
    public static void ApplyFromMenu()
    {
        List<string> errors = new();
        List<string> changed = new();

        PatchPlayerAutoPlanService(errors, changed);
        PatchAutoPlanPanel(errors, changed);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[0918 Normal Battle AutoPlan Hotfix] FAIL\n- " +
                string.Join("\n- ", errors));
            return;
        }

        AssetDatabase.Refresh();

        Debug.Log(
            "[0918 Normal Battle AutoPlan Hotfix] PASS\n" +
            "Patched/verified:\n- " +
            string.Join("\n- ", changed) +
            "\n\nUnity will recompile. After compilation run:\n" +
            "Game System Verification > Battle Auto Plan > Verify 0918 Normal Battle AutoPlan Hotfix");
    }

    private static void PatchPlayerAutoPlanService(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(ServicePath, out string original, errors))
            return;

        string source = Normalize(original);

        if (source.Contains(ServiceMarker, StringComparison.Ordinal))
        {
            changed.Add("PlayerAutoPlanService already patched");
            return;
        }

        string oldText =
            "        if (enemySlots.Count == 0)\n" +
            "        {\n" +
            "            result.Message =\n" +
            "                \"현재 턴에 계획된 적 행동이 없습니다.\";\n" +
            "            return result;\n" +
            "        }";

        string newText =
            "        // " + ServiceMarker + "\n" +
            "        // 자동 지정의 필수 조건은 '적 ActionSlot 존재'가 아니라 '살아 있는 적 존재'다.\n" +
            "        // 일반전투의 단일 HP 적은 TargetPart == null인 Character target으로도 정상 공격 대상이며,\n" +
            "        // 아래 Fill 단계가 enemySlots==0에서도 일방 공격 계획을 만들 수 있다.\n" +
            "        bool hasLivingEnemy = false;\n" +
            "        if (context?.Enemies != null)\n" +
            "        {\n" +
            "            foreach (Character enemy in context.Enemies)\n" +
            "            {\n" +
            "                if (enemy != null && !enemy.IsDead)\n" +
            "                {\n" +
            "                    hasLivingEnemy = true;\n" +
            "                    break;\n" +
            "                }\n" +
            "            }\n" +
            "        }\n" +
            "\n" +
            "        if (!hasLivingEnemy)\n" +
            "        {\n" +
            "            result.Message =\n" +
            "                \"자동 지정 가능한 살아 있는 적이 없습니다.\";\n" +
            "            return result;\n" +
            "        }\n" +
            "\n" +
            "        if (enemySlots.Count == 0)\n" +
            "        {\n" +
            "            Debug.Log(\n" +
            "                \"[PlayerAutoPlan] 적 ActionSlot이 0개이므로 합 매칭 없이 \" +\n" +
            "                \"살아 있는 적을 대상으로 일방 공격 Fill 계획을 계산합니다.\");\n" +
            "        }";

        if (!ReplaceUnique(ref source, oldText, newText, out string error))
        {
            errors.Add(ServicePath + " / " + error);
            return;
        }

        File.WriteAllText(ServicePath, source);
        changed.Add("PlayerAutoPlanService: zero enemy-slot hard abort removed");
    }

    private static void PatchAutoPlanPanel(
        List<string> errors,
        List<string> changed)
    {
        if (!TryRead(PanelPath, out string original, errors))
            return;

        string source = Normalize(original);

        if (source.Contains(PanelMarker, StringComparison.Ordinal))
        {
            changed.Add("BattleAutoPlanButtonPanel already patched");
            return;
        }

        string oldText =
            "        foreach (ActionSlot slot\n" +
            "                 in actionManager.Slots)\n" +
            "        {\n" +
            "            Character owner =\n" +
            "                slot?.Owner;\n" +
            "\n" +
            "            if (owner == null ||\n" +
            "                slot.Skill == null)\n" +
            "            {\n" +
            "                continue;\n" +
            "            }\n" +
            "\n" +
            "            if (battleManager.BattleContext\n" +
            "                    .Enemies.Contains(owner))\n" +
            "            {\n" +
            "                return true;\n" +
            "            }\n" +
            "        }\n" +
            "\n" +
            "        return false;";

        string newText =
            "        // " + PanelMarker + "\n" +
            "        // 버튼 활성 조건을 enemy ActionSlot 존재 여부에 묶지 않는다.\n" +
            "        // 일반전투의 단일 HP 적은 살아 있기만 하면 자동 지정의 유효한 공격 대상이다.\n" +
            "        IReadOnlyList<Character> enemies =\n" +
            "            battleManager.BattleContext.Enemies;\n" +
            "\n" +
            "        if (enemies == null)\n" +
            "            return false;\n" +
            "\n" +
            "        foreach (Character enemy in enemies)\n" +
            "        {\n" +
            "            if (enemy != null && !enemy.IsDead)\n" +
            "                return true;\n" +
            "        }\n" +
            "\n" +
            "        return false;";

        if (!ReplaceUnique(ref source, oldText, newText, out string error))
        {
            errors.Add(PanelPath + " / " + error);
            return;
        }

        if (!source.Contains("using System.Collections.Generic;", StringComparison.Ordinal))
        {
            const string oldUsing = "using TMPro;\nusing UnityEngine;";
            const string newUsing =
                "using System.Collections.Generic;\nusing TMPro;\nusing UnityEngine;";

            if (!ReplaceUnique(
                    ref source,
                    oldUsing,
                    newUsing,
                    out error))
            {
                errors.Add(PanelPath + " / using patch / " + error);
                return;
            }
        }

        File.WriteAllText(PanelPath, source);
        changed.Add("BattleAutoPlanButtonPanel: interactable gate now uses living enemies");
    }

    private static bool TryRead(
        string path,
        out string source,
        List<string> errors)
    {
        source = string.Empty;
        if (!File.Exists(path))
        {
            errors.Add("Source missing: " + path);
            return false;
        }

        source = File.ReadAllText(path);
        return true;
    }

    private static string Normalize(string value) =>
        (value ?? string.Empty).Replace("\r\n", "\n");

    private static bool ReplaceUnique(
        ref string source,
        string oldText,
        string newText,
        out string error)
    {
        error = string.Empty;

        int first = source.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0)
        {
            error = "patch anchor not found; source differs from 835870ec baseline";
            return false;
        }

        int second = source.IndexOf(
            oldText,
            first + oldText.Length,
            StringComparison.Ordinal);

        if (second >= 0)
        {
            error = "patch anchor ambiguous (multiple matches)";
            return false;
        }

        source =
            source.Substring(0, first) +
            newText +
            source.Substring(first + oldText.Length);

        return true;
    }
}
#endif
