using System;
using UnityEngine;

public abstract class CombatMechanic : IBattleEventListener
{
    protected Character owner;
    protected BattleContext battleContext;
    protected BattleEvent battleEvent;

    private readonly BattleEventSubscriptionGroup subscriptions = new();

    public virtual string MechanicName =>
        GetType().Name;

    public Character Owner => owner;
    public bool IsInitialized { get; private set; }
    public bool IsRegistered { get; private set; }
    public bool IsSubscribed => IsRegistered;

    public virtual void Initialize(
        Character owner,
        BattleContext battleContext)
    {
        if (IsRegistered ||
            subscriptions.Count > 0)
        {
            Unregister();
        }

        this.owner = owner;
        this.battleContext = battleContext;
        battleEvent = battleContext?._battleEvent;

        IsInitialized =
            owner != null &&
            battleContext != null &&
            battleEvent != null &&
            !battleEvent.IsDisposed;
    }

    public void Subscribe()
    {
        Register();
    }

    public void Unsubscribe()
    {
        Unregister();
    }

    public void Register()
    {
        TryRegister();
    }

    public bool TryRegister()
    {
        if (IsRegistered)
            return true;

        if (!IsInitialized ||
            owner == null)
        {
            Debug.LogWarning(
                $"{MechanicName} Register 실패 : " +
                "owner가 null이거나 초기화되지 않았습니다.");
            return false;
        }

        if (battleContext == null ||
            battleEvent == null ||
            battleEvent.IsDisposed)
        {
            Debug.LogWarning(
                $"{MechanicName} Register 실패 : " +
                "BattleContext 또는 BattleEvent가 유효하지 않습니다.");
            return false;
        }

        subscriptions.Clear();

        try
        {
            OnRegister();
            IsRegistered = true;
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                OnUnregister();
            }
            catch (Exception cleanupException)
            {
                Debug.LogException(cleanupException);
            }
            finally
            {
                subscriptions.Clear();
                IsRegistered = false;
            }

            Debug.LogError(
                $"[{MechanicName}] 이벤트 등록 실패");

            Debug.LogException(exception);
            return false;
        }
    }

    public void Unregister()
    {
        if (!IsRegistered &&
            subscriptions.Count == 0)
        {
            return;
        }

        try
        {
            OnUnregister();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[{MechanicName}] 해제 후처리 실패");

            Debug.LogException(exception);
        }
        finally
        {
            subscriptions.Clear();
            IsRegistered = false;
        }
    }

    public void Release()
    {
        Unregister();

        owner = null;
        battleContext = null;
        battleEvent = null;
        IsInitialized = false;
    }

    protected void SubscribeToBattleEvent(
        Action subscribe,
        Action unsubscribe,
        string label)
    {
        if (battleEvent == null ||
            battleEvent.IsDisposed)
        {
            throw new InvalidOperationException(
                $"{MechanicName}의 BattleEvent가 유효하지 않습니다.");
        }

        subscriptions.Subscribe(
            subscribe,
            unsubscribe,
            $"{MechanicName}.{label}");
    }

    public virtual void OnRegister() { }
    public virtual void OnUnregister() { }

    public virtual int ModifyRoll(
        BattleAction action,
        int roll) => roll;

    public virtual int ModifyDamageDealt(
        DamageContext context,
        int damage) => damage;

    public virtual int ModifyDamageTaken(
        DamageContext context,
        int damage) => damage;

    public virtual int ModifyMomentumShift(
        ClashResultContext context,
        int shift) => shift;

    public virtual int ModifyPrestigeGain(
        ClashResultContext context,
        int prestigeGain) => prestigeGain;

    public virtual void ModifyActionSlotPolicy(
        ActionSlotPolicyContext context)
    {
    }

    public virtual bool CanUseSkill(
        BodyPart part,
        Skill skill) => true;

    public virtual bool CanOwnerDie() => true;
}
