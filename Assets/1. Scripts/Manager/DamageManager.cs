using UnityEngine;

public class DamageManager
{
    private readonly MomentumManager momentumManager;

    public DamageManager(
        BattleContext battleContext,
        MomentumManager momentumManager)
    {
        this.momentumManager = momentumManager;
    }

    //------------------------------------------------

    public int ApplyDamage(BattleAction action)
    {
        if (!IsValidAction(action))
            return 0;

        //------------------------------------
        // 아직 위력 굴림이 안 되어 있으면 굴림
        //------------------------------------

        if (!action.HasRolled)
        {
            action.RolledPower = action.RollPower();
            action.finalPower = action.RolledPower;
            action.HasRolled = true;
        }
        else
        {
            // 이미 굴린 값이 있는데 finalPower가 비어 있으면 보정
            if (action.finalPower == 0)
                action.finalPower = action.RolledPower;
        }

        //------------------------------------
        // 데미지 계산
        //------------------------------------

        int damage = CalculateDamage(action);

        Debug.Log(
            $"{action.Owner.Data.CharacterName}의 {action.OwnerPart.Type}가 " +
            $"{action.Target.Data.CharacterName}의 {action.TargetPart.Type} 부위에 " +
            $"{damage}만큼의 피해를 줌");

        //------------------------------------
        // 짓눌림 여부
        //------------------------------------

        bool canBreakPart =
            momentumManager.IsOverwhelm(action.Owner);

        //------------------------------------
        // 실제 피해 적용
        //------------------------------------

        action.Target.TakeDamage(
            action.TargetPart,
            damage,
            canBreakPart);

        return damage;
    }

    //------------------------------------------------

    private bool IsValidAction(BattleAction action)
    {
        if (action == null)
            return false;

        if (action.Owner == null)
            return false;

        if (action.Target == null)
            return false;

        if (action.OwnerPart == null)
            return false;

        if (action.TargetPart == null)
            return false;

        if (action.Skill == null)
            return false;

        if (action.Owner.IsDead)
            return false;

        if (action.Target.IsDead)
            return false;

        if (action.OwnerPart.IsBroken)
            return false;

        if (action.TargetPart.IsBroken)
            return false;

        return true;
    }

    //------------------------------------------------

    private int CalculateDamage(BattleAction action)
    {
        float damage = action.RolledPower;

        damage *= action.Owner.CurrentStatus.damageMultiplier;

        // 기세 배율은 공격자 기준으로 한 번만 적용
        damage *= momentumManager.GetDamageMultiplier(action.Owner);

        damage *= GetSkillMultiplier(action);

        damage = ApplyDefense(action, damage);

        damage = Mathf.Max(1f, damage);

        return Mathf.RoundToInt(damage);
    }

    //------------------------------------------------
    // 스킬별 데미지 배율
    //------------------------------------------------

    private float GetSkillMultiplier(BattleAction action)
    {
        switch (action.ActionType)
        {
            case ActionType.Prestige:
                return 0f;

            case ActionType.Preparation:
                return 0f;

            case ActionType.NormalAttack:
                return 1f;

            case ActionType.Duel:
                return 1f;

            default:
                return 1f;
        }
    }

    //------------------------------------------------
    // 방어도
    //------------------------------------------------

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