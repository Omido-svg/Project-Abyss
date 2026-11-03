using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// R7-8 / 1029 §12.3 normal-enemy role runtime.
/// Each enemy decides Normal/Duel/Support independently; no encounter-wide
/// requested Duel quota. Species-specific extra content is authoring-dependent.
/// Tank cover requires a dedicated authored cover skill before activation.
/// </summary>
public sealed class NormalEnemy0922RoleMechanic : CombatMechanic, IAdditionalFinalStaggerDamageProvider20261103
{
    private EnemyRole0922 role = EnemyRole0922.Attacker;
    private R7EnemySpecies1029 species=R7EnemySpecies1029.Unknown;
    private R7EnemyAction1029 plannedAction=R7EnemyAction1029.Normal;
    private int coverExpireTurn=-1;
    private int lastCoverAppliedTurn=-1;
    private int lastOneSidedCounterTurn=-1;
    private bool roleAssigned;
    private bool duelAssignedThisTurn;
    private SkillColor preferredColor = SkillColor.Unset;
    private Character coverTarget;
    private Character pendingCoverTarget;
    private readonly Dictionary<Character,(R7EnemyCommanderStrengthAura20261103 strength,
        R7EnemyCommanderSwiftAura20261103 swift)> commanderAura=new();

    public EnemyRole0922 Role => role;
    public R7EnemySpecies1029 Species=>species;
    public R7EnemyAction1029 PlannedAction=>plannedAction;
    public int CoverExpireTurn=>coverExpireTurn;
    public int GetAdditionalFinalStaggerDamage(BattleAction action,Character target)
    {
        // §12.3: every RED roll by a debuffer adds +10 FINAL flat Stagger,
        // after resistance and before target status flats. Called per roll,
        // including one-sided attacks; never for allies or non-RED.
        return owner!=null&&action?.Owner==owner&&
            action.Skill?.IsRed==true&&target!=null&&!target.IsDead&&
            (species==R7EnemySpecies1029.Bind||
             species==R7EnemySpecies1029.Corrosion)?10:0;
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
        role==EnemyRole0922.Tank&&CoverTarget==null&&
        pendingCoverTarget!=null&&HasAuthoredSkill(
            ActionType.Duel,1,R7EnemyStage1Effect20261103.IsCoverDuelId);

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
        role = EnemyRole0922.Attacker;
        species=R7EnemySpecies1029.Unknown;
        plannedAction=R7EnemyAction1029.Normal;
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
        Canonical0922EnemyTurnCoordinator.PrepareTurn(battleContext,turn);
        MaintainCommanderAura();
    }

    private void OnActionStart(BattleAction action)
    {
        if(action?.Owner==owner)
        {
            if(species==R7EnemySpecies1029.Healer)
                R7EnemyStage1Effect20261103.ApplyHealerPreparation(action);
            if(species==R7EnemySpecies1029.Commander)
                R7EnemyStage1Effect20261103.ApplyCommanderSupply(action);
        }
        if(action?.Owner!=owner||role!=EnemyRole0922.Tank||
           CoverTarget!=null||pendingCoverTarget==null||
           !R7EnemyStage1Effect20261103.IsCoverDuelId(
               action.Skill?.Definition?.SkillId))return;
        int now=battleContext?.Services?.TurnManager?.CurrentTurn??1;
        if(now==lastCoverAppliedTurn)return;
        // The canonical target is chosen when the Duel ACTUALLY starts,
        // not from a stale prediction made at this turn's planning phase.
        var selected=Canonical0922EnemyTurnCoordinator
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
        R7EnemyStage1Effect20261103.ApplyCoverProtection(owner,action);
    }

