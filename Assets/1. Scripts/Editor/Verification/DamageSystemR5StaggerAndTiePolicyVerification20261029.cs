using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 R5 (DMG-06 / DMG-07) 회귀 검증.
///
/// 정본 §6.3 / §12.2 (DMG-06):
///   - 부위 보유 대상의 흐트러짐 최대치는 '부위 1개의 최대 체력'이다.
///   - 단일 HP형은 100이다.
///   - OverrideMaxStaggerGauge는 정본의 명시적 예외로만 유지한다.
///
/// 정본 §3.1 (DMG-07):
///   - 동률이면 승부가 날 때까지 위력만 다시 굴린다(속도 보정 고정).
///   - 서로 항상 같은 값이 나오는 고정 범위는 '콘텐츠 오류'로 판정해
///     무한 루프 없이 중단한다. 64회 후 강제 무승부는 게임 규칙이 아니다.
///
/// Play Mode + 초기화된 BattleManager 필요.
/// </summary>
public static class DamageSystemR5StaggerAndTiePolicyVerification20261029
{
    private const string LogPrefix = "[Damage R5]";
    private const string TieErrorTag = "[1029 DMG-07][CONTENT_ERROR]";

    private static int pass;
    private static int fail;

    private static readonly List<SkillDefinition> createdDefinitions = new();

