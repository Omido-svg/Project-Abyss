using UnityEngine;

/// <summary>
/// 0915 C-25 표준 HP Damage Pipeline.
/// Raw/coefficient → physical resistance → flat status → character-specific modifiers
/// → floor/minimum 1 → armor(block) → final.
/// Stagger resolution itself is DamageManager/RequiredExchangeReactionPipeline의 선행 단계다.
/// </summary>
public sealed class DamagePipeline
{
    private readonly MomentumManager momentumManager;

    public DamagePipeline(MomentumManager momentumManager)
    {
        this.momentumManager = momentumManager;
    }

    public void Calculate(DamageContext context)
    {
        if (context == null)
            return;

        CalculateBaseDamage(context);
        ApplyPhysicalResistance(context);
        ApplyFlatTargetModifiers(context);
        ApplyCharacterSpecificModifiers(context);
        ApplyMinimumDamage(context);
        ApplyGuard(context);
        FinalizeDamage(context);
    }

    private static void CalculateBaseDamage(DamageContext context)
    {
        context.RecordStage(DamageStage.DamagePower, context.RawPower);
        context.MomentumMultiplier = 1f;

        float scaled = context.RawPower * context.DamageCoefficient;
        context.BaseDamage = Mathf.Max(0, Mathf.FloorToInt(scaled));
        context.RecordStage(DamageStage.BaseDamage, context.BaseDamage);
    }

    private static void ApplyPhysicalResistance(DamageContext context)
    {
        int damage = context.BaseDamage;
        context.PhysicalResistanceMultiplier = 1f;

        if (context.Request.ApplyPhysicalResistance &&
            context.Target?.Data?.PhysicalResistances != null)
        {
            StaggerGaugeMechanic stagger =
                context.Target.GetMechanic<StaggerGaugeMechanic>();

            context.PhysicalResistanceMultiplier =
                stagger?.IsVulnerabilityWindowOpen == true
                    ? (context.Target.BattleContext?.Rules?.Stagger?.VulnerabilityHpResistanceOverride ?? 2f)
                    : context.Target.Data.PhysicalResistances.GetMultiplier(context.PhysicalType);

            damage = Mathf.Max(
                0,
                Mathf.FloorToInt(damage * context.PhysicalResistanceMultiplier));
        }

        context.TargetModifiedDamage = damage;
        context.RecordStage(DamageStage.TargetModifiers, damage);
    }

    /// <summary>
    /// Protection/Rupture 등 flat 상태 보정. 내성 이후에 처리한다.
    /// </summary>
    private static void ApplyFlatTargetModifiers(DamageContext context)
    {
        int damage = context.TargetModifiedDamage;

        if (context.Request.ApplyTargetModifiers && context.Target != null)
        {
            foreach (StatusEffect effect in context.Target.StatusEffects)
            {
                if (effect == null)
                    continue;

                damage = Mathf.Max(
                    0,
                    Mathf.FloorToInt(effect.ModifyDamageTaken(context.Action, damage)));
            }

            if (context.TargetPart != null)
            {
                foreach (StatusEffect effect in context.TargetPart.StatusEffects)
                {
                    if (effect == null)
                        continue;

                    damage = Mathf.Max(
                        0,
                        Mathf.FloorToInt(effect.ModifyDamageTaken(context.Action, damage)));
                }
            }
        }

        context.TargetModifiedDamage = damage;
        context.RecordStage(DamageStage.TargetModifiers, damage);
    }

    /// <summary>
    /// 캐릭터 고유 Mechanic/공격자 damage modifier. 구 Hifumi 발악 반감 같은 legacy 경로도
    /// 이 단계에 남아 있으면 Verification에서 검출된다.
    /// </summary>
    private static void ApplyCharacterSpecificModifiers(DamageContext context)
    {
        int damage = context.TargetModifiedDamage;

        if (context.Request.ApplyAttackerModifiers && context.Attacker != null)
        {
            foreach (StatusEffect effect in context.Attacker.StatusEffects)
            {
                if (effect != null)
                    damage = Mathf.Max(0, effect.ModifyDamage(context.Action, damage));
            }

            if (context.Action?.OwnerPart != null)
            {
                foreach (StatusEffect effect in context.Action.OwnerPart.StatusEffects)
                {
                    if (effect != null)
                        damage = Mathf.Max(0, effect.ModifyDamage(context.Action, damage));
                }
            }

            if (context.Attacker.Mechanics != null)
            {
                foreach (CombatMechanic mechanic in context.Attacker.Mechanics)
                {
                    if (mechanic != null)
                        damage = Mathf.Max(0, mechanic.ModifyDamageDealt(context, damage));
                }
            }
        }

        if (context.Request.ApplyTargetModifiers && context.Target?.Mechanics != null)
        {
            foreach (CombatMechanic mechanic in context.Target.Mechanics)
            {
                if (mechanic != null)
                    damage = Mathf.Max(0, mechanic.ModifyDamageTaken(context, damage));
            }
        }

        context.AttackerModifiedDamage = damage;
        context.TargetModifiedDamage = damage;
        context.RecordStage(DamageStage.AttackerModifiers, damage);
    }

    private static void ApplyMinimumDamage(DamageContext context)
    {
        int damage = context.TargetModifiedDamage;

        // 0915 C-25: 유효한 양수 피해 요청은 armor 직전 단 한 번 최소 1을 보장한다.
        if (context.RawPower > 0 &&
            context.DamageCoefficient > 0f &&
            damage <= 0)
        {
            damage = 1;
        }

        context.TargetModifiedDamage = damage;
        context.RecordStage(DamageStage.TargetModifiers, damage);
    }

    private static void ApplyGuard(DamageContext context)
    {
        RuntimeStatus runtime = context.Target?.RuntimeStatus;

        context.GuardBefore =
            context.Request.ApplyGuard && runtime != null
                ? Mathf.Max(0, runtime.currentBlock)
                : 0;

        context.GuardAbsorbed = Mathf.Min(context.GuardBefore, context.TargetModifiedDamage);
        context.GuardAfter = Mathf.Max(0, context.GuardBefore - context.GuardAbsorbed);
        context.DamageAfterGuard = Mathf.Max(0, context.TargetModifiedDamage - context.GuardAbsorbed);
        context.RecordStage(DamageStage.Guard, context.DamageAfterGuard);
    }

    private static void FinalizeDamage(DamageContext context)
    {
        context.FinalDamage = Mathf.Max(0, context.DamageAfterGuard);
        context.RecordStage(DamageStage.Finalized, context.FinalDamage);
    }
}
