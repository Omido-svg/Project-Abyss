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

    public long ActionId =>
        Slot == null ? 0 : Slot.ActionId;

    public int ActionIndex =>
        Slot == null ? 0 : Slot.ActionIndex;

    public Character Owner =>
        Slot == null ? null : Slot.Owner;

    public Character Target =>
        Slot == null ? null : Slot.TargetCharacter;

    public BodyPart OwnerPart =>
        Slot == null ? null : Slot.Part;

    public BodyPart TargetPart =>
        Slot == null ? null : Slot.TargetPart;

    public Skill Skill =>
        Slot == null ? null : Slot.Skill;

    public int Speed =>
        Slot == null ? 0 : Slot.Speed;

    public ActionPhase Phase =>
        Slot == null ? ActionPhase.COMBAT : Slot.Phase;

    public ActionType ActionType =>
        Skill == null
            ? ActionType.NormalAttack
            : Skill.ActionType;

    //--------------------------------
    // 위력 계약
    //--------------------------------

    // 순수 굴림 위력.
    // 피해 계산은 이 값만 읽어야 한다.
    public int RolledPower;

    // 합 판정 전용 최종값.
    public int ClashPower;

    public int SpeedModifier;
    public int MomentumModifier;

    public bool Critical =>
        LastRollResult != null &&
        LastRollResult.IsCritical;

    // 기존 호출부 호환용 필드.
    // 이제 합 수치가 아니라 순수 굴림 위력만 저장한다.
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

    //--------------------------------
    // 로그 및 연출용 최종 적용 피해
    //--------------------------------

    public bool HasDamageLog;
    public int LoggedDamage;
    public int LoggedBeforeHP;
    public int LoggedAfterHP;

    public DamageContext LastDamageContext;
    public DamageResult LastDamageResult;
    public DamageEventResult LastDamageEventResult;

    public int RollPower()
    {
        ResetRollState();

        if (Skill == null)
            return 0;

        LastRollResult =
            Skill.RollPowerResult();

        if (LastRollResult == null)
        {
            SetPurePower(
                Skill.BasePower);

            HasRolled = true;
            return RolledPower;
        }

        int modifiedRoll =
            LastRollResult.RawValue;

        if (Owner != null)
        {
            modifiedRoll =
                Owner.ModifyRoll(
                    this,
                    LastRollResult.RawValue);
        }

        LastRollResult.BasePower =
            Skill.BasePower;

        LastRollResult.SetModifiedValue(
            modifiedRoll);

        LastRollResult.ClearClashModifiers();

        SetPurePower(
            LastRollResult.FinalPower);

        HasRolled = true;

        Debug.Log(
            $"[BattleAction] RollPower / " +
            $"ActionId={ActionId}, " +
            $"ActionIndex={ActionIndex}, " +
            $"Owner={Owner?.Data?.CharacterName}, " +
            $"Skill={Skill?.SkillName}, " +
            $"Type={LastRollResult.ResolverType}, " +
            $"Raw={LastRollResult.RawValue}, " +
            $"Modified={LastRollResult.ModifiedValue}, " +
            $"FinalPower={RolledPower}, " +
            $"Critical={Critical}, " +
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

    public int GetDamagePower()
    {
        return RolledPower;
    }

    private void SetPurePower(int power)
    {
        RolledPower = power;
        finalPower = power;
        ClashPower = power;
    }

    private void ResetRollState()
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
        LoggedDamage = Mathf.Max(0, damage);
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

        SetDamageLog(
            context.GetDisplayDamage(),
            context.GetPrimaryHpBefore(),
            context.GetPrimaryHpAfter());
    }
}
