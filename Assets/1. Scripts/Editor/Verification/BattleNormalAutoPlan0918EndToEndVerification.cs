#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 0918 Normal Battle AutoPlan V2 end-to-end verification.
///
/// Static:
/// - UI/service use the same canonical attack-target gate.
/// - Fill targeting no longer depends directly on Character.GetTargetPoints().
/// - Target validation goes through BattleTargetValidator so Single HP targets use Character/null-part.
///
/// PlayMode (only when the current turn naturally has zero enemy ActionSlots and no player plan):
/// - AutoPlan must still create at least one player COMBAT action.
/// - Single HP targets must be stored as TargetCharacter + TargetPart=null.
/// - Because enemy ActionSlots are zero, generated player COMBAT slots must keep TargetSlot=null.
/// - The probe plan is explicitly cancelled after verification.
/// </summary>
public static class BattleNormalAutoPlan0918EndToEndVerification
{
    private const string ServicePath =
        "Assets/1. Scripts/Runtime/Systems/AI/PlayerAutoPlanService.cs";

    private const string PanelPath =
        "Assets/1. Scripts/Runtime/Presentation/UI/Panels/BattleAutoPlanButtonPanel.cs";

    [MenuItem("Game System Verification/Battle Auto Plan/Verify 0918 Normal Battle AutoPlan E2E Fix")]
    public static void VerifyFromMenu()
    {
        List<string> pass = new();
        List<string> fail = new();
        List<string> pending = new();
        List<string> details = new();

        VerifyStaticSource(pass, fail);

        if (Application.isPlaying)
        {
            VerifyPlayMode(pass, fail, pending, details);
        }
        else
        {
            pending.Add(
                "PlayMode E2E probe: 일반전투를 실행한 뒤 다시 검증하면 runtime 경로를 확인합니다.");
        }

        string summary =
            "[0918 Normal Battle AutoPlan E2E Verification]\n" +
            $"PASS={pass.Count} FAIL={fail.Count} PENDING={pending.Count}\n\n" +
            FormatSection("PASS", pass) + "\n\n" +
            FormatSection("DETAILS", details) + "\n\n" +
            FormatSection("PENDING", pending) + "\n\n" +
            FormatSection("FAIL", fail);

        if (fail.Count > 0)
            Debug.LogError(summary);
        else
            Debug.Log(summary);
    }

    private static void VerifyStaticSource(
        List<string> pass,
        List<string> fail)
    {
        string service = Read(ServicePath);
        string panel = Read(PanelPath);

        bool canonicalGate =
            service.Contains(
                "[0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_GATE]",
                StringComparison.Ordinal) &&
            service.Contains(
                "public static bool HasAnyAutoPlanTarget(",
                StringComparison.Ordinal) &&
            panel.Contains(
                "PlayerAutoPlanService.HasAnyAutoPlanTarget(",
                StringComparison.Ordinal);

        if (canonicalGate)
        {
            pass.Add(
                "UI + PlayerAutoPlanService가 enemy ActionSlot이 아닌 canonical attack target을 공통 gate로 사용");
        }
        else
        {
            fail.Add(
                "canonical target gate가 UI/service 양쪽에 연결되지 않았습니다.");
        }

        bool canonicalEnumeration =
            service.Contains(
                "[0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_ENUMERATION]",
                StringComparison.Ordinal) &&
            service.Contains(
                "BattleTargetValidator.GetTargetPoints(",
                StringComparison.Ordinal);

        bool canonicalValidation =
            service.Contains(
                "[0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_VALIDATION]",
                StringComparison.Ordinal) &&
            service.Contains(
                "return BattleTargetValidator.IsValid(",
                StringComparison.Ordinal);

        bool fillUsesCanonicalPoints =
            service.Contains(
                "[0918_NORMAL_AUTOPLAN_HOTFIX_V2:SINGLE_HP_FILL_TARGET]",
                StringComparison.Ordinal) &&
            !ExtractMethodBlock(
                    service,
                    "private Candidate FindBestFillCandidate(",
                    "private static bool CanPlanSkill(")
                .Contains(
                    "enemy.GetTargetPoints(",
                    StringComparison.Ordinal);

        if (canonicalEnumeration &&
            canonicalValidation &&
            fillUsesCanonicalPoints)
        {
            pass.Add(
                "Fill 후보 생성이 BattleTargetValidator 경로를 사용하여 Single HP Character/null-part target을 보장");
        }
        else
        {
            fail.Add(
                "Fill target enumeration/validation 중 직접 Character target-model 의존이 남아 있습니다.");
        }
    }

