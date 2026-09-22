using System.Collections.Generic;
using UnityEngine;

public enum Yujin0916EffectOperation
{
    None = 0,
    LedgerCleanup = 1,
    StepBackBlock = 2,
    AimCriticalRider = 3,
    ConfirmKillMomentum = 4,
    FamiliarHand = 5,
    AceInTheHole = 6,
    Wanted = 7,
    ContractTargetMomentum = 8,
    DesignateCurrent = 9,
    IntoShadowsBlock = 10,
    GainSense = 11,
    CutThroatMark = 12,
    SpreadRumorMark = 13,
    ProbeWeakness = 14,
    Trap = 15,
    Deadline = 16,
    DesignateAll = 17,
    Period = 18,
    HoldBreath = 19,
    Sharpen = 20
}

/// <summary>
/// 0916(3) XLSX에서 기존 Phase-D YujinMechanic만으로 표현되지 않던 rider들을
/// 실제 전투 Effect 파이프라인에 연결한다.
/// 값이 정본에서 미정인 항목은 이 타입에 넣지 않는다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skill/Effect/0916/Yujin Canonical Rider",
    fileName = "Yujin0916Rider")]
public sealed class Yujin0916SkillEffectDefinition : SkillEffectDefinition
{
    public Yujin0916EffectOperation Operation;
    public int Amount = 1;
    [Min(1)] public int DurationTurns = 3;

    public override void Apply(SkillEffectContext context)
    {
        if (context?.Owner == null)
            return;

        YujinMechanic mechanic =
            context.Owner.GetMechanic<YujinMechanic>();
        if (mechanic == null)
            return;

        switch (Operation)
        {
            case Yujin0916EffectOperation.LedgerCleanup:
                if (mechanic.TryConsumeMark(context.Target, context.TargetPart, 20))
                    context.Owner.AddEnergy(1);
                break;

            case Yujin0916EffectOperation.StepBackBlock:
                context.Owner.AddBlock(10);
                break;

            case Yujin0916EffectOperation.AimCriticalRider:
                ApplyAimCriticalRider(context, mechanic);
                break;

            case Yujin0916EffectOperation.ConfirmKillMomentum:
                if (IsDesignated(context.Target, context.TargetPart))
                    context.BattleContext?.ResolveMomentumManager()?.ApplySkillShift(context.Owner, 10);
                break;

            case Yujin0916EffectOperation.FamiliarHand:
                if (mechanic.WeaponChangedThisTurn)
                    mechanic.GrantSense(1);
                break;

            case Yujin0916EffectOperation.AceInTheHole:
            {
                int cost = mechanic.GetAceInTheHoleSenseCost();
                if (mechanic.TrySpendSense(cost))
                    mechanic.GrantForcedFrontCharges(mechanic.CurrentWeaponProfile.CoinCount);
                break;
            }

            case Yujin0916EffectOperation.Wanted:
                ApplyWeaponDebuffToAllEnemies(context, mechanic);
                break;

            case Yujin0916EffectOperation.ContractTargetMomentum:
                // Legacy serialized compatibility only. 0916 정본의 값이 (미정)이므로 canonical migration은 이 operation을 생성하지 않는다.
                break;

            case Yujin0916EffectOperation.DesignateCurrent:
                ApplyDesignation(context.Owner, context.Target, context.TargetPart, 2, 3);
                break;

            case Yujin0916EffectOperation.IntoShadowsBlock:
                context.Owner.AddBlock(15);
                break;

            case Yujin0916EffectOperation.GainSense:
                mechanic.GrantSense(Mathf.Max(1, Amount));
                break;

            case Yujin0916EffectOperation.CutThroatMark:
                mechanic.GrantMark(context.Target, context.TargetPart, 20, context.Action);
                break;

            case Yujin0916EffectOperation.SpreadRumorMark:
                GrantMarkToAllEnemies(context, mechanic, 8);
                break;

            case Yujin0916EffectOperation.ProbeWeakness:
                ApplyWeaponDebuff(context.Owner, context.Target, context.TargetPart, mechanic.CurrentWeapon);
                break;

            case Yujin0916EffectOperation.Trap:
                mechanic.RegisterMarkTrap(context.Target, context.TargetPart, 20, context.Action);
                break;

            case Yujin0916EffectOperation.Deadline:
                mechanic.RegisterMarkDeadline(context.Target, context.TargetPart, 3, 2, context.Action);
                break;

            case Yujin0916EffectOperation.DesignateAll:
                foreach (Character enemy in EnumerateEnemies(context))
                    ApplyDesignation(context.Owner, enemy, null, 2, 3);
                break;

            case Yujin0916EffectOperation.Period:
                ApplyPeriod(context, mechanic);
                break;

            case Yujin0916EffectOperation.HoldBreath:
                if (mechanic.TryConsumeAnyMarkForPreparation(10))
                    context.Owner.AddBlock(20);
                break;

            case Yujin0916EffectOperation.Sharpen:
                if (mechanic.TryConsumeAnyMarkForPreparation(10))
                    context.Owner.AddStatus(
                        new DeferredStatusEffect(StatusEffectId.Swift, 1, 1),
                        context.Owner);
                break;
        }
    }

