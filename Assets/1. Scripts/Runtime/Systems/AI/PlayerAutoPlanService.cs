using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum PlayerAutoPlanMode
{
    WinRate = 0,
    Damage = 1
}

public sealed class PlayerAutoPlanResult
{
    public bool Success;
    public PlayerAutoPlanMode Mode;
    public int PlannedSlotCount;
    public int MatchedThreatCount;
    public int EnemyThreatCount;
    public int UtilitySlotCount;
    public int CombatSlotCapacity;
    public float EstimatedAverageWinRate;
    public float EstimatedDamage;
    public string Message;
}

/// <summary>
/// 현재 턴에 이미 계획된 적 ActionSlot을 읽고,
/// 플레이어의 스킬/대상/행동 슬롯을 자동으로 구성한다.
///
/// WinRate:
/// - 캐릭터별 선호 점수 없이 현재 실제 굴림 분포로 계산한 합 승률만 1차 최적화한다.
/// - 미배정 적 clash threat는 승률 0으로 포함하여 coverage를 목적함수에 직접 반영한다.
///
/// Damage:
/// - 캐릭터별 선호 점수 없이 현재 턴의 기대 HP 피해 합만 1차 최적화한다.
///
/// 두 모드 모두 primary가 완전히 같을 때만 반대 지표와 낮은 빛 비용을 tie-break로 사용한다.
///
/// 이 서비스는 계획 계산 중 실제 전투 상태나 RNG를 변경하지 않는다.
/// 최종 계획이 완성된 뒤에만 플레이어 ActionSlot을 교체한다.
/// </summary>
public sealed class PlayerAutoPlanService
{
    private readonly PlayerAutoPlanEstimator estimator = new();
    private readonly PlayerAutoPlanApplicationService applicationService = new();

    private sealed class SourceSlot
    {
        public Character Owner;
        public BodyPart Part;
        public int ActionIndex;
        public int Speed;
        public IReadOnlyList<Skill> Skills;

        public string Key =>
            $"{Part?.Type.ToString() ?? "CHAR"}:{ActionIndex}";
    }

    private sealed class Candidate
    {
        public SourceSlot Source;
        public Skill Skill;
        public Character Target;
        public BodyPart TargetPart;
        public ActionSlot Threat;
        public float WinRate;
        public float ExpectedDamage;
    }

    /// <summary>
    /// AutoPlan 탐색용 불변에 가까운 계획 상태.
    /// SourceSlot 자체를 mutate하지 않고 논리 슬롯 key를 별도로 추적하여
    /// 분기 탐색(backtracking)이 캐릭터/런타임 상태를 오염시키지 않게 한다.
    /// </summary>
    private sealed class PlanningState
    {
        public Character Player;
        public int PlannedEnergy;
        public int PlannedCombatSlots;
        public int PlannedUtilitySlots;
        public int PlannedPrestigeCount;
        public readonly List<ActionSlot> Slots =
            new List<ActionSlot>();

        private readonly HashSet<string> usedSourceKeys =
            new HashSet<string>();

        public bool IsSourceUsed(SourceSlot source) =>
            source != null && usedSourceKeys.Contains(source.Key);

        public bool CanAdd(
            SourceSlot source,
            Skill skill)
        {
            if (Player == null ||
                source == null ||
                IsSourceUsed(source) ||
                skill == null)
            {
                return false;
            }

            if (Slots.Count >=
                Mathf.Max(0, Player.GetMaxActionSlots()))
            {
                return false;
            }

            if (skill.DefaultPhase == ActionPhase.COMBAT &&
                PlannedCombatSlots >=
                Mathf.Max(0, Player.GetMaxCombatActionSlots()))
            {
                return false;
            }

            if (PlannedEnergy +
                Mathf.Max(0, skill.EnergyCost) >
                Player.CurrentEnergy)
            {
                return false;
            }

            if (skill.ActionType == ActionType.Prestige)
            {
                switch (skill.PrestigeUsePolicy)
                {
                    case PrestigeUsePolicy.None:
                        return false;

                    case PrestigeUsePolicy.OncePerTurn:
                        if (PlannedPrestigeCount > 0)
                            return false;
                        break;
                }
            }

            return true;
        }

        public void Register(
            SourceSlot source,
            ActionSlot slot)
        {
            if (source == null ||
                slot == null)
            {
                return;
            }

            usedSourceKeys.Add(source.Key);
            Slots.Add(slot);
            PlannedEnergy +=
                Mathf.Max(0, slot.Skill?.EnergyCost ?? 0);

            if (slot.Phase == ActionPhase.COMBAT)
                PlannedCombatSlots++;
            else
                PlannedUtilitySlots++;

            if (slot.Skill?.ActionType ==
                ActionType.Prestige)
            {
                PlannedPrestigeCount++;
            }
        }

        public PlanningState Clone()
        {
            PlanningState clone =
                new PlanningState
                {
                    Player = Player,
                    PlannedEnergy = PlannedEnergy,
                    PlannedCombatSlots = PlannedCombatSlots,
                    PlannedUtilitySlots = PlannedUtilitySlots,
                    PlannedPrestigeCount = PlannedPrestigeCount
                };

            foreach (ActionSlot slot in Slots)
                clone.Slots.Add(slot);

            foreach (string key in usedSourceKeys)
                clone.usedSourceKeys.Add(key);

            return clone;
        }

    }

    private sealed class PurePlanSearchResult
    {
        public bool HasValue;
        public PlanningState State;
        public float WinRateSum;
        public float ExpectedDamage;
        public int EnergyCost;
    }






