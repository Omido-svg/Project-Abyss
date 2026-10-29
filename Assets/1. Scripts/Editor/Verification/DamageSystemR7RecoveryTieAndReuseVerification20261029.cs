using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 R7 — 리뷰 후속 보완 검증.
///
/// 리뷰 지적 2건:
///   A. BLUE 자기 흐트러짐 회복이 상대 흐트러짐 내성에 종속된다
///      (`staggerDamage &lt;= 0` 조기 반환 시 회복도 건너뛴다).
///   B. 정상적인 무작위 동률 반복(4회)을 콘텐츠 오류로 오판한다.
///
/// 추가 점검 3건:
///   C. 굴림 재사용 캐시에서 JudgmentModifier가 두 번 더해지는지
///   D. 비균일 부위 MaxHP에서 대표 부위 선택이 결정적인지
///   E. 부위 최대 HP 변경 경로가 흐트러짐 최대치를 갱신하는지
///
/// Play Mode + 초기화된 BattleManager 필요.
/// </summary>
public static class DamageSystemR7RecoveryTieAndReuseVerification20261029
{
    private const string LogPrefix = "[Damage R7]";
    private const string TieErrorTag = "[1029 DMG-07][CONTENT_ERROR]";

    private static int pass;
    private static int fail;

    private static readonly List<SkillDefinition> createdDefinitions = new();

