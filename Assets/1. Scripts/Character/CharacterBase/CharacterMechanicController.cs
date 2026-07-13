using System.Collections.Generic;
using UnityEngine;

public class CharacterMechanicController
{
    private readonly Character owner;

    private readonly List<CombatMechanic> mechanics =
        new List<CombatMechanic>();

    public IReadOnlyList<CombatMechanic> Mechanics =>
        mechanics;

    public CharacterMechanicController(Character owner)
    {
        this.owner = owner;
    }

    public void Clear()
    {
        mechanics.Clear();
    }

    public void AddMechanic(CombatMechanic mechanic)
    {
        if (mechanic == null)
            return;

        mechanics.Add(mechanic);
    }

    public void InitializeAndRegisterAll(BattleContext context)
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            mechanic.Initialize(owner, context);
            mechanic.Register();
        }
    }

    public void UnregisterAll()
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            mechanic.Unregister();
        }
    }

    public int ModifyRoll(
        BattleAction action,
        int roll)
    {
        int value = roll;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            value = mechanic.ModifyRoll(action, value);
        }

        return value;
    }

    public bool CanUseSkill(
        BodyPart part,
        Skill skill)
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            if (!mechanic.CanUseSkill(part, skill))
                return false;
        }

        return true;
    }

    public void ModifyActionSlotPolicy(
        ActionSlotPolicyContext context)
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            mechanic.ModifyActionSlotPolicy(context);
        }
    }

    public void NotifyBodyPartBreakBeforeDeath(
        BodyPartBreakEventContext context)
    {
        if (context == null)
            return;

        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is not
                IBodyPartBreakImmediateReaction reaction)
            {
                continue;
            }

            reaction.OnBodyPartBrokenBeforeDeath(
                context);
        }
    }

    public bool CanOwnerDie()
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            if (!mechanic.CanOwnerDie())
                return false;
        }

        return true;
    }

    public T GetMechanic<T>() where T : CombatMechanic
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is T result)
                return result;
        }

        return null;
    }
}
