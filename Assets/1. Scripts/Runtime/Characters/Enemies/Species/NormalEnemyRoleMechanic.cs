using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// R7-8 / 1029 §12.3 normal-enemy role runtime.
/// Each enemy decides Normal/Duel/Support independently; no encounter-wide
/// requested Duel quota. Species-specific extra content is authoring-dependent.
/// Tank cover requires a dedicated authored cover skill before activation.
/// </summary>
public sealed class NormalEnemyRoleMechanic : CombatMechanic, IAdditionalFinalStaggerDamageProvider, ICombatSpeedRangeRule
{
    private EnemyRole role = EnemyRole.Attacker;
    private EnemySpecies species=EnemySpecies.Unknown;
    private EnemyActionPattern plannedAction=EnemyActionPattern.Normal;
    private int coverExpireTurn=-1;
    private int lastCoverAppliedTurn=-1;
    private int lastOneSidedCounterTurn=-1;
    private bool roleAssigned;
    private bool duelAssignedThisTurn;
    private SkillColor preferredColor = SkillColor.Unset;
    private Character coverTarget;
    private Character pendingCoverTarget;
    private readonly Dictionary<Character,(EnemyCommanderStrengthAura strength,
        EnemyCommanderSwiftAura swift)> commanderAura=new();

    public void ModifySpeedRange(ref int minimum,ref int maximum)
    {if(species==EnemySpecies.Corrosion){minimum=4;maximum=6;}}
    public EnemyRole Role => role;
    public EnemySpecies Species=>species;
    public EnemyActionPattern PlannedAction=>plannedAction;
    public int CoverExpireTurn=>coverExpireTurn;
    public int GetAdditionalFinalStaggerDamage(BattleAction action,Character target)
    {
        // §12.3: every RED roll by a debuffer adds +10 FINAL flat Stagger,
        // after resistance and before target status flats. Called per roll,
        // including one-sided attacks; never for allies or non-RED.
        return owner!=null&&action?.Owner==owner&&
            action.Skill?.IsRed==true&&target!=null&&!target.IsDead&&
            (species==EnemySpecies.Bind||
             species==EnemySpecies.Corrosion)?10:0;
    }
    public bool RoleAssigned => roleAssigned;
    public bool DuelAssignedThisTurn => duelAssignedThisTurn;
    public SkillColor PreferredColor => preferredColor;
    public Character CoverTarget =>
        IsValidCoverTarget(coverTarget)&&
        (coverExpireTurn<0||
         (battleContext?.Services?.TurnManager?.CurrentTurn??0)<=coverExpireTurn)
            ?coverTarget:null;
    public Character PendingCoverTarget => pendingCoverTarget;
    public bool HasActiveCover => CoverTarget != null;
    public bool RequiresCoverPreparation =>
        false; // 1029: cover is a Duel, never a Preparation.
    public bool RequiresCoverDuel =>
        role==EnemyRole.Tank&&CoverTarget==null&&
        pendingCoverTarget!=null&&HasAuthoredSkill(
            ActionType.Duel,1,EnemyStageOneEffect.IsCoverDuelId);

