using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 R3 (DMG-03 / DMG-04) 회귀 검증 — 턴 종료 정산 트랜잭션.
///
/// 정본 §1.3 / §2.2 / §11.1:
///   - 턴 종료 순서는 '도트 전부 → 턴 종료 회복 전부 → 사망 판정'이다.
///   - 상태 지속시간은 효과를 적용한 뒤 감소한다.
///   - 턴 종료 정산 범위에서만 HP가 음수로 내려갈 수 있다.
///   - 정산 중 음수 HP는 즉시 사망이 아니며, 최종 판정은 정산 종료 후 1회만 한다.
///
/// Play Mode + 초기화된 BattleManager 필요.
/// </summary>
public static class DamageSystemR3TurnEndSettlementVerification20261029
{
    private const string LogPrefix = "[Damage R3]";

    private static int pass;
    private static int fail;

    [MenuItem("Game System Verification/1029 Damage R3 - Turn End Settlement (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage R3 검증은 Play Mode에서 실행해야 합니다.");
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<BattleManager>();

        if (manager == null ||
            manager.BattleContext?.Player == null ||
            manager.BattleContext.Enemies == null)
        {
            throw new InvalidOperationException(
                "초기화된 BattleManager와 Player/Enemy가 필요합니다.");
        }

        Character enemy = null;
        foreach (Character candidate in manager.BattleContext.Enemies)
        {
            if (candidate != null && !candidate.IsDead)
            {
                enemy = candidate;
                break;
            }
        }

        if (enemy == null)
        {
            throw new InvalidOperationException("살아 있는 Enemy가 필요합니다.");
        }

        pass = 0;
        fail = 0;

        try
        {
            RunWithFixture(manager, enemy, VerifyT10);
            RunWithFixture(manager, enemy, VerifyT11);
            RunWithFixture(manager, enemy, VerifyT12);
        }
        finally
        {
        }

        Debug.Log($"{LogPrefix} RESULT PASS={pass} FAIL={fail}");

        if (fail != 0)
        {
            throw new InvalidOperationException(
                $"1029 Damage R3 검증 실패: {fail} checks");
        }
    }

    private static void RunWithFixture(
        BattleManager manager,
        Character enemySource,
        Action<GameSystemVerificationFixture> body)
    {
        using GameSystemVerificationFixture fixture =
            GameSystemVerificationFixture.Create(
                manager.BattleContext.Player,
                enemySource,
                manager.BattleRules);

        CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
        CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);

        body(fixture);
    }

    // T10: HP 2, 출혈 10, 재생 12 → 2 → -8 → 4, 생존. 중간 사망 이벤트 없음.
    private static void VerifyT10(GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Player;

        int deathEvents = 0;
        Action<Character> onDeath = _ => deathEvents++;
        target.BattleEvent.OnCharacterDeath += onDeath;

        try
        {
            Prepare(target, 2, 10, 12);

            int deathsBefore = deathEvents;
            target.TurnEnd();

            Check("T10 settlement survived",
                !target.IsDead,
                $"hp={target.CurrentHP}");

            Check("T10 hp = 2 - 10 + 12 = 4",
                target.CurrentHP == 4,
                $"actual={target.CurrentHP}");

            Check("T10 no temporary death event",
                deathEvents == deathsBefore,
                $"events={deathEvents - deathsBefore}");

            Check("T10 dot stack decreased after application",
                StackOf<Bleeding>(target) == 9,
                $"actual={StackOf<Bleeding>(target)}");

            Check("T10 one-turn regeneration expired after application",
                target.GetStatus<RegenerationStatus>() == null,
                "regeneration should be removed after its final tick");
        }
        finally
        {
            target.BattleEvent.OnCharacterDeath -= onDeath;
        }
    }

    // T11: HP 2, 출혈 10, 재생 3 → 최종 HP <= 0 이므로 사망. 판정은 1회.
    private static void VerifyT11(GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Player;

        int deathEvents = 0;
        Action<Character> onDeath = _ => deathEvents++;
        target.BattleEvent.OnCharacterDeath += onDeath;

        try
        {
            Prepare(target, 2, 10, 3);

            int deathsBefore = deathEvents;
            target.TurnEnd();

            Check("T11 target died",
                target.IsDead,
                $"hp={target.CurrentHP}, dead={target.IsDead}");

            Check("T11 final hp committed to 0",
                target.CurrentHP == 0,
                $"actual={target.CurrentHP}");

            Check("T11 death event raised exactly once",
                deathEvents - deathsBefore == 1,
                $"events={deathEvents - deathsBefore}");
        }
        finally
        {
            target.BattleEvent.OnCharacterDeath -= onDeath;
        }
    }

    // T12: HP 최대치 - 2, 출혈 10, 재생 12.
    //      도트를 먼저 적용해야 재생이 최대치에서 손실되지 않는다.
    private static void VerifyT12(GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Player;
        int maximum = Mathf.Max(20, target.MaxCombatHP);

        Prepare(target, maximum - 2, 10, 12);

        // Prepare가 최대치를 기준으로 HP를 다시 세팅하므로 여기서 재설정한다.
        target.RuntimeStatus.currentHP = maximum - 2;

        int hpBefore = target.CurrentHP;
        target.TurnEnd();

        int expected = Mathf.Min(maximum, hpBefore - 10 + 12);

        Check("T12 regeneration is not wasted before dot",
            target.CurrentHP == expected,
            $"actual={target.CurrentHP}, expected={expected}");

        Check("T12 old order (heal first) would end lower",
            target.CurrentHP > maximum - 10,
            $"actual={target.CurrentHP}, heal-first={maximum - 10}");
    }

    private static void Prepare(
        Character target,
        int hp,
        int bleedingStacks,
        int regenerationHeal)
    {
        CharacterVerificationScenarioTools.ResetCombatState(target);

        target.RuntimeStatus.currentHP = Mathf.Clamp(hp, 0, target.MaxCombatHP);

        if (bleedingStacks > 0)
        {
            target.AddStatus(
                new Bleeding(bleedingStacks),
                target);
        }

        if (regenerationHeal > 0)
        {
            target.AddStatus(
                new RegenerationStatus(1, regenerationHeal),
                target);
        }
    }

    private static int StackOf<TStatus>(Character character)
        where TStatus : StatusEffect
    {
        TStatus status = character.GetStatus<TStatus>();
        return status?.Stack ?? 0;
    }

    private static void Check(
        string label,
        bool condition,
        string detail = null)
    {
        if (condition)
        {
            pass++;
            Debug.Log($"{LogPrefix} PASS: {label}");
            return;
        }

        fail++;
        string suffix =
            string.IsNullOrEmpty(detail)
                ? string.Empty
                : $" ({detail})";

        Debug.LogError($"{LogPrefix} FAIL: {label}{suffix}");
    }
}
