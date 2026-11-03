using System;

/// <summary>
/// 1029 Viljar Duel "Single Combat": a target ActionSlot is exclusive at
/// Planning time to this character's other slots. Replacement and cancel do
/// not auto-restore the previous target. Existing ClashBuilder suppression is
/// retained as an additional resolution safety guard, not the main rule.
/// </summary>
public static class R7OlafSingleCombatAimLock20261103
{
    public static bool IsExclusive(Skill skill) =>
        skill?.Definition?.SkillId==OlafSkillIds.SingleCombat &&
        skill.Definition.name=="Olaf_D_21";

    private static bool SameTarget(ActionSlot a,ActionSlot b) =>
        a!=null&&b!=null&&(ReferenceEquals(a,b) ||
            (a.ActionId>0&&a.ActionId==b.ActionId &&
             ReferenceEquals(a.Owner,b.Owner)) || a.HasSameKey(b));

    public static bool IsLockedForOther(ActionManager manager,
        Character owner,ActionSlot candidateTarget,Skill newSkill,
        ActionSlot replacing)
    {
        if(manager?.Slots==null||owner==null||candidateTarget==null)
            return false;
        foreach(var slot in manager.Slots)
        {
            if(slot==null||slot.Owner!=owner||slot==replacing||
               (replacing!=null&&slot.HasSameKey(replacing))||
               !IsExclusive(slot.Skill))continue;
            if(SameTarget(slot.TargetSlot,candidateTarget))
                return true;
        }
        return false;
    }

    public static void ApplyExclusiveAssignment(ActionManager manager,
        ActionSlot assigned)
    {
        if(manager?.Slots==null||assigned?.Owner==null||
           !IsExclusive(assigned.Skill)||assigned.TargetSlot==null)
            return;
        foreach(var other in manager.Slots)
        {
            if(other==null||other==assigned||other.Owner!=assigned.Owner||
               !SameTarget(other.TargetSlot,assigned.TargetSlot))
                continue;
            // Existing planned targets are expressly discarded by canon.
            other.TargetSlot=null;
            other.TargetCharacter=null;
            other.TargetPart=null;
            other.SecondaryTargetPart=null;
        }
    }
}