    public override string MechanicName => "0922 Normal Enemy Role";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionStart += OnActionStart,
            () => battleEvent.OnActionStart -= OnActionStart,
            "OnActionStart");
        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");
        SubscribeToBattleEvent(
            () => battleEvent.OnClashResolved += OnClashResolved,
            () => battleEvent.OnClashResolved -= OnClashResolved,
            "OnClashResolved");
        SubscribeToBattleEvent(
            () => battleEvent.OnDamageResolved += OnDamageResolved,
            () => battleEvent.OnDamageResolved -= OnDamageResolved,
            "OnDamageResolved");
        SubscribeToBattleEvent(
            () => battleEvent.OnCharacterDeath += OnCharacterDeath,
            () => battleEvent.OnCharacterDeath -= OnCharacterDeath,
            "OnCharacterDeath");
    }

    public override void OnUnregister()
    {
        ClearCommanderAura();
        role = EnemyRole.Attacker;
        species=EnemySpecies.Unknown;
        plannedAction=EnemyActionPattern.Normal;
        coverExpireTurn=-1;lastCoverAppliedTurn=-1;
        lastOneSidedCounterTurn=-1;
        roleAssigned = false;
        duelAssignedThisTurn = false;
        preferredColor = SkillColor.Unset;
        coverTarget = null;
        pendingCoverTarget = null;
    }

    private void OnTurnStart(int turn)
    {
        if(owner==null||owner.IsDead)return;
        if(coverExpireTurn>=0&&turn>coverExpireTurn)
        {
            coverTarget=null;
            coverExpireTurn=-1;
        }
        if(CoverTarget==null)coverTarget=null;
        CanonicalEnemyTurnCoordinator.PrepareTurn(battleContext,turn);
        MaintainCommanderAura();
    }

    private void OnActionStart(BattleAction action)
    {
        if(action?.Owner==owner)
        {
            if(species==EnemySpecies.Healer)
                EnemyStageOneEffect.ApplyHealerPreparation(action);
            if(species==EnemySpecies.Commander)
                EnemyStageOneEffect.ApplyCommanderSupply(action);
        }
        if(action?.Owner!=owner||role!=EnemyRole.Tank||
           CoverTarget!=null||pendingCoverTarget==null||
           !EnemyStageOneEffect.IsCoverDuelId(
               action.Skill?.Definition?.SkillId))return;
        int now=battleContext?.Services?.TurnManager?.CurrentTurn??1;
        if(now==lastCoverAppliedTurn)return;
        // The canonical target is chosen when the Duel ACTUALLY starts,
        // not from a stale prediction made at this turn's planning phase.
        var selected=CanonicalEnemyTurnCoordinator
            .SelectCoverCandidate(owner,battleContext);
        if(!IsValidCoverTarget(selected))
        {
            pendingCoverTarget=null;
            return;
        }
        coverTarget=selected;
        coverExpireTurn=now+2; // skill turn + next two turns = 3
        lastCoverAppliedTurn=now;
        pendingCoverTarget=null;
        EnemyStageOneEffect.ApplyCoverProtection(owner,action);
    }

    private void OnClashResolved(ClashResultContext result)
    {
        if(species!=EnemySpecies.CounterGuard||
           owner is not NormalEnemy counter||
           result?.Exchanges==null||result.Exchanges.Count==0)
            return;
        foreach(var exchange in result.Exchanges)
        {
            var loser=exchange?.LoserAction;
            if(loser?.Owner!=owner || exchange.IsOneSided ||
               !EnemyCounterReaction.IsCounterTaggedSkill(
                   loser.Skill?.Definition?.SkillId) ||
               EnemyCounterReaction.IsCounterReaction(loser))
                continue;
            var attacker=exchange.WinnerAction?.Owner;
            if(attacker==null||attacker.IsDead)continue;
            EnemyCounterReaction.TryQueue(counter,loser,attacker,
                ProjectAbyss.Abilities.ReactionTimingCheckpoint.AfterClashCompleted);
        }
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if(exchange?.WinnerAction?.Owner!=owner||
           species==EnemySpecies.Unknown)return;
        EnemyStageOneEffect.OnExchangeWon(exchange,species);
    }

    private void OnDamageResolved(DamageContext damage)
    {
        if(species==EnemySpecies.CounterGuard&&
           owner is NormalEnemy counter&&
           damage?.Target==owner&&damage.Attacker!=null&&
           damage.Action!=null&&damage.Action.Target==owner&&
           !damage.IsClashDamage&&damage.WasApplied&&
           !EnemyCounterReaction.IsCounterReaction(damage.Action))
        {
            int turn=battleContext?.Services?.TurnManager?.CurrentTurn??1;
            if(lastOneSidedCounterTurn!=turn&&
               EnemyCounterReaction.TryQueue(counter,
                   damage.Action,damage.Attacker,
                   ProjectAbyss.Abilities.ReactionTimingCheckpoint.AfterActionCompleted))
                lastOneSidedCounterTurn=turn;
        }
        if(species!=EnemySpecies.Healer||
           damage?.Attacker!=owner||damage.Target==null||
           damage.Target.IsDead||
           (!damage.WasApplied||damage.RawPower<=0)||
           !EnemyStageOneEffect.IsHealerAttackId(
               damage.Action?.Skill?.Definition?.SkillId))
            return;
        EnemyStageOneEffect.ScheduleHealerLightPenalty(
            owner,damage.Target);
    }

    private void OnCharacterDeath(Character dead)
    {
        if(dead==owner)
        {
            ClearCommanderAura();
            coverTarget=null;
            pendingCoverTarget=null;
        }
        else if(dead==coverTarget)
        {
            coverTarget=null;coverExpireTurn=-1;
        }
        if(dead!=null)commanderAura.Remove(dead);
    }

    private void MaintainCommanderAura()
    {
        if(species!=EnemySpecies.Commander||owner==null||
           owner.IsDead||battleContext?.Enemies==null)return;
        var resolver=battleContext.EffectResolver;
        if(resolver==null)return;
        foreach(var target in battleContext.Enemies)
        {
            if(target==null||target==owner||target.IsDead)continue;
            if(commanderAura.TryGetValue(target,out var current)&&
               target.StatusEffects.Contains(current.strength)&&
               target.StatusEffects.Contains(current.swift))continue;
            if(commanderAura.ContainsKey(target))
            {
                var old=commanderAura[target];
                if(target.StatusEffects.Contains(old.strength))
                    target.RemoveStatus(old.strength,StatusEffectRemoveReason.Manual);
                if(target.StatusEffects.Contains(old.swift))
                    target.RemoveStatus(old.swift,StatusEffectRemoveReason.Manual);
                commanderAura.Remove(target);
            }
            var strength=new EnemyCommanderStrengthAura(
                owner.GetInstanceID());
            var swift=new EnemyCommanderSwiftAura(owner.GetInstanceID());
            bool a=resolver.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(owner,target,strength));
            bool b=resolver.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(owner,target,swift));
            if(a&&b)commanderAura.Add(target,(strength,swift));
        }
    }

    private void ClearCommanderAura()
    {
        foreach(var pair in commanderAura)
        {
            var target=pair.Key;
            if(target==null)continue;
            if(target.StatusEffects.Contains(pair.Value.strength))
                target.RemoveStatus(pair.Value.strength,StatusEffectRemoveReason.Manual);
            if(target.StatusEffects.Contains(pair.Value.swift))
                target.RemoveStatus(pair.Value.swift,StatusEffectRemoveReason.Manual);
        }
        commanderAura.Clear();
    }

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (skill == null || !roleAssigned)
            return true;

        bool hasLivingAlly =
            CanonicalEnemyTurnCoordinator.HasLivingAlly(owner, battleContext);

        // Normal mobs in 0922 are Normal / Duel / Preparation role actors.
        // Old Prestige content is legacy and must not leak into canonical role planning.
        if (skill.ActionType == ActionType.Prestige)
            return false;

        if(species!=EnemySpecies.Unknown)
        {
            bool matches=plannedAction switch
            {
                EnemyActionPattern.Normal=>skill.ActionType==ActionType.NormalAttack,
                EnemyActionPattern.BasicDuel=>skill.ActionType==ActionType.Duel&&
                    skill.EnergyCost==1,
                EnemyActionPattern.StrongDuel=>skill.ActionType==ActionType.Duel&&
                    skill.EnergyCost==2,
                EnemyActionPattern.CoverDuel=>skill.ActionType==ActionType.Duel&&
                    EnemyStageOneEffect.IsCoverDuelId(skill.Definition?.SkillId),
                EnemyActionPattern.HealPreparation=>skill.ActionType==ActionType.Preparation&&
                    EnemyStageOneEffect.IsHealId(skill.Definition?.SkillId),
                EnemyActionPattern.SupplyPreparation=>skill.ActionType==ActionType.Preparation&&
                    EnemyStageOneEffect.IsSupplyId(skill.Definition?.SkillId),
                _=>false
            };
            if(!matches)return false;
        }
        else
        {
            if(skill.ActionType==ActionType.Preparation)return false;
            if(duelAssignedThisTurn)
            {
                if(skill.ActionType!=ActionType.Duel)return false;
            }
            else if(skill.ActionType!=ActionType.NormalAttack)return false;
        }

        // 100% RED roles are strict. Mixed-color roles use a per-turn preferred
        // color when authored; Unset legacy skills remain available as fallback.
        SkillColor color = skill.Color;
        if (color == SkillColor.Unset)
            return true;

        // 1029 §12.3: ALL stage-1 normal-enemy attacking skills are RED.
        // Unset is a temporary legacy-data escape, never an authored BLUE.
        return color == SkillColor.Red;
    }

    internal void PrepareCoverIntentForTurn()
    {
        if (role != EnemyRole.Tank)
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
            CanonicalEnemyTurnCoordinator
                .SelectCoverCandidate(owner,battleContext);
    }

    internal bool HasRawPreparationSkill() =>
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

    internal void ConfigureEncounterRole(EnemyRole value)
    {
        role = value == EnemyRole.DealerSlot
            ? EnemyRole.Attacker
            : value;
        roleAssigned = true;
        // This runs on the canonical encounter's first turn before attacks.
        // HP depends on ROLE, never whether 3 or 4 enemies spawned.
        if(owner is NormalEnemy normal)
            normal.SetMaxCombatHpForDebug(
                EnemyCombatNativeContract.RoleHp(role),
                preserveCurrentHpRatio:true);
    }

    internal void ConfigureSpecies(EnemySpecies value)
    {
        species=value;
    }

    internal void ConfigureCanonicalAction(EnemyActionPattern choice)
    {
        plannedAction=choice;
        duelAssignedThisTurn=choice==EnemyActionPattern.BasicDuel||
            choice==EnemyActionPattern.StrongDuel||
            choice==EnemyActionPattern.CoverDuel;
        preferredColor=SkillColor.Red;
    }

    internal bool HasAuthoredSkill(ActionType type,int cost,
        System.Func<string,bool> identity=null)
    {
        var skills=owner?.GetSelectableSkills(null,0);
        if(skills==null)return false;
        foreach(var s in skills)
            if(s!=null&&s.ActionType==type&&
               s.EnergyCost==cost&&s.CanUseByResource(owner)&&
               (identity==null||identity(s.Definition?.SkillId)))
                return true;
        // Stage1 native authored cards are stored in their own Resources
        // catalog, not mixed into original CharacterData/CombatLoadout.
        return species!=EnemySpecies.Unknown&&
            EnemyStageOneSkillCatalog.HasAffordable(
                species,type,cost,identity,owner?.CurrentEnergy??0);
    }

    internal void ConfigureTurnPlan(bool assignedDuel, SkillColor color)
    {
        duelAssignedThisTurn=assignedDuel;
        plannedAction=assignedDuel?
            EnemyActionPattern.BasicDuel:EnemyActionPattern.Normal;
        preferredColor=color;
    }

    public void SetRoleForVerification(EnemyRole value) =>
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

