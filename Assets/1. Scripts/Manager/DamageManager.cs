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

        if (skillMultiplier <= 0f)
            return 0;

        float damage =
            action.RolledPower;

        //--------------------------------
        // 공격력 보너스
        //--------------------------------

        damage += action.Owner.CurrentStatus.flatDamageBonus;

        //--------------------------------
        // 피해 배율
        //--------------------------------

        damage *= action.Owner.CurrentStatus.damageMultiplier;

        if (momentumManager != null)
        {
            damage *=
                momentumManager.GetDamageMultiplier(
                    action.Owner);
        }

        damage *= skillMultiplier;

        //--------------------------------
        // 방어력 / 방어도 / 방어무시율
        //--------------------------------

        damage =
            ApplyDefense(
                action,
                damage);

        return Mathf.Max(
            0,
            Mathf.RoundToInt(damage));
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

        if (damage <= 0f)
            return 0f;

        if (action.Target == null ||
            action.Target.RuntimeStatus == null ||
            action.Target.CurrentStatus == null)
        {
            return damage;
        }

        if (action.Owner == null ||
            action.Owner.CurrentStatus == null)
        {
            return damage;
        }

        CurrentStatus attackerStatus =
            action.Owner.CurrentStatus;

        CurrentStatus targetStatus =
            action.Target.CurrentStatus;

        RuntimeStatus targetRuntime =
            action.Target.RuntimeStatus;

        //--------------------------------
        // 1. 방어 무시율 계산
        //--------------------------------

        float penetrationRate =
            Mathf.Clamp01(
                attackerStatus.defensePenetrationRate);

        if (action.Skill != null)
        {
            penetrationRate =
                Mathf.Clamp01(
                    penetrationRate + action.Skill.IgnoreBlock);
        }

        //--------------------------------
        // 2. 방어를 무시하는 피해와
        //    방어 가능한 피해로 분리
        //--------------------------------

        float piercingDamage =
            damage * penetrationRate;

        float blockableDamage =
            damage - piercingDamage;

        //--------------------------------
        // 3. 방어력 적용
        // 방어력은 방어 가능한 피해만 감소시킴
        //--------------------------------

        float afterDefenseDamage =
            Mathf.Max(
                0f,
                blockableDamage - targetStatus.defense);

        //--------------------------------
        // 4. 임시 방어도 / 보호막 적용
        //--------------------------------

        float blockedDamage =
            Mathf.Min(
                targetRuntime.currentBlock,
                afterDefenseDamage);

        targetRuntime.currentBlock -=
            Mathf.RoundToInt(blockedDamage);

        //--------------------------------
        // 5. 최종 피해
        //--------------------------------

        float finalDamage =
            piercingDamage +
            (afterDefenseDamage - blockedDamage);

        return Mathf.Max(0f, finalDamage);
    }
}