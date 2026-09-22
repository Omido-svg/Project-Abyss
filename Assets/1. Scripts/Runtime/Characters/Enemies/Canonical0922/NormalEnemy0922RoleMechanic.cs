using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 0922 §12 normal-enemy role runtime.
/// Encounter-wide Duel quota is assigned once per turn by the coordinator.
/// Tank cover activation itself requires a Preparation action; exact duplicate-cover
/// and duration semantics remain PENDING_CANONICAL.
/// </summary>
public sealed class NormalEnemy0922RoleMechanic : CombatMechanic
{
    private EnemyRole0922 role = EnemyRole0922.Attacker;
    private bool roleAssigned;
    private bool duelAssignedThisTurn;
    private SkillColor preferredColor = SkillColor.Unset;
    private Character coverTarget;
    private Character pendingCoverTarget;

    public EnemyRole0922 Role => role;
    public bool RoleAssigned => roleAssigned;
    public bool DuelAssignedThisTurn => duelAssignedThisTurn;
    public SkillColor PreferredColor => preferredColor;
    public Character CoverTarget => IsValidCoverTarget(coverTarget) ? coverTarget : null;
    public Character PendingCoverTarget => pendingCoverTarget;
    public bool HasActiveCover => CoverTarget != null;
    public bool RequiresCoverPreparation =>
        role == EnemyRole0922.Tank &&
        CoverTarget == null &&
        pendingCoverTarget != null &&
        HasRawPreparationSkill();

