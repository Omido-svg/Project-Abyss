using UnityEngine;

/// <summary>
/// 표준 대미지 계산 파이프라인.
///
/// 피해 기준 위력
/// → 스킬·기세 배율
/// → 공격자 보정
/// → 가드
/// → 대상 보정
/// → 보호
/// → 최종 피해
///
/// 크리티컬은 굴림 단계에서 이미 높은 위력으로 반영되며,
/// 별도의 공격력·방어력·관통력 보조 계산 경로는 사용하지 않습니다.
/// </summary>
public sealed class DamagePipeline
{
    private readonly MomentumManager momentumManager;

    public DamagePipeline(
        MomentumManager momentumManager)
    {
        this.momentumManager = momentumManager;
    }

    public void Calculate(
        DamageContext context)
    {
        if (context == null)
            return;

        CalculateBaseDamage(context);
        ApplyAttackerModifiers(context);
        ApplyGuard(context);
        ApplyTargetModifiers(context);
        ApplyProtection(context);
        FinalizeDamage(context);
    }

    private void CalculateBaseDamage(
        DamageContext context)
    {
        context.RecordStage(
            DamageStage.DamagePower,
            context.RawPower);

        context.MomentumMultiplier =
            context.Request.ApplyMomentum &&
            momentumManager != null
                ? momentumManager.GetDamageMultiplier(
                    context.Attacker)
                : 1f;

        float scaledDamage =
            context.RawPower *
            context.SkillMultiplier *
            context.MomentumMultiplier;

        int damage =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    scaledDamage));

        // 유효한 공격 굴림은 모든 배율 적용 후에도 최소 1 피해를 보장합니다.
        if (context.RawPower > 0 &&
            context.SkillMultiplier > 0f &&
            damage <= 0)
        {
            damage = 1;
        }

        context.BaseDamage = damage;

        context.RecordStage(
            DamageStage.BaseDamage,
            context.BaseDamage);
    }

    private static void ApplyAttackerModifiers(
        DamageContext context)
    {
        int damage =
            context.BaseDamage;

        if (context.Request.ApplyAttackerModifiers &&
            context.Attacker?.Mechanics != null)
        {
            foreach (CombatMechanic mechanic
                     in context.Attacker.Mechanics)
            {
                if (mechanic == null)
                    continue;

                damage =
                    mechanic.ModifyDamageDealt(
                        context,
                        damage);

                damage = Mathf.Max(
                    0,
                    damage);
            }
        }

        context.AttackerModifiedDamage =
            Mathf.Max(
                0,
                damage);

        context.RecordStage(
            DamageStage.AttackerModifiers,
            context.AttackerModifiedDamage);
    }

    private static void ApplyGuard(
        DamageContext context)
    {
        RuntimeStatus runtime =
            context.Target?.RuntimeStatus;

        context.GuardBefore =
            context.Request.ApplyGuard &&
            runtime != null
                ? Mathf.Max(
                    0,
                    runtime.currentBlock)
                : 0;

        context.GuardAbsorbed =
            Mathf.Min(
                context.GuardBefore,
                context.AttackerModifiedDamage);

        context.GuardAfter =
            Mathf.Max(
                0,
                context.GuardBefore -
                context.GuardAbsorbed);

        context.DamageAfterGuard =
            Mathf.Max(
                0,
                context.AttackerModifiedDamage -
                context.GuardAbsorbed);

        context.RecordStage(
            DamageStage.Guard,
            context.DamageAfterGuard);
    }

    private static void ApplyTargetModifiers(
        DamageContext context)
    {
        int damage =
            context.DamageAfterGuard;

        if (context.Request.ApplyTargetModifiers &&
            context.Target?.Mechanics != null)
        {
            foreach (CombatMechanic mechanic
                     in context.Target.Mechanics)
            {
                if (mechanic == null)
                    continue;

                damage =
                    mechanic.ModifyDamageTaken(
                        context,
                        damage);

                damage = Mathf.Max(
                    0,
                    damage);
            }
        }

        context.TargetModifiedDamage =
            Mathf.Max(
                0,
                damage);

        context.RecordStage(
            DamageStage.TargetModifiers,
            context.TargetModifiedDamage);
    }

    private static void ApplyProtection(
        DamageContext context)
    {
        int incomingDamage =
            context.TargetModifiedDamage;

        if (!context.Request.ApplyProtection)
            context.ProtectionValue = 0;

        context.ProtectionValue =
            Mathf.Max(
                0,
                context.ProtectionValue);

        context.ProtectionAbsorbed =
            Mathf.Min(
                context.ProtectionValue,
                incomingDamage);

        context.DamageAfterProtection =
            Mathf.Max(
                0,
                incomingDamage -
                context.ProtectionAbsorbed);

        context.RecordStage(
            DamageStage.Protection,
            context.DamageAfterProtection);
    }

    private static void FinalizeDamage(
        DamageContext context)
    {
        context.FinalDamage =
            Mathf.Max(
                0,
                context.DamageAfterProtection);

        context.RecordStage(
            DamageStage.Finalized,
            context.FinalDamage);
    }
}
