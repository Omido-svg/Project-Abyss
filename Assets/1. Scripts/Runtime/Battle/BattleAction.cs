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
                SpeedModifier +
                MomentumModifier;
        }
    }

    public bool HasRolled;
    public RollResult LastRollResult;

    public List<RollResult> RollHistory { get; } = new();
    public List<DamageContext> DamageContexts { get; } = new();

    private RollResult cachedActionRollResult;

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
        cachedActionRollResult = null;

        HasDamageLog = false;
        LoggedDamage = 0;
        LoggedBeforeHP = 0;
        LoggedAfterHP = 0;
        LastDamageContext = null;
        LastDamageResult = null;
        LastDamageEventResult = null;
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

        if (Skill == null)
            return 0;

        bool reuse =
            Skill.RollReusePolicy ==
                SkillRollReusePolicy.OncePerAction &&
            cachedActionRollResult != null;

        LastRollResult = reuse
            ? cachedActionRollResult.Clone()
            : Skill.RollPowerResult();

        if (LastRollResult == null)
        {
            SetPurePower(Skill.BasePower);
            HasRolled = true;
            return RolledPower;
        }

        if (reuse)
        {
            LastRollResult.WasReused = true;
            LastRollResult.ClearClashModifiers();
            SetPurePower(LastRollResult.FinalPower);
        }
        else
        {
            int modifiedRoll = LastRollResult.RawValue;

            if (Owner != null)
            {
                modifiedRoll = Owner.ModifyRoll(
                    this,
                    LastRollResult.RawValue);
            }

            LastRollResult.BasePower = Skill.BasePower;
            LastRollResult.SetModifiedValue(modifiedRoll);
            LastRollResult.ClearClashModifiers();
            SetPurePower(LastRollResult.FinalPower);

            if (Skill.RollReusePolicy ==
                SkillRollReusePolicy.OncePerAction)
            {
                cachedActionRollResult =
                    LastRollResult.Clone();
            }
        }

        HasRolled = true;
        RollHistory.Add(LastRollResult.Clone());

        Debug.Log(
            $"[BattleAction] Exchange Roll / " +
            $"ActionId={ActionId}, " +
            $"Exchange={exchangeIndex}, " +
            $"Owner={Owner?.Data?.CharacterName}, " +
            $"Skill={Skill?.SkillName}, " +
            $"Type={LastRollResult.ResolverType}, " +
            $"Raw={LastRollResult.RawValue}, " +
            $"Modified={LastRollResult.ModifiedValue}, " +
            $"FinalPower={RolledPower}, " +
            $"Critical={Critical}, " +
            $"Reused={LastRollResult.WasReused}, " +
            $"Display={LastRollResult.GetShortDisplayText()}");

        return RolledPower;
    }

    public void ApplyClashModifiers(
        int speedModifier,
        int momentumModifier)
    {
        SpeedModifier = speedModifier;
        MomentumModifier = momentumModifier;

        ClashPower =
            RolledPower +
            SpeedModifier +
            MomentumModifier;

        LastRollResult?.SetClashModifiers(
            SpeedModifier,
            MomentumModifier);
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

    public void SetDamageContext(
        DamageContext context)
    {
        LastDamageContext = context;
        LastDamageResult = context?.Result;
        LastDamageEventResult = context?.EventResult;

        if (context == null)
            return;

        if (!DamageContexts.Contains(context))
            DamageContexts.Add(context);

        SetDamageLog(
            context.GetDisplayDamage(),
            context.GetPrimaryHpBefore(),
            context.GetPrimaryHpAfter());
    }
}