    [MenuItem("Game System Verification/1029 Damage R5 - Stagger Maximum and Tie Policy (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage R5 검증은 Play Mode에서 실행해야 합니다.");
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

        if (bodyPartEnemy == null)
        {
            throw new InvalidOperationException(
                "부위 보유 Enemy가 필요합니다.");
        }

        if (singleHpEnemy == null)
        {
            throw new InvalidOperationException(
                "단일 HP Enemy가 필요합니다.");
        }

        pass = 0;
        fail = 0;
        createdDefinitions.Clear();

        try
        {
            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT17InitialFromPartData(fixture, "player"));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT17InitialFromPartData(fixture, "enemy"));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyT17SingleHp(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT17OverrideException(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT17Recalculation(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT19FixedTieIsContentError(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyT19ControlFixedNonTie(fixture));
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
                $"1029 Damage R5 검증 실패: {fail} checks");
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

    // T17: 부위형 대상의 초기 흐트러짐 최대치는 '부위 1개의 최대 체력'이다.
    //      등급 기본값(Player 350 / Elite 100)과 일치하면 안 된다.
    private static void VerifyT17InitialFromPartData(
        GameSystemVerificationFixture fixture,
        string label)
    {
        Character target =
            label == "player"
                ? fixture.Player
                : fixture.Enemy;

        StaggerGaugeMechanic gauge =
            target.GetMechanic<StaggerGaugeMechanic>();

        Check($"T17 {label} has body parts",
            target.UsesBodyParts && target.BodyParts.Count > 0,
            $"usesParts={target.UsesBodyParts}");

        Check($"T17 {label} gauge exists",
            gauge != null);

        if (gauge == null || target.BodyParts.Count == 0)
            return;

        BodyPart representative = target.BodyParts[0];
        int expected = Mathf.Max(1, Mathf.CeilToInt(representative.MaxPartHP));

        int tierMaximum =
            fixture.Context?.Rules?.Stagger?.GetTierMaximum(
                target.Data.CombatantTier) ?? -1;

        Check($"T17 {label} max gauge follows representative part MaxHP",
            gauge.MaxGauge == expected,
            $"actual={gauge.MaxGauge}, expected={expected}, " +
            $"part={representative.Type}, tierDefault={tierMaximum}");

        Check($"T17 {label} max gauge ignores tier default when it differs",
            expected == tierMaximum || gauge.MaxGauge != tierMaximum,
            $"actual={gauge.MaxGauge}, tierDefault={tierMaximum}");

        Check($"T17 {label} starts full",
            gauge.CurrentGauge == gauge.MaxGauge,
            $"current={gauge.CurrentGauge}, max={gauge.MaxGauge}");
    }

    // T17: 단일 HP형은 100 고정.
    private static void VerifyT17SingleHp(
        GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Enemy;

        StaggerGaugeMechanic gauge =
            target.GetMechanic<StaggerGaugeMechanic>();

        Check("T17 single-hp target is single-hp",
            !target.UsesBodyParts,
            $"usesParts={target.UsesBodyParts}");

        Check("T17 single-hp gauge exists", gauge != null);

        if (gauge == null)
            return;

        Check("T17 single-hp max gauge is 100",
            gauge.MaxGauge == 100,
            $"actual={gauge.MaxGauge}");
    }

    // T17: OverrideMaxStaggerGauge는 정본의 명시적 예외로 유지된다.
    private static void VerifyT17OverrideException(
        GameSystemVerificationFixture fixture)
    {
        CharacterData data =
            ScriptableObject.CreateInstance<CharacterData>();

        try
        {
            data.EnableStaggerGauge = true;
            data.OverrideMaxStaggerGauge = true;
            data.MaxStaggerGauge = 123;

            int resolved = StaggerGaugeMaximumResolver.Resolve(
                data,
                usesBodyParts: true,
                fixture.Player.BodyParts);

            Check("T17 override keeps explicit data value",
                resolved == 123,
                $"actual={resolved}");

            data.OverrideMaxStaggerGauge = false;

            int derived = StaggerGaugeMaximumResolver.Resolve(
                data,
                usesBodyParts: true,
                fixture.Player.BodyParts);

            int expected = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    StaggerGaugeMaximumResolver
                        .GetRepresentativePart(fixture.Player.BodyParts)
                        .MaxPartHP));

            Check("T17 without override derives from part MaxHP",
                derived == expected,
                $"actual={derived}, expected={expected}");

            int singleHp = StaggerGaugeMaximumResolver.Resolve(
                data,
                usesBodyParts: false,
                fixture.Player.BodyParts);

            Check("T17 single-hp resolves to 100",
                singleHp == 100,
                $"actual={singleHp}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    // T17(재계산): 런 중 부위 최대 HP가 변하면 명시적 갱신 시점에 최대치를 다시 계산하고,
    //      현재 게이지는 새 최대치로 clamp한다. (정본 100 / 150 예시 경로)
    private static void VerifyT17Recalculation(
        GameSystemVerificationFixture fixture)
    {
        Character target = fixture.Player;
        StaggerGaugeMechanic gauge =
            target.GetMechanic<StaggerGaugeMechanic>();

        Check("T17-recalc gauge exists", gauge != null);

        if (gauge == null)
            return;

        SetAllPartMaxHp(target, 100f);
        bool refreshed = target.RefreshMaxStaggerGauge();

        Check("T17-recalc refresh applies part max 100",
            refreshed && gauge.MaxGauge == 100,
            $"refreshed={refreshed}, max={gauge.MaxGauge}");

        Check("T17-recalc current gauge clamped to new max",
            gauge.CurrentGauge <= gauge.MaxGauge,
            $"current={gauge.CurrentGauge}, max={gauge.MaxGauge}");

        SetAllPartMaxHp(target, 150f);
        target.RefreshMaxStaggerGauge();

        Check("T17-recalc refresh applies part max 150",
            gauge.MaxGauge == 150,
            $"actual={gauge.MaxGauge}");

        int currentBeforeShrink = gauge.CurrentGauge;

        SetAllPartMaxHp(target, 50f);
        target.RefreshMaxStaggerGauge();

        Check("T17-recalc shrink clamps current gauge without ratio rescale",
            gauge.MaxGauge == 50 &&
            gauge.CurrentGauge == Mathf.Min(currentBeforeShrink, 50),
            $"max={gauge.MaxGauge}, current={gauge.CurrentGauge}, " +
            $"before={currentBeforeShrink}");
    }

    private static void SetAllPartMaxHp(
        Character character,
        float maxHp)
    {
        if (character?.BodyParts == null)
            return;

        foreach (BodyPart part in character.BodyParts)
        {
            part?.SetDebugState(maxHp, maxHp, false, false);
        }
    }

    // T19: 양쪽 굴림이 고정 동률(10 vs 10)이면 정본은 승부가 날 때까지 재굴림을 요구하지만
    //      값이 절대 변하지 않으므로 '콘텐츠 오류'로 판정해 즉시 중단해야 한다.
    //      64회 재굴림 후 무승부 반환은 게임 규칙이 아니다.
    private static void VerifyT19FixedTieIsContentError(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        int playerHpBefore = player.CurrentHP;
        int enemyHpBefore = enemy.CurrentHP;

        Skill playerSkill =
            CreateSkill(player, 10, SkillColor.Red, ActionType.NormalAttack);
        Skill enemySkill =
            CreateSkill(enemy, 10, SkillColor.Red, ActionType.NormalAttack);

        ActionSlot playerSlot =
            CreateSlot(player, enemy, playerSkill, 10290501);
        ActionSlot enemySlot =
            CreateSlot(enemy, player, enemySkill, 10290502);

        List<string> capturedErrors = new();
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error && condition != null)
                capturedErrors.Add(condition);
        };

        ClashResultContext result;

        Application.logMessageReceived += handler;
        try
        {
            result = fixture.ClashManager.ResolvePair(
                new ClashPair(playerSlot, enemySlot));
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        ClashExchangeResult exchange =
            result != null && result.Exchanges.Count > 0
                ? result.Exchanges[0]
                : null;

        Check("T19 fixed tie exchange resolved",
            exchange != null,
            $"exchanges={result?.Exchanges?.Count}");

        if (exchange == null)
            return;

        Check("T19 fixed tie is classified as content error",
            exchange.IsContentError,
            $"reason={exchange.ContentErrorReason}");

        Check("T19 content error is not a game-rule draw",
            !exchange.IsTie,
            $"isTie={exchange.IsTie}");

        Check("T19 content error cancels the exchange",
            exchange.WasCancelled,
            $"cancelled={exchange.WasCancelled}");

        Check("T19 content error has no winner",
            exchange.WinnerAction == null,
            $"winner={exchange.WinnerAction?.Owner?.Data?.CharacterName}");

        Check("T19 fixed tie detected early, not after 64 rerolls",
            exchange.TieRerollCount <= 4,
            $"rerolls={exchange.TieRerollCount}");

        Check("T19 content error deals no HP damage",
            player.CurrentHP == playerHpBefore &&
            enemy.CurrentHP == enemyHpBefore,
            $"player={playerHpBefore}->{player.CurrentHP}, " +
            $"enemy={enemyHpBefore}->{enemy.CurrentHP}");

        Check("T19 content error is logged",
            capturedErrors.Exists(
                message => message.Contains(TieErrorTag)),
            $"errors={capturedErrors.Count}");
    }

    // T19 대조군: 고정값이지만 동률이 아닌 경우(10 vs 12)는 정상적으로 승부가 난다.
    private static void VerifyT19ControlFixedNonTie(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        Skill playerSkill =
            CreateSkill(player, 10, SkillColor.Red, ActionType.NormalAttack);
        Skill enemySkill =
            CreateSkill(enemy, 12, SkillColor.Red, ActionType.NormalAttack);

        ActionSlot playerSlot =
            CreateSlot(player, enemy, playerSkill, 10290511);
        ActionSlot enemySlot =
            CreateSlot(enemy, player, enemySkill, 10290512);

        List<string> capturedErrors = new();
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Error && condition != null)
                capturedErrors.Add(condition);
        };

        ClashResultContext result;

        Application.logMessageReceived += handler;
        try
        {
            result = fixture.ClashManager.ResolvePair(
                new ClashPair(playerSlot, enemySlot));
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        ClashExchangeResult exchange =
            result != null && result.Exchanges.Count > 0
                ? result.Exchanges[0]
                : null;

        Check("T19-control fixed non-tie exchange resolved",
            exchange != null,
            $"exchanges={result?.Exchanges?.Count}");

        if (exchange == null)
            return;

        Check("T19-control fixed non-tie is not a content error",
            !exchange.IsContentError,
            $"reason={exchange.ContentErrorReason}");

        Check("T19-control fixed non-tie has a winner",
            exchange.WinnerAction != null &&
            exchange.LoserAction != null,
            $"winner={exchange.WinnerAction?.Owner?.Data?.CharacterName}");

        Check("T19-control winner holds the higher clash power",
            exchange.WinnerAction != null &&
            exchange.FirstClashPower != exchange.SecondClashPower &&
            (exchange.WinnerAction == exchange.FirstAction
                ? exchange.FirstClashPower > exchange.SecondClashPower
                : exchange.SecondClashPower > exchange.FirstClashPower),
            $"first={exchange.FirstClashPower}, " +
            $"second={exchange.SecondClashPower}");

        Check("T19-control loser took HP damage",
            exchange.LoserAction?.Owner != null &&
            exchange.LoserAction.Owner.CurrentHP <
                exchange.LoserAction.Owner.MaxCombatHP,
            $"loserHp={exchange.LoserAction?.Owner?.CurrentHP}, " +
            $"loserMax={exchange.LoserAction?.Owner?.MaxCombatHP}");

        Check("T19-control fixed non-tie logs no content error",
            !capturedErrors.Exists(
                message => message.Contains(TieErrorTag)),
            $"errors={capturedErrors.Count}");
    }

    private static Skill CreateSkill(
        Character owner,
        int power,
        SkillColor color,
        ActionType actionType)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name = $"R5Verification_{power}_{color}_{actionType}";
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
            SlotId = "VERIFY:DMG-R5",
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
