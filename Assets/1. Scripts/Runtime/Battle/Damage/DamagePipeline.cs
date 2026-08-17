using UnityEngine;

/// <summary>
/// 표준 대미지 계산 파이프라인.
///
/// 피해 기준 위력
/// → 명시적 피해 계수·기세 배율
/// → 공격자 보정
/// → 대상 보정
/// → 가드 흡수
/// → 최종 피해
///
/// ActionType별 피해 배율과 별도 보호 단계는 사용하지 않습니다.
/// 크리티컬은 굴림 단계에서 이미 높은 위력으로 반영됩니다.
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
        ApplyTargetModifiers(context);
        ApplyGuard(context);
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
            context.DamageCoefficient *
            context.MomentumMultiplier;

        int damage =
            Mathf.Max(
                0,
                Mathf.FloorToInt(
                    scaledDamage));

        // 유효한 피해 요청은 모든 계수 적용 후에도 최소 1 피해를 보장합니다.
        if (context.RawPower > 0 &&
            context.DamageCoefficient > 0f &&
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
        int damage = context.BaseDamage;

        if (context.Request.ApplyAttackerModifiers &&
            context.Attacker != null)
        {
            foreach (StatusEffect effect in context.Attacker.StatusEffects)
            {
                if (effect == null)
                    continue;

                damage = Mathf.Max(
                    0,
                    effect.ModifyDamage(context.Action, damage));
            }

            if (context.Action?.OwnerPart != null)
            {
                foreach (StatusEffect effect in context.Action.OwnerPart.StatusEffects)
                {
                    if (effect == null)
                        continue;

                    damage = Mathf.Max(
                        0,
                        effect.ModifyDamage(context.Action, damage));
                }
            }

            if (context.Attacker.Mechanics != null)
            {
                foreach (CombatMechanic mechanic in context.Attacker.Mechanics)
                {
                    if (mechanic == null)
                        continue;

                    damage = Mathf.Max(
                        0,
                        mechanic.ModifyDamageDealt(context, damage));
                }
            }
        }

        context.AttackerModifiedDamage = Mathf.Max(0, damage);
        context.RecordStage(
            DamageStage.AttackerModifiers,
            context.AttackerModifiedDamage);
    }

    private static void ApplyTargetModifiers(
        DamageContext context)
    {
        int damage = context.AttackerModifiedDamage;

        if (context.Request.ApplyTargetModifiers &&
            context.Target != null)
        {
            foreach (StatusEffect effect in context.Target.StatusEffects)
            {
                if (effect == null)
                    continue;

                damage = Mathf.Max(
                    0,
                    Mathf.FloorToInt(
                        effect.ModifyDamageTaken(context.Action, damage)));
            }

            if (context.TargetPart != null)
            {
                foreach (StatusEffect effect in context.TargetPart.StatusEffects)
                {
                    if (effect == null)
                        continue;

                    damage = Mathf.Max(
                        0,
                        Mathf.FloorToInt(
                            effect.ModifyDamageTaken(context.Action, damage)));
                }
            }

            if (context.Target.Mechanics != null)
            {
                foreach (CombatMechanic mechanic in context.Target.Mechanics)
                {
                    if (mechanic == null)
                        continue;

                    damage = Mathf.Max(
                        0,
                        mechanic.ModifyDamageTaken(context, damage));
                }
            }
        }

        context.PhysicalResistanceMultiplier = 1f;
        if (context.Request.ApplyPhysicalResistance &&
            context.Target?.Data?.PhysicalResistances != null)
        {
            context.PhysicalResistanceMultiplier =
                context.Target.Data.PhysicalResistances.GetMultiplier(
                    context.PhysicalType);

            damage = Mathf.Max(
                0,
                Mathf.FloorToInt(
                    damage * context.PhysicalResistanceMultiplier));
        }

        context.TargetModifiedDamage = Mathf.Max(0, damage);
        context.RecordStage(
            DamageStage.TargetModifiers,
            context.TargetModifiedDamage);
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
                context.TargetModifiedDamage);

        context.GuardAfter =
            Mathf.Max(
                0,
                context.GuardBefore -
                context.GuardAbsorbed);

        context.DamageAfterGuard =
            Mathf.Max(
                0,
                context.TargetModifiedDamage -
                context.GuardAbsorbed);

        context.RecordStage(
            DamageStage.Guard,
            context.DamageAfterGuard);
    }

    private static void FinalizeDamage(
        DamageContext context)
    {
        context.FinalDamage =
            Mathf.Max(
                0,
                context.DamageAfterGuard);

        context.RecordStage(
            DamageStage.Finalized,
            context.FinalDamage);
    }
}
