using System.Linq;
using UnityEngine;

public class CharacterBodyPartController
{
    private readonly Character owner;

    public CharacterBodyPartController(Character owner)
    {
        this.owner = owner;
    }

    public void WeakenPart(BodyPart part)
    {
        if (owner == null)
            return;

        if (part == null)
            return;

        if (part.IsBroken)
            return;

        if (part.IsWeakened)
            return;

        part.Weaken();

        owner.BattleEvent?.RaiseBodyPartWeakened(
            owner,
            part);
    }

    public bool TryBreakWeakenedPart(BodyPart part)
    {
        if (owner == null)
            return false;

        if (part == null)
            return false;

        if (part.IsBroken)
            return false;

        if (!part.IsWeakened)
        {
            Debug.Log(
                $"{owner.Data.CharacterName} {part.Type} 부위는 약화 상태가 아니므로 파괴할 수 없습니다.");

            return false;
        }

        BreakPartInternal(part);
        return true;
    }

    public void ForceBreakPart(BodyPart part)
    {
        if (owner == null)
            return;

        if (part == null)
            return;

        if (part.IsBroken)
            return;

        BreakPartInternal(part);
    }

    private void BreakPartInternal(BodyPart part)
    {
        if (part == null)
            return;

        if (part.IsBroken)
            return;

        Debug.LogWarning(
            $"[BREAK BEFORE] {owner.Data.CharacterName} {part.Type} " +
            $"State={part.State}, HP={part.PartHP}/{part.MaxPartHP}");

        PrintActionSlotsOfPart(
            "[BREAK SLOT BEFORE]",
            part);

        RemoveActionSlotsOfPart(part);

        PrintActionSlotsOfPart(
            "[BREAK SLOT AFTER]",
            part);

        int remainingPartHP =
            Mathf.Max(
                0,
                Mathf.RoundToInt(part.PartHP));

        if (remainingPartHP > 0)
        {
            owner.ReduceCurrentHP(
                remainingPartHP);
        }

        part.Break();

        owner.TransferPartStatusesToCharacter(part);

        owner.OnBodyPartBroken(part, null);

        owner.BattleEvent?.RaiseBodyPartDestroyed(
            owner,
            part);

        Debug.LogWarning(
            $"[BREAK AFTER] {owner.Data.CharacterName} {part.Type} " +
            $"State={part.State}, HP={part.PartHP}/{part.MaxPartHP}");

        owner.CheckDead();
    }
    
    private void PrintActionSlotsOfPart(
        string title,
        BodyPart part)
    {
        ActionManager actionManager =
            GetActionManager();

        if (actionManager == null)
        {
            Debug.LogWarning($"{title} ActionManager NULL");
            return;
        }

        Debug.Log(
            $"{title} {owner.Data.CharacterName} / {part.Type}");

        int count = 0;

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != owner)
                continue;

            if (slot.Part != part)
                continue;

            count++;

            Debug.Log(
                $"  Slot Found : " +
                $"Owner={slot.Owner.Data.CharacterName}, " +
                $"Part={slot.Part.Type}, " +
                $"Skill={slot.Skill?.SkillName}, " +
                $"Phase={slot.Phase}");
        }

        Debug.Log($"{title} Count = {count}");
    }
    
    private ActionManager GetActionManager()
    {
        if (owner == null)
            return null;

        BattleContext context =
            owner.BattleContext;

        if (context == null)
            return null;

        if (context.battleManager == null)
            return null;

        return context.battleManager.ActionManager;
    }

    public void RecoverPart(BodyPart part)
    {
        if (owner == null)
            return;

        if (part == null)
            return;

        if (!part.IsBroken && !part.IsWeakened)
            return;

        foreach (StatusEffect effect in part.StatusEffects.ToArray())
        {
            part.RemoveStatus(effect);

            owner.BattleEvent?.RaiseBodyPartStatusRemoved(
                owner,
                part,
                effect);
        }

        part.Recover();

        owner.BattleEvent?.RaiseBodyPartRecovered(
            owner,
            part);

        owner.ForceRecalculateHP();

        Debug.Log(
            $"{owner.Data.CharacterName} {part.Type} 부위 회복");
    }
    
    private void RemoveActionSlotsOfPart(BodyPart part)
    {
        if (part == null)
            return;

        ActionManager actionManager =
            GetActionManager();

        if (actionManager == null)
        {
            Debug.LogWarning("[REMOVE SLOT] ActionManager NULL");
            return;
        }

        int removeCount = 0;

        while (true)
        {
            ActionSlot slot =
                actionManager.FindSlot(
                    owner,
                    part);

            if (slot == null)
                break;

            Debug.Log(
                $"[REMOVE SLOT] " +
                $"{owner.Data.CharacterName} / {part.Type} / " +
                $"{slot.Skill?.SkillName}");

            actionManager.RemoveSlot(
                owner,
                part);

            removeCount++;
        }

        Debug.Log(
            $"[REMOVE SLOT RESULT] " +
            $"{owner.Data.CharacterName} / {part.Type} / Removed={removeCount}");
    }
}