    private static void ApplyAimCriticalRider(
        SkillEffectContext context,
        YujinMechanic mechanic)
    {
        bool critical =
            context.RollResult?.IsCritical == true ||
            context.DamageContext?.WasCritical == true;

        if (!critical)
            return;

        switch (mechanic.CurrentWeapon)
        {
            case YujinWeaponType.Baeku:
                mechanic.GrantMark(context.Target, context.TargetPart, 6, context.Action);
                break;

            case YujinWeaponType.Jeokseol:
                ApplyNextTurnStatus(
                    context.Owner,
                    context.Target,
                    context.TargetPart,
                    StatusEffectId.Fracture,
                    1);
                break;

            case YujinWeaponType.Nakil:
                ApplyFixedDamage(context, 8);
                break;
        }
    }

    private static void ApplyWeaponDebuffToAllEnemies(
        SkillEffectContext context,
        YujinMechanic mechanic)
    {
        foreach (Character enemy in EnumerateEnemies(context))
            ApplyWeaponDebuff(context.Owner, enemy, null, mechanic.CurrentWeapon);
    }

    private static void ApplyWeaponDebuff(
        Character source,
        Character target,
        BodyPart part,
        YujinWeaponType weapon)
    {
        StatusEffectId id = weapon switch
        {
            YujinWeaponType.Baeku => StatusEffectId.Weakness,
            YujinWeaponType.Jeokseol => StatusEffectId.Fracture,
            _ => StatusEffectId.Rupture
        };

        ApplyNextTurnStatus(source, target, part, id, 1);
    }

    private static void ApplyNextTurnStatus(
        Character source,
        Character target,
        BodyPart part,
        StatusEffectId id,
        int stack)
    {
        if (target == null)
            return;

        DeferredStatusEffect deferred =
            new DeferredStatusEffect(id, Mathf.Max(1, stack), 1);

        if (part != null)
            target.AddPartStatus(part, deferred, source);
        else
            target.AddStatus(deferred, source);
    }

    private static void ApplyDesignation(
        Character source,
        Character target,
        BodyPart part,
        int value,
        int turns)
    {
        if (target == null)
            return;

        YujinDesignationStatus designation =
            new YujinDesignationStatus(
                Mathf.Max(1, value),
                turns < 0
                    ? StatusEffect.InfiniteDuration
                    : Mathf.Max(1, turns));

        if (part != null)
            target.AddPartStatus(part, designation, source);
        else
            target.AddStatus(designation, source);
    }

    private static bool IsDesignated(
        Character target,
        BodyPart part)
    {
        if (part?.StatusEffects != null)
        {
            foreach (StatusEffect effect in part.StatusEffects)
                if (effect is YujinDesignationStatus) return true;
        }

        if (target?.StatusEffects != null)
        {
            foreach (StatusEffect effect in target.StatusEffects)
                if (effect is YujinDesignationStatus) return true;
        }

        return false;
    }

    private static IEnumerable<Character> EnumerateEnemies(
        SkillEffectContext context)
    {
        BattleContext battle = context?.BattleContext;
        Character owner = context?.Owner;
        if (battle == null || owner == null)
            yield break;

        if (ReferenceEquals(owner, battle.Player))
        {
            if (battle.Enemies == null)
                yield break;

            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                Character enemy = battle.Enemies[i];
                if (enemy != null && !enemy.IsDead)
                    yield return enemy;
            }
            yield break;
        }

        if (battle.Player != null && !battle.Player.IsDead)
            yield return battle.Player;
    }

    private static void GrantMarkToAllEnemies(
        SkillEffectContext context,
        YujinMechanic mechanic,
        int amount)
    {
        foreach (Character enemy in EnumerateEnemies(context))
            mechanic.GrantMark(enemy, null, amount, context.Action);
    }

    private static void ApplyPeriod(
        SkillEffectContext context,
        YujinMechanic mechanic)
    {
        if (mechanic.CurrentWeapon == YujinWeaponType.Nakil)
        {
            // 낙일은 "처형(크리로 파괴)"만 조건이다. 같은 타격이 처치까지 만들더라도
            // OnKill wake와 OnPartBroken wake에서 두 번 지급하지 않는다.
            if (context.Timing == SkillEffectTiming.OnPartBroken &&
                context.DamageContext?.BrokePart == true &&
                context.DamageContext.WasCritical)
            {
                mechanic.GrantSense(2);
            }
            return;
        }

        // 백우/적설은 적 전체 처치가 조건이다.
        if (context.Timing == SkillEffectTiming.OnKill &&
            (context.KillContext?.Victim != null ||
             context.DamageContext?.WasKilled == true))
        {
            mechanic.GrantSense(2);
        }
    }

    private static void ApplyFixedDamage(
        SkillEffectContext context,
        int damage)
    {
        if (context?.Target == null || damage <= 0)
            return;

        DamageRequest request = DamageRequest.Custom(
            context.TargetPart == null ? DamageType.Direct : DamageType.SkillPart,
            context.Owner,
            context.Target,
            context.TargetPart,
            damage,
            1f,
            canBreakPart: false,
            applyMomentum: false,
            applyGuard: true,
            sourceAction: context.Action);

        context.BattleContext?.ResolveDamageManager()?.ApplyDamageContext(request);
    }
}