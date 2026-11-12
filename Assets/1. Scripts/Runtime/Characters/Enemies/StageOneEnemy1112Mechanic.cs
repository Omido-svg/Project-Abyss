using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum StageOneEnemyKind1112 { None, Fanatic, MachineElite, Inquisitor, MachineBoss }

/// <summary>1112 §12.5–12.6. Only confirmed stage-one rules and numbers.</summary>
public sealed class StageOneEnemy1112Mechanic : CombatMechanic
{
    public const string ResourcePath = "ProjectAbyss/Canonical1112/Enemies";
    private readonly StageOneEnemyKind1112 kind;
    private readonly Dictionary<BodyPart, int> charges = new();
    private readonly HashSet<BodyPart> overclock = new();
    private readonly HashSet<BodyPart> rampage = new();
    private readonly Dictionary<BodyPart, int> rampageReadyTurns = new();
    private readonly Dictionary<BodyPart, int> suspicion = new();
    private readonly HashSet<BodyPart> brands = new();
    private readonly HashSet<long> paidActions = new();
    private readonly HashSet<long> freeActions = new();
    private readonly HashSet<long> sealedActions = new();
    private readonly Dictionary<long, int> lostRampage = new();
    private int charge, accusation, stigmaTurns, nextSuspicionBonus, suspicionBonus;
    private bool swiftGranted, interrogation;
    private int interrogationRemaining;
    private bool interrogationOffensive;
    private readonly Dictionary<string, SkillDefinition> definitions = new();
    public StageOneEnemy1112Mechanic(StageOneEnemyKind1112 kind) { this.kind = kind; }
    public override string MechanicName => "1112 " + kind;
    public StageOneEnemyKind1112 Kind => kind;
    public int Charge => charge;
    public int Accusation => accusation;
    public bool HasStigma => stigmaTurns > 0;
    public int CoolingCards { get; private set; }
    public int ChargeAt(BodyPart part) => part != null && charges.TryGetValue(part, out var n) ? n : 0;
    public bool IsOverclocked(BodyPart part) => part != null && overclock.Contains(part);
    private bool RampageReady(BodyPart part) => rampage.Contains(part) &&
        rampageReadyTurns.TryGetValue(part, out var turn) &&
        turn <= (battleContext.Services?.TurnManager?.CurrentTurn ?? 1);
    public bool IsRampaging(BodyPart part) => part != null && rampage.Contains(part);
    public int SuspicionAt(BodyPart part) => part != null && suspicion.TryGetValue(part, out var n) ? n : 0;
    public bool ShouldPauseBasePosture => kind == StageOneEnemyKind1112.Inquisitor && brands.Count > 0;
    public void RestoreCooling(BodyPart part) { if (part != null && !part.IsBroken) { overclock.Add(part); CoolingCards = Mathf.Min(2, CoolingCards + 1); } }
    public void RestoreSuspicion(BodyPart part, int n) { if (part != null && !part.IsBroken) suspicion[part] = n; }
    public bool HasBrand(BodyPart part) => part != null && brands.Contains(part);
    private bool Human => kind == StageOneEnemyKind1112.Fanatic || kind == StageOneEnemyKind1112.Inquisitor;
    private int Index(BodyPart part) => owner?.BodyParts == null ? -1 : owner.BodyParts.ToList().IndexOf(part);
    private bool Impaired(int index) => owner?.BodyParts != null && owner.BodyParts.Count > index &&
        (owner.BodyParts[index].IsWeakened || owner.BodyParts[index].IsBroken);
    private int Level => Impaired(Human ? 0 : 4) ? 0 :
        battleContext?.Services?.FervorManager?.GetFervorLevel(owner) ?? 0;
    public int SpeedRangePenalty => Impaired(Human ? 4 : 1) ? 1 : 0;
    public int ModifyBlockGain(int n) => Human && Impaired(1) ? Mathf.Max(0, n - 5) : n;
    public int ModifyEnergyGain(int n) => n > 0 && Human && Level >= 2 &&
        (kind == StageOneEnemyKind1112.Fanatic ? HasStigma : brands.Count > 0) ? n + 1 : n;
    public int AttackSlots
    {
        get
        {
            int alive = owner?.BodyParts?.Count(p => p != null && !p.IsBroken) ?? 0;
            int usual = owner?.GetMechanic<EnemyPostureMechanic>()?.CurrentAttackSlotLimit ?? 3;
            return Mathf.Min(alive, interrogation ? (interrogationOffensive ? 4 : 3) : usual);
        }
    }
    public override void OnRegister()
    {
        foreach (var d in Resources.LoadAll<SkillDefinition>(ResourcePath + "/" + kind))
            if (d != null) definitions[d.name] = d;
        SubscribeToBattleEvent(() => battleEvent.OnTurnStart += Start, () => battleEvent.OnTurnStart -= Start, "1112.Start");
        SubscribeToBattleEvent(() => battleEvent.OnTurnEnd += End, () => battleEvent.OnTurnEnd -= End, "1112.End");
        SubscribeToBattleEvent(() => battleEvent.OnExchangeResolved += Exchange, () => battleEvent.OnExchangeResolved -= Exchange, "1112.Exchange");
        SubscribeToBattleEvent(() => battleEvent.OnActionEnd += ActionEnd, () => battleEvent.OnActionEnd -= ActionEnd, "1112.ActionEnd");
        SubscribeToBattleEvent(() => battleEvent.OnClashResolved += ClashEnd, () => battleEvent.OnClashResolved -= ClashEnd, "1112.ClashEnd");
    }
    public override void OnUnregister()
    {
        charges.Clear(); overclock.Clear(); rampage.Clear(); rampageReadyTurns.Clear(); suspicion.Clear(); brands.Clear();
        paidActions.Clear(); freeActions.Clear(); sealedActions.Clear(); lostRampage.Clear();
        definitions.Clear(); CoolingCards = 0; charge = accusation = stigmaTurns = 0;
    }
    private int nextClashBonus;
    private void Start(int turn)
    {
        if (nextClashBonus > 0) { owner.AddTurnClashPowerBonus(nextClashBonus); nextClashBonus = 0; }
        suspicionBonus = nextSuspicionBonus; nextSuspicionBonus = 0;
        foreach (var p in brands.Where(p => p.IsBroken).ToArray()) { brands.Remove(p); suspicion.Remove(p); }
        if (kind == StageOneEnemyKind1112.Inquisitor)
        {
            bool shouldInterrogate = brands.Count > 0;
            if (shouldInterrogate != interrogation)
            {
                interrogation = shouldInterrogate; interrogationOffensive = false;
                interrogationRemaining = UnityEngine.Random.Range(2, 4);
            }
            else if (interrogation && --interrogationRemaining <= 0)
            {
                interrogationOffensive = !interrogationOffensive;
                interrogationRemaining = UnityEngine.Random.Range(2, 4);
            }
        }
        if (Human)
        {
            if (Level >= 1 && !swiftGranted)
            { fervorSwift = StatusEffectFactory.CreateCanonical0922(StatusEffectId.Swift, 1, -1);
                owner.AddStatus(fervorSwift, owner); swiftGranted = true; }
            if (Level == 0 && swiftGranted)
            {
                // Remove only this enemy's fervor Swift, never unrelated sources.
                if (fervorSwift != null) owner.RemoveStatus(fervorSwift);
                swiftGranted = false;
            }
        }
        else if (Level >= 1)
        {
            if (kind == StageOneEnemyKind1112.MachineElite) GainCharge(null, 1);
            else foreach (var p in owner.BodyParts.Where(p => !p.IsBroken)) GainCharge(p, 1);
        }
    }
    private StatusEffect fervorSwift;
    private void End(int turn)
    {
        if (stigmaTurns > 0) stigmaTurns--;
        if (!Human && Impaired(3))
        {
            if (kind == StageOneEnemyKind1112.MachineElite) charge = Mathf.Max(0, charge - 3);
            else foreach (var p in owner.BodyParts) charges[p] = Mathf.Max(0, ChargeAt(p) - 1);
        }
    }
    public void GainCharge(BodyPart part, int amount)
    {
        amount = Mathf.Max(0, amount - (Impaired(0) ? 1 : 0));
        if (kind == StageOneEnemyKind1112.MachineElite) { charge = Mathf.Min(15, charge + amount); return; }
        if (kind != StageOneEnemyKind1112.MachineBoss || part == null || part.IsBroken || rampage.Contains(part)) return;
        int value = ChargeAt(part) + amount;
        if (value < 6) { charges[part] = value; return; }
        charges[part] = 0;
        if (overclock.Remove(part))
        {
            rampage.Add(part);
            rampageReadyTurns[part] = (battleContext.Services?.TurnManager?.CurrentTurn ?? 1) + 1;
        }
        else
        {
            overclock.Add(part); CoolingCards = Mathf.Min(2, CoolingCards + 1);
            if (Level >= 2) owner.AddEnergy(1);
        }
    }
    public bool Cool(BodyPart part)
    {
        if (CoolingCards <= 0 || part == null || part.IsBroken || !overclock.Remove(part)) return false;
        CoolingCards--; return true;
    }
    public void ClearSuspicion(BodyPart part) { if (part != null) suspicion.Remove(part); }
    private void AddSuspicion(BodyPart part, int amount)
    {
        if (part == null || part.IsBroken || part.StatusEffects.Any(s => s is SealedPartStatus)) return;
        int max = 5 + (Impaired(2) ? 1 : 0) - (Level >= 3 ? 1 : 0);
        suspicion[part] = Mathf.Min(max, SuspicionAt(part) + amount + suspicionBonus);
        if (suspicion[part] >= max) brands.Add(part);
    }
    private void AddAccusation(int n)
    {
        int max = 5 + (Impaired(2) ? 1 : 0);
        if (accusation + n > max) { accusation = 0; stigmaTurns = (Level >= 3 ? 5 : 3) - (Impaired(3) ? 1 : 0); }
        else accusation += n;
    }
    private void Status(Character target, BodyPart part, StatusEffectId id, int n, int turns, bool next = false)
    {
        if (target == null || n <= 0) return;
        StatusEffect effect = next ? new DeferredStatusEffect(id, n, turns) : StatusEffectFactory.CreateCanonical0922(id, n, turns);
        if (part != null) target.AddPartStatus(part, effect, owner); else target.AddStatus(effect, owner);
    }
    public bool BypassDuelGate(BattleAction action) => action?.Owner == owner &&
        (kind == StageOneEnemyKind1112.Fanatic && Key(action) == "D" && HasStigma ||
         kind == StageOneEnemyKind1112.MachineElite && Key(action) == "E" && charge == 15 ||
         kind == StageOneEnemyKind1112.Inquisitor && Key(action) == "Seal" && HasBrand(action.TargetPart));
    public void RollUsing(BattleAction a, int index)
    {
        string key = Key(a);
        if (kind == StageOneEnemyKind1112.Fanatic)
        { if (key == "A" || key == "B") AddAccusation(1); }
        else if (kind == StageOneEnemyKind1112.MachineElite)
        {
            if (key == "A" || key == "B") GainCharge(null, key == "A" ? 3 : 2);
            if (index == 0 && (key == "C" || key == "D"))
            {
                int cost = key == "C" ? 5 : 8;
                if (charge >= cost)
                {
                    bool free = Level >= 3 && UnityEngine.Random.value < .25f;
                    if (!free) { charge -= cost; paidActions.Add(a.ActionId); }
                    else freeActions.Add(a.ActionId);
                    if (key == "C") Status(owner, null, StatusEffectId.Protection, 3, 1, true);
                    else a.RulebreakerFlatPowerBonus++;
                }
            }
            if (index == 0 && key == "E" && charge > 0)
            {
                if (Level >= 3 && UnityEngine.Random.value < .25f) freeActions.Add(a.ActionId);
                else { charge = 0; paidActions.Add(a.ActionId); }
            }
        }
        else if (kind == StageOneEnemyKind1112.MachineBoss && key == "C")
        {
            charges[a.OwnerPart] = Mathf.Max(0, ChargeAt(a.OwnerPart) - 3);
            Status(owner, a.OwnerPart, StatusEffectId.Protection, IsOverclocked(a.OwnerPart) ? 4 : 2, 1, true);
        }
    }
    public bool CanBreak(BattleAction a)
    {
        if (a == null || a.ActionType == ActionType.Duel && !a.DuelEffectsEligible && !BypassDuelGate(a)) return false;
        string key = Key(a);
        return kind switch
        {
            StageOneEnemyKind1112.Fanatic => key == "D" && HasStigma,
            StageOneEnemyKind1112.MachineElite => key == "D" && (paidActions.Contains(a.ActionId) || freeActions.Contains(a.ActionId)),
            StageOneEnemyKind1112.Inquisitor => key == "Break",
            StageOneEnemyKind1112.MachineBoss => key == "D" || key == "Rampage",
            _ => false
        };
    }
    private static string Key(BattleAction a) => (a?.Skill as StageOneEnemySkill1112)?.Key ?? string.Empty;
    private void Exchange(ClashExchangeResult e)
    {
        if (e?.WinnerAction == null) return;
        var a = e.WinnerAction;
        if (kind == StageOneEnemyKind1112.MachineBoss && a.Owner != owner && a.Target == owner && a.TargetPart != null)
            GainCharge(a.TargetPart, 1);
        if (a.Owner == owner)
        {
            string key = Key(a);
            bool eligible = a.ActionType != ActionType.Duel || a.DuelEffectsEligible || BypassDuelGate(a);
            if (!eligible) return;
            if (kind == StageOneEnemyKind1112.Inquisitor)
            {
                if (key == "A" || key == "B") AddSuspicion(a.TargetPart, key == "B" && a.CurrentRollIndex == 1 ? 2 : 1);
                if (key == "Seal" && sealedActions.Add(a.ActionId)) ReserveSeal(a.Target, a.TargetPart);
                if (key == "Confess" && a.DuelEffectsEligible)
                {
                    ClearSuspicion(a.TargetPart);
                    if (brands.Remove(a.TargetPart)) ReserveSeal(a.Target, a.TargetPart);
                }
            }
            else if (kind == StageOneEnemyKind1112.Fanatic && key == "E") stigmaTurns = 0;
            else if (kind == StageOneEnemyKind1112.MachineBoss && (key == "A" || key == "B"))
                GainCharge(a.OwnerPart, key == "A" ? 1 : 2);
        }
        if (kind == StageOneEnemyKind1112.MachineBoss && !e.IsOneSided)
        {
            var own = e.FirstAction?.Owner == owner ? e.FirstAction : e.SecondAction?.Owner == owner ? e.SecondAction : null;
            if (own != null && own != a && Key(own) == "Rampage" && own.DuelEffectsEligible)
            {
                owner.GetMechanic<StaggerGaugeMechanic>()?.SacrificeGauge(30);
                lostRampage.TryGetValue(own.ActionId, out var lost); lostRampage[own.ActionId] = ++lost;
                if (lost >= 4) owner.GetMechanic<StaggerGaugeMechanic>()?.SacrificeGauge(int.MaxValue);
            }
        }
    }
    private void ReserveSeal(Character target, BodyPart part)
    {
        battleContext.TurnStartEffects.Enqueue(() =>
        { if (target != null && !target.IsDead && part != null && !part.IsBroken) target.AddPartStatus(part, new SealedPartStatus(1), owner); });
    }
    private void ActionEnd(BattleAction a)
    {
        if (a?.Owner != owner) return;
        if (kind == StageOneEnemyKind1112.MachineElite && paidActions.Remove(a.ActionId) && Level >= 2) GainCharge(null, 1);
        if (kind == StageOneEnemyKind1112.MachineBoss && Key(a) == "Rampage")
        {
            var part = a.OwnerPart; rampage.Remove(part); rampageReadyTurns.Remove(part); lostRampage.Remove(a.ActionId);
            if (part != null && !part.IsBroken)
            { if (part.IsWeakened) owner.ForceBreakPart(part, owner, a); else owner.WeakenPart(part, owner, a); }
        }
        paidActions.Remove(a.ActionId); freeActions.Remove(a.ActionId); sealedActions.Remove(a.ActionId);
    }
    private void ClashEnd(ClashResultContext c)
    {
        // Confession/seal cleanup needs all paired exchanges, never one-sided leftovers.
        if (kind != StageOneEnemyKind1112.Inquisitor || c?.Exchanges == null) return;
        var exchanges = c.Exchanges.Where(e => e != null && !e.IsOneSided).ToArray();
        if (exchanges.Length == 0) return;
        var own = exchanges.SelectMany(e => new[] { e.FirstAction, e.SecondAction }).FirstOrDefault(a => a?.Owner == owner);
        if (own == null || !own.DuelEffectsEligible) return;
        if (Key(own) == "Seal" && exchanges.All(e => e.WinnerAction?.Owner != owner))
        { brands.Remove(own.TargetPart); ClearSuspicion(own.TargetPart); }
        if (Key(own) == "Confess" && exchanges.All(e => e.WinnerAction?.Owner != owner))
            battleContext.TemporaryCards1112.SealSkill(own.Target, this);
    }
    public bool NegateConfessionDamage(BattleAction a) => kind == StageOneEnemyKind1112.Inquisitor &&
        Key(a) == "Confess" && a.DuelEffectsEligible && HasBrand(a.TargetPart);
    public int RollCount(string key, BodyPart part)
    {
        if (kind == StageOneEnemyKind1112.MachineBoss)
            return key == "Rampage" ? (Level >= 3 ? 5 : 4) : key == "A" ? (IsOverclocked(part) ? 4 : 3) :
                key == "D" && IsOverclocked(part) ? 3 : 2;
        if (kind == StageOneEnemyKind1112.MachineElite && key == "E") return charge == 0 ? 1 : charge < 5 ? 2 : charge < 10 ? 3 : charge < 15 ? 4 : 5;
        if (kind == StageOneEnemyKind1112.Fanatic && key == "C") return HasStigma ? 3 : 2;
        return definitions.TryGetValue(key, out var d) ? d.ExchangeRollCount : 1;
    }
    public int ExtraPower(string key, BodyPart part) => kind == StageOneEnemyKind1112.Fanatic && key == "D" && HasStigma ? 1 :
        kind == StageOneEnemyKind1112.MachineBoss && key == "B" && IsOverclocked(part) ? 2 : 0;
    public List<ActionSlot> Plan()
    {
        var result = new List<ActionSlot>();
        if (owner.IsDead || battleContext.Player == null || definitions.Count == 0) return result;
        var alive = owner.BodyParts.Where(p => p != null && !p.IsBroken).OrderByDescending(p => p.PartHP)
            .ThenBy(p => UnityEngine.Random.value).ToList();
        int attacks = AttackSlots;
        var attacking = alive.OrderByDescending(RampageReady).Take(attacks).ToList();
        bool lowEnergy = owner.CurrentEnergy <= 1;
        var posture = owner.GetMechanic<EnemyPostureMechanic>()?.Current ?? EnemyPosture.Normal;
        int preparationIndex = 0;
        foreach (var p in alive.Except(attacking))
        {
            bool classicElite = kind == StageOneEnemyKind1112.Fanatic || kind == StageOneEnemyKind1112.MachineElite;
            if (classicElite && posture == EnemyPosture.Offensive) PrepareSpecial(p);
            else if (classicElite && posture == EnemyPosture.Crouching)
            {
                if (preparationIndex == 0)
                { if (lowEnergy) owner.AddEnergy(2); else owner.AddBlock(15); }
                else if (preparationIndex == 1)
                { if (lowEnergy) owner.AddBlock(15); else PrepareSpecial(p); }
                else if (lowEnergy || UnityEngine.Random.value < .5f) PrepareSpecial(p);
                else owner.AddBlock(15);
            }
            else if (classicElite)
            {
                if (preparationIndex == 0)
                { if (lowEnergy) owner.AddEnergy(2); else PrepareSpecial(p); }
                else if (UnityEngine.Random.value < .5f) PrepareSpecial(p);
                else owner.AddBlock(15);
            }
            else if (preparationIndex == 0 && lowEnergy)
                owner.AddEnergy(kind == StageOneEnemyKind1112.MachineBoss && IsOverclocked(p) ? 3 : 2);
            else if (preparationIndex == (lowEnergy ? 1 : 0)) PrepareSpecial(p);
            else if (!Human && UnityEngine.Random.value < .5f)
            { if (!Impaired(2)) GainCharge(p, IsOverclocked(p) ? 5 : 3); }
            else owner.AddBlock(kind == StageOneEnemyKind1112.MachineBoss && IsOverclocked(p) ? 25 : 15);
            preparationIndex++;
            if (definitions.TryGetValue("Preparation", out var prepDef))
            {
                var prep = new StageOneEnemyPreparation1112(prepDef);
                prep.Initialize(owner, battleEvent);
                result.Add(new ActionSlot { Owner = owner, Part = p, Skill = prep,
                    TargetCharacter = owner, TargetPart = p, Phase = ActionPhase.FORESIGHT,
                    PlanningEffectCommitted = true, ResourceCostCommitted = true, SkipResolution = true });
            }
        }
        int remaining = owner.CurrentEnergy;
        int seals = 0; bool global = false, breaker = false;
        float globalDraw = UnityEngine.Random.value;
        var sealTargets = new HashSet<BodyPart>();
        foreach (var p in attacking)
        {
            string key = null;
            if (RampageReady(p)) key = "Rampage";
            else if (kind == StageOneEnemyKind1112.Inquisitor)
            {
                if (!global && (brands.Count > 0 || suspicion.Values.Any(n => n > 0) && globalDraw < .5f)) { key = "Confess"; global = true; }
                else if (interrogation && seals < Mathf.Min(brands.Count, Impaired(3) ? 1 : 2) && remaining >= 1) { key = "Seal"; seals++; }
                else if (interrogation && !breaker && remaining >= 2) { key = "Break"; breaker = true; }
            }
            else if (kind == StageOneEnemyKind1112.Fanatic && !global && HasStigma && globalDraw < .2f) { key = "E"; global = true; }
            else if (kind == StageOneEnemyKind1112.MachineElite && !global)
            {
                float chance = charge < 5 ? 0 : charge < 8 ? .05f : charge < 13 ? .10f : .40f;
                if (globalDraw < chance) { key = "E"; global = true; }
            }
            if (key == null)
            {
                // Keep the minimum cost of later assigned parts available.
                // A BC-only part has no free A fallback; spending its last
                // light on an earlier optional D would erase its whole action.
                int reserve = attacking.Skip(attacking.IndexOf(p) + 1)
                    .Where(q => !RampageReady(q)).Sum(MinimumCost);
                key = Choose(p, Mathf.Max(0, remaining - reserve));
            }
            if (key == null || !definitions.TryGetValue(key, out var definition)) continue;
            var target = ChooseTarget(key, seals);
            if (key == "Seal")
            {
                target = battleContext.Player.BodyParts.Where(t => !t.IsBroken && brands.Contains(t) && !sealTargets.Contains(t))
                    .OrderBy(_ => UnityEngine.Random.value).FirstOrDefault();
                if (target != null) sealTargets.Add(target);
            }
            if (target == null) continue;
            var skill = new StageOneEnemySkill1112(definition, key, this, p);
            skill.Initialize(owner, battleEvent);
            remaining -= skill.EnergyCost;
            result.Add(new ActionSlot { Owner = owner, Part = p, Skill = skill, ActionIndex = 0,
                TargetCharacter = battleContext.Player, TargetPart = target, Phase = ActionPhase.COMBAT,
                Speed = battleContext.Services.SpeedManager.GetSpeed(p) });
        }
        return result;
    }
    private BodyPart ChooseTarget(string key, int sealIndex)
    {
        var valid = battleContext.Player.BodyParts.Where(p => !p.IsBroken && !YujinStealthStatus.IsHidden(p)).ToArray();
        if (valid.Length == 0) return null;
        if (kind == StageOneEnemyKind1112.Inquisitor && (key == "Confess" || key == "Seal"))
        {
            var branded = valid.Where(brands.Contains).OrderBy(_ => UnityEngine.Random.value).ToArray();
            if (branded.Length > 0) return branded[(sealIndex > 0 ? sealIndex - 1 : 0) % branded.Length];
            if (key == "Confess") return valid.OrderByDescending(SuspicionAt).ThenBy(_ => UnityEngine.Random.value).First();
        }
        return valid[UnityEngine.Random.Range(0, valid.Length)];
    }
    private string Pool(BodyPart part)
    {
        return Human ? kind == StageOneEnemyKind1112.Inquisitor ? "AB" : new[] { "BC", "AC", "AB", "CD", "AD" }[Index(part)] :
            new[] { "AB", "AC", "BC", "BD", "AD" }[Index(part)];
    }
    private int MinimumCost(BodyPart part)
    {
        var costs = Pool(part).Select(c => c.ToString()).Where(definitions.ContainsKey)
            .Select(k => definitions[k].EnergyCost).ToArray();
        return costs.Length == 0 ? 0 : costs.Min();
    }
    private string Choose(BodyPart part, int energy)
    {
        string pool = Pool(part);
        int[] weights = Human ? kind == StageOneEnemyKind1112.Inquisitor ? new[] { 60, 40, 0, 0 } : HasStigma ? new[] { 10, 10, 45, 35 } : new[] { 40, 30, 20, 10 } :
            kind == StageOneEnemyKind1112.MachineBoss ? IsOverclocked(part) ? new[] { 20, 30, 10, 40 } : new[] { 40, 30, 15, 15 } :
            charge < 5 ? new[] { 40, 40, 10, 10 } : charge < 8 ? new[] { 30, 25, 30, 10 } : charge < 13 ? new[] { 20, 20, 20, 30 } : new[] { 10, 10, 15, 25 };
        var options = pool.Select(c => c.ToString()).Where(k => definitions.ContainsKey(k) && definitions[k].EnergyCost <= energy).ToArray();
        int total = options.Sum(k => weights[k[0] - 'A']);
        if (total <= 0) return null;
        int n = UnityEngine.Random.Range(0, total);
        foreach (var k in options) { n -= weights[k[0] - 'A']; if (n < 0) return k; }
        return null;
    }
    private void PrepareSpecial(BodyPart p)
    {
        var posture = owner.GetMechanic<EnemyPostureMechanic>()?.Current ?? EnemyPosture.Normal;
        if (kind == StageOneEnemyKind1112.Inquisitor)
        {
            if (interrogation) { if (interrogationOffensive) Status(owner, null, StatusEffectId.Strength, 1, 1); else owner.AddBlock(brands.Count * 10); }
            else if (posture == EnemyPosture.Normal) Status(battleContext.Player, null, StatusEffectId.Disarm, 2, 1);
            else if (posture == EnemyPosture.Crouching) nextSuspicionBonus = 1;
            else if (suspicion.Values.Count(n => n > 0) >= 2) Status(owner, null, StatusEffectId.Strength, 1, 1);
        }
        else if (kind == StageOneEnemyKind1112.Fanatic)
        {
            if (posture == EnemyPosture.Normal) Status(battleContext.Player, null, StatusEffectId.Disarm, 2, 1);
            else if (posture == EnemyPosture.Crouching) nextClashBonus = 1;
            else if (HasStigma) Status(owner, null, StatusEffectId.Strength, 1, 1);
        }
        else if (kind == StageOneEnemyKind1112.MachineElite)
        {
            if (posture == EnemyPosture.Normal) { if (!Impaired(2)) GainCharge(p, 3); }
            else if (posture == EnemyPosture.Crouching) Status(owner, null, StatusEffectId.Protection, charge == 15 ? 3 : charge / 5, 1);
            else if (charge >= 10) Status(owner, null, StatusEffectId.Strength, 1, 1);
        }
        else
        {
            bool boosted = IsOverclocked(p);
            if (posture == EnemyPosture.Normal) Status(owner, p, StatusEffectId.Protection, boosted ? 2 : 1, 1, true);
            else if (posture == EnemyPosture.Crouching) foreach (var part in owner.BodyParts) GainCharge(part, boosted ? 2 : 1);
            else if (overclock.Count > 0) Status(owner, null, StatusEffectId.Strength, boosted ? 2 : 1, 1);
        }
    }
}