    public override string MechanicName => "0922 Normal Enemy Role";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionEnd += OnActionEnd,
            () => battleEvent.OnActionEnd -= OnActionEnd,
            "OnActionEnd");
    }

    public override void OnUnregister()
    {
        role = EnemyRole0922.Attacker;
        roleAssigned = false;
        duelAssignedThisTurn = false;
        preferredColor = SkillColor.Unset;
        coverTarget = null;
        pendingCoverTarget = null;
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null || owner.IsDead)
            return;

        Canonical0922EnemyTurnCoordinator.PrepareTurn(
            battleContext,
            turn);
    }

    private void OnActionEnd(BattleAction action)
    {
        if (action?.Owner != owner ||
            role != EnemyRole0922.Tank ||
            pendingCoverTarget == null ||
            action.Skill?.ActionType != ActionType.Preparation)
        {
            return;
        }

        if (IsValidCoverTarget(pendingCoverTarget))
            coverTarget = pendingCoverTarget;

        pendingCoverTarget = null;
    }

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (skill == null || !roleAssigned)
            return true;

        bool hasLivingAlly =
            Canonical0922EnemyTurnCoordinator.HasLivingAlly(owner, battleContext);

        // Normal mobs in 0922 are Normal / Duel / Preparation role actors.
        // Old Prestige content is legacy and must not leak into canonical role planning.
        if (skill.ActionType == ActionType.Prestige)
            return false;

        if (role == EnemyRole0922.Buffer && hasLivingAlly)
        {
            // The 12 concrete normal-enemy skill sets are still PENDING_CANONICAL.
            // Once a support Preparation exists, Buffer uses only that path.
            // Legacy fixture data has no Preparation at all, so keep a Normal-only
            // fallback instead of producing an empty AI turn; Duel remains excluded.
            return HasRawPreparationSkill()
                ? skill.ActionType == ActionType.Preparation
                : skill.ActionType == ActionType.NormalAttack;
        }

        if (RequiresCoverPreparation)
            return skill.ActionType == ActionType.Preparation;

        if (skill.ActionType == ActionType.Preparation)
            return false;

        if (duelAssignedThisTurn)
        {
            if (skill.ActionType != ActionType.Duel)
                return false;
        }
        else if (skill.ActionType != ActionType.NormalAttack)
        {
            return false;
        }

        // 100% RED roles are strict. Mixed-color roles use a per-turn preferred
        // color when authored; Unset legacy skills remain available as fallback.
        SkillColor color = skill.Color;
        if (color == SkillColor.Unset)
            return true;

        if (role == EnemyRole0922.Attacker ||
            role == EnemyRole0922.Duelist)
        {
            return color == SkillColor.Red;
        }

        if (preferredColor == SkillColor.Unset || color == preferredColor)
            return true;

        // Role colors are authoritative once the role has an authored option.
        // Current legacy fixture data is RED-only; until the concrete 12 enemy
        // skill sets exist, do not turn a BLUE preference into an empty turn.
        return !HasRawSkillOption(skill.ActionType, preferredColor);
    }

    internal void PrepareCoverIntentForTurn()
    {
        if (role != EnemyRole0922.Tank)
        {
            pendingCoverTarget = null;
            return;
        }

        if (!IsValidCoverTarget(coverTarget))
            coverTarget = null;

        if (coverTarget != null)
        {
            pendingCoverTarget = null;
            return;
        }

        pendingCoverTarget =
            Canonical0922EnemyTurnCoordinator
                .SelectCoverCandidate(owner, battleContext);
    }

    private bool HasRawPreparationSkill() =>
        HasRawSkillOption(ActionType.Preparation, SkillColor.Unset);

    private bool HasRawSkillOption(ActionType actionType, SkillColor color)
    {
        IReadOnlyList<Skill> skills =
            owner?.GetSelectableSkills(null, 0);
        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            Skill candidate = skills[i];
            if (candidate == null || candidate.ActionType != actionType)
                continue;

            if (color == SkillColor.Unset ||
                candidate.Color == color)
            {
                return true;
            }
        }

        return false;
    }

    internal void ConfigureEncounterRole(EnemyRole0922 value)
    {
        role = value == EnemyRole0922.DealerSlot
            ? EnemyRole0922.Attacker
            : value;
        roleAssigned = true;
    }

    internal void ConfigureTurnPlan(bool assignedDuel, SkillColor color)
    {
        duelAssignedThisTurn = assignedDuel;
        preferredColor = color;
    }

    public void SetRoleForVerification(EnemyRole0922 value) =>
        ConfigureEncounterRole(value);

    public void SetTurnPlanForVerification(bool assignedDuel, SkillColor color) =>
        ConfigureTurnPlan(assignedDuel, color);

    public void SetCoverTargetForVerification(Character target)
    {
        coverTarget = target;
        pendingCoverTarget = null;
    }

    private bool IsValidCoverTarget(Character target)
    {
        return target != null &&
               target != owner &&
               !target.IsDead &&
               battleContext?.Enemies != null &&
               battleContext.Enemies.Contains(target);
    }
}

public static class Canonical0922EnemyTurnCoordinator
{
    private sealed class EncounterState
    {
        public int Turn = int.MinValue;
        public readonly Dictionary<NormalEnemy, EnemyRole0922> Roles = new();
        public bool Initialized;
        public EnemyDuelQuotaPlan0922 LastQuota;
    }

