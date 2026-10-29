using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 R4 (DMG-05) 회귀 검증 — 일방타격 기세 이동.
///
/// 정본 §4.1 / §5.2 / §10.1:
///   - 색과 무관하게 성공한 일방타격 1히트는 기세를 20 움직인다.
///   - BLUE 일방타격은 HP 피해를 만들지 않지만 기세는 움직인다.
///   - 결투 대 결투 교환의 승리는 총 40이며, 20 + 40 이중 가산하지 않는다.
///
/// Play Mode + 초기화된 BattleManager 필요.
/// </summary>
public static class DamageSystemR4OneSidedMomentumVerification20261029
{
    private const string LogPrefix = "[Damage R4]";

    private static int pass;
    private static int fail;

    private static readonly List<SkillDefinition> createdDefinitions = new();

    [MenuItem("Game System Verification/1029 Damage R4 - One Sided Momentum (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage R4 검증은 Play Mode에서 실행해야 합니다.");
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
        createdDefinitions.Clear();

        try
        {
            RunWithFixture(manager, enemy, VerifyT08);
            RunWithFixture(manager, enemy, VerifyT09);
        }
        finally
        {
            foreach (SkillDefinition definition in createdDefinitions)
            {
                if (definition != null)
                    UnityEngine.Object.DestroyImmediate(definition);
            }

            createdDefinitions.Clear();
        }

        Debug.Log($"{LogPrefix} RESULT PASS={pass} FAIL={fail}");

        if (fail != 0)
        {
            throw new InvalidOperationException(
                $"1029 Damage R4 검증 실패: {fail} checks");
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

        fixture.MomentumManager?.Reset();

        body(fixture);
    }

    // T08: BLUE 위력10 일방타격, 기세0 → 기세 +20(플레이어 공격), HP 피해 0.
    private static void VerifyT08(GameSystemVerificationFixture fixture)
    {
        Character attacker = fixture.Player;
        Character target = fixture.Enemy;

        StaggerGaugeMechanic targetStagger =
            target.GetMechanic<StaggerGaugeMechanic>();

        int targetHpBefore = target.CurrentHP;
        int targetGaugeBefore =
            targetStagger?.CurrentGauge ?? 0;

        fixture.MomentumManager.SetMomentumForDebug(0);

        Skill skill = CreateSkill(attacker, 10, SkillColor.Blue);
        ActionSlot slot = CreateSlot(attacker, target, skill, 10290401);

        ClashResultContext result =
            fixture.ClashManager.ResolvePair(
                new ClashPair(slot));

        Check("T08 pair resolved",
            result != null && result.Exchanges.Count == 1,
            $"exchanges={result?.Exchanges?.Count}");

        ClashExchangeResult exchange =
            result != null && result.Exchanges.Count > 0
                ? result.Exchanges[0]
                : null;

        Check("T08 blue one-sided has no HP damage",
            target.CurrentHP == targetHpBefore,
            $"before={targetHpBefore}, after={target.CurrentHP}");

        Check("T08 blue one-sided creates no damage context",
            result?.DamageContext == null,
            $"actual={(result?.DamageContext != null)}");

        Check("T08 blue one-sided moves momentum by hit shift",
            fixture.MomentumManager.CurrentMomentum ==
                fixture.MomentumManager.Settings.HitShift,
            $"actual={fixture.MomentumManager.CurrentMomentum}, " +
            $"expected={fixture.MomentumManager.Settings.HitShift}");

        Check("T08 exchange DTO records momentum shift",
            exchange != null &&
            exchange.MomentumAfter - exchange.MomentumBefore ==
                fixture.MomentumManager.Settings.HitShift,
            $"before={exchange?.MomentumBefore}, " +
            $"after={exchange?.MomentumAfter}, " +
            $"shift={exchange?.MomentumShift}");

        Check("T08 blue one-sided still reduces target stagger",
            targetStagger != null &&
            targetStagger.CurrentGauge < targetGaugeBefore,
            $"before={targetGaugeBefore}, after={targetStagger?.CurrentGauge}");
    }

    // T09: RED 결투 대 결투 승리, 기세0 → 총 +40 (20 + 40 이중 가산 금지).
    private static void VerifyT09(GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        fixture.MomentumManager.SetMomentumForDebug(0);

        Skill playerSkill = CreateSkill(
            player, 20, SkillColor.Red, ActionType.Duel);
        Skill enemySkill = CreateSkill(
            enemy, 10, SkillColor.Red, ActionType.Duel);

        ActionSlot playerSlot =
            CreateSlot(player, enemy, playerSkill, 10290411);
        ActionSlot enemySlot =
            CreateSlot(enemy, player, enemySkill, 10290412);

        ClashResultContext result =
            fixture.ClashManager.ResolvePair(
                new ClashPair(playerSlot, enemySlot));

        Check("T09 duel clash resolved",
            result != null && result.Exchanges.Count == 1,
            $"exchanges={result?.Exchanges?.Count}");

        ClashExchangeResult exchange =
            result != null && result.Exchanges.Count > 0
                ? result.Exchanges[0]
                : null;

        Check("T09 player won the duel exchange",
            exchange?.WinnerAction?.Owner == player,
            $"winner={exchange?.WinnerAction?.Owner?.Data?.CharacterName}");

        Check("T09 duel exchange is flagged",
            exchange?.IsDuelExchange == true,
            $"actual={exchange?.IsDuelExchange}");

        Check("T09 duel victory shifts momentum by total, not hit+total",
            fixture.MomentumManager.CurrentMomentum ==
                fixture.MomentumManager.Settings.DuelExchangeTotalShift,
            $"actual={fixture.MomentumManager.CurrentMomentum}, " +
            $"expected={fixture.MomentumManager.Settings.DuelExchangeTotalShift}");

        Check("T09 hit shift is not added on top of duel shift",
            exchange != null &&
            exchange.HitMomentumShift == 0 &&
            exchange.DuelMomentumShift ==
                fixture.MomentumManager.Settings.DuelExchangeTotalShift,
            $"hit={exchange?.HitMomentumShift}, " +
            $"duel={exchange?.DuelMomentumShift}");
    }

    private static Skill CreateSkill(
        Character owner,
        int power,
        SkillColor color,
        ActionType actionType = ActionType.NormalAttack)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name = $"R4Verification_{power}_{color}_{actionType}";
        definition.ActionType = actionType;
        definition.Color = color;
        definition.ResolverType = SkillResolverType.Dice;
        definition.ExchangeRollCount = 1;
        definition.Rolls = new List<SkillRollData>
        {
            new()
            {
                Index = 0,
                Type = CombatRollType.Attack,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = power,
                MaxPower = power,
                RngSource = RollRngSource.CharacterDefault
            }
        };
        definition.Rolls[0].Sanitize(0);

        createdDefinitions.Add(definition);

        Skill skill = definition.CreateRuntimeSkill();
        skill.Initialize(owner, owner.BattleEvent);
        return skill;
    }

    private static ActionSlot CreateSlot(
        Character owner,
        Character target,
        Skill skill,
        long actionId)
    {
        BodyPart ownerPart =
            owner.BodyParts != null && owner.BodyParts.Count > 0
                ? owner.BodyParts[0]
                : null;

        BodyPart targetPart =
            target.BodyParts != null && target.BodyParts.Count > 0
                ? target.BodyParts[0]
                : null;

        return new ActionSlot
        {
            ActionId = actionId,
            Owner = owner,
            Part = ownerPart,
            Skill = skill,
            Speed = 10,
            ActionIndex = 0,
            SlotId = "VERIFY:DMG-R4",
            Phase = ActionPhase.COMBAT,
            TargetCharacter = target,
            TargetPart = targetPart,
            ResourceCostCommitted = true
        };
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
