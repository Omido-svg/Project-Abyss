using UnityEngine;

public class OlafBloodyAxeMechanic : CombatMechanic
{
    public override string MechanicName => "피 묻은 도끼";

    public override void OnRegister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnDamageResolved += OnDamageResolved;
    }

    public override void OnUnregister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnDamageResolved -= OnDamageResolved;
    }

    private void OnDamageResolved(
        DamageContext context)
    {
        if (context == null)
            return;

        if (context.Attacker != owner)
            return;

        if (!(owner is Olaf))
            return;

        if (context.Target == null)
            return;

        if (context.Action == null ||
            context.Action.Skill == null)
            return;

        if (context.Action.ActionType != ActionType.NormalAttack)
            return;

        if (context.FinalDamage <= 0)
            return;

        context.Target.AddStatus(
            new Bleeding(1),
            owner);

        Debug.Log(
            $"{owner.Data.CharacterName} 아이템 발동 : {MechanicName} / 출혈 1 추가");
    }
}