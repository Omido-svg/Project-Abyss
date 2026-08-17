using UnityEngine;

public sealed class DamageCalculator
{
    private readonly MomentumManager momentumManager;
    private readonly BattleRuleSettings battleRules;

    public DamageCalculator(
        MomentumManager momentumManager,
        BattleRuleSettings battleRules = null)
    {
        this.momentumManager = momentumManager;
        this.battleRules =
            battleRules ??
            new BattleRuleSettings();
        this.battleRules.Normalize();
    }

    public void Calculate(
        DamageContext context)
    {
        if (context == null)
            return;

        CalculateRawDamage(context);
        ApplyAttackerModifiers(context);
        ApplyCritical(context);
        ApplyDefense(context);
        ApplyGuard(context);
        ApplyTargetModifiers(context);
        ApplyProtection(context);
        FinalizeDamage(context);
    }

    private void CalculateRawDamage(
        DamageContext context)
    {
        DamageRequest request =
            context.Request;

        context.RecordStage(
            DamageStage.RawPower,
            context.RawPower);

        bool useLegacyStats =
            battleRules.UseLegacyAttackDefenseStats;

        context.FlatDamageBonus =
            useLegacyStats &&
            request.ApplyFlatDamageBonus &&
            context.Attacker?.CurrentStatus != null
                ? context.Attacker.CurrentStatus.flatDamageBonus
                : 0;

        context.OwnerDamageMultiplier =
            useLegacyStats &&
            request.ApplyOwnerMultiplier &&
            context.Attacker?.CurrentStatus != null
                ? context.Attacker.CurrentStatus.damageMultiplier
                : 1f;

        context.MomentumMultiplier =
            request.ApplyMomentum &&
            momentumManager != null
                ? momentumManager.GetDamageMultiplier(
                    context.Attacker)
                : 1f;

        float baseDamage =
            context.RawPower *
            context.SkillMultiplier;

        context.BaseDamage =
            Mathf.Max(
                0,
                Mathf.FloorToInt(baseDamage));

        float rawDamage =
            context.RawPower +
            context.FlatDamageBonus;

        rawDamage *=
            context.OwnerDamageMultiplier;

        rawDamage *=
            context.MomentumMultiplier;

        rawDamage *=
            context.SkillMultiplier;

        int flooredDamage =
            Mathf.Max(
                0,
                Mathf.FloorToInt(rawDamage));

        // 최신 규칙: 승리한 공격 굴림의 피해는 기세 배율 적용 후 버림,
        // 그리고 실제 공격 피해라면 최소 1을 보장한다.
        if (context.RawPower > 0 &&
            context.SkillMultiplier > 0f &&
            flooredDamage <= 0)
        {
            flooredDamage = 1;
        }

        context.RawDamage = flooredDamage;

        context.ModifiedDamage =
            context.RawDamage;

        context.RecordStage(
            DamageStage.RawDamage,
            context.RawDamage);
    }