public sealed class StageOneEnemySkill1112 : Skill
{
    private readonly SkillDefinition definition;
    private readonly StageOneEnemy1112Mechanic mechanic;
    private readonly BodyPart part;
    public string Key { get; }
    protected override SkillDefinition RuntimeDefinition => definition;
    public override void Execute(BattleAction action) { }
    public override ActionType ActionType => definition.ActionType;
    public override int ExchangeRollCount => mechanic.RollCount(Key, part);
    public StageOneEnemySkill1112(SkillDefinition d, string key, StageOneEnemy1112Mechanic m, BodyPart p)
    { definition = d; Key = key; mechanic = m; part = p; SkillName = d.SkillName; BasePower = d.BasePower; Resolver = new DiceResolver(1, 8); }
    public override RollResult RollPowerResultForExchange(int i)
    {
        var r = base.RollPowerResultForExchange(i);
        int extra = mechanic.ExtraPower(Key, part);
        r.RawValue += extra; r.ModifiedValue += extra; r.FinalPower += extra;
        return r;
    }
    public override PartBreakMode ResolvePartBreakMode(BattleAction a, BodyPart target) =>
        mechanic.CanBreak(a) ? PartBreakMode.WeakenedOnly : PartBreakMode.None;
}

public sealed class StageOneEnemyPreparation1112 : DataPreparationSkill
{
    public StageOneEnemyPreparation1112(SkillDefinition definition) : base(definition) { }
}