    private void OnClashResolved(ClashResultContext result)
    {
        if(species!=R7EnemySpecies1029.CounterGuard||
           owner is not NormalEnemy counter||
           result?.Exchanges==null||result.Exchanges.Count==0)
            return;
        foreach(var exchange in result.Exchanges)
        {
            var loser=exchange?.LoserAction;
            if(loser?.Owner!=owner || exchange.IsOneSided ||
               !R7EnemyCounterReaction20261103.IsCounterTaggedSkill(
                   loser.Skill?.Definition?.SkillId) ||
               R7EnemyCounterReaction20261103.IsCounterReaction(loser))
                continue;
            var attacker=exchange.WinnerAction?.Owner;
            if(attacker==null||attacker.IsDead)continue;
            R7EnemyCounterReaction20261103.TryQueue(counter,loser,attacker,
                ProjectAbyss.Abilities.ReactionTimingCheckpoint.AfterClashCompleted);
        }
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if(exchange?.WinnerAction?.Owner!=owner||
           species==R7EnemySpecies1029.Unknown)return;
        R7EnemyStage1Effect20261103.OnExchangeWon(exchange,species);
    }

    private void OnDamageResolved(DamageContext damage)
    {
        if(species==R7EnemySpecies1029.CounterGuard&&
           owner is NormalEnemy counter&&
           damage?.Target==owner&&damage.Attacker!=null&&
           damage.Action!=null&&damage.Action.Target==owner&&
           !damage.IsClashDamage&&damage.WasApplied&&
           !R7EnemyCounterReaction20261103.IsCounterReaction(damage.Action))
        {
            int turn=battleContext?.Services?.TurnManager?.CurrentTurn??1;
            if(lastOneSidedCounterTurn!=turn&&
               R7EnemyCounterReaction20261103.TryQueue(counter,
                   damage.Action,damage.Attacker,
                   ProjectAbyss.Abilities.ReactionTimingCheckpoint.AfterActionCompleted))
                lastOneSidedCounterTurn=turn;
        }
        if(species!=R7EnemySpecies1029.Healer||
           damage?.Attacker!=owner||damage.Target==null||
           damage.Target.IsDead||
           (!damage.WasApplied||damage.RawPower<=0)||
           !R7EnemyStage1Effect20261103.IsHealerAttackId(
               damage.Action?.Skill?.Definition?.SkillId))
            return;
        R7EnemyStage1Effect20261103.ScheduleHealerLightPenalty(
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
        if(species!=R7EnemySpecies1029.Commander||owner==null||
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
            var strength=new R7EnemyCommanderStrengthAura20261103(
                owner.GetInstanceID());
            var swift=new R7EnemyCommanderSwiftAura20261103(owner.GetInstanceID());
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
            Canonical0922EnemyTurnCoordinator.HasLivingAlly(owner, battleContext);

        // Normal mobs in 0922 are Normal / Duel / Preparation role actors.
        // Old Prestige content is legacy and must not leak into canonical role planning.
        if (skill.ActionType == ActionType.Prestige)
            return false;

        if(species!=R7EnemySpecies1029.Unknown)
        {
            bool matches=plannedAction switch
            {
                R7EnemyAction1029.Normal=>skill.ActionType==ActionType.NormalAttack,
                R7EnemyAction1029.BasicDuel=>skill.ActionType==ActionType.Duel&&
                    skill.EnergyCost==1,
                R7EnemyAction1029.StrongDuel=>skill.ActionType==ActionType.Duel&&
                    skill.EnergyCost==2,
                R7EnemyAction1029.CoverDuel=>skill.ActionType==ActionType.Duel&&
                    R7EnemyStage1Effect20261103.IsCoverDuelId(skill.Definition?.SkillId),
                R7EnemyAction1029.HealPreparation=>skill.ActionType==ActionType.Preparation&&
                    R7EnemyStage1Effect20261103.IsHealId(skill.Definition?.SkillId),
                R7EnemyAction1029.SupplyPreparation=>skill.ActionType==ActionType.Preparation&&
                    R7EnemyStage1Effect20261103.IsSupplyId(skill.Definition?.SkillId),
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

    internal void ConfigureEncounterRole(EnemyRole0922 value)
    {
        role = value == EnemyRole0922.DealerSlot
            ? EnemyRole0922.Attacker
            : value;
        roleAssigned = true;
        // This runs on the canonical encounter's first turn before attacks.
        // HP depends on ROLE, never whether 3 or 4 enemies spawned.
        if(owner is NormalEnemy normal)
            normal.SetMaxCombatHpForDebug(
                R7Enemy1029NativeContract20261103.RoleHp(role),
                preserveCurrentHpRatio:true);
    }

    internal void ConfigureSpecies(R7EnemySpecies1029 value)
    {
        species=value;
    }

    internal void ConfigureCanonicalAction(R7EnemyAction1029 choice)
    {
        plannedAction=choice;
        duelAssignedThisTurn=choice==R7EnemyAction1029.BasicDuel||
            choice==R7EnemyAction1029.StrongDuel||
            choice==R7EnemyAction1029.CoverDuel;
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
        return species!=R7EnemySpecies1029.Unknown&&
            R7EnemyStage1SkillCatalog20261103.HasAffordable(
                species,type,cost,identity,owner?.CurrentEnergy??0);
    }

    internal void ConfigureTurnPlan(bool assignedDuel, SkillColor color)
    {
        duelAssignedThisTurn=assignedDuel;
        plannedAction=assignedDuel?
            R7EnemyAction1029.BasicDuel:R7EnemyAction1029.Normal;
        preferredColor=color;
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

        int actualDuels=0;
        foreach(var pair in state.Roles)
        {
            var enemy=pair.Key;
            if(enemy==null||enemy.IsDead)continue;
            var mechanic=enemy.GetMechanic<NormalEnemy0922RoleMechanic>();
            if(mechanic==null)continue;
            bool normal=mechanic.HasAuthoredSkill(ActionType.NormalAttack,0);
            bool basic=mechanic.HasAuthoredSkill(ActionType.Duel,1,
                id=>!R7EnemyStage1Effect20261103.IsCoverDuelId(id));
            bool strong=mechanic.HasAuthoredSkill(ActionType.Duel,2);
            bool cover=mechanic.HasAuthoredSkill(ActionType.Duel,1,
                R7EnemyStage1Effect20261103.IsCoverDuelId);
            bool heal=mechanic.HasAuthoredSkill(ActionType.Preparation,1,
                R7EnemyStage1Effect20261103.IsHealId);
            bool supply=mechanic.HasAuthoredSkill(ActionType.Preparation,1,
                R7EnemyStage1Effect20261103.IsSupplyId);
            var choice=R7EnemySpeciesPolicy20261103.ChooseAvailable(
                mechanic.Species,enemy.CurrentEnergy,UnityEngine.Random.value,
                normal,basic,strong,mechanic.HasActiveCover,
                cover,mechanic.PendingCoverTarget!=null,heal,
                R7EnemyStage1Effect20261103.HasHealerTarget(enemy,context),
                supply,R7EnemyStage1Effect20261103.HasSupplyTarget(enemy,context));
            mechanic.ConfigureCanonicalAction(choice);
            if(choice==R7EnemyAction1029.BasicDuel||
               choice==R7EnemyAction1029.StrongDuel||
               choice==R7EnemyAction1029.CoverDuel)actualDuels++;
        }
        // Legacy read-only API remains for verification consumers, but
        // Requested=0 means no global quota exists under 1029.
        state.LastQuota=new EnemyDuelQuotaPlan0922(0,actualDuels,false);
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

        bool commanderChosen=false;
        for(int i=0;i<normals.Count;i++)
        {
            var role=composition.Roles[i];
            if(role==EnemyRole0922.DealerSlot)
                role=Canonical0922EnemyEncounterRules.ResolveDealerSlot(
                    UnityEngine.Random.value);
            var species=R7EnemySpeciesPolicy20261103.ChooseSpecies(
                role,UnityEngine.Random.value,commanderChosen);
            if(species==R7EnemySpecies1029.Commander)commanderChosen=true;
            var enemy=normals[i];
            state.Roles[enemy]=role;
            var mechanic=enemy.GetMechanic<NormalEnemy0922RoleMechanic>();
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
            var m=enemy.GetMechanic<NormalEnemy0922RoleMechanic>();
            if(m?.Role!=EnemyRole0922.Tank)continue;
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

    public static EnemyDuelQuotaPlan0922 GetLastQuotaForVerification(BattleContext context)
    {
        return context != null && states.TryGetValue(context, out EncounterState state)
            ? state.LastQuota
            : new EnemyDuelQuotaPlan0922(0, 0, false);
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