    private static void VerifyPlayMode(
        List<string> pass,
        List<string> fail,
        List<string> pending,
        List<string> details)
    {
        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<BattleManager>(
                FindObjectsInactive.Include);

        if (manager == null ||
            !manager.IsInitialized ||
            manager.BattleContext == null ||
            manager.ActionManager == null ||
            manager.ActionManager.IsDisposed)
        {
            pending.Add(
                "PlayMode E2E probe: 초기화된 BattleManager를 찾지 못했습니다.");
            return;
        }

        BattleContext context = manager.BattleContext;
        Character player = context.Player;

        if (player == null || player.IsDead)
        {
            pending.Add(
                "PlayMode E2E probe: 살아 있는 플레이어가 없습니다.");
            return;
        }

        List<Character> livingSingleHpEnemies = new();
        int livingEnemies = 0;

        if (context.Enemies != null)
        {
            foreach (Character enemy in context.Enemies)
            {
                if (enemy == null || enemy.IsDead)
                    continue;

                livingEnemies++;

                if (enemy.IsSingleHpTarget)
                    livingSingleHpEnemies.Add(enemy);
            }
        }

        int enemySlots = 0;
        int playerSlots = 0;

        foreach (ActionSlot slot in manager.ActionManager.Slots)
        {
            if (slot?.Owner == null)
                continue;

            if (slot.Owner == player)
                playerSlots++;

            if (context.Enemies != null &&
                context.Enemies.Contains(slot.Owner))
            {
                enemySlots++;
            }
        }

        details.Add(
            $"PlayMode snapshot: livingEnemies={livingEnemies}, " +
            $"singleHpEnemies={livingSingleHpEnemies.Count}, enemySlots={enemySlots}, playerSlots={playerSlots}");

        if (livingSingleHpEnemies.Count > 0)
        {
            bool everySingleHpHasCharacterTarget = true;

            foreach (Character enemy in livingSingleHpEnemies)
            {
                IReadOnlyList<TargetPoint> points =
                    BattleTargetValidator.GetTargetPoints(
                        enemy,
                        TargetSelectionRule.StandardAttack);

                bool found = false;
                if (points != null)
                {
                    foreach (TargetPoint point in points)
                    {
                        if (point.IsValid &&
                            point.Character == enemy &&
                            point.Part == null)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    everySingleHpHasCharacterTarget = false;
                    break;
                }
            }

            if (everySingleHpHasCharacterTarget)
            {
                pass.Add(
                    "현재 일반몹 Single HP 대상이 Character target(TargetPart=null)으로 노출됨");
            }
            else
            {
                fail.Add(
                    "현재 일반몹 중 Character/null-part target을 만들지 못하는 대상이 있습니다.");
            }
        }
        else
        {
            pending.Add(
                "PlayMode E2E probe: 현재 전투에 Single HP 일반몹이 없어 일반전투 타깃 계약을 직접 확인하지 못했습니다.");
        }

        if (!PlayerAutoPlanService.HasAnyAutoPlanTarget(context))
        {
            fail.Add(
                "살아 있는 적이 있지만 AutoPlan canonical target gate가 유효 공격 대상을 찾지 못했습니다.");
            return;
        }

        if (enemySlots != 0)
        {
            pending.Add(
                "Zero-enemy-slot E2E probe: 현재 적 ActionSlot이 0개가 아니므로 자연 발생한 문제 상태에서의 자동지정은 검사하지 않았습니다.");
            return;
        }

        if (livingSingleHpEnemies.Count == 0)
        {
            pending.Add(
                "Zero-enemy-slot E2E probe: 적 슬롯은 0개지만 Single HP 일반몹이 없어 대상 시나리오와 다릅니다.");
            return;
        }

        if (playerSlots != 0)
        {
            pending.Add(
                "Zero-enemy-slot E2E probe: 기존 플레이어 계획을 보존하기 위해 자동 적용 probe를 생략했습니다. 아군 계획을 비운 뒤 다시 실행하세요.");
            return;
        }

        PlayerAutoPlanService service =
            new PlayerAutoPlanService();

        PlayerAutoPlanResult result =
            service.BuildAndApply(
                manager,
                PlayerAutoPlanMode.Damage);

        if (result?.Success != true)
        {
            fail.Add(
                "enemySlots=0 + living Single HP enemy 상태에서 AutoPlan 적용 실패: " +
                (result?.Message ?? "결과 없음"));

            manager.ResetPlayerActions();
            return;
        }

        int combatSlots = 0;
        bool invalidCombatTarget = false;
        bool unexpectedTargetSlot = false;

        foreach (ActionSlot slot in manager.ActionManager.Slots)
        {
            if (slot?.Owner != player ||
                slot.Phase != ActionPhase.COMBAT ||
                slot.Skill == null)
            {
                continue;
            }

            combatSlots++;

            if (slot.TargetCharacter == null ||
                slot.TargetCharacter.IsDead ||
                !BattleTargetValidator.IsValid(
                    slot.TargetCharacter,
                    slot.TargetPart,
                    TargetSelectionRule.StandardAttack))
            {
                invalidCombatTarget = true;
            }

            if (slot.TargetCharacter?.IsSingleHpTarget == true &&
                slot.TargetPart != null)
            {
                invalidCombatTarget = true;
            }

            if (slot.TargetSlot != null)
                unexpectedTargetSlot = true;
        }

        if (combatSlots <= 0)
        {
            fail.Add(
                "enemySlots=0 상태 AutoPlan은 성공했지만 플레이어 COMBAT 슬롯을 만들지 못했습니다.");
        }
        else if (invalidCombatTarget)
        {
            fail.Add(
                "enemySlots=0 상태에서 생성된 COMBAT 슬롯 중 Single HP/target 계약이 잘못된 슬롯이 있습니다.");
        }
        else if (unexpectedTargetSlot)
        {
            fail.Add(
                "enemySlots=0인데 생성된 COMBAT 슬롯에 TargetSlot 링크가 남아 있습니다.");
        }
        else
        {
            pass.Add(
                $"enemySlots=0 + living Single HP enemy E2E: COMBAT {combatSlots}개가 Character target 일방공격 계획으로 정상 적용됨");
        }

        // probe가 만든 비용/도사림 즉시효과/슬롯을 명시적 Planning 취소 경로로 복구한다.
        manager.ResetPlayerActions();

        if (manager.ActionManager.CountSlots(player) == 0)
        {
            pass.Add(
                "E2E probe 후 플레이어 Planning을 정상 취소/정리");
        }
        else
        {
            fail.Add(
                "E2E probe 후 플레이어 Planning 슬롯이 남았습니다.");
        }
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

    private static string FormatSection(
        string title,
        IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
            return title + "\n- none";

        return title + "\n- " +
               string.Join("\n- ", lines);
    }
}
#endif