public static class CanonicalEnemyTurnCoordinator
{
    private sealed class EncounterState
    {
        public int Turn = int.MinValue;
        public readonly Dictionary<NormalEnemy, EnemyRole> Roles = new();
        public bool Initialized;
        public EnemyDuelQuotaPlan LastQuota;
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
        foreach (KeyValuePair<NormalEnemy, EnemyRole> pair in state.Roles)
        {
            if (pair.Key == null || pair.Key.IsDead)
                continue;

            pair.Key.GetMechanic<NormalEnemyRoleMechanic>()
                ?.PrepareCoverIntentForTurn();
        }

        int actualDuels=0;
        foreach(var pair in state.Roles)
        {
            var enemy=pair.Key;
            if(enemy==null||enemy.IsDead)continue;
            var mechanic=enemy.GetMechanic<NormalEnemyRoleMechanic>();
            if(mechanic==null)continue;
            bool normal=mechanic.HasAuthoredSkill(ActionType.NormalAttack,0);
            bool basic=mechanic.HasAuthoredSkill(ActionType.Duel,1,
                id=>!EnemyStageOneEffect.IsCoverDuelId(id));
            bool strong=mechanic.HasAuthoredSkill(ActionType.Duel,2);
            bool cover=mechanic.HasAuthoredSkill(ActionType.Duel,1,
                EnemyStageOneEffect.IsCoverDuelId);
            bool heal=mechanic.HasAuthoredSkill(ActionType.Preparation,1,
                EnemyStageOneEffect.IsHealId);
            bool supply=mechanic.HasAuthoredSkill(ActionType.Preparation,1,
                EnemyStageOneEffect.IsSupplyId);
            var choice=EnemySpeciesPolicy.ChooseAvailable(
                mechanic.Species,enemy.CurrentEnergy,UnityEngine.Random.value,
                normal,basic,strong,mechanic.HasActiveCover,
                cover,mechanic.PendingCoverTarget!=null,heal,
                EnemyStageOneEffect.HasHealerTarget(enemy,context),
                supply,EnemyStageOneEffect.HasSupplyTarget(enemy,context));
            mechanic.ConfigureCanonicalAction(choice);
            if(choice==EnemyActionPattern.BasicDuel||
               choice==EnemyActionPattern.StrongDuel||
               choice==EnemyActionPattern.CoverDuel)actualDuels++;
        }
        // Legacy read-only API remains for verification consumers, but
        // Requested=0 means no global quota exists under 1029.
        state.LastQuota=new EnemyDuelQuotaPlan(0,actualDuels,false);
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

