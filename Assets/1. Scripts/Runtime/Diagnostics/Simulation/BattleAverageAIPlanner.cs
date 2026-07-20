using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 임시 밸런스 검증용 플레이어 자동 계획기.
/// 현재 적 AI의 점수 구조와 같은 방향을 사용하지만,
/// 다수 적을 대상으로 동작하도록 별도 구현한다.
/// 실제 게임용 플레이어 AI가 아니다.
/// </summary>
public sealed class BattleAverageAIPlanner
{
    private sealed class PlanningState
    {
        public Character Owner;
        public BattleContext Context;
        public readonly List<ActionSlot> Planned = new();
        public int PrestigeCount;
        public int PlannedEnergyCost;

        public bool CanPlanEnergy(Skill skill)
        {
            return Owner != null &&
                   skill != null &&
                   PlannedEnergyCost +
                   Mathf.Max(0, skill.EnergyCost) <=
                   Owner.CurrentEnergy;
        }

        public int CountSkill(Skill skill)
        {
            int count = 0;
            foreach (ActionSlot slot in Planned)
            {
                if (slot?.Skill == skill)
                    count++;
            }
            return count;
        }

        public int CountType(ActionType type)
        {
            int count = 0;
            foreach (ActionSlot slot in Planned)
            {
                if (slot?.Skill?.ActionType == type)
                    count++;
            }
            return count;
        }
    }

    private readonly struct Decision
    {
        public Decision(
            Skill skill,
            TargetPoint target,
            float score)
        {
            Skill = skill;
            Target = target;
            Score = score;
        }

        public Skill Skill { get; }
        public TargetPoint Target { get; }
        public float Score { get; }
        public bool IsValid => Skill != null && Target.IsValid;
    }

    public int PlanPlayer(BattleManager manager)
    {
        BattleContext context = manager?.BattleContext;
        Character player = context?.Player;
        ActionManager actionManager = manager?.ActionManager;
        SpeedManager speedManager = manager?.SpeedManager;

        if (player == null ||
            player.IsDead ||
            actionManager == null ||
            speedManager == null)
        {
            return 0;
        }

        actionManager.RemoveSlotsByOwner(player);

        if (!player.UsesBodyParts || player.BodyParts == null)
        {
            Debug.LogWarning(
                "[BattleAverageAIPlanner] 현재 임시 플레이어 AI는 부위형 캐릭터를 기준으로 합니다.");
            return 0;
        }

        PlanningState state = new PlanningState
        {
            Owner = player,
            Context = context
        };

        foreach (BodyPart part in player.BodyParts)
        {
            if (part == null || part.IsBroken)
                continue;

            int maxSlots = Mathf.Max(
                0,
                player.GetMaxActionSlotsForPart(part));

            for (int actionIndex = 0;
                 actionIndex < maxSlots;
                 actionIndex++)
            {
                Decision decision = SelectDecision(
                    state,
                    part,
                    actionIndex,
                    actionManager);

                if (!decision.IsValid)
                    continue;

                ActionSlot slot = new ActionSlot
                {
                    Owner = player,
                    Part = part,
                    Skill = decision.Skill,
                    Speed = speedManager.GetSpeed(player, part),
                    ActionIndex = actionIndex,
                    Phase = decision.Skill.DefaultPhase,
                    TargetCharacter = decision.Target.Character,
                    TargetPart = decision.Target.Part,
                    TargetSlot = null
                };

                if (!actionManager.TryAddOrReplaceSlot(slot))
                    continue;

                state.Planned.Add(slot);
                state.PlannedEnergyCost +=
                    Mathf.Max(0, decision.Skill.EnergyCost);

                if (decision.Skill.ActionType == ActionType.Prestige)
                    state.PrestigeCount++;
            }
        }

        return state.Planned.Count;
    }

    private Decision SelectDecision(
        PlanningState state,
        BodyPart part,
        int actionIndex,
        ActionManager actionManager)
    {
        Decision best = default;
        float bestScore = float.NegativeInfinity;

        foreach (Skill skill in part.AvailableSkills)
        {
            if (!CanUse(state, part, skill))
                continue;

            TargetPoint target = SelectTarget(
                state,
                part,
                skill,
                actionManager,
                out float targetScore);

            if (!target.IsValid)
                continue;

            float score = targetScore;
            score += skill.ActionType switch
            {
                ActionType.Prestige => 10000f,
                ActionType.Duel => 135f,
                ActionType.NormalAttack => 105f,
                ActionType.Preparation => 65f,
                _ => 0f
            };

            score += (skill.MinPower + skill.MaxPower) * 1f;

            if (skill.CanClash)
                score += 25f;

            if (skill.CanBreakPart &&
                target.Part?.IsWeakened == true)
            {
                score += 160f;
            }

            score -= state.CountSkill(skill) * 18f;
            score -= actionIndex * 3f;

            if (skill.ActionType == ActionType.Preparation)
            {
                score -= state.CountType(
                    ActionType.Preparation) * 45f;
            }

            // 현재 적 AI와 마찬가지로 완전히 결정론적인 최적해가 아니라
            // 여러 번 실행했을 때 평균 성능을 얻기 위한 작은 흔들림을 둔다.
            score += Random.Range(0f, 12f);

            if (score <= bestScore)
                continue;

            bestScore = score;
            best = new Decision(skill, target, score);
        }

        return best;
    }

