using System.Collections.Generic;
using UnityEngine;

public enum ActionType
{
    NormalAttack,
    Duel,
    Preparation,
    Prestige
}

public class BattleAction
{
    public ActionSlot Slot;

    public long ActionId => Slot == null ? 0 : Slot.ActionId;
    public int ActionIndex => Slot == null ? 0 : Slot.ActionIndex;
    public Character Owner => Slot == null ? null : Slot.Owner;
    public Character Target => Slot == null ? null : Slot.TargetCharacter;
    public BodyPart OwnerPart => Slot == null ? null : Slot.Part;
    public BodyPart TargetPart => Slot == null ? null : Slot.TargetPart;
    public Skill Skill => Slot == null ? null : Slot.Skill;
    public int Speed => Slot == null ? 0 : Slot.Speed;
    public ActionPhase Phase => Slot == null ? ActionPhase.COMBAT : Slot.Phase;

    public ActionType ActionType =>
        Skill == null
            ? ActionType.NormalAttack
            : Skill.ActionType;

    public int RolledPower;
    public int ClashPower;
    public int SpeedModifier;
    public int MomentumModifier;
    public int PreparationModifier;
    public int JudgmentModifier;
    public int CurrentRollIndex;
    public CombatRollType CurrentRollType = CombatRollType.Attack;

    public bool Critical =>
        LastRollResult != null &&
        LastRollResult.IsCritical;

    public int finalPower;

    public int FinalPower
    {
        get => finalPower;
        set
        {
            finalPower = value;
            RolledPower = value;
            ClashPower =
                value +
                JudgmentModifier +
                SpeedModifier +
                MomentumModifier +
                PreparationModifier;
        }
    }

    public bool HasRolled;
    public RollResult LastRollResult;

    public List<RollResult> RollHistory { get; } = new();
    public List<DamageContext> DamageContexts { get; } = new();

    private readonly List<AttackWeightTarget>
        attackWeightTargets =
            new List<AttackWeightTarget>();

    private readonly List<AttackWeightHitResult>
        attackWeightHitResults =
            new List<AttackWeightHitResult>();

    private readonly Dictionary<int, RollResult> cachedRollResults = new();

    public IReadOnlyList<AttackWeightTarget>
        AttackWeightTargets =>
            attackWeightTargets;

    public IReadOnlyList<AttackWeightHitResult>
        AttackWeightHitResults =>
            attackWeightHitResults;

    public bool HasResolvedAttackWeightTargets
    {
        get;
        private set;
    }

    public int RequestedAttackWeight =>
        Mathf.Max(
            1,
            Skill?.AttackWeight ?? 1);

    public int ResolvedAttackWeight =>
        attackWeightTargets.Count;

    public DamageContext PrimaryDamageContext
    {
        get
        {
            foreach (DamageContext context
                     in DamageContexts)
            {
                if (context != null &&
                    context.Target == Target &&
                    context.TargetPart == TargetPart)
                {
                    return context;
                }
            }

            return LastDamageContext;
        }
    }

    public bool HasDamageLog;
    public int LoggedDamage;
    public int LoggedBeforeHP;
    public int LoggedAfterHP;

    public DamageContext LastDamageContext;
    public DamageResult LastDamageResult;
    public DamageEventResult LastDamageEventResult;

    public int TotalResolvedDamage
    {
        get
        {
            int total = 0;

            foreach (DamageContext context in DamageContexts)
            {
                total += context?.GetDisplayDamage() ?? 0;
            }

            return total;
        }
    }

    public void BeginResolutionSequence()
    {
        ResetCurrentRollState();
        RollHistory.Clear();
        DamageContexts.Clear();
        cachedRollResults.Clear();

        attackWeightTargets.Clear();
        attackWeightHitResults.Clear();
        HasResolvedAttackWeightTargets = false;

        HasDamageLog = false;
        LoggedDamage = 0;
        LoggedBeforeHP = 0;
        LoggedAfterHP = 0;
        LastDamageContext = null;
        LastDamageResult = null;
        LastDamageEventResult = null;
    }

    public int GetEffectiveExchangeRollCount()
    {
        int baseCount =
            Mathf.Max(
                1,
                Skill?.ExchangeRollCount ?? 1);

        return Owner == null
            ? baseCount
            : Owner.ModifyExchangeRollCount(
                this,
                baseCount);
    }

    public int RollPower()
    {
        return RollPowerForExchange(
            RollHistory.Count);
    }