    private static readonly Dictionary<BattleContext, EncounterState> states = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        states.Clear();
    }

    public static void PrepareTurn(BattleContext context, int turn)
    {
        if (context?.Enemies == null)
            return;

        if (!states.TryGetValue(context, out EncounterState state))
        {
            state = new EncounterState();
            states.Add(context, state);
        }

        EnsureRoles(context, state);

        if (state.Turn == turn)
            return;

        state.Turn = turn;

        // Establish Tank cover intent before Duel quota allocation. A Tank that
        // actually owns a cover Preparation is not also counted as this turn's
        // Duel candidate. Legacy fixtures without such content stay in attack mode.
        foreach (KeyValuePair<NormalEnemy, EnemyRole0922> pair in state.Roles)
        {
            if (pair.Key == null || pair.Key.IsDead)
                continue;

            pair.Key.GetMechanic<NormalEnemy0922RoleMechanic>()
                ?.PrepareCoverIntentForTurn();
        }

        List<NormalEnemy> candidates = new();
        List<float> weights = new();

        foreach (KeyValuePair<NormalEnemy, EnemyRole0922> pair in state.Roles)
        {
            NormalEnemy enemy = pair.Key;
            if (enemy == null || enemy.IsDead)
                continue;

            bool hasLivingAlly = HasLivingAlly(enemy, context);
            float duelWeight =
                Canonical0922EnemyEncounterRules.GetDuelWeight(
                    pair.Value,
                    hasLivingAlly);

            NormalEnemy0922RoleMechanic roleMechanic =
                enemy.GetMechanic<NormalEnemy0922RoleMechanic>();
            if (roleMechanic?.RequiresCoverPreparation == true)
                duelWeight = 0f;

            if (duelWeight > 0f && HasRawDuelSkill(enemy))
            {
                candidates.Add(enemy);
                weights.Add(duelWeight);
            }
        }

        int requestedQuota =
            Canonical0922EnemyEncounterRules.RollDuelQuota(Random.value);

        HashSet<NormalEnemy> assigned =
            WeightedPickWithoutReplacement(
                candidates,
                weights,
                requestedQuota);

        bool insufficient = candidates.Count < requestedQuota;
        state.LastQuota = new EnemyDuelQuotaPlan0922(
            requestedQuota,
            assigned.Count,
            insufficient);

        foreach (KeyValuePair<NormalEnemy, EnemyRole0922> pair in state.Roles)
        {
            NormalEnemy enemy = pair.Key;
            if (enemy == null || enemy.IsDead)
                continue;

            NormalEnemy0922RoleMechanic mechanic =
                enemy.GetMechanic<NormalEnemy0922RoleMechanic>();
            if (mechanic == null)
                continue;

            bool hasLivingAlly = HasLivingAlly(enemy, context);
            SkillColor preferred =
                Canonical0922EnemyEncounterRules.RollPreferredColor(
                    pair.Value,
                    hasLivingAlly,
                    Random.value);

            mechanic.ConfigureTurnPlan(
                assigned.Contains(enemy),
                preferred);
        }

        if (insufficient)
        {
            Debug.LogWarning(
                $"[0922 Enemy DuelQuota][PENDING_CANONICAL] " +
                $"Requested={requestedQuota}, Candidates={candidates.Count}, " +
                "후보 부족 처리 방식은 정본 미정이므로 가용 후보만 배정했습니다.");
        }
    }

    private static void EnsureRoles(BattleContext context, EncounterState state)
    {
        if (state.Initialized)
            return;

        state.Initialized = true;

        List<NormalEnemy> normals = new();
        bool allNormal = true;
        foreach (Character character in context.Enemies)
        {
            if (character is NormalEnemy normal)
                normals.Add(normal);
            else if (character != null)
                allNormal = false;
        }

        if (!allNormal || !Canonical0922EnemyEncounterRules.IsCanonicalNormalEncounterCount(normals.Count))
            return;

        Canonical0922EnemyEncounterEntry composition =
            Canonical0922EnemyEncounterRules.GetComposition(
                normals.Count,
                Random.value);

        if (composition?.Roles == null || composition.Roles.Count != normals.Count)
            return;

        for (int i = 0; i < normals.Count; i++)
        {
            EnemyRole0922 role = composition.Roles[i];
            if (role == EnemyRole0922.DealerSlot)
                role = Canonical0922EnemyEncounterRules.ResolveDealerSlot(Random.value);

            NormalEnemy enemy = normals[i];
            state.Roles[enemy] = role;
            enemy.GetMechanic<NormalEnemy0922RoleMechanic>()
                ?.ConfigureEncounterRole(role);
        }

        Debug.Log(
            $"[0922 Enemy Encounter] Composition={composition.Id}, " +
            $"Count={normals.Count}, HP/Enemy=" +
            $"{(normals.Count == 3 ? Canonical0922EnemyEncounterRules.ThreeEnemyHp : Canonical0922EnemyEncounterRules.FourEnemyHp)}");
    }

    public static bool HasLivingAlly(Character owner, BattleContext context)
    {
        if (owner == null || context?.Enemies == null)
            return false;

        foreach (Character enemy in context.Enemies)
        {
            if (enemy != null && enemy != owner && !enemy.IsDead)
                return true;
        }
        return false;
    }

    public static Character SelectCoverCandidate(Character tank, BattleContext context)
    {
        if (tank == null || context?.Enemies == null)
            return null;

        Character best = null;
        int bestPriority = int.MaxValue;

        foreach (Character candidate in context.Enemies)
        {
            if (candidate == null || candidate == tank || candidate.IsDead)
                continue;

            EnemyRole0922 role = EnemyRole0922.Attacker;
            if (candidate is NormalEnemy normal)
            {
                NormalEnemy0922RoleMechanic mechanic =
                    normal.GetMechanic<NormalEnemy0922RoleMechanic>();
                if (mechanic?.RoleAssigned == true)
                    role = mechanic.Role;
            }

            int priority =
                Canonical0922EnemyEncounterRules.GetCoverPriority(candidate, role);

            if (priority < bestPriority)
            {
                bestPriority = priority;
                best = candidate;
            }
        }

        return best;
    }

    public static EnemyDuelQuotaPlan0922 GetLastQuotaForVerification(BattleContext context)
    {
        return context != null && states.TryGetValue(context, out EncounterState state)
            ? state.LastQuota
            : new EnemyDuelQuotaPlan0922(0, 0, false);
    }

    private static bool HasRawDuelSkill(NormalEnemy enemy)
    {
        IReadOnlyList<Skill> skills = enemy?.GetSelectableSkills(null, 0);
        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i]?.ActionType == ActionType.Duel)
                return true;
        }
        return false;
    }

    private static HashSet<NormalEnemy> WeightedPickWithoutReplacement(
        List<NormalEnemy> candidates,
        List<float> weights,
        int requested)
    {
        HashSet<NormalEnemy> result = new();
        if (candidates == null || weights == null)
            return result;

        List<NormalEnemy> pool = new(candidates);
        List<float> poolWeights = new(weights);
        int count = Mathf.Min(Mathf.Max(0, requested), pool.Count);

        for (int pick = 0; pick < count; pick++)
        {
            float total = 0f;
            for (int i = 0; i < poolWeights.Count; i++)
                total += Mathf.Max(0f, poolWeights[i]);

            if (total <= 0f)
                break;

            float roll = Random.value * total;
            int selected = pool.Count - 1;
            float cumulative = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                cumulative += Mathf.Max(0f, poolWeights[i]);
                if (roll <= cumulative)
                {
                    selected = i;
                    break;
                }
            }

            result.Add(pool[selected]);
            pool.RemoveAt(selected);
            poolWeights.RemoveAt(selected);
        }

        return result;
    }
}

public static class EnemyCover0922Resolver
{
    public const string RuntimeMarker = "[0922_PHASE9_ONE_SIDED_COVER_REDIRECT]";

    public static bool TryRedirectOneSided(BattleAction action, BattleContext context)
    {
        if (action?.Slot == null ||
            context?.Enemies == null ||
            action.Target == null ||
            action.Target.IsDead)
        {
            return false;
        }

        Character originalTarget = action.Target;

        foreach (Character candidate in context.Enemies)
        {
            if (candidate is not NormalEnemy tank ||
                tank.IsDead ||
                tank == originalTarget)
            {
                continue;
            }

            NormalEnemy0922RoleMechanic mechanic =
                tank.GetMechanic<NormalEnemy0922RoleMechanic>();

            if (mechanic?.Role != EnemyRole0922.Tank ||
                mechanic.CoverTarget != originalTarget)
            {
                continue;
            }

            action.Slot.TargetCharacter = tank;
            action.Slot.TargetPart = null;
            action.Slot.TargetSlot = null;

            Debug.Log(
                $"{RuntimeMarker} {originalTarget.name} -> {tank.name}");
            return true;
        }

        return false;
    }
}
