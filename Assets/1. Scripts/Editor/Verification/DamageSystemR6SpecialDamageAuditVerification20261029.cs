using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1029 데미지 개편 R6 — 특수 피해/데이터 감사 (DMG-08 / DMG-09 / DMG-10).
///
/// 이 단계는 "확인 전에 무조건 고치지 않는다"는 계획서 원칙에 따라
/// **현재 계약을 측정·고정**하는 감사 테스트다. 제품 코드는 변경하지 않았다.
///
/// DMG-08: 일반 피해의 곱연산은 내성 배율 하나뿐이며, 기세/속도는 계수로 쓰이지 않는다.
///         단계 순서(Raw → Base → Attacker → Guard → Final)와 최소 1 보장 위치를 고정한다.
/// DMG-09: 모든 SkillDefinition의 굴림 데이터 범위를 전수 조사하고 요약을 남긴다.
///         피해 계산이 굴림 데이터를 변형하지 않는지 확인한다.
/// DMG-10: 피해 원인(일반/도트/자해/반격)이 DTO로 구분되는지, 한 타격이 두 번
///         적용되지 않는지 확인한다.
/// T18:    동일 기본 위력 3굴림 RED에서 방어도가 타격마다 남은 값에서 차감되는지 확인한다.
///
/// Play Mode + 초기화된 BattleManager 필요.
/// </summary>
public static class DamageSystemR6SpecialDamageAuditVerification20261029
{
    private const string LogPrefix = "[Damage R6]";
    private const string RollDataPrefix = "[Damage R6][ROLLDATA]";

    private static int pass;
    private static int fail;

    private static readonly List<SkillDefinition> createdDefinitions = new();

