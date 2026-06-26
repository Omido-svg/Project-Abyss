using UnityEngine;

public class DamageManager
{
    private readonly BattleContext battleContext;
    private readonly MomentumManager momentumManager;

    public DamageManager(
        BattleContext battleContext,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.momentumManager = momentumManager;
    }

    //------------------------------------------------

    public int ApplyDamage(BattleAction action)
    {
        if (action.RolledPower == 0)
            action.RolledPower = action.RollPower();

        int damage;

        if (action.IsPreparation)
            damage = action.RolledPower;
        else
            damage = CalculateDamage(action);

        bool overwhelm =
            momentumManager.IsOverwhelm(action.Owner);

        action.Target.TakeDamage(
            action.TargetPart,
            damage,
            overwhelm);
            
        return damage;
    }

    //------------------------------------------------

    private int CalculateDamage(BattleAction action)
    {
        // 이미 굴려진 위력 사용
        float damage = action.RolledPower;

        // 최종 피해 증가
        damage *= action.Owner.CurrentStatus.damageMultiplier;

        // 기세 배율
        damage *= momentumManager.GetDamageMultiplier(action.Owner);

        // 방어도(Block)
        RuntimeStatus runtime = action.Target.RuntimeStatus;

        float ignore =
            Mathf.Clamp01(
                action.Owner.CurrentStatus.defensePenetration);

        float ignoreDamage = damage * ignore;

        float blockableDamage = damage - ignoreDamage;

        float blocked =
            Mathf.Min(runtime.currentDefensePenetration, blockableDamage);

        runtime.currentDefensePenetration -= Mathf.RoundToInt(blocked);

        damage =
            ignoreDamage +
            (blockableDamage - blocked);

        //--------------------------------

        if (damage < 1f)
            damage = 1f;

        return Mathf.RoundToInt(damage);
    }
}