        if (!allNormal || !EnemyEncounterRules.IsCanonicalNormalEncounterCount(normals.Count))
            return;

        CanonicalEnemyEncounterEntry composition =
            EnemyEncounterRules.GetComposition(
                normals.Count,
                Random.value);

        if (composition?.Roles == null || composition.Roles.Count != normals.Count)
            return;

        bool commanderChosen=false;
        for(int i=0;i<normals.Count;i++)
        {
            var role=composition.Roles[i];
            if(role==EnemyRole.DealerSlot)
                role=EnemyEncounterRules.ResolveDealerSlot(
                    UnityEngine.Random.value);
            var species=EnemySpeciesPolicy.ChooseSpecies(
                role,UnityEngine.Random.value,commanderChosen);
            if(species==EnemySpecies.Commander)commanderChosen=true;
            var enemy=normals[i];
            state.Roles[enemy]=role;
            var mechanic=enemy.GetMechanic<NormalEnemyRoleMechanic>();
            mechanic?.ConfigureEncounterRole(role);
            mechanic?.ConfigureSpecies(species);
        }

        Debug.Log(
            $"[0922 Enemy Encounter] Composition={composition.Id}, " +
            $"Count={normals.Count}, HP=role-based 70/80/90/105 (1029)");
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

    public static bool IsCoveredByAnotherTank(Character target,
        Character except,BattleContext context)
    {
        if(target==null||context?.Enemies==null)return false;
        foreach(var enemy in context.Enemies)
        {
            if(enemy==null||enemy==except||enemy.IsDead||
               enemy is not NormalEnemy)continue;
            var m=enemy.GetMechanic<NormalEnemyRoleMechanic>();
            if(m?.Role!=EnemyRole.Tank)continue;
            // Pending plans are NOT installed cover. If two tanks plan
            // the same ally, the second will reselect on its actual action start.
            if(m.CoverTarget==target)return true;
        }
        return false;
    }

