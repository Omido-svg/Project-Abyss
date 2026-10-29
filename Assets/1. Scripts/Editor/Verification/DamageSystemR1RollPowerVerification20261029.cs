using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 R1 (DMG-01) 회귀 검증.
///
/// 정본 계약:
///   피해 기준 위력 = 원본 굴림 RNG + 굴림 자체 보정(힘/쇠약/공포 등)
///   합 판정값     = 피해 기준 위력 + 속도차 + 합 전용 보정
///   피해에는 속도/합 전용 보정이 들어가지 않는다.
///
/// Play Mode + 초기화된 BattleManager가 있는 씬에서 실행한다.
/// 검증은 실제 BattleRuntimeFactory 그래프를 복제한 격리 Fixture에서 수행한다.
/// </summary>
public static class DamageSystemR1RollPowerVerification20261029
{
    private const string LogPrefix = "[Damage R1]";

    private static int pass;
    private static int fail;

    private static readonly List<SkillDefinition> createdDefinitions = new();

    [MenuItem("Game System Verification/1029 Damage R1 - Roll Power Contract (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage R1 검증은 Play Mode에서 실행해야 합니다.");
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
            throw new InvalidOperationException(
                "살아 있는 Enemy가 필요합니다.");
        }

        pass = 0;
        fail = 0;
        createdDefinitions.Clear();

