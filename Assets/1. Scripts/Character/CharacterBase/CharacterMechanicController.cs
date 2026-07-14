using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterMechanicController : IDisposable
{
    private readonly Character owner;
    private readonly List<CombatMechanic> mechanics = new();

    private bool isDisposed;

    public IReadOnlyList<CombatMechanic> Mechanics =>
        mechanics;

    public bool IsDisposed => isDisposed;

    public CharacterMechanicController(Character owner)
    {
        this.owner = owner;
    }

    public void Reset()
    {
        if (isDisposed)
            return;

        ReleaseAll();
        mechanics.Clear();
    }

    public void Clear()
    {
        Reset();
    }

    public void AddMechanic(
        CombatMechanic mechanic)
    {
        if (isDisposed ||
            mechanic == null ||
            mechanics.Contains(mechanic))
        {
            return;
        }

        mechanics.Add(mechanic);
    }

    public void InitializeAndRegisterAll(
        BattleContext context)
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(
                nameof(CharacterMechanicController));
        }

        UnregisterAll();

        List<CombatMechanic> registered = new();

        try
        {
            foreach (CombatMechanic mechanic in mechanics)
            {
                if (mechanic == null)
                    continue;

                mechanic.Initialize(
                    owner,
                    context);

                if (!mechanic.TryRegister())
                {
                    throw new InvalidOperationException(
                        $"메커닉 등록 실패 : {mechanic.MechanicName}");
                }

                registered.Add(mechanic);
            }
        }
        catch
        {
            for (int i = registered.Count - 1;
                 i >= 0;
                 i--)
            {
                SafeUnregister(registered[i]);
            }

            throw;
        }
    }

    public void UnregisterAll()
    {
        for (int i = mechanics.Count - 1;
             i >= 0;
             i--)
        {
            SafeUnregister(mechanics[i]);
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

            try
            {
                value = mechanic.ModifyRoll(
                    action,
                    value);
            }
            catch (Exception exception)
            {
                LogMechanicException(
                    mechanic,
                    nameof(ModifyRoll),
                    exception);
            }
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

            try
            {
                if (!mechanic.CanUseSkill(part, skill))
                    return false;
            }
            catch (Exception exception)
            {
                LogMechanicException(
                    mechanic,
                    nameof(CanUseSkill),
                    exception);

                // 검증 실패 시 스킬 사용을 허용하지 않는다.
                return false;
            }
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

            try
            {
                mechanic.ModifyActionSlotPolicy(context);
            }
            catch (Exception exception)
            {
                LogMechanicException(
                    mechanic,
                    nameof(ModifyActionSlotPolicy),
                    exception);
            }
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

            try
            {
                reaction.OnBodyPartBrokenBeforeDeath(
                    context);
            }
            catch (Exception exception)
            {
                LogMechanicException(
                    mechanic,
                    nameof(NotifyBodyPartBreakBeforeDeath),
                    exception);
            }
        }
    }

    public bool CanOwnerDie()
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic == null)
                continue;

            try
            {
                if (!mechanic.CanOwnerDie())
                    return false;
            }
            catch (Exception exception)
            {
                LogMechanicException(
                    mechanic,
                    nameof(CanOwnerDie),
                    exception);
            }
        }

        return true;
    }

    public T GetMechanic<T>()
        where T : CombatMechanic
    {
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is T result)
                return result;
        }

        return null;
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        ReleaseAll();
        mechanics.Clear();
        isDisposed = true;
    }

    private void ReleaseAll()
    {
        for (int i = mechanics.Count - 1;
             i >= 0;
             i--)
        {
            CombatMechanic mechanic = mechanics[i];

            if (mechanic == null)
                continue;

            try
            {
                mechanic.Release();
            }
            catch (Exception exception)
            {
                LogMechanicException(
                    mechanic,
                    nameof(ReleaseAll),
                    exception);
            }
        }
    }

    private static void SafeUnregister(
        CombatMechanic mechanic)
    {
        if (mechanic == null)
            return;

        try
        {
            mechanic.Unregister();
        }
        catch (Exception exception)
        {
            LogMechanicException(
                mechanic,
                nameof(SafeUnregister),
                exception);
        }
    }

    private static void LogMechanicException(
        CombatMechanic mechanic,
        string operation,
        Exception exception)
    {
        Debug.LogError(
            "[CharacterMechanicController] 메커닉 예외 격리 / " +
            $"Mechanic={mechanic?.MechanicName}, " +
            $"Operation={operation}");

        Debug.LogException(exception);
    }
}
