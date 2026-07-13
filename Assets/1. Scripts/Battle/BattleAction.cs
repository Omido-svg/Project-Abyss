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

    public int RolledPower;

    // 기존 호출부 호환용 필드다.
    public int finalPower;

    public int FinalPower
    {
        get => finalPower;
        set => finalPower = value;
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
        if (Skill == null)
            return 0;

        LastRollResult =
            Skill.RollPowerResult();

        if (LastRollResult == null)
        {
            RolledPower = Skill.BasePower;
            finalPower = RolledPower;
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

        LastRollResult.ModifiedValue =
            modifiedRoll;

        LastRollResult.FinalPower =
            Skill.BasePower + modifiedRoll;

        Debug.Log(
            $"[BattleAction] RollPower / " +
            $"ActionId={ActionId}, " +
            $"ActionIndex={ActionIndex}, " +
            $"Owner={Owner?.Data?.CharacterName}, " +
            $"Skill={Skill?.SkillName}, " +
            $"Type={LastRollResult?.ResolverType}, " +
            $"Raw={LastRollResult?.RawValue}, " +
            $"Modified={LastRollResult?.ModifiedValue}, " +
            $"Final={LastRollResult?.FinalPower}, " +
            $"Display={LastRollResult?.GetShortDisplayText()}");

        return LastRollResult.FinalPower;
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
