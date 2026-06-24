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

    public void ApplyDamage(BattleAction action)
    {
        int damage;
        
        if (action.IsPreparation)
            damage = action.RollPower();
        else
            damage = CalculateDamage(action);

        action.Target.TakeDamage(
            action.TargetPart,
            damage);
    }

    //------------------------------------------------

    private int CalculateDamage(BattleAction action)
    {
        // 1. 스킬 위력
        float damage = action.RollPower();

        // 2. 최종 피해 증가
        damage *= action.Owner.CurrentStatus.damageMultiplier;

        // 3. 기세 배율
        damage *= momentumManager.GetDamageMultiplier(action.Owner);

        // 4. 방어도(Block)
        RuntimeStatus runtime = action.Target.RuntimeStatus;

        float ignore =
            Mathf.Clamp01(
                action.Owner.CurrentStatus.defensePenetration);

        // 방어도를 무시하고 바로 들어가는 피해
        float ignoreDamage = damage * ignore;

        // Block으로 막을 수 있는 피해
        float blockableDamage = damage - ignoreDamage;

        // 실제 막은 피해
        float blocked =
            Mathf.Min(runtime.currentBlock, blockableDamage);

        runtime.currentBlock -= Mathf.RoundToInt(blocked);

        // 최종 피해
        damage =
            ignoreDamage +
            (blockableDamage - blocked);

        //--------------------------------

        if (damage < 1f)
            damage = 1f;

        return Mathf.RoundToInt(damage);
    }
}