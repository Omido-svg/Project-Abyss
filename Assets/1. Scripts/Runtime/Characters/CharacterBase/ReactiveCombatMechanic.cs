using System;

[Flags]
public enum ReactiveCombatEventMask
{
    None = 0,

    TurnStart = 1 << 0,
    TurnEnd = 1 << 1,

    ActionStart = 1 << 2,
    ActionEnd = 1 << 3,

    ClashWin = 1 << 4,
    ClashLose = 1 << 5,

    DamageResolved = 1 << 6,
    StatusApplied = 1 << 7,
    BodyPartBroken = 1 << 8,
    KillResolved = 1 << 9
}

/// <summary>
/// 아이템/증강용 반응형 메커닉 기반 클래스.
///
/// 전투 후 이벤트:
/// 합 승리/패배, 피해 적용 후, 상태이상 부여 후, 부위 파괴 후,
/// 처치, 턴 시작/종료 등을 선택적으로 구독한다.
///
/// 피해 적용 전 보정은 이벤트가 아니라 CombatMechanic의
/// ModifyDamageDealt / ModifyDamageTaken을 오버라이드해서 처리한다.
/// </summary>
public abstract class ReactiveCombatMechanic : CombatMechanic
{
    protected abstract ReactiveCombatEventMask EventMask { get; }

    public sealed override void OnRegister()
    {
        ReactiveCombatEventMask mask = EventMask;

        if (Has(mask, ReactiveCombatEventMask.TurnStart))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnTurnStart += DispatchTurnStart,
                () => battleEvent.OnTurnStart -= DispatchTurnStart,
                "OnTurnStart");
        }

        if (Has(mask, ReactiveCombatEventMask.TurnEnd))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnTurnEnd += DispatchTurnEnd,
                () => battleEvent.OnTurnEnd -= DispatchTurnEnd,
                "OnTurnEnd");
        }

        if (Has(mask, ReactiveCombatEventMask.ActionStart))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnActionStart += DispatchActionStart,
                () => battleEvent.OnActionStart -= DispatchActionStart,
                "OnActionStart");
        }

        if (Has(mask, ReactiveCombatEventMask.ActionEnd))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnActionEnd += DispatchActionEnd,
                () => battleEvent.OnActionEnd -= DispatchActionEnd,
                "OnActionEnd");
        }

        if (Has(mask, ReactiveCombatEventMask.ClashWin))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnClashWin += DispatchClashWin,
                () => battleEvent.OnClashWin -= DispatchClashWin,
                "OnClashWin");
        }

        if (Has(mask, ReactiveCombatEventMask.ClashLose))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnClashLose += DispatchClashLose,
                () => battleEvent.OnClashLose -= DispatchClashLose,
                "OnClashLose");
        }

        if (Has(mask, ReactiveCombatEventMask.DamageResolved))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnDamageEventResolved += DispatchDamageResolved,
                () => battleEvent.OnDamageEventResolved -= DispatchDamageResolved,
                "OnDamageEventResolved");
        }

        if (Has(mask, ReactiveCombatEventMask.StatusApplied))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnStatusApplyResolved += DispatchStatusApplied,
                () => battleEvent.OnStatusApplyResolved -= DispatchStatusApplied,
                "OnStatusApplyResolved");
        }

        if (Has(mask, ReactiveCombatEventMask.BodyPartBroken))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnBodyPartBreakResolved += DispatchBodyPartBroken,
                () => battleEvent.OnBodyPartBreakResolved -= DispatchBodyPartBroken,
                "OnBodyPartBreakResolved");
        }

        if (Has(mask, ReactiveCombatEventMask.KillResolved))
        {
            SubscribeToBattleEvent(
                () => battleEvent.OnKillResolved += DispatchKillResolved,
                () => battleEvent.OnKillResolved -= DispatchKillResolved,
                "OnKillResolved");
        }

        OnReactiveRegistered();
    }

    public sealed override void OnUnregister()
    {
        OnReactiveUnregistered();
    }

    protected virtual void OnReactiveRegistered()
    {
    }

    protected virtual void OnReactiveUnregistered()
    {
    }

    protected virtual void OnTurnStarted(int turn)
    {
    }

    protected virtual void OnTurnEnded(int turn)
    {
    }

    protected virtual void OnActionStarted(BattleAction action)
    {
    }

    protected virtual void OnActionEnded(BattleAction action)
    {
    }

    protected virtual void OnClashWon(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
    }

    protected virtual void OnClashLost(
        BattleAction loserAction,
        BattleAction winnerAction)
    {
    }

    protected virtual void OnDamageResolved(
        DamageEventResult result)
    {
    }

    protected virtual void OnStatusApplied(
        StatusEffectApplyResult result)
    {
    }

    protected virtual void OnBodyPartBroken(
        BodyPartBreakEventContext context)
    {
    }

    protected virtual void OnKillResolved(
        KillEventContext context)
    {
    }

    protected bool IsOwner(Character character)
    {
        return character != null &&
               character == owner;
    }

    protected bool IsOwnerAction(BattleAction action)
    {
        return action != null &&
               action.Owner == owner;
    }

    protected bool IsOwnerDamageSource(DamageContext context)
    {
        return context != null &&
               context.Attacker == owner;
    }

    protected bool IsOwnerDamageTarget(DamageContext context)
    {
        return context != null &&
               context.Target == owner;
    }

    private static bool Has(
        ReactiveCombatEventMask value,
        ReactiveCombatEventMask flag)
    {
        return (value & flag) != 0;
    }

    private void DispatchTurnStart(int turn)
    {
        OnTurnStarted(turn);
    }

    private void DispatchTurnEnd(int turn)
    {
        OnTurnEnded(turn);
    }

    private void DispatchActionStart(BattleAction action)
    {
        OnActionStarted(action);
    }

    private void DispatchActionEnd(BattleAction action)
    {
        OnActionEnded(action);
    }

    private void DispatchClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        OnClashWon(
            winnerAction,
            loserAction);
    }

    private void DispatchClashLose(
        BattleAction loserAction,
        BattleAction winnerAction)
    {
        OnClashLost(
            loserAction,
            winnerAction);
    }

    private void DispatchDamageResolved(
        DamageEventResult result)
    {
        OnDamageResolved(result);
    }

    private void DispatchStatusApplied(
        StatusEffectApplyResult result)
    {
        OnStatusApplied(result);
    }

    private void DispatchBodyPartBroken(
        BodyPartBreakEventContext context)
    {
        OnBodyPartBroken(context);
    }

    private void DispatchKillResolved(
        KillEventContext context)
    {
        OnKillResolved(context);
    }
}