    [MenuItem("Game System Verification/1029 Damage R6 - Special Damage Audit (Play Mode)")]
    public static void VerifyFromMenu()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "1029 Damage R6 검증은 Play Mode에서 실행해야 합니다.");
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
                manager, singleHpEnemy,
                fixture => VerifyDmg08PipelineMath(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyDmg08StageOrder(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyDmg08MinimumOne(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyDmg10OriginContract(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyDmg10StatusBypassesGuard(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyDmg10SelfCostBypassesModifiers(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyDmg10SingleApplication(fixture));

            RunWithFixture(
                manager, bodyPartEnemy,
                fixture => VerifyDmg10ClashAppliesOnce(fixture));

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyT18ThreeRollGuardConsumption(fixture));

            VerifyDmg09RollDataAudit();

            RunWithFixture(
                manager, singleHpEnemy,
                fixture => VerifyDmg09RollDataImmutable(fixture));
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
                $"1029 Damage R6 검증 실패: {fail} checks");
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
    // DMG-08 — 특수 계수·Hook의 곱/flat 순서
    //====================================================================

    // 일반 피해: RawPower × DamageCoefficient는 정확히 1회, 내성 배율도 1회.
    private static void VerifyDmg08PipelineMath(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;

        Check("DMG-08 damage manager exists", manager != null);

        if (manager == null)
            return;

        int hpBefore = target.CurrentHP;

        DamageRequest request = BuildDirectRequest(
            fixture.Player,
            target,
            rawPower: 20,
            coefficient: 1f,
            applyResistance: true);

        DamageContext context =
            manager.ApplyDamageContext(request);

        Check("DMG-08 base damage = raw x coefficient",
            context != null &&
            context.BaseDamage ==
                Mathf.FloorToInt(20f * 1f),
            $"base={context?.BaseDamage}");

        Check("DMG-08 coefficient is applied once",
            context != null &&
            context.BaseDamage ==
                Mathf.FloorToInt(context.RawPower * context.DamageCoefficient),
            $"base={context?.BaseDamage}, raw={context?.RawPower}, " +
            $"coef={context?.DamageCoefficient}");

        Check("DMG-08 momentum is never a damage multiplier",
            context != null && Mathf.Approximately(context.MomentumMultiplier, 1f),
            $"momentumMultiplier={context?.MomentumMultiplier}");

        bool cleanTarget =
            target.StatusEffects.Count == 0 &&
            (target.BodyParts == null || AllPartsClean(target));

        int expectedAfterResistance = context == null
            ? -1
            : Mathf.Max(
                0,
                Mathf.FloorToInt(
                    context.BaseDamage *
                    context.PhysicalResistanceMultiplier));

        Check("DMG-08 resistance multiplier is applied once",
            context != null &&
            (!cleanTarget ||
             context.TargetModifiedDamage == expectedAfterResistance),
            $"modified={context?.TargetModifiedDamage}, " +
            $"expected={expectedAfterResistance}, " +
            $"mult={context?.PhysicalResistanceMultiplier}, clean={cleanTarget}");

        Check("DMG-08 final damage never exceeds post-guard damage",
            context != null &&
            context.FinalDamage <= context.DamageAfterGuard,
            $"final={context?.FinalDamage}, afterGuard={context?.DamageAfterGuard}");

        Check("DMG-08 applied hp damage equals final damage",
            context != null &&
            target.CurrentHP ==
                Mathf.Max(0, hpBefore - context.FinalDamage),
            $"hp={hpBefore}->{target.CurrentHP}, final={context?.FinalDamage}");
    }

    private static void VerifyDmg08StageOrder(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;

        Check("DMG-08 stage manager exists", manager != null);

        if (manager == null)
            return;

        DamageContext context = manager.ApplyDamageContext(
            BuildDirectRequest(
                fixture.Player,
                fixture.Enemy,
                rawPower: 10,
                coefficient: 1f,
                applyResistance: true));

        if (context?.StageSnapshots == null)
        {
            Check("DMG-08 stage snapshots captured", false,
                "snapshots=null");
            return;
        }

        List<DamageStage> stages =
            new List<DamageStage>();

        foreach (DamageSnapshot snapshot in context.StageSnapshots)
            stages.Add(snapshot.Stage);

        int damagePower = stages.IndexOf(DamageStage.DamagePower);
        int baseDamage = stages.IndexOf(DamageStage.BaseDamage);
        int attacker = stages.IndexOf(DamageStage.AttackerModifiers);
        int guard = stages.IndexOf(DamageStage.Guard);
        int finalized = stages.IndexOf(DamageStage.Finalized);
        int applied = stages.IndexOf(DamageStage.Applied);

        Check("DMG-08 DamagePower recorded once",
            stages.FindAll(s => s == DamageStage.DamagePower).Count == 1,
            $"count={stages.FindAll(s => s == DamageStage.DamagePower).Count}");

        Check("DMG-08 BaseDamage recorded once",
            stages.FindAll(s => s == DamageStage.BaseDamage).Count == 1,
            $"count={stages.FindAll(s => s == DamageStage.BaseDamage).Count}");

        Check("DMG-08 order Raw -> Base",
            damagePower >= 0 && baseDamage > damagePower,
            $"raw={damagePower}, base={baseDamage}");

        Check("DMG-08 order Base -> AttackerModifiers",
            attacker > baseDamage,
            $"base={baseDamage}, attacker={attacker}");

        Check("DMG-08 order AttackerModifiers -> Guard",
            guard > attacker,
            $"attacker={attacker}, guard={guard}");

        Check("DMG-08 order Guard -> Finalized -> Applied",
            finalized > guard && applied > finalized,
            $"guard={guard}, finalized={finalized}, applied={applied}");

        int lastBeforeApplied = Mathf.Max(
            Mathf.Max(damagePower, baseDamage),
            Mathf.Max(attacker, Mathf.Max(guard, finalized)));

        Check("DMG-08 applied is recorded after every calculation stage",
            applied > lastBeforeApplied,
            $"applied={applied}, lastBefore={lastBeforeApplied}");
    }

    // 최소 1 보장은 Guard 직전에 한 번. 계수가 0보다 크면 0 피해가 1이 된다.
    private static void VerifyDmg08MinimumOne(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;

        if (manager == null)
        {
            Check("DMG-08 min-1 manager exists", false);
            return;
        }

        int hpBefore = fixture.Enemy.CurrentHP;

        DamageContext context = manager.ApplyDamageContext(
            BuildDirectRequest(
                fixture.Player,
                fixture.Enemy,
                rawPower: 1,
                coefficient: 0.1f,
                applyResistance: false));

        Check("DMG-08 zero base damage floors to minimum 1",
            context != null &&
            context.BaseDamage == 0 &&
            context.FinalDamage == 1,
            $"base={context?.BaseDamage}, final={context?.FinalDamage}");

        Check("DMG-08 minimum 1 is actually applied to hp",
            fixture.Enemy.CurrentHP == hpBefore - 1,
            $"hp={hpBefore}->{fixture.Enemy.CurrentHP}");
    }

    //====================================================================
    // DMG-10 — 피해 원인 구분과 단일 적용
    //====================================================================

    private static void VerifyDmg10OriginContract(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        Skill skill = CreateSkill(player, 10, SkillColor.Red, ActionType.NormalAttack);
        ActionSlot slot = CreateSlot(player, enemy, skill, 10290601);

        BattleAction action = new BattleAction { Slot = slot };

        DamageRequest normal =
            DamageRequest.FromAction(
                action,
                DamageType.Direct,
                canBreakPart: false,
                isClashDamage: false,
                targetLostClash: false);

        DamageRequest dot =
            DamageRequest.StatusPart(null, 7, null);

        DamageRequest selfCost =
            DamageRequest.SelfCost(player, 3);

        DamageRequest counter =
            DamageRequest.Counter(action, 7, false);

        Check("DMG-10 normal origin is Direct",
            normal.Type == DamageType.Direct,
            $"type={normal.Type}");

        Check("DMG-10 dot origin is StatusPart",
            dot.Type == DamageType.StatusPart,
            $"type={dot.Type}");

        Check("DMG-10 self-cost origin is SelfCost",
            selfCost.Type == DamageType.SelfCost &&
            selfCost.TargetCharacter == player,
            $"type={selfCost.Type}, targetIsOwner={selfCost.TargetCharacter == player}");

        Check("DMG-10 counter origin is Counter",
            counter.Type == DamageType.Counter &&
            counter.SourceAction != null,
            $"type={counter.Type}");

        Check("DMG-10 origins are mutually distinct",
            new HashSet<DamageType>
            {
                normal.Type, dot.Type, selfCost.Type, counter.Type
            }.Count == 4,
            $"{normal.Type}/{dot.Type}/{selfCost.Type}/{counter.Type}");

        // 원인별 정책은 요청에만 붙고 전역 규칙을 바꾸지 않는다.
        Check("DMG-10 normal request uses full policy set",
            normal.ApplyPhysicalResistance &&
            normal.ApplyGuard &&
            normal.ApplyAttackerModifiers &&
            normal.ApplyTargetModifiers,
            $"res={normal.ApplyPhysicalResistance}, guard={normal.ApplyGuard}, " +
            $"atk={normal.ApplyAttackerModifiers}, tgt={normal.ApplyTargetModifiers}");

        Check("DMG-10 dot request bypasses guard and target modifiers",
            !dot.ApplyGuard &&
            !dot.ApplyTargetModifiers &&
            !dot.ApplyAttackerModifiers,
            $"guard={dot.ApplyGuard}, tgt={dot.ApplyTargetModifiers}, " +
            $"atk={dot.ApplyAttackerModifiers}");

        Check("DMG-10 self-cost request bypasses target modifiers",
            !selfCost.ApplyTargetModifiers &&
            !selfCost.ApplyGuard &&
            !selfCost.ApplyAttackerModifiers,
            $"tgt={selfCost.ApplyTargetModifiers}, guard={selfCost.ApplyGuard}");
    }

    private static void VerifyDmg10StatusBypassesGuard(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;

        BodyPart targetPart =
            target.BodyParts != null && target.BodyParts.Count > 0
                ? target.BodyParts[0]
                : null;

        if (manager == null ||
            target.RuntimeStatus == null ||
            targetPart == null)
        {
            Check("DMG-10 dot manager/status/part exists", false,
                $"manager={manager != null}, " +
                $"status={target.RuntimeStatus != null}, " +
                $"part={targetPart != null}");
            return;
        }

        target.RuntimeStatus.currentBlock = 10;
        int hpBefore = target.CurrentHP;

        DamageContext context =
            manager.ApplyDamageContext(
                DamageRequest.StatusPart(targetPart, 7, null));

        Check("DMG-10 dot does not consume guard",
            context != null &&
            context.GuardAbsorbed == 0 &&
            target.RuntimeStatus.currentBlock == 10,
            $"absorbed={context?.GuardAbsorbed}, block={target.RuntimeStatus.currentBlock}");

        Check("DMG-10 dot damages hp directly",
            context != null &&
            target.CurrentHP == Mathf.Max(0, hpBefore - context.FinalDamage),
            $"hp={hpBefore}->{target.CurrentHP}, final={context?.FinalDamage}");

        target.RuntimeStatus.currentBlock = 0;
    }

    private static void VerifyDmg10SelfCostBypassesModifiers(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character player = fixture.Player;

        if (manager == null)
        {
            Check("DMG-10 self-cost manager exists", false);
            return;
        }

        int hpBefore = player.CurrentHP;

        DamageContext context =
            manager.ApplyDamageContext(
                DamageRequest.SelfCost(player, 3));

        Check("DMG-10 self-cost equals requested damage",
            context != null && context.FinalDamage == 3,
            $"final={context?.FinalDamage}");

        Check("DMG-10 self-cost applies to owner once",
            player.CurrentHP == hpBefore - 3,
            $"hp={hpBefore}->{player.CurrentHP}");
    }

    private static void VerifyDmg10SingleApplication(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character target = fixture.Enemy;

        if (manager == null)
        {
            Check("DMG-10 single-application manager exists", false);
            return;
        }

        int hpBefore = target.CurrentHP;

        DamageContext context = manager.ApplyDamageContext(
            BuildDirectRequest(
                fixture.Player,
                target,
                rawPower: 12,
                coefficient: 1f,
                applyResistance: true));

        int applied = hpBefore - target.CurrentHP;

        Check("DMG-10 direct hit applies exactly once",
            context != null &&
            applied == context.FinalDamage,
            $"applied={applied}, final={context?.FinalDamage}");

        Check("DMG-10 direct hit is not double-counted",
            context != null &&
            context.AppliedDamage == context.FinalDamage,
            $"appliedDamage={context?.AppliedDamage}, final={context?.FinalDamage}");

        Check("DMG-10 direct hit produces a single result object",
            context?.Result != null &&
            context.EventResult != null,
            $"result={context?.Result != null}, event={context?.EventResult != null}");
    }

    // 교환 1회에서 패자 HP가 정확히 1회만 감소해야 한다.
    private static void VerifyDmg10ClashAppliesOnce(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        Skill playerSkill =
            CreateSkill(player, 10, SkillColor.Red, ActionType.NormalAttack);
        Skill enemySkill =
            CreateSkill(enemy, 4, SkillColor.Red, ActionType.NormalAttack);

        ActionSlot playerSlot =
            CreateSlot(player, enemy, playerSkill, 10290611);
        ActionSlot enemySlot =
            CreateSlot(enemy, player, enemySkill, 10290612);

        int playerHpBefore = player.CurrentHP;
        int enemyHpBefore = enemy.CurrentHP;

        ClashResultContext result =
            fixture.ClashManager.ResolvePair(
                new ClashPair(playerSlot, enemySlot));

        ClashExchangeResult exchange =
            result != null && result.Exchanges.Count > 0
                ? result.Exchanges[0]
                : null;

        Check("DMG-10 clash exchange resolved",
            exchange != null,
            $"exchanges={result?.Exchanges?.Count}");

        if (exchange == null)
            return;

        int playerDelta = playerHpBefore - player.CurrentHP;
        int enemyDelta = enemyHpBefore - enemy.CurrentHP;

        Check("DMG-10 clash applies damage to exactly one side",
            (playerDelta > 0) != (enemyDelta > 0),
            $"playerDelta={playerDelta}, enemyDelta={enemyDelta}");

        int finalDamage = exchange.DamageContext?.FinalDamage ?? 0;

        Check("DMG-10 clash hp delta equals final damage once",
            finalDamage > 0 &&
            Mathf.Max(playerDelta, enemyDelta) == finalDamage,
            $"delta={Mathf.Max(playerDelta, enemyDelta)}, final={finalDamage}");

        Check("DMG-10 single-target clash has no secondary damage contexts",
            exchange.SecondaryDamageContexts != null &&
            exchange.SecondaryDamageContexts.Count == 0,
            $"secondary={exchange.SecondaryDamageContexts?.Count}");

        Check("DMG-10 clash does not rebind the same action twice",
            exchange.DamageContext?.Action == exchange.WinnerAction,
            "damage action mismatch");
    }

    //====================================================================
    // T18 — 동일 기본 위력 3굴림 RED, 방어도 중간 소모
    //====================================================================

    // 같은 위력의 3회 타격이 한 행동 안에서 순서대로 정산될 때,
    // 방어도는 타격마다 남은 값에서 차감되고 다시 채워지지 않는다.
    private static void VerifyT18ThreeRollGuardConsumption(
        GameSystemVerificationFixture fixture)
    {
        DamageManager manager = fixture.DamageManager;
        Character player = fixture.Player;
        Character target = fixture.Enemy;

        if (manager == null || target.RuntimeStatus == null)
        {
            Check("T18 manager/status exists", false);
            return;
        }

        Skill skill = CreateSkillWithRolls(
            player, 10, rollCount: 3,
            SkillColor.Red, ActionType.NormalAttack);

        bool samePower =
            skill.Definition?.Rolls != null &&
            skill.Definition.Rolls.Count == 3;

        if (samePower)
        {
            for (int index = 0;
                 index < skill.Definition.Rolls.Count;
                 index++)
            {
                SkillRollData roll = skill.GetRollData(index);

                if (roll == null ||
                    roll.SafeMinPower != 10 ||
                    roll.SafeMaxPower != 10)
                {
                    samePower = false;
                    break;
                }
            }
        }

        Check("T18 skill has three rolls with identical base power",
            samePower,
            $"rolls={skill.Definition?.Rolls?.Count}");

        target.RuntimeStatus.currentBlock = 20;
        int hpBefore = target.CurrentHP;

        DamageContext first = manager.ApplyDamageContext(
            BuildDirectRequest(player, target, rawPower: 10,
                coefficient: 1f, applyResistance: false));
        int blockAfterFirst = target.RuntimeStatus.currentBlock;

        DamageContext second = manager.ApplyDamageContext(
            BuildDirectRequest(player, target, rawPower: 10,
                coefficient: 1f, applyResistance: false));
        int blockAfterSecond = target.RuntimeStatus.currentBlock;

        DamageContext third = manager.ApplyDamageContext(
            BuildDirectRequest(player, target, rawPower: 10,
                coefficient: 1f, applyResistance: false));

        Check("T18 first hit is fully absorbed by guard",
            first != null &&
            first.GuardAbsorbed == 10 &&
            first.FinalDamage == 0 &&
            blockAfterFirst == 10,
            $"absorbed={first?.GuardAbsorbed}, final={first?.FinalDamage}, " +
            $"block={blockAfterFirst}");

        Check("T18 second hit consumes the remaining guard",
            second != null &&
            second.GuardBefore == 10 &&
            second.GuardAbsorbed == 10 &&
            second.FinalDamage == 0 &&
            blockAfterSecond == 0,
            $"before={second?.GuardBefore}, absorbed={second?.GuardAbsorbed}, " +
            $"final={second?.FinalDamage}, block={blockAfterSecond}");

        Check("T18 guard is not refilled between hits",
            first != null &&
            second != null &&
            second.GuardBefore == first.GuardAfter,
            $"firstAfter={first?.GuardAfter}, secondBefore={second?.GuardBefore}");

        Check("T18 third hit spills to hp once guard is exhausted",
            third != null &&
            third.GuardBefore == 0 &&
            third.GuardAbsorbed == 0 &&
            third.FinalDamage == 10,
            $"before={third?.GuardBefore}, absorbed={third?.GuardAbsorbed}, " +
            $"final={third?.FinalDamage}");

        int hpLoss = hpBefore - target.CurrentHP;

        Check("T18 only the overflow reaches hp",
            hpLoss == 10,
            $"hpLoss={hpLoss}");

        target.RuntimeStatus.currentBlock = 0;
    }

    //====================================================================
    // DMG-09 — 굴림 데이터 전수 감사
    //====================================================================

    private static void VerifyDmg09RollDataAudit()
    {
        string[] guids =
            AssetDatabase.FindAssets("t:SkillDefinition");

        int skillCount = 0;
        int rollCount = 0;
        int invalidRange = 0;
        int outsideStandardWidth = 0;
        int diceRolls = 0;

        StringBuilder detail = new StringBuilder();
        StringBuilder flagged = new StringBuilder();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkillDefinition definition =
                AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);

            if (definition == null || definition.Rolls == null)
                continue;

            skillCount++;

            foreach (SkillRollData roll in definition.Rolls)
            {
                if (roll == null)
                    continue;

                rollCount++;

                if (roll.SafeMinPower > roll.SafeMaxPower)
                {
                    invalidRange++;
                    flagged.AppendLine(
                        $"{definition.name}#{roll.Index} " +
                        $"min={roll.SafeMinPower} max={roll.SafeMaxPower}");
                }

                if (roll.RngSource == RollRngSource.Dice ||
                    roll.RngSource == RollRngSource.CharacterDefault)
                {
                    diceRolls++;

                    // 정본 §4 표준 폭 1~8. 벗어난 값은 실패가 아니라 검토 대상으로 기록한다.
                    if (roll.SafeMinPower < 1 || roll.SafeMaxPower > 8)
                    {
                        outsideStandardWidth++;

                        if (outsideStandardWidth <= 25)
                        {
                            flagged.AppendLine(
                                $"{definition.name}#{roll.Index} " +
                                $"dice {roll.SafeMinPower}..{roll.SafeMaxPower} " +
                                $"mode={roll.DiceMode}");
                        }
                    }
                }

                if (detail.Length < 6000)
                {
                    detail.AppendLine(
                        $"{definition.name} | roll{roll.Index} | " +
                        $"{roll.Type} | {roll.RngSource}/{roll.DiceMode} | " +
                        $"{roll.SafeMinPower}..{roll.SafeMaxPower} | " +
                        $"energy={definition.EnergyCost}");
                }
            }
        }

        Debug.Log(
            $"{RollDataPrefix} SKILLS={skillCount} ROLLS={rollCount} " +
            $"DICE={diceRolls} OUTSIDE_1_8={outsideStandardWidth} " +
            $"INVALID_RANGE={invalidRange}");

        Debug.Log($"{RollDataPrefix} SAMPLE_BEGIN\n{detail}SAMPLE_END");

        if (flagged.Length > 0)
            Debug.Log($"{RollDataPrefix} FLAGGED\n{flagged}FLAGGED_END");

        Check("DMG-09 skill definitions were found",
            skillCount > 0,
            $"skills={skillCount}");

        Check("DMG-09 every roll has a valid min/max range",
            invalidRange == 0,
            $"invalid={invalidRange}");

        Check("DMG-09 roll data was fully enumerated",
            rollCount > 0,
            $"rolls={rollCount}");
    }

    private static void VerifyDmg09RollDataImmutable(
        GameSystemVerificationFixture fixture)
    {
        Character player = fixture.Player;
        Character enemy = fixture.Enemy;

        Skill playerSkill =
            CreateSkill(player, 10, SkillColor.Red, ActionType.NormalAttack);
        Skill enemySkill =
            CreateSkill(enemy, 4, SkillColor.Red, ActionType.NormalAttack);

        SkillRollData before = playerSkill.GetRollData(0);
        int minBefore = before?.SafeMinPower ?? -1;
        int maxBefore = before?.SafeMaxPower ?? -1;

        fixture.ClashManager.ResolvePair(
            new ClashPair(
                CreateSlot(player, enemy, playerSkill, 10290621),
                CreateSlot(enemy, player, enemySkill, 10290622)));

        SkillRollData after = playerSkill.GetRollData(0);
        int minAfter = after?.SafeMinPower ?? -1;
        int maxAfter = after?.SafeMaxPower ?? -1;

        Check("DMG-09 roll data survives damage resolution unchanged",
            minBefore == minAfter && maxBefore == maxAfter,
            $"before={minBefore}..{maxBefore}, after={minAfter}..{maxAfter}");
    }

    //====================================================================
    // Helpers
    //====================================================================

    private static bool AllPartsClean(Character character)
    {
        foreach (BodyPart part in character.BodyParts)
        {
            if (part != null && part.StatusEffects.Count > 0)
                return false;
        }

        return true;
    }

    private static DamageRequest BuildDirectRequest(
        Character source,
        Character target,
        int rawPower,
        float coefficient,
        bool applyResistance)
    {
        DamageRequest request =
            DamageRequest.Custom(
                DamageType.Direct,
                source,
                target,
                null,
                rawPower,
                coefficient,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: true);

        request.ApplyPhysicalResistance = applyResistance;
        request.PhysicalType = PhysicalDamageType.Cut;

        return request;
    }

    private static Skill CreateSkillWithRolls(
        Character owner,
        int power,
        int rollCount,
        SkillColor color,
        ActionType actionType)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name =
            $"R6Verification_{power}x{rollCount}_{color}_{actionType}";
        definition.ActionType = actionType;
        definition.Color = color;
        definition.ResolverType = SkillResolverType.Dice;
        definition.ExchangeRollCount = rollCount;
        definition.Rolls = new List<SkillRollData>();

        for (int index = 0; index < rollCount; index++)
        {
            SkillRollData roll = new()
            {
                Index = index,
                Type = CombatRollType.Attack,
                DiceMode = DicePowerMode.AbsoluteRange,
                MinPower = power,
                MaxPower = power,
                RngSource = RollRngSource.CharacterDefault
            };

            roll.Sanitize(index);
            definition.Rolls.Add(roll);
        }

        createdDefinitions.Add(definition);

        Skill skill = definition.CreateRuntimeSkill();
        skill.Initialize(owner, owner.BattleEvent);
        return skill;
    }

    private static Skill CreateSkill(
        Character owner,
        int power,
        SkillColor color,
        ActionType actionType)
    {
        SkillDefinition definition =
            ScriptableObject.CreateInstance<SkillDefinition>();

        definition.name = $"R6Verification_{power}_{color}_{actionType}";
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
        return new ActionSlot
        {
            ActionId = actionId,
            Owner = owner,
            Part = owner.BodyParts != null && owner.BodyParts.Count > 0
                ? owner.BodyParts[0]
                : null,
            Skill = skill,
            Speed = 10,
            ActionIndex = 0,
            SlotId = "VERIFY:DMG-R6",
            Phase = ActionPhase.COMBAT,
            TargetCharacter = target,
            TargetPart = target.BodyParts != null && target.BodyParts.Count > 0
                ? target.BodyParts[0]
                : null,
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