        try
        {
            using GameSystemVerificationFixture fixture =
                GameSystemVerificationFixture.Create(
                    manager.BattleContext.Player,
                    enemy,
                    manager.BattleRules);

            // 공격자는 Enemy, 대상은 Player로 고정한다.
            // 플레이어 고유 메커니즘/장비가 굴림에 섞이지 않게 하기 위함이다.
            Character attacker = fixture.Enemy;
            Character target = fixture.Player;

            CharacterVerificationScenarioTools.ResetCombatState(attacker);
            CharacterVerificationScenarioTools.ResetCombatState(target);

            VerifyT01StrengthAndSpeed(attacker, target);
            VerifyT02Weakness(attacker, target);
            VerifyT03FearBlue(attacker, target);
            VerifyT04SpeedOnly(attacker, target);
            VerifyPipelineBasis(attacker, target, fixture.DamageManager);
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
                $"1029 Damage R1 검증 실패: {fail} checks");
        }
    }

    // T01: 기본 17, 힘 +3, 속도 +2 → 위력 20 / 합 22 / HP 피해 20
    private static void VerifyT01StrengthAndSpeed(
        Character attacker,
        Character target)
    {
        Skill skill = CreateSkill(attacker, 17, SkillColor.Red);
        BattleAction action = CreateAction(attacker, target, skill);

        Check("T01 base roll 17", action.RollPowerForExchange(0) == 17);

        StrengthStatus strength = new(3, 5);
        attacker.AddStatus(strength, attacker);

        try
        {
            Check(
                "T01 rerolled base 17 with strength active",
                action.RollPowerForExchange(0) == 17);

            action.ApplyClashModifiers(2, 0);

            Check(
                "T01 damage power = 17 + strength 3 = 20",
                action.GetDamagePower() == 20,
                $"actual={action.GetDamagePower()}");

            Check(
                "T01 clash power = 17 + strength 3 + speed 2 = 22",
                action.ClashPower == 22,
                $"actual={action.ClashPower}");

            Check(
                "T01 judgment modifier stays 3",
                action.JudgmentModifier == 3,
                $"actual={action.JudgmentModifier}");

            DamageRequest request =
                DamageRequest.FromAction(
                    action,
                    DamageType.SkillPart,
                    canBreakPart: false,
                    isClashDamage: false,
                    targetLostClash: false);

            Check(
                "T01 DamageRequest raw power = 20",
                request.RawPower == 20,
                $"actual={request.RawPower}");

            Check(
                "T01 speed does not leak into damage",
                action.GetDamagePower() != action.ClashPower,
                $"damage={action.GetDamagePower()}, clash={action.ClashPower}");
        }
        finally
        {
            attacker.RemoveStatus(strength, StatusEffectRemoveReason.Manual);
        }
    }

    // T02: 기본 17, 쇠약 2 → 위력 15 / HP 피해 15
    private static void VerifyT02Weakness(
        Character attacker,
        Character target)
    {
        Skill skill = CreateSkill(attacker, 17, SkillColor.Red);
        BattleAction action = CreateAction(attacker, target, skill);

        Check("T02 base roll 17", action.RollPowerForExchange(0) == 17);

        WeaknessStatus weakness = new(2, 5);
        attacker.AddStatus(weakness, attacker);

        try
        {
            Check(
                "T02 rerolled base 17 with weakness active",
                action.RollPowerForExchange(0) == 17);

            action.ApplyClashModifiers(0, 0);

            Check(
                "T02 damage power = 17 - weakness 2 = 15",
                action.GetDamagePower() == 15,
                $"actual={action.GetDamagePower()}");

            Check(
                "T02 clash power = 15",
                action.ClashPower == 15,
                $"actual={action.ClashPower}");

            DamageRequest request =
                DamageRequest.FromAction(
                    action,
                    DamageType.SkillPart,
                    canBreakPart: false,
                    isClashDamage: false,
                    targetLostClash: false);

            Check(
                "T02 DamageRequest raw power = 15",
                request.RawPower == 15,
                $"actual={request.RawPower}");
        }
        finally
        {
            attacker.RemoveStatus(weakness, StatusEffectRemoveReason.Manual);
        }
    }

    // T03: 기본 17, 공포 -1, BLUE → 위력 16 / 상대 흐트러짐 16 / 자기 회복 16
    private static void VerifyT03FearBlue(
        Character attacker,
        Character target)
    {
        Skill skill = CreateSkill(attacker, 17, SkillColor.Blue);
        BattleAction action = CreateAction(attacker, target, skill);

        Check("T03 base roll 17", action.RollPowerForExchange(0) == 17);

        OlafFearStatus fear = new();
        attacker.AddStatus(fear, attacker);

        try
        {
            Check(
                "T03 rerolled base 17 with fear active",
                action.RollPowerForExchange(0) == 17);

            Check(
                "T03 damage power = 17 - fear 1 = 16",
                action.GetDamagePower() == 16,
                $"actual={action.GetDamagePower()}");

            DamagePowerResolution resolution =
                DamagePowerResolver.ResolveOneSided(action);

            Check(
                "T03 BLUE one-sided has no HP damage",
                !resolution.HasDamage,
                $"HasDamage={resolution.HasDamage}");

            // BLUE의 상대 흐트러짐/자기 회복은 StaggerGaugeMechanic이
            // action.GetDamagePower()를 사용한다. 따라서 16이면 두 경로 모두 16이다.
            Check(
                "T03 BLUE stagger/self-recovery basis = 16",
                action.GetDamagePower() == 16,
                $"actual={action.GetDamagePower()}");
        }
        finally
        {
            attacker.RemoveStatus(fear, StatusEffectRemoveReason.Manual);
        }
    }

    // T04: 기본 20, 속도 +2만 → 합 22 / HP 피해 20
    private static void VerifyT04SpeedOnly(
        Character attacker,
        Character target)
    {
        Skill skill = CreateSkill(attacker, 20, SkillColor.Red);
        BattleAction action = CreateAction(attacker, target, skill);

        Check("T04 base roll 20", action.RollPowerForExchange(0) == 20);

        action.ApplyClashModifiers(2, 0);

        Check(
            "T04 damage power stays 20 with speed only",
            action.GetDamagePower() == 20,
            $"actual={action.GetDamagePower()}");

        Check(
            "T04 clash power = 20 + speed 2 = 22",
            action.ClashPower == 22,
            $"actual={action.ClashPower}");
    }

    private static void VerifyPipelineBasis(
        Character attacker,
        Character target,
        DamageManager damageManager)
    {
        if (damageManager == null)
        {
            Check("T01 DamageManager exists", false, "DamageManager=NULL");
            return;
        }

        Skill skill = CreateSkill(attacker, 17, SkillColor.Red);
        BattleAction action = CreateAction(attacker, target, skill);

        StrengthStatus strength = new(3, 5);
        attacker.AddStatus(strength, attacker);

        try
        {
            action.RollPowerForExchange(0);
            action.ApplyClashModifiers(0, 0);

            DamageContext context =
                damageManager.ApplyDamageContext(
                    action,
                    isClashDamage: false,
                    targetLostClash: false);

            Check(
                "T01 DamageContext exists",
                context != null,
                "context=NULL");

            if (context == null)
                return;

            Check(
                "T01 DamageContext.RawPower = 20",
                context.RawPower == 20,
                $"actual={context.RawPower}");

            Check(
                "T01 DamageContext.BaseDamage = 20",
                context.BaseDamage == 20,
                $"actual={context.BaseDamage}");
        }
        finally
        {
            attacker.RemoveStatus(strength, StatusEffectRemoveReason.Manual);
        }
    }

    private static Skill CreateSkill(
        Character owner,
        int power,
        SkillColor color)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name =
            $"R1Verification_{power}_{color}";

        definition.ActionType = ActionType.NormalAttack;
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

    private static BattleAction CreateAction(
        Character owner,
        Character target,
        Skill skill)
    {
        BodyPart ownerPart =
            owner.BodyParts != null && owner.BodyParts.Count > 0
                ? owner.BodyParts[0]
                : null;

        BodyPart targetPart =
            target.BodyParts != null && target.BodyParts.Count > 0
                ? target.BodyParts[0]
                : null;

        ActionSlot slot = new()
        {
            ActionId = 10290001,
            Owner = owner,
            Part = ownerPart,
            Skill = skill,
            Speed = 10,
            ActionIndex = 0,
            SlotId = "VERIFY:DMG-R1",
            Phase = ActionPhase.COMBAT,
            TargetCharacter = target,
            TargetPart = targetPart
        };

        return new BattleAction { Slot = slot };
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