    private bool CanUse(
        PlanningState state,
        BodyPart part,
        Skill skill)
    {
        if (skill == null ||
            state.Owner == null ||
            !state.Owner.CanUseSkill(part, skill) ||
            !skill.CanAIUse(state.Owner, part, state.Context))
        {
            return false;
        }

        if (!state.CanPlanEnergy(skill))
            return false;

        if (skill.ActionType != ActionType.Prestige)
            return true;

        return skill.PrestigeUsePolicy switch
        {
            PrestigeUsePolicy.None => false,
            PrestigeUsePolicy.OncePerTurn =>
                state.PrestigeCount == 0,
            PrestigeUsePolicy.Unlimited => true,
            _ => false
        };
    }

    private TargetPoint SelectTarget(
        PlanningState state,
        BodyPart sourcePart,
        Skill skill,
        ActionManager actionManager,
        out float bestScore)
    {
        bestScore = float.NegativeInfinity;

        if (skill.ActionType == ActionType.Preparation)
        {
            if (state.Owner.IsValidTargetPart(
                    sourcePart,
                    allowBrokenPart: false))
            {
                bestScore = 100f;
                return TargetPoint.ForBodyPart(
                    state.Owner,
                    sourcePart);
            }

            return default;
        }

        TargetPoint bestPoint = default;
        bool includeBroken =
            skill.ActionType == ActionType.NormalAttack ||
            skill.ActionType == ActionType.Duel;

        if (state.Context?.Enemies == null)
            return default;

        foreach (Character enemy in state.Context.Enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            foreach (TargetPoint point in
                     enemy.GetTargetPoints(includeBroken))
            {
                if (!point.IsValid ||
                    !enemy.IsValidTargetPart(
                        point.Part,
                        includeBroken))
                {
                    continue;
                }

                float score = ScoreTarget(
                    state,
                    skill,
                    point,
                    actionManager);

                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestPoint = point;
            }
        }

        return bestPoint;
    }

    private float ScoreTarget(
        PlanningState state,
        Skill skill,
        TargetPoint point,
        ActionManager actionManager)
    {
        Character target = point.Character;
        BodyPart targetPart = point.Part;
        float score = Random.Range(0f, 8f);

        if (targetPart == null)
        {
            float hpRate = target.MaxCombatHP <= 0
                ? 1f
                : (float)target.CurrentHP / target.MaxCombatHP;

            score += 80f;
            score += (1f - hpRate) * 70f;
        }
        else if (targetPart.IsBroken)
        {
            score += skill.ActionType == ActionType.NormalAttack
                ? 120f
                : 90f;
        }
        else
        {
            score += 35f;

            if (targetPart.IsWeakened)
            {
                score += skill.CanBreakPart
                    ? 130f
                    : 60f;
            }

            if (targetPart.MaxPartHP > 0f)
            {
                float hpRate = Mathf.Clamp01(
                    targetPart.PartHP /
                    targetPart.MaxPartHP);

                score += (1f - hpRate) * 75f;
            }

            if (skill.CanClash &&
                HasClashOpportunity(
                    actionManager,
                    target,
                    targetPart))
            {
                score += skill.ActionType == ActionType.Duel
                    ? 120f
                    : 65f;
            }
        }

        int duplicateTargets = 0;

        foreach (ActionSlot planned in state.Planned)
        {
            if (planned != null &&
                planned.TargetCharacter == target &&
                planned.TargetPart == targetPart)
            {
                duplicateTargets++;
            }
        }

        score += duplicateTargets == 0
            ? 45f
            : -30f * duplicateTargets;

        return score;
    }

    private static bool HasClashOpportunity(
        ActionManager actionManager,
        Character target,
        BodyPart targetPart)
    {
        if (actionManager?.Slots == null)
            return false;

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot != null &&
                slot.Owner == target &&
                slot.Part == targetPart &&
                slot.Skill?.CanClash == true)
            {
                return true;
            }
        }

        return false;
    }
}