    public static Character SelectCoverCandidate(Character tank,BattleContext context)
    {
        if(tank==null||context?.Enemies==null)return null;
        // 1029: lowest ABSOLUTE remaining HP, not a percent/role priority.
        int lowest=int.MaxValue;
        var tied=new List<Character>();
        foreach(var candidate in context.Enemies)
        {
            if(candidate==null||candidate==tank||candidate.IsDead||
               IsCoveredByAnotherTank(candidate,tank,context))continue;
            int hp=Mathf.Max(0,candidate.CurrentHP);
            if(hp<lowest){lowest=hp;tied.Clear();tied.Add(candidate);}
            else if(hp==lowest)tied.Add(candidate);
        }
        return tied.Count==0?null:tied[UnityEngine.Random.Range(0,tied.Count)];
    }

    public static EnemyDuelQuotaPlan GetLastQuotaForVerification(BattleContext context)
    {
        return context != null && states.TryGetValue(context, out EncounterState state)
            ? state.LastQuota
            : new EnemyDuelQuotaPlan(0, 0, false);
    }

    private static bool HasRawNormalSkill(NormalEnemy enemy)
    {
        var skills=enemy?.GetSelectableSkills(null,0);
        return skills!=null&&skills.Any(x=>x?.ActionType==ActionType.NormalAttack&&
            x.CanUseByResource(enemy));
    }

    private static bool HasRawDuelSkill(NormalEnemy enemy)
    {
        IReadOnlyList<Skill> skills = enemy?.GetSelectableSkills(null, 0);
        if (skills == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i]?.ActionType == ActionType.Duel&&
                skills[i].CanUseByResource(enemy))
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

public static class EnemyCoverResolver
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

            NormalEnemyRoleMechanic mechanic =
                tank.GetMechanic<NormalEnemyRoleMechanic>();

            if (mechanic?.Role != EnemyRole.Tank ||
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
