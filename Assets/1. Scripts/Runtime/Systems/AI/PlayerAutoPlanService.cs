using System;
using System.Collections.Generic;
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
/// - 적의 합 가능 행동을 최대한 덮는다.
/// - 각 굴림 분포와 현재 속도/도사림 보정을 사용해
///   다수결 합 승률을 근사 계산한다.
///
/// Damage:
/// - 적 행동을 가능한 만큼 합으로 받아내면서
///   예상 공격 피해와 남은 일방 공격 피해를 우선한다.
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
        public bool Used;

        public string Key =>
            $"{Part?.Type.ToString() ?? "CHAR"}:{ActionIndex}";

        public bool HasCombatSkill =>
            ContainsPhase(ActionPhase.COMBAT);

        public bool HasPreparationSkill
        {
            get
            {
                if (Skills == null)
                    return false;

                foreach (Skill skill in Skills)
                {
                    if (skill?.ActionType == ActionType.Preparation)
                        return true;
                }

                return false;
            }
        }

        private bool ContainsPhase(ActionPhase phase)
        {
            if (Skills == null)
                return false;

            foreach (Skill skill in Skills)
            {
                if (skill != null &&
                    skill.DefaultPhase == phase)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class Candidate
    {
        public SourceSlot Source;
        public Skill Skill;
        public Character Target;
        public BodyPart TargetPart;
        public ActionSlot Threat;
        public float Score;
        public float WinRate;
        public float ExpectedDamage;
        public float ThreatDamage;
    }

    private sealed class PlanningState
    {
        public Character Player;
        public int PlannedEnergy;
        public int PlannedCombatSlots;
        public int PlannedUtilitySlots;
        public int PlannedPrestigeCount;
        public readonly List<ActionSlot> Slots =
            new List<ActionSlot>();

        public bool CanAdd(
            SourceSlot source,
            Skill skill)
        {
            if (Player == null ||
                source == null ||
                source.Used ||
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

            source.Used = true;
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

        public int CountSkill(
            Skill skill)
        {
            if (skill == null)
                return 0;

            int count = 0;

            foreach (ActionSlot slot in Slots)
            {
                if (slot?.Skill == skill ||
                    (!string.IsNullOrEmpty(skill.Definition?.SkillId) &&
                     slot?.Skill?.Definition?.SkillId ==
                     skill.Definition.SkillId))
                {
                    count++;
                }
            }

            return count;
        }
    }







    public PlayerAutoPlanResult BuildAndApply(
        BattleManager battleManager,
        PlayerAutoPlanMode mode)
    {
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
            result.Message = failure;
            return result;
        }

        CharacterAutoPlanAdvisorRegistry.PreparePlan(
            context,
            player,
            mode);

        List<ActionSlot> enemySlots =
            CollectEnemySlots(
                context,
                actionManager);

        if (enemySlots.Count == 0)
        {
            result.Message =
                "현재 턴에 계획된 적 행동이 없습니다.";
            return result;
        }

        List<ActionSlot> enemyThreats =
            CollectClashThreats(enemySlots);

        List<SourceSlot> sources =
            BuildSources(
                player,
                speedManager);

        if (sources.Count == 0)
        {
            result.Message =
                "사용 가능한 아군 행동 슬롯이 없습니다.";
            return result;
        }

        PlanningState state =
            new PlanningState
            {
                Player = player
            };

        HashSet<ActionSlot> assignedThreats =
            new HashSet<ActionSlot>();

        // 0단계: 도사림도 수동 계획과 동일하게 FORESIGHT ActionSlot로 계획한다.
        // START 전에는 효과/자원을 확정하지 않으며 우클릭/Reset 가능한 동일한 계획 모델을 사용한다.
        Candidate utility =
            FindBestMandatoryUtilityCandidate(
                context,
                state,
                sources,
                mode);

        if (utility != null)
        {
            ActionSlot utilitySlot =
                CreatePlannedSlot(utility);

            if (utilitySlot != null)
            {
                state.Register(
                    utility.Source,
                    utilitySlot);
            }
        }

        // 1단계:
        // 적의 합 가능 행동을 실제 행동 원천 부위 기준으로 최대한 덮는다.
        while (true)
        {
            Candidate best =
                FindBestThreatCandidate(
                    context,
                    state,
                    sources,
                    enemyThreats,
                    assignedThreats,
                    speedManager,
                    mode);

            if (best == null)
                break;

            ActionSlot planned =
                CreatePlannedSlot(best);

            state.Register(
                best.Source,
                planned);

            assignedThreats.Add(
                best.Threat);
        }

        // 2단계:
        // 남은 슬롯은 피해 기대값이 높은 공격으로 채운다.
        while (true)
        {
            Candidate best =
                FindBestFillCandidate(
                    context,
                    state,
                    sources,
                    enemySlots,
                    assignedThreats,
                    speedManager,
                    mode);

            if (best == null)
                break;

            ActionSlot planned =
                CreatePlannedSlot(best);

            state.Register(
                best.Source,
                planned);

            // 남은 공격도 가능하면 정확한 적 ActionSlot을 의도로 보존한다.
            // 실제 합이 불가능한 속도/스킬 조합이면 ClashBuilder가 일방 공격으로 해석한다.
            if (planned != null &&
                best.Threat != null)
            {
                assignedThreats.Add(
                    best.Threat);
            }
        }

        if (state.Slots.Count == 0)
        {
            result.Message =
                "현재 자원·부위·스킬 조건에서 자동 지정 가능한 행동이 없습니다.";
            return result;
        }

        int applied =
            applicationService.Apply(
                actionManager,
                player,
                state.Slots);

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
            enemySlots,
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

        string mechanicSummary =
            CharacterAutoPlanAdvisorRegistry.GetPlanSummary(player);

        result.Message =
            mode == PlayerAutoPlanMode.WinRate
                ? $"승률 자동 지정 · 합 {matched}/{enemyThreats.Count}" +
                  $"(전투 슬롯 {player.GetMaxCombatActionSlots()}) · " +
                  $"비전투 {state.PlannedUtilitySlots} · 행동 {applied}"
                : $"피해량 자동 지정 · 예상 피해 {totalDamage:0.0} · " +
                  $"비전투 {state.PlannedUtilitySlots} · 행동 {applied}";

        if (!string.IsNullOrWhiteSpace(mechanicSummary))
            result.Message += $" · {mechanicSummary}";

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

    private Candidate FindBestMandatoryUtilityCandidate(
        BattleContext context,
        PlanningState state,
        IReadOnlyList<SourceSlot> sources,
        PlayerAutoPlanMode mode)
    {
        Candidate best = null;

        if (context == null ||
            state == null ||
            sources == null)
        {
            return null;
        }

        foreach (SourceSlot source in sources)
        {
            if (source == null ||
                source.Used ||
                source.Skills == null ||
                source.HasCombatSkill ||
                !source.HasPreparationSkill)
            {
                continue;
            }

            foreach (Skill skill in source.Skills)
            {
                if (skill?.ActionType != ActionType.Preparation ||
                    !CanPlanSkill(
                        context,
                        state,
                        source,
                        skill,
                        requireClash: false))
                {
                    continue;
                }

                Candidate candidate =
                    BuildUtilityCandidate(
                        context,
                        source,
                        skill,
                        mode);

                if (candidate == null)
                    continue;

                candidate.Score -=
                    state.CountSkill(skill) * 180f;

                if (best == null ||
                    candidate.Score > best.Score)
                {
                    best = candidate;
                }
            }
        }

        return best;
    }

    private Candidate BuildUtilityCandidate(
        BattleContext context,
        SourceSlot source,
        Skill skill,
        PlayerAutoPlanMode mode)
    {
        if (source?.Owner == null ||
            skill == null ||
            skill.ActionType != ActionType.Preparation)
        {
            return null;
        }

        BodyPart targetPart =
            source.Part != null &&
            !source.Part.IsBroken
                ? source.Part
                : null;

        float score =
            mode == PlayerAutoPlanMode.WinRate
                ? 18000f
                : 14000f;

        score += CharacterAutoPlanAdvisorRegistry.ScoreCandidate(
            new AutoPlanCandidateContext(
                context,
                mode,
                source.Owner,
                source.Part,
                skill,
                source.Owner,
                targetPart,
                null,
                0f,
                0f));

        // 같은 효과라면 빛을 적게 쓰고, 실행 속도가 빠른 도사림을 선호한다.
        score -= Mathf.Max(0, skill.EnergyCost) * 120f;
        score += source.Speed * 0.01f;
        score -= source.ActionIndex * 0.001f;

        return new Candidate
        {
            Source = source,
            Skill = skill,
            Target = source.Owner,
            TargetPart = targetPart,
            Score = score,
            WinRate = 0f,
            ExpectedDamage = 0f
        };
    }

    private Candidate FindBestThreatCandidate(
        BattleContext context,
        PlanningState state,
        IReadOnlyList<SourceSlot> sources,
        IReadOnlyList<ActionSlot> threats,
        HashSet<ActionSlot> assignedThreats,
        SpeedManager speedManager,
        PlayerAutoPlanMode mode)
    {
        Candidate best = null;

        if (sources == null ||
            threats == null)
        {
            return null;
        }

        foreach (ActionSlot threat in threats)
        {
            if (threat == null ||
                assignedThreats.Contains(threat))
            {
                continue;
            }

            foreach (SourceSlot source in sources)
            {
                if (source == null ||
                    source.Used ||
                    source.Skills == null)
                {
                    continue;
                }

                foreach (Skill skill in source.Skills)
                {
                    if (!CanPlanSkill(
                            context,
                            state,
                            source,
                            skill,
                            requireClash: true))
                    {
                        continue;
                    }

                    // Focused Encounter의 TargetSlot은 "상대 행동"이고,
                    // 실제 피해 TargetPart는 별도의 개념이다.
                    // Stage 1 Boss의 A/A/B처럼 행동 슬롯 자체가 Part=null이어도
                    // 보스의 유효한 BodyPart를 공격 대상으로 선택할 수 있어야 한다.
                    List<TargetPoint> targetPoints =
                        GetValidTargetPoints(
                            threat.Owner,
                            skill);

                    if (targetPoints.Count == 0)
                        continue;

                    ActionSlot challengeProbe =
                        new ActionSlot
                        {
                            Owner = source.Owner,
                            Part = source.Part,
                            Skill = skill,
                            TargetCharacter = threat.Owner,
                            TargetPart = targetPoints[0].Part,
                            TargetSlot = threat,
                            Speed = source.Speed,
                            ActionIndex = source.ActionIndex,
                            Phase = ActionPhase.COMBAT
                        };

                    ClashMatchPolicy focusedPolicy =
                        new ClashMatchPolicy(
                            new ActionPhaseSorter());

                    if (!focusedPolicy.CanChallenge(
                            challengeProbe,
                            threat))
                    {
                        continue;
                    }

                    foreach (TargetPoint point
                             in targetPoints)
                    {
                        Candidate candidate =
                            BuildThreatCandidate(
                                context,
                                source,
                                skill,
                                threat,
                                point.Part,
                                speedManager,
                                mode);

                        if (candidate == null)
                            continue;

                        candidate.Score -=
                            state.CountSkill(skill) *
                            (mode == PlayerAutoPlanMode.WinRate
                                ? 220f
                                : 120f);

                        if (best == null ||
                            candidate.Score > best.Score)
                        {
                            best = candidate;
                        }
                    }
                }
            }
        }

        return best;
    }

    private Candidate FindBestFillCandidate(
        BattleContext context,
        PlanningState state,
        IReadOnlyList<SourceSlot> sources,
        IReadOnlyList<ActionSlot> enemySlots,
        HashSet<ActionSlot> assignedTargetSlots,
        SpeedManager speedManager,
        PlayerAutoPlanMode mode)
    {
        Candidate best = null;

        if (context?.Enemies == null ||
            sources == null)
        {
            return null;
        }

        foreach (SourceSlot source in sources)
        {
            if (source == null ||
                source.Used ||
                source.Skills == null)
            {
                continue;
            }

            foreach (Skill skill in source.Skills)
            {
                if (!CanPlanSkill(
                        context,
                        state,
                        source,
                        skill,
                        requireClash: false))
                {
                    continue;
                }

                // 비전투 전용 부위의 도사림은 0단계에서 먼저 처리한다.
                // 이 단계는 남은 COMBAT 행동만 공격 대상으로 채운다.
                if (skill.DefaultPhase !=
                    ActionPhase.COMBAT)
                {
                    continue;
                }

                foreach (Character enemy
                         in context.Enemies)
                {
                    if (enemy == null ||
                        enemy.IsDead)
                    {
                        continue;
                    }

                    bool includeBroken =
                        AITargetSelector
                            .ShouldIncludeBrokenTargets(
                                skill);

                    IReadOnlyList<TargetPoint> points =
                        enemy.GetTargetPoints(
                            includeBroken);

                    foreach (TargetPoint point in points)
                    {
                        if (!point.IsValid ||
                            !CanTarget(
                                point.Character,
                                point.Part,
                                skill))
                        {
                            continue;
                        }

                        // Enemy ActionSlot의 Part는 "행동 원천 부위"다.
                        // 공격할 실제 BodyPart와 같을 필요가 없다.
                        // 따라서 같은 적 캐릭터의 아직 미지정 COMBAT 슬롯을
                        // exact TargetSlot 의도로 연결한다.
                        ActionSlot matchingThreat =
                            FindAvailableTargetSlotForCharacter(
                                enemySlots,
                                point.Character,
                                assignedTargetSlots);

                        Candidate candidate =
                            BuildFillCandidate(
                                context,
                                source,
                                skill,
                                point.Character,
                                point.Part,
                                matchingThreat,
                                speedManager,
                                mode);

                        if (candidate == null)
                            continue;

                        candidate.Score -=
                            state.CountSkill(skill) *
                            (mode == PlayerAutoPlanMode.WinRate
                                ? 220f
                                : 120f);

                        if (best == null ||
                            candidate.Score > best.Score)
                        {
                            best = candidate;
                        }
                    }
                }
            }
        }

        return best;
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
        BodyPart attackTargetPart,
        SpeedManager speedManager,
        PlayerAutoPlanMode mode)
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

        float threatDamage =
            estimator.EstimateOneSidedDamage(
                context,
                threat.Owner,
                threat.Part,
                threat.Speed,
                threat.Skill,
                threat.TargetCharacter,
                threat.TargetPart);

        float vulnerability =
            estimator.ScoreTargetVulnerability(
                threat.Owner,
                attackTargetPart,
                skill);

        float score;

        if (mode == PlayerAutoPlanMode.WinRate)
        {
            score =
                50000f +
                estimate.WinRate * 10000f +
                threatDamage * 45f +
                estimate.ExpectedDamage * 8f +
                vulnerability;
        }
        else
        {
            score =
                40000f +
                estimate.ExpectedDamage * 450f +
                estimate.WinRate * 900f +
                threatDamage * 12f +
                vulnerability * 2f;
        }

        // 같은 점수에서는 빠른 슬롯과 낮은 행동 인덱스를 안정적으로 선호한다.
        score += CharacterAutoPlanAdvisorRegistry.ScoreCandidate(
            new AutoPlanCandidateContext(
                context,
                mode,
                source.Owner,
                source.Part,
                skill,
                threat.Owner,
                attackTargetPart,
                threat,
                estimate.WinRate,
                estimate.ExpectedDamage));

        score += source.Speed * 0.01f;
        score -= source.ActionIndex * 0.001f;

        return new Candidate
        {
            Source = source,
            Skill = skill,
            Target = threat.Owner,
            TargetPart = attackTargetPart,
            Threat = threat,
            Score = score,
            WinRate = estimate.WinRate,
            ExpectedDamage =
                estimate.ExpectedDamage,
            ThreatDamage = threatDamage
        };
    }

    private Candidate BuildFillCandidate(
        BattleContext context,
        SourceSlot source,
        Skill skill,
        Character target,
        BodyPart targetPart,
        ActionSlot possibleThreat,
        SpeedManager speedManager,
        PlayerAutoPlanMode mode)
    {
        if (source == null ||
            skill == null ||
            target == null)
        {
            return null;
        }

        float winRate = 0f;
        float expectedDamage;

        bool willClash =
            CanChallengeTargetSlot(
                source,
                skill,
                target,
                targetPart,
                possibleThreat);

        if (willClash)
        {
            PlayerAutoPlanEstimator.ClashEstimate clash =
                estimator.EstimateClash(
                    context,
                    source.Owner,
                    source.Part,
                    source.Speed,
                    skill,
                    possibleThreat.Owner,
                    possibleThreat.Part,
                    possibleThreat.Speed,
                    possibleThreat.Skill,
                    targetPart,
                    possibleThreat.TargetPart);

            winRate =
                clash.WinRate;

            expectedDamage =
                clash.ExpectedDamage;
        }
        else
        {
            expectedDamage =
                estimator.EstimateOneSidedDamage(
                    context,
                    source.Owner,
                    source.Part,
                    source.Speed,
                    skill,
                    target,
                    targetPart);
        }

        // 약화 부위는 파괴 전 단계이므로 이번 타격의 HP 기대 피해는 0이다.
        // CanBreakPart 스킬의 가치는 아래 vulnerability/utility 점수로만 반영한다.
        if (targetPart?.IsWeakened == true)
            expectedDamage = 0f;

        float vulnerability =
            estimator.ScoreTargetVulnerability(
                target,
                targetPart,
                skill);

        float score =
            mode == PlayerAutoPlanMode.Damage
                ? expectedDamage * 500f +
                  vulnerability * 3f +
                  winRate * 250f
                : expectedDamage * 70f +
                  vulnerability +
                  winRate * 600f;

        if (targetPart?.IsWeakened == true &&
            skill.CanBreakPart)
        {
            score +=
                mode == PlayerAutoPlanMode.Damage
                    ? 1200f
                    : 250f;
        }

        if (targetPart?.IsBroken == true)
            score += 160f;

        score += CharacterAutoPlanAdvisorRegistry.ScoreCandidate(
            new AutoPlanCandidateContext(
                context,
                mode,
                source.Owner,
                source.Part,
                skill,
                target,
                targetPart,
                possibleThreat,
                winRate,
                expectedDamage));

        score += source.Speed * 0.01f;
        score -= source.ActionIndex * 0.001f;

        return new Candidate
        {
            Source = source,
            Skill = skill,
            Target = target,
            TargetPart = targetPart,
            Threat = possibleThreat,
            Score = score,
            WinRate = winRate,
            ExpectedDamage = expectedDamage
        };
    }

    private static ActionSlot CreatePlannedSlot(
        Candidate candidate)
    {
        if (candidate?.Source == null)
            return null;

        return new ActionSlot
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
                    : null,
            UseCharacterRerollResource =
                ResolveCharacterRerollChoice(
                    candidate.Source.Owner,
                    candidate.Skill)
        };
    }

    private static bool ResolveCharacterRerollChoice(
        Character owner,
        Skill skill)
    {
        YujinMechanic mechanic =
            owner?.GetMechanic<YujinMechanic>();

        return mechanic != null &&
               mechanic.AutoUseSense &&
               YujinMechanic.IsSenseEligibleSkill(
                   skill?.Definition?.SkillId);
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

        return target.IsValidTargetPart(
            targetPart,
            allowBroken);
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

        IReadOnlyList<TargetPoint> points =
            target.GetTargetPoints(
                includeBroken);

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

    private static ActionSlot
        FindAvailableTargetSlotForCharacter(
            IReadOnlyList<ActionSlot> slots,
            Character target,
            HashSet<ActionSlot> assignedTargetSlots)
    {
        if (slots == null ||
            target == null)
        {
            return null;
        }

        foreach (ActionSlot slot in slots)
        {
            if (slot == null ||
                slot.Owner != target ||
                slot.Phase != ActionPhase.COMBAT ||
                slot.Skill == null ||
                (assignedTargetSlots != null &&
                 assignedTargetSlots.Contains(slot)))
            {
                continue;
            }

            return slot;
        }

        return null;
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
        IReadOnlyList<ActionSlot> enemySlots,
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

        int clashCount = 0;

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

                clashCount++;
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

        if (clashCount > 0)
        {
            averageWinRate /=
                clashCount;
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
