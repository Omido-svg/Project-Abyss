using System.Linq;
using UnityEngine;

public class CharacterBodyPartController
{
    private readonly Character owner;
    
    // 로그 필요시 true 로 설정
    private const bool VerboseLog = false;

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
        if (owner == null)
            return;

        if (part == null)
            return;

        if (part.IsBroken)
            return;

        LogVerboseWarning(
            $"[BREAK BEFORE] {owner.Data.CharacterName} {part.Type} " +
            $"State={part.State}, HP={part.PartHP}/{part.MaxPartHP}");

        int removedSlotCount =
            RemoveActionSlotsOfPart(part);

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

        LogVerboseWarning(
            $"[BREAK AFTER] {owner.Data.CharacterName} {part.Type} " +
            $"State={part.State}, HP={part.PartHP}/{part.MaxPartHP}, " +
            $"RemovedSlots={removedSlotCount}");

        owner.CheckDead();
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
    
    private int RemoveActionSlotsOfPart(BodyPart part)
    {
        if (owner == null)
            return 0;

        if (part == null)
            return 0;

        ActionManager actionManager =
            GetActionManager();

        if (actionManager == null)
        {
            LogVerboseWarning("[REMOVE SLOT] ActionManager NULL");
            return 0;
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

            LogVerbose(
                $"[REMOVE SLOT] " +
                $"{owner.Data.CharacterName} / {part.Type} / " +
                $"{slot.Skill?.SkillName}");

            actionManager.RemoveSlot(
                owner,
                part);

            removeCount++;
        }

        LogVerbose(
            $"[REMOVE SLOT RESULT] " +
            $"{owner.Data.CharacterName} / {part.Type} / Removed={removeCount}");

        return removeCount;
    }
    
    private void LogVerbose(string message)
    {
        if (!VerboseLog)
            return;

        Debug.Log(message);
    }

    private void LogVerboseWarning(string message)
    {
        if (!VerboseLog)
            return;

        Debug.LogWarning(message);
    }
}