    private void ApplyAttackerModifiers(
        DamageContext context)
    {
        int damage = context.RawDamage;

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

                damage = Mathf.Max(0, damage);
                context.ModifiedDamage = damage;
            }
        }

        context.AttackerModifiedDamage =
            Mathf.Max(0, damage);

        context.RecordStage(
            DamageStage.AttackerModifiers,
            context.AttackerModifiedDamage);
    }

    private void ApplyCritical(
        DamageContext context)
    {
        int damage =
            context.AttackerModifiedDamage;

        // 새 설계의 크리티컬은 Resolver가 높은 위력을 직접 반환한다.
        // 별도 곱연산은 구식 수치 호환 모드에서만 허용한다.
        if (battleRules.UseLegacyAttackDefenseStats &&
            context.WasCritical)
        {
            damage =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        damage *
                        Mathf.Max(
                            1f,
                            context.CriticalMultiplier)));
        }

        context.DamageAfterCritical = damage;
        context.ModifiedDamage = damage;

        context.RecordStage(
            DamageStage.Critical,
            damage);
    }

    private void ApplyDefense(
        DamageContext context)
    {
        int incomingDamage =
            context.DamageAfterCritical;

        if (!battleRules.UseLegacyAttackDefenseStats ||
            !context.Request.ApplyDefense ||
            incomingDamage <= 0 ||
            context.Target?.CurrentStatus == null)
        {
            context.DefenseValue = 0;
            context.PenetrationRate = 0f;
            context.PiercingDamage = 0;
            context.BlockableDamage = incomingDamage;
            context.DamageAfterArmor = incomingDamage;

            context.RecordStage(
                DamageStage.Defense,
                incomingDamage);

            return;
        }

        context.DefenseValue =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    context.Target.CurrentStatus.defense));

        float penetrationRate = 0f;

        if (context.Attacker?.CurrentStatus != null)
        {
            penetrationRate =
                Mathf.Clamp01(
                    context.Attacker
                        .CurrentStatus
                        .defensePenetrationRate);
        }

        if (context.Action?.Skill != null)
        {
            penetrationRate =
                Mathf.Clamp01(
                    penetrationRate +
                    context.Action.Skill.IgnoreBlock);
        }

        context.PenetrationRate =
            penetrationRate;

        float piercingDamage =
            incomingDamage *
            penetrationRate;

        float blockableDamage =
            incomingDamage -
            piercingDamage;

        float blockableAfterDefense =
            Mathf.Max(
                0f,
                blockableDamage -
                context.DefenseValue);

        context.PiercingDamage =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    piercingDamage));

        context.BlockableDamage =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    blockableDamage));

        context.DamageAfterArmor =
            Mathf.Max(
                0,
                Mathf.RoundToInt(
                    piercingDamage +
                    blockableAfterDefense));

        context.RecordStage(
            DamageStage.Defense,
            context.DamageAfterArmor);
    }

    private void ApplyGuard(
        DamageContext context)
    {
        int damageAfterArmor =
            context.DamageAfterArmor;

        RuntimeStatus runtime =
            context.Target?.RuntimeStatus;

        context.GuardBefore =
            context.Request.ApplyGuard &&
            runtime != null
                ? Mathf.Max(
                    0,
                    runtime.currentBlock)
                : 0;

        int blockableAfterArmor =
            Mathf.Max(
                0,
                damageAfterArmor -
                context.PiercingDamage);

        context.GuardAbsorbed =
            Mathf.Min(
                context.GuardBefore,
                blockableAfterArmor);

        context.GuardAfter =
            Mathf.Max(
                0,
                context.GuardBefore -
                context.GuardAbsorbed);

        context.DamageAfterDefense =
            Mathf.Max(
                0,
                damageAfterArmor -
                context.GuardAbsorbed);

        context.ModifiedDamage =
            context.DamageAfterDefense;

        context.RecordStage(
            DamageStage.Guard,
            context.DamageAfterDefense);
    }

    private void ApplyTargetModifiers(
        DamageContext context)
    {
        int damage =
            context.DamageAfterDefense;

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

                damage = Mathf.Max(0, damage);
                context.ModifiedDamage = damage;
            }
        }

        context.TargetModifiedDamage =
            Mathf.Max(0, damage);

        context.RecordStage(
            DamageStage.TargetModifiers,
            context.TargetModifiedDamage);
    }

    private void ApplyProtection(
        DamageContext context)
    {
        int incomingDamage =
            context.TargetModifiedDamage;

        if (!context.Request.ApplyProtection)
        {
            context.ProtectionValue = 0;
        }

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

        context.ModifiedDamage =
            context.DamageAfterProtection;

        context.RecordStage(
            DamageStage.Protection,
            context.DamageAfterProtection);
    }

    private void FinalizeDamage(
        DamageContext context)
    {
        context.FinalDamage =
            Mathf.Max(
                0,
                context.DamageAfterProtection);

        context.ModifiedDamage =
            context.FinalDamage;

        context.RecordStage(
            DamageStage.Finalized,
            context.FinalDamage);
    }
}