    public PlayerAutoPlanResult BuildAndApply(
        BattleManager battleManager,
        PlayerAutoPlanMode mode)
    {
        Debug.Log(
            $"[AUTO_PLAN_DIAG][SERVICE_ENTER][{mode}] managerNull={battleManager == null}");

        PlayerAutoPlanResult result =
            new PlayerAutoPlanResult
            {
                Mode = mode
            };

        if (!TryResolveRuntime(
                battleManager,
                out BattleContext context,
                out Character player,
                out ActionManager actionManager,
                out SpeedManager speedManager,
                out string failure))
        {
            Debug.LogWarning(
                $"[AUTO_PLAN_DIAG][SERVICE_RUNTIME_BLOCK][{mode}] {failure}");

            result.Message = failure;
            return result;
        }

        Debug.Log(
            $"[AUTO_PLAN_DIAG][SERVICE_RUNTIME_OK][{mode}] " +
            $"player={player.name} energy={player.CurrentEnergy}/{player.MaxEnergy} " +
            $"enemies={context.Enemies?.Count ?? -1} actionSlots={actionManager.Slots?.Count ?? -1}");

        List<ActionSlot> enemySlots =
            CollectEnemySlots(
                context,
                actionManager);

        Debug.Log(
            BuildSlotDiagnostic(
                $"[AUTO_PLAN_DIAG][ENEMY_SLOTS][{mode}]",
                enemySlots));

        // [0918_NORMAL_AUTOPLAN_HOTFIX:ALLOW_ZERO_ENEMY_SLOTS]
        // [0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_GATE]
        // 적 ActionSlot은 합 매칭 정보일 뿐 공격 대상의 존재를 대표하지 않는다.
        // 특히 NormalEnemy는 SingleHpTargetModel이라 BodyPart 없이 Character 자체가 정식 타깃이다.
        // 따라서 자동계획 가능 여부는 BattleTargetValidator가 제공하는 실제 공격 타깃으로 판정한다.
        bool hasAutoPlanTarget =
            HasAnyAutoPlanTarget(context);

        Debug.Log(
            BuildTargetDiagnostic(
                context,
                mode,
                hasAutoPlanTarget));

        if (!hasAutoPlanTarget)
        {
            Debug.LogWarning(
                $"[AUTO_PLAN_DIAG][SERVICE_TARGET_BLOCK][{mode}] NO_CANONICAL_AUTOPLAN_TARGET");

            result.Message =
                "자동 지정 가능한 살아 있는 공격 대상이 없습니다.";
            return result;
        }

        if (enemySlots.Count == 0)
        {
            Debug.Log(
                "[PlayerAutoPlan] 적 ActionSlot이 0개이므로 합 매칭 없이 " +
                "살아 있는 적을 대상으로 일방 공격 Fill 계획을 계산합니다.");
        }

        List<ActionSlot> enemyThreats =
            CollectClashThreats(enemySlots);

        Debug.Log(
            BuildSlotDiagnostic(
                $"[AUTO_PLAN_DIAG][ENEMY_CLASH_THREATS][{mode}]",
                enemyThreats));

        List<SourceSlot> sources =
            BuildSources(
                player,
                speedManager);

        Debug.Log(
            $"[AUTO_PLAN_DIAG][PLAYER_SOURCES][{mode}] count={sources.Count} combatCapacity={player.GetMaxCombatActionSlots()}");

        if (sources.Count == 0)
        {
            result.Message =
                "사용 가능한 아군 행동 슬롯이 없습니다.";
            return result;
        }

        // [0918_PURE_AUTOPLAN_OBJECTIVE]
        // AutoPlan은 캐릭터별 "좋아 보이는 카드" 휴리스틱을 사용하지 않는다.
        // WinRate = 모든 적 합 위협을 기준으로 한 실제 합 승리확률 합 최대화,
        // Damage  = 현재 턴의 실제 기대 HP 피해 합 최대화.
        // 캐릭터 다형성은 ModifyRoll/Skill/Mechanic/Planning legality처럼
        // 실제 게임 규칙을 계산하는 경로에만 남긴다.
        PlanningState state =
            BuildPureObjectivePlan(
                context,
                player,
                actionManager,
                sources,
                enemyThreats,
                mode);

        Debug.Log(
            $"[AUTO_PLAN_DIAG][PURE_OBJECTIVE][{mode}] " +
            $"planned={state.Slots.Count} energy={state.PlannedEnergy}/{player.CurrentEnergy} " +
            $"utility={state.PlannedUtilitySlots} advisorScore=OFF preparationAutoPick=OFF");

        Debug.Log(
            BuildSlotDiagnostic(
                $"[AUTO_PLAN_DIAG][PLANNED_SLOTS_BEFORE_APPLY][{mode}]",
                state.Slots));

        if (state.Slots.Count == 0)
        {
            Debug.LogWarning(
                $"[AUTO_PLAN_DIAG][NO_PLAN_CANDIDATE][{mode}] sources={sources.Count} enemySlots={enemySlots.Count} threats={enemyThreats.Count}");

            result.Message =
                "현재 자원·부위·스킬 조건에서 자동 지정 가능한 행동이 없습니다.";
            return result;
        }

        Debug.Log(
            $"[AUTO_PLAN_DIAG][APPLICATION_ENTER][{mode}] planned={state.Slots.Count} energyBefore={player.CurrentEnergy}/{player.MaxEnergy}");

        int applied =
            applicationService.Apply(
                actionManager,
                player,
                state.Slots);

        Debug.Log(
            $"[AUTO_PLAN_DIAG][APPLICATION_EXIT][{mode}] applied={applied} energyAfter={player.CurrentEnergy}/{player.MaxEnergy} liveSlots={actionManager.Slots?.Count ?? -1}");

        if (applied <= 0)
        {
            result.Message =
                "자동 계획을 ActionManager에 적용하지 못해 기존 행동을 복구했습니다.";
            return result;
        }

        int matched =
            CountActualPreviewClashes(
                actionManager.Slots,
                player);

        CalculatePlanSummary(
            context,
            state.Slots,
            enemyThreats,
            speedManager,
            out float averageWinRate,
            out float totalDamage);

        result.Success = true;
        result.PlannedSlotCount = applied;
        result.MatchedThreatCount = matched;
        result.EnemyThreatCount = enemyThreats.Count;
        result.UtilitySlotCount = state.PlannedUtilitySlots;
        result.CombatSlotCapacity =
            Mathf.Max(0, player.GetMaxCombatActionSlots());
        result.EstimatedAverageWinRate =
            averageWinRate;
        result.EstimatedDamage =
            totalDamage;

        result.Message =
            mode == PlayerAutoPlanMode.WinRate
                ? $"승률 자동 지정 · 전체 위협 기준 예상 승률 {averageWinRate:P1} · " +
                  $"합 {matched}/{enemyThreats.Count} · 행동 {applied}"
                : $"피해량 자동 지정 · 예상 HP 피해 {totalDamage:0.0} · " +
                  $"합 {matched}/{enemyThreats.Count} · 행동 {applied}";

        Debug.Log(
            "[PlayerAutoPlan][APPLIED] " +
            $"Mode={mode}, " +
            $"Slots={applied}, " +
            $"Matched={matched}/{enemyThreats.Count}, " +
            $"Utility={state.PlannedUtilitySlots}, " +
            $"AverageWinRate={averageWinRate:P1}, " +
            $"ExpectedDamage={totalDamage:0.00}");

        LogPlan(
            state.Slots,
            enemySlots,
            speedManager);

        return result;
    }