    public int RollPowerForExchange(
        int exchangeIndex)
    {
        ResetCurrentRollState();
        if (Skill == null) return 0;

        CurrentRollIndex = Mathf.Max(0, exchangeIndex);
        CurrentRollType = Skill.GetRollType(CurrentRollIndex);

        RollResult cached = null;
        bool reuse =
            Skill.ShouldReuseRollData(CurrentRollIndex) &&
            cachedRollResults.TryGetValue(
                CurrentRollIndex,
                out cached);

        LastRollResult = reuse && cached != null
            ? cached.Clone()
            : Skill.RollPowerResultForExchange(CurrentRollIndex);

        if (LastRollResult == null)
        {
            SetPurePower(Skill.BasePower);
            HasRolled = true;
            return RolledPower;
        }

        LastRollResult.RollIndex = CurrentRollIndex;
        LastRollResult.RollType = CurrentRollType;
        LastRollResult.ClearClashModifiers();
        LastRollResult.WasReused = reuse;

        SetPurePower(LastRollResult.FinalPower);

        int judgedPower = Owner != null
            ? Owner.ModifyRoll(this, RolledPower)
            : RolledPower;

        JudgmentModifier =
            (judgedPower - RolledPower) +
            LastRollResult.JudgmentModifier;

        LastRollResult.JudgmentModifier = JudgmentModifier;
        LastRollResult.RecalculateClashPower();

        if (!reuse && Skill.ShouldReuseRollData(CurrentRollIndex))
            cachedRollResults[CurrentRollIndex] = LastRollResult.Clone();

        HasRolled = true;
        RollHistory.Add(LastRollResult.Clone());
        return RolledPower;
    }

    public void InvalidateCachedRoll(
        int exchangeIndex)
    {
        cachedRollResults.Remove(
            Mathf.Max(0, exchangeIndex));
    }

    public void ApplyClashModifiers(
        int speedModifier,
        int momentumModifier,
        int preparationModifier = 0)
    {
        SpeedModifier = speedModifier;
        MomentumModifier = momentumModifier;
        PreparationModifier = preparationModifier;

        ClashPower =
            RolledPower +
            JudgmentModifier +
            SpeedModifier +
            MomentumModifier +
            PreparationModifier;

        LastRollResult?.SetClashModifiers(
            SpeedModifier,
            MomentumModifier,
            PreparationModifier);
    }

    public void ClearClashModifiers()
    {
        ApplyClashModifiers(0, 0);
    }

    public int GetDamagePower() => RolledPower;

    private void SetPurePower(int power)
    {
        RolledPower = power;
        finalPower = power;
        ClashPower = power;
    }

    private void ResetCurrentRollState()
    {
        RolledPower = 0;
        finalPower = 0;
        ClashPower = 0;
        SpeedModifier = 0;
        MomentumModifier = 0;
        PreparationModifier = 0;
        JudgmentModifier = 0;
        CurrentRollType = CombatRollType.Attack;
        HasRolled = false;
        LastRollResult = null;
    }

    public void SetDamageLog(
        int damage,
        int beforeHP,
        int afterHP)
    {
        HasDamageLog = true;
        LoggedDamage += Mathf.Max(0, damage);

        if (DamageContexts.Count <= 1)
            LoggedBeforeHP = Mathf.Max(0, beforeHP);

        LoggedAfterHP = Mathf.Max(0, afterHP);
    }

    public void SetAttackWeightTargets(
        IEnumerable<AttackWeightTarget> targets)
    {
        attackWeightTargets.Clear();

        if (targets != null)
        {
            foreach (AttackWeightTarget target
                     in targets)
            {
                if (target == null ||
                    target.TargetCharacter == null)
                {
                    continue;
                }

                attackWeightTargets.Add(
                    target);
            }
        }

        HasResolvedAttackWeightTargets =
            true;
    }

    public void AddAttackWeightHitResult(
        AttackWeightHitResult result)
    {
        if (result == null ||
            result.Target == null ||
            result.DamageContext == null)
        {
            return;
        }

        attackWeightHitResults.Add(
            result);
    }

    public void SetDamageContext(
        DamageContext context)
    {
        LastDamageContext = context;
        LastDamageResult = context?.Result;
        LastDamageEventResult = context?.EventResult;

        if (context == null)
            return;

        bool isNewContext =
            !DamageContexts.Contains(
                context);

        if (!isNewContext)
            return;

        DamageContexts.Add(
            context);

        SetDamageLog(
            context.GetDisplayDamage(),
            context.GetPrimaryHpBefore(),
            context.GetPrimaryHpAfter());
    }
}