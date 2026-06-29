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

        int damage = CalculateDamage(action);

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
        // 1. 기본 데미지
        float damage = action.RolledPower;

        // 2. 캐릭터 배율
        damage *= action.Owner.CurrentStatus.damageMultiplier;

        // 3. 기세
        damage *=
            momentumManager.GetDamageMultiplier(action.Owner);

        damage *=
            momentumManager.GetDamageTakenMultiplier(action.Target);

        // 4. 스킬 보정
        damage *= GetSkillMultiplier(action);

        // 5. 방어도
        damage = ApplyDefense(action, damage);

        // 6. 최소 데미지
        damage = Mathf.Max(1f, damage);

        return Mathf.RoundToInt(damage);
    }

    // 스킬별 배율
    private float GetSkillMultiplier(BattleAction action)
    {
        switch (action.ActionType)
        {
            case ActionType.Preparation:
                return 1f;

            case ActionType.NormalAttack:
                return 1f;

            case ActionType.Duel:
                return 1f;

            default:
                return 1f;
        }
    }

    // 방어도
    private float ApplyDefense(
        BattleAction action,
        float damage)
    {
        RuntimeStatus runtime = action.Target.RuntimeStatus;

        float ignore =
            Mathf.Clamp01(
                action.Owner.CurrentStatus.defensePenetration);

        float ignoreDamage =
            damage * ignore;

        float blockableDamage =
            damage - ignoreDamage;

        float blocked =
            Mathf.Min(
                runtime.currentDefensePenetration,
                blockableDamage);

        runtime.currentDefensePenetration -=
            Mathf.RoundToInt(blocked);

        return ignoreDamage +
               (blockableDamage - blocked);
    }
}