    private static bool TryResolveRuntime(
        BattleManager battleManager,
        out BattleContext context,
        out Character player,
        out ActionManager actionManager,
        out SpeedManager speedManager,
        out string failure)
    {
        context = null;
        player = null;
        actionManager = null;
        speedManager = null;
        failure = null;

        if (battleManager == null)
        {
            failure =
                "BattleManager를 찾지 못했습니다.";
            return false;
        }

        if (!battleManager.IsInitialized ||
            battleManager.IsEndingOrEnded)
        {
            failure =
                "전투가 아직 준비되지 않았거나 이미 종료 중입니다.";
            return false;
        }

        if (battleManager.TurnManager == null ||
            !battleManager.TurnManager.IsBattleRunning)
        {
            failure =
                "진행 중인 전투 턴이 없습니다.";
            return false;
        }

        if (battleManager.TurnManager.IsResolving)
        {
            failure =
                "합 연출 또는 턴 해석 중에는 자동 지정할 수 없습니다.";
            return false;
        }

        context =
            battleManager.BattleContext;

        player =
            context?.Player;

        actionManager =
            battleManager.ActionManager;

        speedManager =
            battleManager.SpeedManager;

        if (context == null ||
            player == null ||
            player.IsDead ||
            actionManager == null ||
            actionManager.IsDisposed ||
            speedManager == null)
        {
            failure =
                "전투 컨텍스트 또는 플레이어 행동 시스템이 준비되지 않았습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// AutoPlan의 최소 타깃 계약.
    /// Enemy ActionSlot이 없어도 살아 있는 적에게 StandardAttack 타깃 포인트가 하나라도 있으면
    /// 플레이어의 일방 공격 계획을 만들 수 있다.
    /// SingleHpTargetModel은 TargetPart == null인 Character target을 반환한다.
    /// </summary>
    public static bool HasAnyAutoPlanTarget(
        BattleContext context)
    {
        if (context?.Enemies == null)
            return false;

        TargetSelectionRule rule =
            TargetSelectionRule.StandardAttack;

        foreach (Character enemy in context.Enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            IReadOnlyList<TargetPoint> points =
                BattleTargetValidator.GetTargetPoints(
                    enemy,
                    rule);

            if (points == null)
                continue;

            foreach (TargetPoint point in points)
            {
                if (!point.IsValid ||
                    point.Character != enemy)
                {
                    continue;
                }

                if (BattleTargetValidator.IsValid(
                        enemy,
                        point.Part,
                        rule))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string BuildTargetDiagnostic(
        BattleContext context,
        PlayerAutoPlanMode mode,
        bool hasAnyTarget)
    {
        StringBuilder sb =
            new StringBuilder(1024);

        sb.AppendLine(
            $"[AUTO_PLAN_DIAG][TARGETS][{mode}] HasAnyAutoPlanTarget={hasAnyTarget} enemies={context?.Enemies?.Count ?? -1}");

        if (context?.Enemies == null)
            return sb.ToString();

        TargetSelectionRule rule =
            TargetSelectionRule.StandardAttack;

        for (int i = 0; i < context.Enemies.Count; i++)
        {
            Character enemy = context.Enemies[i];

            if (enemy == null)
            {
                sb.AppendLine(
                    $"Enemy[{i}]=NULL");
                continue;
            }

            IReadOnlyList<TargetPoint> points =
                BattleTargetValidator.GetTargetPoints(
                    enemy,
                    rule);

            sb.AppendLine(
                $"Enemy[{i}] name={enemy.name} dead={enemy.IsDead} usesBodyParts={enemy.UsesBodyParts} points={points?.Count ?? -1}");

            if (points == null)
                continue;

            for (int p = 0; p < points.Count; p++)
            {
                TargetPoint point = points[p];
                bool ruleValid =
                    point.Character != null &&
                    BattleTargetValidator.IsValid(
                        point.Character,
                        point.Part,
                        rule);

                sb.AppendLine(
                    $"  Target[{p}] isValid={point.IsValid} char={(point.Character == null ? "NULL" : point.Character.name)} part={(point.Part == null ? "NULL" : point.Part.Type.ToString())} ruleValid={ruleValid}");
            }
        }

        return sb.ToString();
    }

    private static string BuildSlotDiagnostic(
        string header,
        IReadOnlyList<ActionSlot> slots)
    {
        StringBuilder sb =
            new StringBuilder(1024);

        sb.AppendLine(
            $"{header} count={slots?.Count ?? -1}");

        if (slots == null)
            return sb.ToString();

        for (int i = 0; i < slots.Count; i++)
        {
            ActionSlot slot = slots[i];

            if (slot == null)
            {
                sb.AppendLine(
                    $"Slot[{i}]=NULL");
                continue;
            }

            sb.AppendLine(
                $"Slot[{i}] owner={(slot.Owner == null ? "NULL" : slot.Owner.name)} " +
                $"skill={(slot.Skill == null ? "NULL" : slot.Skill.SkillName)} " +
                $"phase={slot.Phase} " +
                $"targetChar={(slot.TargetCharacter == null ? "NULL" : slot.TargetCharacter.name)} " +
                $"targetPart={(slot.TargetPart == null ? "NULL" : slot.TargetPart.Type.ToString())} " +
                $"targetSlotNull={slot.TargetSlot == null}");
        }

        return sb.ToString();
    }

    private static List<ActionSlot> CollectEnemySlots(
        BattleContext context,
        ActionManager actionManager)
    {
        List<ActionSlot> result =
            new List<ActionSlot>();

        if (context?.Enemies == null ||
            actionManager?.Slots == null)
        {
            return result;
        }

        HashSet<Character> enemies =
            new HashSet<Character>();

        foreach (Character enemy in context.Enemies)
        {
            if (enemy != null &&
                !enemy.IsDead)
            {
                enemies.Add(enemy);
            }
        }

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner == null ||
                slot.Skill == null ||
                !enemies.Contains(slot.Owner) ||
                slot.Owner.IsDead)
            {
                continue;
            }

            result.Add(slot);
        }

        result.Sort(
            new ActionPhaseSorter()
                .CompareForExecution);

        return result;
    }

    private static List<ActionSlot> CollectClashThreats(
        IReadOnlyList<ActionSlot> enemySlots)
    {
        List<ActionSlot> result =
            new List<ActionSlot>();

        if (enemySlots == null)
            return result;

        ClashMatchPolicy policy =
            new ClashMatchPolicy(
                new ActionPhaseSorter());

        foreach (ActionSlot slot in enemySlots)
        {
            if (policy.CanEnterClash(slot))
                result.Add(slot);
        }

        return result;
    }

    private static List<SourceSlot> BuildSources(
        Character player,
        SpeedManager speedManager)
    {
        List<SourceSlot> result =
            new List<SourceSlot>();

        if (player == null)
            return result;

        if (player.UsesBodyParts &&
            player.BodyParts != null)
        {
            foreach (BodyPart part in player.BodyParts)
            {
                if (part == null ||
                    part.IsBroken)
                {
                    continue;
                }

                AddSourcesForPart(
                    result,
                    player,
                    part,
                    speedManager);
            }
        }
        else
        {
            AddSourcesForPart(
                result,
                player,
                null,
                speedManager);
        }

        result.Sort(
            (left, right) =>
            {
                int speed =
                    right.Speed.CompareTo(
                        left.Speed);

                if (speed != 0)
                    return speed;

                int part =
                    string.CompareOrdinal(
                        left.Part?.Type.ToString() ?? "",
                        right.Part?.Type.ToString() ?? "");

                if (part != 0)
                    return part;

                return left.ActionIndex.CompareTo(
                    right.ActionIndex);
            });

        return result;
    }

    private static void AddSourcesForPart(
        List<SourceSlot> destination,
        Character owner,
        BodyPart part,
        SpeedManager speedManager)
    {
        if (destination == null ||
            owner == null)
        {
            return;
        }

        int maxSlots =
            Mathf.Max(
                0,
                owner.GetMaxActionSlotsForPart(part));

        for (int actionIndex = 0;
             actionIndex < maxSlots;
             actionIndex++)
        {
            IReadOnlyList<Skill> skills =
                owner.GetSelectableSkills(
                    part,
                    actionIndex);

            if (skills == null ||
                skills.Count == 0)
            {
                continue;
            }

            destination.Add(
                new SourceSlot
                {
                    Owner = owner,
                    Part = part,
                    ActionIndex = actionIndex,
                    Speed =
                        speedManager?.GetSpeed(
                            owner,
                            part) ?? 0,
                    Skills = skills
                });
        }
    }

    /// <summary>
    /// 캐릭터별 Advisor 점수 없이 현재 턴에서 관측 가능한 실제 수치만으로
    /// 전체 슬롯 조합을 탐색한다.
    ///
    /// WinRate primary : 적 clash threat 각각에 배정된 승률의 합.
    ///                    미배정 threat는 0으로 취급되어 coverage도 자연스럽게 반영된다.
    /// Damage primary  : 선택한 모든 COMBAT 행동의 기대 HP 피해 합.
    ///
    /// 동률일 때만 반대 지표를 secondary로 사용하고, 그 다음 낮은 빛 비용을 선호한다.
    /// </summary>
    private PlanningState BuildPureObjectivePlan(
        BattleContext context,
        Character player,
        ActionManager actionManager,
        IReadOnlyList<SourceSlot> sources,
        IReadOnlyList<ActionSlot> enemyThreats,
        PlayerAutoPlanMode mode)
    {
        PlanningState root =
            new PlanningState
            {
                Player = player
            };

        if (context == null ||
            player == null ||
            actionManager == null ||
            sources == null ||
            sources.Count == 0)
        {
            return root;
        }

        ActionPlanValidator validator =
            new ActionPlanValidator(actionManager);

        PurePlanSearchResult best =
            new PurePlanSearchResult();

        SearchPureObjectivePlan(
            context,
            player,
            validator,
            sources,
            enemyThreats ?? Array.Empty<ActionSlot>(),
            mode,
            sourceIndex: 0,
            state: root,
            assignedThreats: new HashSet<ActionSlot>(),
            winRateSum: 0f,
            expectedDamage: 0f,
            best: best);

        float normalizedWinRate =
            enemyThreats != null && enemyThreats.Count > 0
                ? best.WinRateSum / enemyThreats.Count
                : 0f;

        Debug.Log(
            $"[PlayerAutoPlan][PURE_SEARCH] Mode={mode}, " +
            $"Threats={enemyThreats?.Count ?? 0}, " +
            $"CoverageAdjustedWinRate={normalizedWinRate:P1}, " +
            $"ExpectedHPDamage={best.ExpectedDamage:0.00}, " +
            $"Energy={best.EnergyCost}, " +
            $"Slots={best.State?.Slots.Count ?? 0}");

        return best.State ?? root;
    }

    private void SearchPureObjectivePlan(
        BattleContext context,
        Character player,
        ActionPlanValidator validator,
        IReadOnlyList<SourceSlot> sources,
        IReadOnlyList<ActionSlot> enemyThreats,
        PlayerAutoPlanMode mode,
        int sourceIndex,
        PlanningState state,
        HashSet<ActionSlot> assignedThreats,
        float winRateSum,
        float expectedDamage,
        PurePlanSearchResult best)
    {
        ConsiderPurePlan(
            mode,
            state,
            winRateSum,
            expectedDamage,
            best);

        if (sourceIndex >= sources.Count)
            return;

        SourceSlot source =
            sources[sourceIndex];

        // 이 슬롯을 비워 두는 조합도 반드시 비교한다.
        SearchPureObjectivePlan(
            context,
            player,
            validator,
            sources,
            enemyThreats,
            mode,
            sourceIndex + 1,
            state,
            assignedThreats,
            winRateSum,
            expectedDamage,
            best);

        if (source == null ||
            state.IsSourceUsed(source))
        {
            return;
        }

        List<Candidate> candidates =
            BuildPureCandidatesForSource(
                context,
                state,
                source,
                enemyThreats,
                assignedThreats,
                mode);

        foreach (Candidate candidate in candidates)
        {
            if (candidate == null ||
                candidate.Skill == null)
            {
                continue;
            }

            ActionSlot planned =
                CreatePlannedSlot(
                    candidate,
                    state.Slots);

            if (planned == null)
                continue;

            PlanningState nextState =
                state.Clone();

            nextState.Register(
                source,
                planned);

            // 최종 Apply와 동일한 validator를 탐색 중에도 사용해
            // "계산은 됐지만 마지막에 전체 rollback"되는 조합을 후보에서 제거한다.
            ActionPlanValidationResult validation =
                validator.ValidateReplacementPlan(
                    player,
                    nextState.Slots);

            if (!validation.Success)
            {
                Debug.Log(
                    $"[AUTO_PLAN_DIAG][PURE_BRANCH_BLOCK] " +
                    $"mode={mode} skill={candidate.Skill.SkillName} " +
                    $"code={validation.Code} reason={validation.Reason}");
                continue;
            }

            HashSet<ActionSlot> nextAssigned =
                new HashSet<ActionSlot>(assignedThreats);

            float nextWinRateSum =
                winRateSum;

            if (candidate.Threat != null)
            {
                if (!nextAssigned.Add(candidate.Threat))
                    continue;

                nextWinRateSum +=
                    Mathf.Clamp01(candidate.WinRate);
            }

            float nextDamage =
                expectedDamage +
                Mathf.Max(0f, candidate.ExpectedDamage);

            SearchPureObjectivePlan(
                context,
                player,
                validator,
                sources,
                enemyThreats,
                mode,
                sourceIndex + 1,
                nextState,
                nextAssigned,
                nextWinRateSum,
                nextDamage,
                best);
        }
    }

    private static void ConsiderPurePlan(
        PlayerAutoPlanMode mode,
        PlanningState state,
        float winRateSum,
        float expectedDamage,
        PurePlanSearchResult best)
    {
        if (state == null ||
            state.Slots.Count == 0 ||
            best == null)
        {
            return;
        }

        int energy =
            Mathf.Max(0, state.PlannedEnergy);

        if (!best.HasValue ||
            IsBetterPurePlan(
                mode,
                winRateSum,
                expectedDamage,
                energy,
                state.Slots.Count,
                best))
        {
            best.HasValue = true;
            best.State = state.Clone();
            best.WinRateSum = winRateSum;
            best.ExpectedDamage = expectedDamage;
            best.EnergyCost = energy;
        }
    }

    private static bool IsBetterPurePlan(
        PlayerAutoPlanMode mode,
        float winRateSum,
        float expectedDamage,
        int energyCost,
        int slotCount,
        PurePlanSearchResult best)
    {
        const float epsilon = 0.0001f;

        float primary =
            mode == PlayerAutoPlanMode.WinRate
                ? winRateSum
                : expectedDamage;

        float bestPrimary =
            mode == PlayerAutoPlanMode.WinRate
                ? best.WinRateSum
                : best.ExpectedDamage;

        if (primary > bestPrimary + epsilon)
            return true;
        if (primary < bestPrimary - epsilon)
            return false;

        // primary가 완전히 같은 경우에만 반대 지표를 tie-break로 쓴다.
        float secondary =
            mode == PlayerAutoPlanMode.WinRate
                ? expectedDamage
                : winRateSum;

        float bestSecondary =
            mode == PlayerAutoPlanMode.WinRate
                ? best.ExpectedDamage
                : best.WinRateSum;

        if (secondary > bestSecondary + epsilon)
            return true;
        if (secondary < bestSecondary - epsilon)
            return false;

        if (energyCost != best.EnergyCost)
            return energyCost < best.EnergyCost;

        int bestSlotCount =
            best.State?.Slots.Count ?? int.MaxValue;

        // 수치가 완전히 같으면 불필요한 행동을 덜 쓰는 계획을 선택한다.
        return slotCount < bestSlotCount;
    }

    private List<Candidate> BuildPureCandidatesForSource(
        BattleContext context,
        PlanningState state,
        SourceSlot source,
        IReadOnlyList<ActionSlot> enemyThreats,
        HashSet<ActionSlot> assignedThreats,
        PlayerAutoPlanMode mode)
    {
        List<Candidate> raw =
            new List<Candidate>();

        if (context?.Enemies == null ||
            state == null ||
            source?.Skills == null)
        {
            return raw;
        }

        foreach (Skill skill in source.Skills)
        {
            // [PURE OBJECTIVE CONTRACT]
            // 도사림/위세 같은 비-COMBAT 행동은 현재 Estimator가 부작용 없이
            // "사용 후 전체 턴 확률/피해"를 재계산할 Preview 계약이 없다.
            // 임의 고정 점수로 선택하면 다시 휴리스틱 AI가 되므로 자동 선택하지 않는다.
            // 이미 수동으로 적용된 턴 보정은 Estimator가 현재 runtime state에서 읽는다.
            if (skill?.DefaultPhase != ActionPhase.COMBAT)
                continue;

            if (!CanPlanSkill(
                    context,
                    state,
                    source,
                    skill,
                    requireClash: false))
            {
                continue;
            }

            // 1) 실제 적 ActionSlot과 합 가능한 후보.
            if (enemyThreats != null)
            {
                foreach (ActionSlot threat in enemyThreats)
                {
                    if (threat?.Skill == null ||
                        threat.Owner == null ||
                        assignedThreats?.Contains(threat) == true)
                    {
                        continue;
                    }

                    List<TargetPoint> targetPoints =
                        GetValidTargetPoints(
                            threat.Owner,
                            skill);

                    foreach (TargetPoint point in targetPoints)
                    {
                        if (!CanChallengeTargetSlot(
                                source,
                                skill,
                                threat.Owner,
                                point.Part,
                                threat))
                        {
                            continue;
                        }

                        Candidate candidate =
                            BuildThreatCandidate(
                                context,
                                source,
                                skill,
                                threat,
                                point.Part);

                        if (candidate != null)
                            raw.Add(candidate);
                    }
                }
            }

            // 2) TargetSlot을 갖지 않는 순수 일방 공격 후보.
            // 적 행동과 합하지 않는 대신 ExpectedDamage만 목적함수에 기여한다.
            foreach (Character enemy in context.Enemies)
            {
                if (enemy == null || enemy.IsDead)
                    continue;

                List<TargetPoint> targetPoints =
                    GetValidTargetPoints(
                        enemy,
                        skill);

                foreach (TargetPoint point in targetPoints)
                {
                    Candidate candidate =
                        BuildFillCandidate(
                            context,
                            source,
                            skill,
                            point.Character,
                            point.Part);

                    if (candidate != null)
                        raw.Add(candidate);
                }
            }
        }

        return ReducePureCandidates(
            raw,
            mode,
            enemyThreats);
    }

    /// <summary>
    /// 탐색 폭을 줄이되 목적함수 결과는 보존한다.
    /// 같은 Source + 같은 Threat route + 같은 빛 비용에서는
    /// pure objective가 더 좋은 후보 하나만 남긴다.
    /// Fill(route 없음)도 비용별 최적 후보 하나만 남긴다.
    /// </summary>
    private static List<Candidate> ReducePureCandidates(
        IReadOnlyList<Candidate> candidates,
        PlayerAutoPlanMode mode,
        IReadOnlyList<ActionSlot> enemyThreats)
    {
        Dictionary<string, Candidate> bestByRouteAndEnergy =
            new Dictionary<string, Candidate>();

        if (candidates == null)
            return new List<Candidate>();

        foreach (Candidate candidate in candidates)
        {
            if (candidate?.Skill == null)
                continue;

            int threatIndex = -1;
            if (candidate.Threat != null && enemyThreats != null)
            {
                for (int i = 0; i < enemyThreats.Count; i++)
                {
                    if (ReferenceEquals(enemyThreats[i], candidate.Threat))
                    {
                        threatIndex = i;
                        break;
                    }
                }
            }

            int energy =
                Mathf.Max(0, candidate.Skill.EnergyCost);

            string key =
                threatIndex >= 0
                    ? $"T:{threatIndex}:E:{energy}"
                    : $"F:E:{energy}";

            if (!bestByRouteAndEnergy.TryGetValue(
                    key,
                    out Candidate current) ||
                IsBetterPureCandidate(
                    candidate,
                    current,
                    mode))
            {
                bestByRouteAndEnergy[key] =
                    candidate;
            }
        }

        List<Candidate> result =
            new List<Candidate>(
                bestByRouteAndEnergy.Values);

        result.Sort(
            (left, right) =>
            {
                if (IsBetterPureCandidate(left, right, mode))
                    return -1;
                if (IsBetterPureCandidate(right, left, mode))
                    return 1;
                return 0;
            });

        return result;
    }

    private static bool IsBetterPureCandidate(
        Candidate candidate,
        Candidate current,
        PlayerAutoPlanMode mode)
    {
        if (candidate == null)
            return false;
        if (current == null)
            return true;

        const float epsilon = 0.0001f;

        float candidatePrimary =
            mode == PlayerAutoPlanMode.WinRate
                ? (candidate.Threat != null ? candidate.WinRate : 0f)
                : candidate.ExpectedDamage;

        float currentPrimary =
            mode == PlayerAutoPlanMode.WinRate
                ? (current.Threat != null ? current.WinRate : 0f)
                : current.ExpectedDamage;

        if (candidatePrimary > currentPrimary + epsilon)
            return true;
        if (candidatePrimary < currentPrimary - epsilon)
            return false;

        float candidateSecondary =
            mode == PlayerAutoPlanMode.WinRate
                ? candidate.ExpectedDamage
                : (candidate.Threat != null ? candidate.WinRate : 0f);

        float currentSecondary =
            mode == PlayerAutoPlanMode.WinRate
                ? current.ExpectedDamage
                : (current.Threat != null ? current.WinRate : 0f);

        if (candidateSecondary > currentSecondary + epsilon)
            return true;
        if (candidateSecondary < currentSecondary - epsilon)
            return false;

        int candidateEnergy =
            Mathf.Max(0, candidate.Skill?.EnergyCost ?? 0);
        int currentEnergy =
            Mathf.Max(0, current.Skill?.EnergyCost ?? 0);

        if (candidateEnergy != currentEnergy)
            return candidateEnergy < currentEnergy;

        // 완전 동률은 stable한 이름 비교로 결정해 실행마다 결과가 흔들리지 않게 한다.
        int skillName =
            string.CompareOrdinal(
                candidate.Skill?.SkillName ?? string.Empty,
                current.Skill?.SkillName ?? string.Empty);

        if (skillName != 0)
            return skillName < 0;

        string candidatePart =
            candidate.TargetPart?.Type.ToString() ?? string.Empty;
        string currentPart =
            current.TargetPart?.Type.ToString() ?? string.Empty;

        return string.CompareOrdinal(
            candidatePart,
            currentPart) < 0;
    }

    private static bool CanPlanSkill(
        BattleContext context,
        PlanningState state,
        SourceSlot source,
        Skill skill,
        bool requireClash)
    {
        if (context == null ||
            state == null ||
            source == null ||
            skill == null ||
            source.Owner == null ||
            source.Owner.IsDead ||
            source.Part?.IsBroken == true)
        {
            return false;
        }

        if (!state.CanAdd(
                source,
                skill))
        {
            return false;
        }

        // [0918_NORMAL_AUTOPLAN_FIX:MECHANIC_SELECTION_GATE]
        // AutoPlan 후보 생성도 수동 Planning/ActionPlanValidator와 동일한
        // 캐릭터 고유 선택 제약을 반드시 통과해야 한다.
        // 예: 유진의 숨 고르기(표식 10), 장부 정리(표식 20)처럼
        // CanUseSkill/CanAIUse만으로는 막히지 않는 카드가 최종 검증에서
        // MechanicBlocked가 되어 계획 전체를 원자적으로 실패시키는 문제를 방지한다.
        ActionPlanningSkillContext planningContext =
            new ActionPlanningSkillContext(
                source.Owner,
                source.Part,
                skill,
                source.ActionIndex,
                state.Slots);

        string mechanicBlockReason =
            ActionPlanningMechanicPolicy
                .GetSkillSelectionBlockReason(
                    planningContext);

        if (!string.IsNullOrWhiteSpace(mechanicBlockReason))
        {
            Debug.Log(
                $"[AUTO_PLAN_DIAG][CANDIDATE_BLOCK] " +
                $"owner={source.Owner.Data?.CharacterName ?? source.Owner.name} " +
                $"skill={skill.SkillName} " +
                $"part={source.Part?.Type.ToString() ?? "CHAR"} " +
                $"actionIndex={source.ActionIndex} " +
                $"reason={mechanicBlockReason}");
            return false;
        }

        if (!source.Owner.CanUseSkill(
                source.Part,
                skill))
        {
            return false;
        }

        if (!skill.CanAIUse(
                source.Owner,
                source.Part,
                context))
        {
            return false;
        }

        if (requireClash &&
            (skill.DefaultPhase != ActionPhase.COMBAT ||
             !skill.CanClash))
        {
            return false;
        }

        return true;
    }

    private Candidate BuildThreatCandidate(
        BattleContext context,
        SourceSlot source,
        Skill skill,
        ActionSlot threat,
        BodyPart attackTargetPart)
    {
        if (source == null ||
            skill == null ||
            threat?.Skill == null)
        {
            return null;
        }

        PlayerAutoPlanEstimator.ClashEstimate estimate =
            estimator.EstimateClash(
                context,
                source.Owner,
                source.Part,
                source.Speed,
                skill,
                threat.Owner,
                threat.Part,
                threat.Speed,
                threat.Skill,
                attackTargetPart,
                threat.TargetPart);

        return new Candidate
        {
            Source = source,
            Skill = skill,
            Target = threat.Owner,
            TargetPart = attackTargetPart,
            Threat = threat,
            WinRate = Mathf.Clamp01(estimate.WinRate),
            ExpectedDamage = Mathf.Max(0f, estimate.ExpectedDamage)
        };
    }

    private Candidate BuildFillCandidate(
        BattleContext context,
        SourceSlot source,
        Skill skill,
        Character target,
        BodyPart targetPart)
    {
        if (source == null ||
            skill == null ||
            target == null)
        {
            return null;
        }

        // Pure Fill 후보는 TargetSlot을 의도적으로 갖지 않는다.
        // 따라서 합 확률이 아니라 실제 일방공격 기대 HP 피해만 계산한다.
        float expectedDamage =
            estimator.EstimateOneSidedDamage(
                context,
                source.Owner,
                source.Part,
                source.Speed,
                skill,
                target,
                targetPart);

        return new Candidate
        {
            Source = source,
            Skill = skill,
            Target = target,
            TargetPart = targetPart,
            Threat = null,
            WinRate = 0f,
            ExpectedDamage = Mathf.Max(0f, expectedDamage)
        };
    }

    private static ActionSlot CreatePlannedSlot(
        Candidate candidate,
        IReadOnlyList<ActionSlot> plannedSlots)
    {
        if (candidate?.Source == null)
            return null;

        ActionSlot slot =
            new ActionSlot
            {
                Owner =
                    candidate.Source.Owner,
                Part =
                    candidate.Source.Part,
                Skill =
                    candidate.Skill,
                TargetCharacter =
                    candidate.Target,
                TargetPart =
                    candidate.TargetPart,
                Speed =
                    candidate.Source.Speed,
                Phase =
                    candidate.Skill?.DefaultPhase ??
                    ActionPhase.COMBAT,
                ActionIndex =
                    candidate.Source.ActionIndex,

                // 자동 계획도 수동 Focused Encounter 입력과 동일하게
                // 선택한 정확한 적 ActionSlot을 TargetSlot로 기록한다.
                TargetSlot =
                    candidate.Skill?.DefaultPhase == ActionPhase.COMBAT
                        ? candidate.Threat
                        : null
            };

        ActionPlanningSkillContext planningContext =
            new ActionPlanningSkillContext(
                slot.Owner,
                slot.Part,
                slot.Skill,
                slot.ActionIndex,
                plannedSlots);

        // 캐릭터별 계획 legality/configuration 경계는 재사용하되,
        // AutoPlan이 캐릭터 상태/토글을 임의로 변경하지는 않는다.
        // 현재 사용자가 선택한 감 재굴림 등의 planning 설정은 그대로 존중한다.
        ActionPlanningMechanicPolicy.ConfigurePlannedSlot(
            planningContext,
            slot);

        return slot;
    }

    private static bool CanTarget(
        Character target,
        BodyPart targetPart,
        Skill skill)
    {
        if (target == null ||
            target.IsDead ||
            skill == null)
        {
            return false;
        }

        bool allowBroken =
            AITargetSelector
                .ShouldIncludeBrokenTargets(
                    skill);

        // [0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_VALIDATION]
        // Character.TargetModel 직접 접근 대신 공용 validator를 사용한다.
        // validator는 Single HP Character target(TargetPart == null)을 정식으로 허용하고,
        // 초기화 경계에서 TargetModel이 비어 있어도 안전한 fallback 모델로 판정한다.
        return BattleTargetValidator.IsValid(
            target,
            targetPart,
            new TargetSelectionRule(
                allowBrokenParts: allowBroken));
    }

    private static List<TargetPoint> GetValidTargetPoints(
        Character target,
        Skill skill)
    {
        List<TargetPoint> result =
            new List<TargetPoint>();

        if (target == null ||
            target.IsDead ||
            skill == null)
        {
            return result;
        }

        bool includeBroken =
            AITargetSelector
                .ShouldIncludeBrokenTargets(
                    skill);

        TargetSelectionRule rule =
            new TargetSelectionRule(
                allowBrokenParts: includeBroken);

        // [0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_ENUMERATION]
        // 직접 Character.GetTargetPoints()를 호출하지 않는다.
        // 공용 validator 경로를 사용해 Single HP 일반몹을 Character target으로 항상 노출한다.
        IReadOnlyList<TargetPoint> points =
            BattleTargetValidator.GetTargetPoints(
                target,
                rule);

        if (points == null)
            return result;

        foreach (TargetPoint point in points)
        {
            if (!point.IsValid ||
                point.Character != target ||
                !CanTarget(
                    point.Character,
                    point.Part,
                    skill))
            {
                continue;
            }

            result.Add(point);
        }

        return result;
    }

    private static bool CanChallengeTargetSlot(
        SourceSlot source,
        Skill skill,
        Character target,
        BodyPart targetPart,
        ActionSlot targetSlot)
    {
        if (source == null ||
            source.Owner == null ||
            skill == null ||
            target == null ||
            targetSlot == null)
        {
            return false;
        }

        ActionSlot probe =
            new ActionSlot
            {
                ActionId = -1,
                Owner = source.Owner,
                Part = source.Part,
                Skill = skill,
                TargetCharacter = target,
                TargetPart = targetPart,
                TargetSlot = targetSlot,
                Speed = source.Speed,
                ActionIndex = source.ActionIndex,
                Phase = ActionPhase.COMBAT
            };

        ClashMatchPolicy policy =
            new ClashMatchPolicy(
                new ActionPhaseSorter());

        return policy.CanChallenge(
            probe,
            targetSlot);
    }

    /// <summary>
    /// 전술 UI용 간이 합 승률 프리뷰.
    /// 자동계획이 실제로 사용하는 동일한 굴림 분포/속도/턴 보정 계산을 재사용하며
    /// 전투 상태와 RNG를 변경하지 않는다.
    /// </summary>
    public bool TryEstimateClashWinRate(
        BattleContext context,
        Character playerOwner,
        BodyPart playerPart,
        int playerSpeed,
        Skill playerSkill,
        ActionSlot enemySlot,
        out float winRate)
    {
        return estimator.TryEstimateClashWinRate(
            context,
            playerOwner,
            playerPart,
            playerSpeed,
            playerSkill,
            enemySlot,
            out winRate);
    }



































    private static int CountActualPreviewClashes(
        IReadOnlyList<ActionSlot> allSlots,
        Character player)
    {
        if (allSlots == null ||
            player == null)
        {
            return 0;
        }

        ClashBuilder builder =
            new ClashBuilder();

        IReadOnlyList<ClashPair> pairs =
            builder.BuildClashPreview(
                allSlots);

        int count = 0;

        foreach (ClashPair pair in pairs)
        {
            if (pair == null ||
                !pair.IsClash)
            {
                continue;
            }

            if (pair.First?.Owner == player ||
                pair.Second?.Owner == player)
            {
                count++;
            }
        }

        return count;
    }

    private void CalculatePlanSummary(
        BattleContext context,
        IReadOnlyList<ActionSlot> playerSlots,
        IReadOnlyList<ActionSlot> enemyThreats,
        SpeedManager speedManager,
        out float averageWinRate,
        out float expectedDamage)
    {
        averageWinRate = 0f;
        expectedDamage = 0f;

        if (playerSlots == null ||
            playerSlots.Count == 0)
        {
            return;
        }

        foreach (ActionSlot playerSlot
                 in playerSlots)
        {
            if (playerSlot?.Skill == null)
                continue;

            if (playerSlot.Phase != ActionPhase.COMBAT)
                continue;

            ActionSlot threat =
                playerSlot.TargetSlot;

            ClashMatchPolicy policy =
                new ClashMatchPolicy(
                    new ActionPhaseSorter());

            bool willClash =
                threat?.Skill != null &&
                playerSlot.Skill.CanClash &&
                threat.Skill.CanClash &&
                policy.CanChallenge(
                    playerSlot,
                    threat);

            if (willClash)
            {
                PlayerAutoPlanEstimator.ClashEstimate estimate =
                    estimator.EstimateClash(
                        context,
                        playerSlot.Owner,
                        playerSlot.Part,
                        playerSlot.Speed,
                        playerSlot.Skill,
                        threat.Owner,
                        threat.Part,
                        threat.Speed,
                        threat.Skill,
                        playerSlot.TargetPart,
                        threat.TargetPart);

                averageWinRate +=
                    estimate.WinRate;

                expectedDamage +=
                    estimate.ExpectedDamage;

            }
            else
            {
                expectedDamage +=
                    estimator.EstimateOneSidedDamage(
                        context,
                        playerSlot.Owner,
                        playerSlot.Part,
                        playerSlot.Speed,
                        playerSlot.Skill,
                        playerSlot.TargetCharacter,
                        playerSlot.TargetPart);
            }
        }

        // WinRate 목적함수와 동일하게 "전체 적 clash threat"를 분모로 사용한다.
        // 미배정 threat는 승률 0으로 취급되어 coverage가 결과에 직접 반영된다.
        int totalThreatCount =
            enemyThreats?.Count ?? 0;

        if (totalThreatCount > 0)
        {
            averageWinRate /=
                totalThreatCount;
        }
        else
        {
            averageWinRate = 0f;
        }
    }





    private static void LogPlan(
        IReadOnlyList<ActionSlot> playerSlots,
        IReadOnlyList<ActionSlot> enemySlots,
        SpeedManager speedManager)
    {
        if (playerSlots == null)
            return;

        foreach (ActionSlot slot in playerSlots)
        {
            if (slot == null)
                continue;

            Debug.Log(
                "[PlayerAutoPlan][SLOT] " +
                $"Owner={GetCharacterName(slot.Owner)}, " +
                $"Part={slot.Part?.Type.ToString() ?? "CHARACTER"}, " +
                $"Index={slot.ActionIndex}, " +
                $"Skill={slot.Skill?.SkillName ?? "NULL"}, " +
                $"Target={GetCharacterName(slot.TargetCharacter)}/" +
                $"{slot.TargetPart?.Type.ToString() ?? "CHARACTER"}, " +
                $"Speed={slot.Speed}");
        }
    }

    private static string GetCharacterName(
        Character character)
    {
        return character?.Data?.CharacterName ??
               character?.name ??
               "NULL";
    }










}