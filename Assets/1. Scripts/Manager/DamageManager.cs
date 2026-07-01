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
        if (!IsValidAction(action))
            return 0;

        int rawDamage =
            CalculateDamage(action);

        DamageContext context = new DamageContext
        {
            Action = action,

            Attacker = action.Owner,
            Target = action.Target,

            TargetPart = action.TargetPart,

            RawDamage = rawDamage,
            ModifiedDamage = rawDamage,
            FinalDamage = rawDamage,

            CanBreakPart = ShouldBreakPart(action)
        };

        //--------------------------------
        // 공격자 쪽 데미지 보정
        //--------------------------------

        if (action.Owner.Mechanics != null)
        {
            foreach (CombatMechanic mechanic in action.Owner.Mechanics)
            {
                if (mechanic == null)
                    continue;

                context.ModifiedDamage =
                    mechanic.ModifyDamageDealt(
                        context,
                        context.ModifiedDamage);
            }
        }

        //--------------------------------
        // 피격자 쪽 데미지 보정
        //--------------------------------

        if (action.Target.Mechanics != null)
        {
            foreach (CombatMechanic mechanic in action.Target.Mechanics)
            {
                if (mechanic == null)
                    continue;

                context.ModifiedDamage =
                    mechanic.ModifyDamageTaken(
                        context,
                        context.ModifiedDamage);
            }
        }

        context.FinalDamage =
            Mathf.Max(0, context.ModifiedDamage);

        //--------------------------------
        // 최종 피해 적용
        //--------------------------------

        if (context.FinalDamage > 0)
        {
            action.Target.TakeDamage(
                action.TargetPart,
                context.FinalDamage,
                context.CanBreakPart);
        }

        //--------------------------------
        // 데미지 처리 완료 이벤트
        //--------------------------------

        if (battleContext != null &&
            battleContext._battleEvent != null)
        {
            battleContext._battleEvent.RaiseDamageResolved(
                context);
        }

        return context.FinalDamage;
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
        if (action == null)
            return 0;

        float skillMultiplier =
            GetSkillMultiplier(action);

        // 도사림 / 위세처럼 스킬 Execute에서 직접 처리하는 스킬은
        // DamageManager 자동 피해를 주지 않음
        if (skillMultiplier <= 0f)
            return 0;

        float damage = action.RolledPower;

        damage *= action.Owner.CurrentStatus.damageMultiplier;

        if (momentumManager != null)
        {
            damage *= momentumManager.GetDamageMultiplier(
                action.Owner);
        }

        damage *= skillMultiplier;

        damage = ApplyDefense(
            action,
            damage);

        damage = Mathf.Max(1f, damage);

        return Mathf.RoundToInt(damage);
    }

    //------------------------------------------------
    // 스킬별 데미지 배율
    //------------------------------------------------

    private float GetSkillMultiplier(BattleAction action)
    {
        if (action == null)
            return 0f;

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
    // 부위 파괴 가능 여부
    //------------------------------------------------

    private bool ShouldBreakPart(BattleAction action)
    {
        if (action == null)
            return false;

        if (action.Skill == null)
            return false;

        if (action.ActionType == ActionType.Prestige)
            return true;

        if (momentumManager == null)
            return false;

        // 기본 전투 룰:
        // 짓눌림 상태에서만 일반 피해가 약화 부위를 파괴 가능
        return momentumManager.IsOverwhelm(
            action.Owner);
    }

    //------------------------------------------------
    // 방어도
    //------------------------------------------------

    private float ApplyDefense(
        BattleAction action,
        float damage)
    {
        if (action == null)
            return damage;

        if (action.Target == null ||
            action.Target.RuntimeStatus == null)
            return damage;

        if (action.Owner == null ||
            action.Owner.CurrentStatus == null)
            return damage;

        RuntimeStatus runtime =
            action.Target.RuntimeStatus;

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