    [MenuItem("Game System Verification/1029 Damage R7 - Recovery, Tie Policy and Roll Reuse (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage R7 검증은 Play Mode에서 실행해야 합니다.");
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

        Character bodyPartEnemy = FindEnemy(
            manager,
            candidate => candidate.UsesBodyParts);

        Character singleHpEnemy = FindEnemy(
            manager,
            candidate => !candidate.UsesBodyParts);

        if (bodyPartEnemy == null || singleHpEnemy == null)
        {
            throw new InvalidOperationException(
                "부위형/단일HP Enemy가 각각 필요합니다.");
        }

        pass = 0;
        fail = 0;
        createdDefinitions.Clear();

        try
        {
            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyRollReuseDoesNotDoubleModifier(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyBlueRecoveryIgnoresTargetResistance(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyBlueRecoveryControlWithResistance(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyRepeatedRandomTieIsNotContentError(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyTiePolicyClassification(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyRepresentativePartIsDeterministic(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyPartMaxHpChangeRefreshesGauge(fixture));
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
                $"1029 Damage R7 검증 실패: {fail} checks");
        }
    }

    private static Character FindEnemy(
        BattleManager manager,
        Func<Character, bool> predicate)
    {
        foreach (Character candidate in manager.BattleContext.Enemies)
        {
            if (candidate == null || candidate.IsDead)
                continue;

            if (predicate(candidate))
                return candidate;
        }

        return null;
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

    //====================================================================
    // C. 굴림 재사용 — 판정 보정이 두 번 더해지지 않는다
    //====================================================================

    private static void VerifyRollReuseDoesNotDoubleModifier(
        GameSystemVerificationFixture fixture)
    {
        Character attacker = fixture.Player;

        Skill skill = CreateSkill(
            attacker,
            10,
            SkillColor.Red,
            ActionType.NormalAttack,
            reuseValue: true,
            minPower: 10,
            maxPower: 10);

        ActionSlot slot = CreateSlot(attacker, fixture.Enemy, skill, 10290701);
        BattleAction action = new() { Slot = slot };

        StrengthStatus strength = new(3, 5);
        attacker.AddStatus(strength, attacker);

        try
        {
            int firstPower = action.RollPowerForExchange(0);
            action.ApplyClashModifiers(0, 0);

            int firstJudgment = action.JudgmentModifier;
            int firstClash = action.ClashPower;
            int firstDamagePower = action.GetDamagePower();

            int secondPower = action.RollPowerForExchange(0);
            action.ApplyClashModifiers(0, 0);

            int secondJudgment = action.JudgmentModifier;
            int secondClash = action.ClashPower;
            int secondDamagePower = action.GetDamagePower();

            Check("R7-reuse first roll keeps base power 10",
                firstPower == 10,
                $"power={firstPower}");

            Check("R7-reuse first judgment modifier equals strength 3",
                firstJudgment == 3,
                $"judgment={firstJudgment}");

            Check("R7-reuse cached roll returns the same base power",
                secondPower == 10,
                $"power={secondPower}");

            Check("R7-reuse does not add the judgment modifier twice",
                secondJudgment == firstJudgment,
                $"first={firstJudgment}, second={secondJudgment}");

            Check("R7-reuse keeps the clash power identical",
                secondClash == firstClash,
                $"first={firstClash}, second={secondClash}");

            Check("R7-reuse keeps the damage power identical",
                secondDamagePower == firstDamagePower,
                $"first={firstDamagePower}, second={secondDamagePower}");

            Check("R7-reuse is flagged on the reused roll result",
                action.LastRollResult != null &&
                action.LastRollResult.WasReused,
                $"wasReused={action.LastRollResult?.WasReused}");
        }
        finally
        {
            attacker.RemoveStatus(
                strength,
                StatusEffectRemoveReason.Manual);
        }
    }

    //====================================================================
    // A. BLUE 자기 회복 — 상대 내성과 독립
    //====================================================================

    private static void VerifyBlueRecoveryIgnoresTargetResistance(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        StaggerGaugeMechanic playerGauge =
            player.GetMechanic<StaggerGaugeMechanic>();
        StaggerGaugeMechanic enemyGauge =
            enemy.GetMechanic<StaggerGaugeMechanic>();

        Check("R7-blue player gauge exists",
            playerGauge != null && playerGauge.MaxGauge > 0,
            $"max={playerGauge?.MaxGauge}");

        Check("R7-blue enemy gauge exists",
            enemyGauge != null && enemyGauge.MaxGauge > 0,
            $"max={enemyGauge?.MaxGauge}");

        if (playerGauge == null || enemyGauge == null)
            return;

        PhysicalResistanceProfile resistance =
            enemy.Data?.StaggerResistances;

        float cut = resistance?.CutMultiplier ?? 1f;
        float blunt = resistance?.BluntMultiplier ?? 1f;
        float pierce = resistance?.PierceMultiplier ?? 1f;

        if (resistance != null)
        {
            resistance.CutMultiplier = 0f;
            resistance.BluntMultiplier = 0f;
            resistance.PierceMultiplier = 0f;
        }

        try
        {
            // 게이지를 20으로 낮춰 회복 여지를 만든다(0이면 창이 열려 회복 불가).
            playerGauge.SacrificeGauge(
                Mathf.Max(0, playerGauge.CurrentGauge - 20));

            int playerBefore = playerGauge.CurrentGauge;
            int enemyBefore = enemyGauge.CurrentGauge;

            Skill playerSkill = CreateSkill(
                player, 10, SkillColor.Blue, ActionType.NormalAttack,
                reuseValue: false, minPower: 10, maxPower: 10);

            Skill enemySkill = CreateSkill(
                enemy, 5, SkillColor.Red, ActionType.NormalAttack,
                reuseValue: false, minPower: 5, maxPower: 5);

            ActionSlot playerSlot =
                CreateSlot(player, enemy, playerSkill, 10290711);
            ActionSlot enemySlot =
                CreateSlot(enemy, player, enemySkill, 10290712);

            ClashResultContext result =
                fixture.ClashManager.ResolvePair(
                    new ClashPair(playerSlot, enemySlot));

            ClashExchangeResult exchange =
                result != null && result.Exchanges.Count > 0
                    ? result.Exchanges[0]
                    : null;

            Check("R7-blue exchange resolved",
                exchange != null && !exchange.WasCancelled,
                $"cancelled={exchange?.WasCancelled}");

            Check("R7-blue winner is the BLUE action",
                exchange != null &&
                exchange.WinnerAction != null &&
                exchange.WinnerAction.Skill?.IsBlue == true,
                $"winner={exchange?.WinnerAction?.Skill?.Color}");

            Check("R7-blue target takes no stagger damage at x0 resistance",
                enemyGauge.CurrentGauge == enemyBefore,
                $"before={enemyBefore}, after={enemyGauge.CurrentGauge}");

            Check("R7-blue self recovery is independent of target resistance",
                playerGauge.CurrentGauge == playerBefore + 10,
                $"before={playerBefore}, after={playerGauge.CurrentGauge}");
        }
        finally
        {
            if (resistance != null)
            {
                resistance.CutMultiplier = cut;
                resistance.BluntMultiplier = blunt;
                resistance.PierceMultiplier = pierce;
            }
        }
    }

    // A 대조군: 상대 내성이 정상(×1)이면 상대 게이지가 줄고 자기 회복도 동일하게 적용된다.
    private static void VerifyBlueRecoveryControlWithResistance(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        StaggerGaugeMechanic playerGauge =
            player.GetMechanic<StaggerGaugeMechanic>();
        StaggerGaugeMechanic enemyGauge =
            enemy.GetMechanic<StaggerGaugeMechanic>();

        if (playerGauge == null || enemyGauge == null)
        {
            Check("R7-blue-control gauges exist", false);
            return;
        }

        playerGauge.SacrificeGauge(
            Mathf.Max(0, playerGauge.CurrentGauge - 20));

        int playerBefore = playerGauge.CurrentGauge;
        int enemyBefore = enemyGauge.CurrentGauge;

        Skill playerSkill = CreateSkill(
            player, 10, SkillColor.Blue, ActionType.NormalAttack,
            reuseValue: false, minPower: 10, maxPower: 10);

        Skill enemySkill = CreateSkill(
            enemy, 5, SkillColor.Red, ActionType.NormalAttack,
            reuseValue: false, minPower: 5, maxPower: 5);

        ActionSlot playerSlot =
            CreateSlot(player, enemy, playerSkill, 10290721);
        ActionSlot enemySlot =
            CreateSlot(enemy, player, enemySkill, 10290722);

        fixture.ClashManager.ResolvePair(
            new ClashPair(playerSlot, enemySlot));

        Check("R7-blue-control target gauge decreases",
            enemyGauge.CurrentGauge < enemyBefore,
            $"before={enemyBefore}, after={enemyGauge.CurrentGauge}");

        Check("R7-blue-control self recovery still applies",
            playerGauge.CurrentGauge == playerBefore + 10,
            $"before={playerBefore}, after={playerGauge.CurrentGauge}");
    }

    //====================================================================
    // B. 무작위 동률 반복은 콘텐츠 오류가 아니다
    //====================================================================

    private static void VerifyRepeatedRandomTieIsNotContentError(
        GameSystemVerificationFixture fixture)
    {
        const int iterations = 60;

        int contentErrors = 0;
        int cancelled = 0;
        int resolved = 0;

        for (int i = 0; i < iterations; i++)
        {
            CharacterVerificationScenarioTools.ResetCombatState(fixture.Player);
            CharacterVerificationScenarioTools.ResetCombatState(fixture.Enemy);

            // 1..2 범위는 동률이 자주 나오는 좁은 분포다.
            // 같은 동률 쌍이 연속 반복되어도 '콘텐츠 오류'가 아니어야 한다.
            Skill playerSkill = CreateSkill(
                fixture.Player, 1, SkillColor.Red, ActionType.NormalAttack,
                reuseValue: false, minPower: 1, maxPower: 2);

            Skill enemySkill = CreateSkill(
                fixture.Enemy, 1, SkillColor.Red, ActionType.NormalAttack,
                reuseValue: false, minPower: 1, maxPower: 2);

            ActionSlot playerSlot = CreateSlot(
                fixture.Player, fixture.Enemy, playerSkill,
                1029073000 + (i * 2));
            ActionSlot enemySlot = CreateSlot(
                fixture.Enemy, fixture.Player, enemySkill,
                1029073001 + (i * 2));

            ClashResultContext result =
                fixture.ClashManager.ResolvePair(
                    new ClashPair(playerSlot, enemySlot));

            ClashExchangeResult exchange =
                result != null && result.Exchanges.Count > 0
                    ? result.Exchanges[0]
                    : null;

            if (exchange == null)
                continue;

            if (exchange.IsContentError)
            {
                contentErrors++;

                if (contentErrors <= 3)
                {
                    Debug.LogWarning(
                        $"{LogPrefix} content error sample: " +
                        $"{exchange.ContentErrorReason}");
                }
            }

            if (exchange.WasCancelled)
                cancelled++;
            else
                resolved++;
        }

        Check("R7-tie narrow-range exchanges were resolved",
            resolved > 0,
            $"resolved={resolved}, cancelled={cancelled}");

        Check("R7-tie probabilistic repeated ties are not content errors",
            contentErrors == 0,
            $"contentErrors={contentErrors}/{iterations}");
    }

    private static void VerifyTiePolicyClassification(
        GameSystemVerificationFixture fixture)
    {
        Skill fixedSkill = CreateSkill(
            fixture.Player, 10, SkillColor.Red, ActionType.NormalAttack,
            reuseValue: false, minPower: 10, maxPower: 10);

        Skill variableSkill = CreateSkill(
            fixture.Player, 1, SkillColor.Red, ActionType.NormalAttack,
            reuseValue: false, minPower: 1, maxPower: 10);

        ActionSlot fixedSlot =
            CreateSlot(fixture.Player, fixture.Enemy, fixedSkill, 10290731);
        ActionSlot variableSlot =
            CreateSlot(fixture.Player, fixture.Enemy, variableSkill, 10290732);

        BattleAction fixedA = new() { Slot = fixedSlot };
        BattleAction fixedB = new() { Slot = fixedSlot };
        BattleAction variableA = new() { Slot = variableSlot };
        BattleAction variableB = new() { Slot = variableSlot };

        // ClashPower를 동일하게 맞춘 뒤 정책 분류를 확인한다.
        fixedA.RollPowerForExchange(0);
        fixedB.RollPowerForExchange(0);
        variableA.RollPowerForExchange(0);
        variableB.RollPowerForExchange(0);

        Check("R7-tie fixed rolls are deterministic",
            ClashTiePolicy.IsDeterministicRoll(fixedA, 0) &&
            ClashTiePolicy.IsDeterministicRoll(fixedB, 0),
            "fixed expected");

        Check("R7-tie variable rolls are not deterministic",
            !ClashTiePolicy.IsDeterministicRoll(variableA, 0) &&
            !ClashTiePolicy.IsDeterministicRoll(variableB, 0),
            "variable expected");

        Check("R7-tie fixed equal powers are a guaranteed tie",
            ClashTiePolicy.IsGuaranteedTie(fixedA, fixedB, 0),
            $"first={fixedA.ClashPower}, second={fixedB.ClashPower}");

        Check("R7-tie variable equal powers are not a guaranteed tie",
            !ClashTiePolicy.IsGuaranteedTie(variableA, variableB, 0),
            $"first={variableA.ClashPower}, second={variableB.ClashPower}");
    }

    //====================================================================
    // D. 비균일 부위 대표 선택 (정본 미정 — 결정적 규칙 고정)
    //====================================================================

    private static void VerifyRepresentativePartIsDeterministic(
        GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Player;

        if (target.BodyParts == null || target.BodyParts.Count < 2)
        {
            Check("R7-representative player has 2+ parts", false);
            return;
        }

        float[] maxima = { 50f, 80f, 120f, 150f };

        for (int i = 0; i < target.BodyParts.Count; i++)
        {
            BodyPart part = target.BodyParts[i];
            if (part == null)
                continue;

            part.SetDebugState(
                maxima[Mathf.Min(i, maxima.Length - 1)],
                maxima[Mathf.Min(i, maxima.Length - 1)],
                false,
                false);
        }

        BodyPart representative =
            StaggerGaugeMaximumResolver.GetRepresentativePart(
                target.BodyParts);

        Check("R7-representative first valid part is chosen",
            representative != null &&
            ReferenceEquals(representative, target.BodyParts[0]),
            $"chosen={representative?.Type}");

        StaggerGaugeMechanic gauge =
            target.GetMechanic<StaggerGaugeMechanic>();

        target.RefreshMaxStaggerGauge();

        Check("R7-representative gauge uses the first part max",
            gauge != null &&
            gauge.MaxGauge == Mathf.CeilToInt(target.BodyParts[0].MaxPartHP),
            $"max={gauge?.MaxGauge}, part={target.BodyParts[0].MaxPartHP}");

        // 대표가 아닌 부위의 최대 HP를 올려도 대표 값은 유지된다.
        BodyPart second = target.BodyParts[1];
        second.SetDebugState(200f, 200f, false, false);
        target.RefreshMaxStaggerGauge();

        Check("R7-representative ignores non-representative part changes",
            gauge != null &&
            gauge.MaxGauge == Mathf.CeilToInt(target.BodyParts[0].MaxPartHP),
            $"max={gauge?.MaxGauge}, second={second.MaxPartHP}");
    }

    //====================================================================
    // E. 부위 최대 HP 변경 → 흐트러짐 최대치 갱신
    //====================================================================

    private static void VerifyPartMaxHpChangeRefreshesGauge(
        GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Player;

        StaggerGaugeMechanic gauge =
            target.GetMechanic<StaggerGaugeMechanic>();

        if (gauge == null || target.BodyParts == null ||
            target.BodyParts.Count == 0)
        {
            Check("R7-refresh gauge exists", false);
            return;
        }

        BodyPart representative =
            StaggerGaugeMaximumResolver.GetRepresentativePart(
                target.BodyParts);

        representative.SetDebugState(50f, 50f, false, false);
        target.RefreshMaxStaggerGauge();

        int before = gauge.MaxGauge;

        // 런 중 성장 경로(감정 증강/견고한 육체)와 동일한 API.
        representative.IncreaseMaxHPPercent(1f);

        int expected = Mathf.CeilToInt(representative.MaxPartHP);

        Check("R7-refresh part max hp grows",
            Mathf.Approximately(representative.MaxPartHP, 100f),
            $"maxPartHp={representative.MaxPartHP}");

        Check("R7-refresh gauge max follows the part max without manual call",
            gauge.MaxGauge == expected,
            $"before={before}, expected={expected}, actual={gauge.MaxGauge}");

        Check("R7-refresh current gauge stays clamped",
            gauge.CurrentGauge <= gauge.MaxGauge,
            $"current={gauge.CurrentGauge}, max={gauge.MaxGauge}");
    }

    //====================================================================
    // Helpers
    //====================================================================

    private static Skill CreateSkill(
        Character owner,
        int power,
        SkillColor color,
        ActionType actionType,
        bool reuseValue = false,
        int minPower = -1,
        int maxPower = -1)
    {
        int min = minPower < 0 ? power : minPower;
        int max = maxPower < 0 ? power : maxPower;

        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name =
            $"R7Verification_{min}_{max}_{color}_{actionType}";
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
                MinPower = min,
                MaxPower = max,
                RngSource = RollRngSource.CharacterDefault,
                ReuseValueAcrossAction = reuseValue
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
            SlotId = "VERIFY:DMG-R7",
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
