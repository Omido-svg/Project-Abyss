using UnityEngine;

/// <summary>
/// 올라프 위세 3형 「배수진」.
/// 발동한 턴 동안 전신 HP와 부위 HP가 1 아래로 내려가지 않고
/// 부위 파괴와 사망을 막는다.
/// </summary>
public sealed class OlafImmortalFuryMechanic :
    CombatMechanic
{
    private bool isActive;

    public bool IsActive => isActive;

    public override string MechanicName =>
        "Olaf Backs To Wall";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");
    }

    public override void OnUnregister()
    {
        isActive = false;
    }

    public void Activate(
        BattleAction sourceAction)
    {
        if (owner == null ||
            owner.IsDead)
        {
            return;
        }

        isActive = true;
    }

    public override int ModifyDamageTaken(
        DamageContext context,
        int damage)
    {
        if (!isActive ||
            context?.Target != owner ||
            damage <= 0)
        {
            return damage;
        }

        int maximum =
            Mathf.Max(
                0,
                owner.CurrentHP - 1);

        if (context.TargetPart != null &&
            !context.TargetPart.IsBroken)
        {
            maximum = Mathf.Min(
                maximum,
                Mathf.Max(
                    0,
                    Mathf.CeilToInt(
                        context.TargetPart.PartHP) - 1));
        }

        return Mathf.Min(
            damage,
            maximum);
    }

    public override bool CanBreakOwnerPart(
        BodyPart part,
        BattleAction sourceAction)
    {
        return !isActive;
    }

    public override bool CanOwnerDie()
    {
        return !isActive;
    }

    private void OnExchangeResolved(
        ClashExchangeResult exchange)
    {
        if (!isActive ||
            exchange == null ||
            exchange.IsOneSided ||
            exchange.WasCancelled ||
            exchange.LoserAction?.Owner != owner ||
            exchange.DamageContext == null)
        {
            return;
        }

        owner.GetMechanic<OlafMadnessMechanic>()
            ?.AddMadness(1);
    }

    private void OnTurnEnd(int turn)
    {
        isActive = false;
    }
}
