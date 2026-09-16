using UnityEngine;

public enum TempBalanceSkillProxyOperation
{
    GainBlock = 0,
    GainEnergy = 1,
    GainPrestige = 2,
    OlafMadness = 3,
    TargetBleeding = 4,
    YujinSense = 5,
    YujinMark = 6,
    YujinMarkTrap = 7,
    YujinMarkDeadline = 8,
    TurnClashPowerBonus = 9
}

/// <summary>
/// TEMP_BALANCE_V1 전용 카드 rider.
/// 아직 전용 runtime hook이 없는 0916 카드에 최소한의 실제 실행 효과를 제공한다.
/// 정식 카드 구현 시 해당 카드에서 이 effect만 제거하면 된다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Skill/Effect/TEMP Balance Proxy",
    fileName = "TempBalanceSkillProxyEffect")]
public sealed class TempBalanceSkillProxyEffectDefinition : SkillEffectDefinition
{
    public TempBalanceSkillProxyOperation Operation;
    public int BaseAmount = 1;
    [Min(1)] public int DurationTurns = 3;
    [Min(1)] public int Multiplier = 2;

    public override void Apply(SkillEffectContext context)
    {
        Apply(context, null);
    }

    public override void Apply(
        SkillEffectContext context,
        SkillEffectOverrides overrides)
    {
        if (context == null)
            return;

        int amount = overrides?.OverrideAmount == true
            ? overrides.Amount
            : BaseAmount;

        amount = Mathf.Max(0, amount);
        Character owner = context.Owner;
        Character target = context.Target;
        BodyPart targetPart = context.TargetPart;

        switch (Operation)
        {
            case TempBalanceSkillProxyOperation.GainBlock:
                owner?.AddBlock(amount);
                break;

            case TempBalanceSkillProxyOperation.GainEnergy:
                owner?.AddEnergy(amount);
                break;

            case TempBalanceSkillProxyOperation.GainPrestige:
                owner?.AddPrestige(amount);
                break;

            case TempBalanceSkillProxyOperation.OlafMadness:
                owner?.GetMechanic<OlafMadnessMechanic>()?.AddMadness(amount);
                break;

            case TempBalanceSkillProxyOperation.TargetBleeding:
                if (target == null || amount <= 0)
                    break;
                if (targetPart != null)
                    target.AddPartStatus(targetPart, new Bleeding(amount), owner);
                else
                    target.AddStatus(new Bleeding(amount), owner);
                break;

            case TempBalanceSkillProxyOperation.YujinSense:
                owner?.GetMechanic<YujinMechanic>()?.GrantSense(amount);
                break;

            case TempBalanceSkillProxyOperation.YujinMark:
                owner?.GetMechanic<YujinMechanic>()
                    ?.GrantMark(target, targetPart, amount, context.Action);
                break;

            case TempBalanceSkillProxyOperation.YujinMarkTrap:
                owner?.GetMechanic<YujinMechanic>()
                    ?.RegisterMarkTrap(target, targetPart, amount, context.Action);
                break;

            case TempBalanceSkillProxyOperation.YujinMarkDeadline:
                owner?.GetMechanic<YujinMechanic>()
                    ?.RegisterMarkDeadline(
                        target,
                        targetPart,
                        Mathf.Max(1, DurationTurns),
                        Mathf.Max(1, Multiplier),
                        context.Action);
                break;

            case TempBalanceSkillProxyOperation.TurnClashPowerBonus:
                owner?.AddTurnClashPowerBonus(amount);
                break;
        }
    }
}
