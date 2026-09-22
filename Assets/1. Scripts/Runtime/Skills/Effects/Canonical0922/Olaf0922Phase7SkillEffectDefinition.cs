using UnityEngine;

public enum Olaf0922Phase7EffectOperation
{
    None = 0,
    RandomTargetDebuffNextTurn = 1,
    TargetStatusNextTurn = 2,
    OwnerStatusNextTurn = 3,
    OwnerStatusImmediate = 4,
    TargetBleeding = 5,
    OwnerBleeding = 6,
    OwnerBlock = 7,
    ClearTargetBlock = 8,
    BloodPriceStrengthByOwnerBleeding = 9,
    ConsumeTargetBleedingForEnergy = 10,
    PrepareBloodOathDuel = 11,
    ResolveBloodOathDuelWin = 12,
    AddMadness = 13,
    StrengthByTargetBleedingBandNextTurn = 14,
    TargetStatusNextTurnIfBleedingAtLeast = 15
}

/// <summary>
/// 0922 Phase 7에서 기존 0916/0917 카드 자산의 "확정된 변경점"만 표현하는 효과.
/// 미정 수치는 이 타입에서 임의로 채우지 않는다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skill/Effect/0922/Olaf Phase7",
    fileName = "Olaf0922Phase7Effect")]
public sealed class Olaf0922Phase7SkillEffectDefinition :
    SkillEffectDefinition
{
    public Olaf0922Phase7EffectOperation Operation;
    public StatusEffectId StatusId = StatusEffectId.Rupture;
    [Min(0)] public int Amount = 1;
    public int Duration = 1;
    [Min(0)] public int MinimumBleeding = 0;

    public override void Apply(SkillEffectContext context)
    {
        if (context?.Owner == null)
            return;

        OlafRulebreakerMechanic rulebreaker =
            context.Owner.GetMechanic<OlafRulebreakerMechanic>();

        if (rulebreaker == null)
            return;

        switch (Operation)
        {
            case Olaf0922Phase7EffectOperation.RandomTargetDebuffNextTurn:
                ApplyRandomDebuff(context);
                break;

            case Olaf0922Phase7EffectOperation.TargetStatusNextTurn:
                ApplyDeferred(
                    context.Owner,
                    context.Target,
                    context.TargetPart,
                    StatusId,
                    Amount,
                    Duration);
                break;

            case Olaf0922Phase7EffectOperation.OwnerStatusNextTurn:
                ApplyDeferred(
                    context.Owner,
                    context.Owner,
                    null,
                    StatusId,
                    Amount,
                    Duration);
                break;

            case Olaf0922Phase7EffectOperation.OwnerStatusImmediate:
                ApplyImmediateOwnerStatus(
                    context,
                    StatusId,
                    Amount,
                    Duration);
                break;

            case Olaf0922Phase7EffectOperation.TargetBleeding:
                rulebreaker.AddBleeding(
                    context.Target,
                    context.TargetPart,
                    Amount,
                    context.Action);
                break;

            case Olaf0922Phase7EffectOperation.OwnerBleeding:
                rulebreaker.AddBleeding(
                    context.Owner,
                    null,
                    Amount,
                    context.Action);
                break;

            case Olaf0922Phase7EffectOperation.OwnerBlock:
                context.Owner.AddBlock(
                    Mathf.Max(0, Amount));
                break;

            case Olaf0922Phase7EffectOperation.ClearTargetBlock:
                context.Target?.ClearBlock();
                break;

            case Olaf0922Phase7EffectOperation.BloodPriceStrengthByOwnerBleeding:
                ApplyBloodPriceStrength(
                    context,
                    rulebreaker);
                break;

            case Olaf0922Phase7EffectOperation.ConsumeTargetBleedingForEnergy:
                if (rulebreaker.TryConsumeBleeding(
                        context.Target,
                        context.TargetPart,
                        Mathf.Max(1, Amount)))
                {
                    context.Owner.AddEnergy(
                        2,
                        CombatResourceChangeReason.SkillEffect,
                        context.Action,
                        context.Action?.Skill);
                }
                break;

            case Olaf0922Phase7EffectOperation.PrepareBloodOathDuel:
                rulebreaker.PrepareBloodOathDuel(
                    context.Action);
                break;

            case Olaf0922Phase7EffectOperation.ResolveBloodOathDuelWin:
                rulebreaker.ResolveBloodOathDuelWin(
                    context.Action,
                    context.RollNumber);
                break;

            case Olaf0922Phase7EffectOperation.AddMadness:
                context.Owner.GetMechanic<OlafMadnessMechanic>()
                    ?.AddMadness(Mathf.Max(0, Amount));
                break;

            case Olaf0922Phase7EffectOperation.StrengthByTargetBleedingBandNextTurn:
            {
                int bleeding = rulebreaker.GetBleeding(context.Target, context.TargetPart);
                int value = bleeding >= 10 ? 3 : bleeding >= 5 ? 2 : 1;
                ApplyDeferred(
                    context.Owner, context.Owner, null,
                    StatusEffectId.Strength, value, Duration);
                break;
            }

            case Olaf0922Phase7EffectOperation.TargetStatusNextTurnIfBleedingAtLeast:
                if (rulebreaker.GetBleeding(context.Target, context.TargetPart) >=
                    Mathf.Max(0, MinimumBleeding))
                {
                    ApplyDeferred(
                        context.Owner, context.Target, context.TargetPart,
                        StatusId, Amount, Duration);
                }
                break;
        }
    }

    private static void ApplyRandomDebuff(
        SkillEffectContext context)
    {
        StatusEffectId id =
            Random.Range(0, 3) switch
            {
                0 => StatusEffectId.Rupture,
                1 => StatusEffectId.Fracture,
                _ => StatusEffectId.Weakness
            };

        ApplyDeferred(
            context.Owner,
            context.Target,
            context.TargetPart,
            id,
            1,
            1);
    }

    private static void ApplyBloodPriceStrength(
        SkillEffectContext context,
        OlafRulebreakerMechanic rulebreaker)
    {
        int bleeding =
            rulebreaker.GetOwnerBleedingTotal();

        int value =
            bleeding >= 10
                ? 3
                : bleeding >= 5
                    ? 2
                    : 1;

        ApplyDeferred(
            context.Owner,
            context.Owner,
            null,
            StatusEffectId.Strength,
            value,
            3);
    }

    private static void ApplyDeferred(
        Character source,
        Character target,
        BodyPart part,
        StatusEffectId id,
        int value,
        int duration)
    {
        if (target == null)
            return;

        DeferredStatusEffect effect =
            new DeferredStatusEffect(
                id,
                Mathf.Max(1, value),
                duration < 0
                    ? StatusEffect.InfiniteDuration
                    : Mathf.Max(1, duration));

        if (part != null)
            target.AddPartStatus(part, effect, source);
        else
            target.AddStatus(effect, source);
    }

    private static void ApplyImmediateOwnerStatus(
        SkillEffectContext context,
        StatusEffectId id,
        int value,
        int duration)
    {
        StatusEffect effect =
            StatusEffectFactory.CreateCanonical0922(
                id,
                Mathf.Max(1, value),
                duration < 0
                    ? StatusEffect.InfiniteDuration
                    : Mathf.Max(1, duration));

        if (effect == null)
            return;

        context.Owner.AddStatus(
            effect,
            context.Owner);
    }
}
