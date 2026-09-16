using UnityEngine;

/// <summary>
/// TEMP_BALANCE_V1 전용 감정 증강 proxy.
/// 정본에 없는 one-off 규칙을 새로 만들지 않고, 이미 존재하는 공통 전투 축만 사용한다.
/// 기획 확정 뒤 canonical effect로 교체할 수 있도록 별도 타입으로 격리한다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Progression/TEMP Balance Emotion Proxy Effect",
    fileName = "TempBalanceEmotionProxyEffect")]
public sealed class TempBalanceEmotionProxyEffectDefinition : EmotionAugmentEffectDefinition
{
    [Header("Immediate")]
    [Min(0)] public int GainEnergy;
    [Min(0)] public int GainPrestige;
    [Min(0)] public int GainBlock;
    [Range(0f, 1f)] public float RecoverStaggerRatio;

    [Header("Battle-long flat proxy")]
    public int RollBonus;
    public int DamageDealtFlatBonus;
    [Min(0)] public int DamageTakenFlatReduction;
    [Min(0)] public int PositiveMomentumBonus;
    [Min(0)] public int PrestigeGainFlatBonus;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        Character owner = context?.Owner;
        if (owner == null)
            return;

        if (GainEnergy > 0)
            owner.AddEnergy(GainEnergy);

        if (GainPrestige > 0)
            owner.AddPrestige(GainPrestige);

        if (GainBlock > 0)
            owner.AddBlock(GainBlock);

        if (RecoverStaggerRatio > 0f)
        {
            StaggerGaugeMechanic stagger = owner.GetMechanic<StaggerGaugeMechanic>();
            if (stagger != null)
            {
                int amount = Mathf.CeilToInt(
                    stagger.MaxGauge * Mathf.Clamp01(RecoverStaggerRatio));
                stagger.Recover(amount);
            }
        }
    }

    public override CombatMechanic CreateMechanic(EmotionAugmentRuntimeContext context)
    {
        bool hasPersistent =
            RollBonus != 0 ||
            DamageDealtFlatBonus != 0 ||
            DamageTakenFlatReduction > 0 ||
            PositiveMomentumBonus > 0 ||
            PrestigeGainFlatBonus > 0;

        if (!hasPersistent)
            return null;

        return new TempBalanceEmotionProxyMechanic(
            context?.Augment?.DisplayName,
            RollBonus,
            DamageDealtFlatBonus,
            DamageTakenFlatReduction,
            PositiveMomentumBonus,
            PrestigeGainFlatBonus);
    }
}

internal sealed class TempBalanceEmotionProxyMechanic : CombatMechanic
{
    private readonly string label;
    private readonly int rollBonus;
    private readonly int damageDealtFlatBonus;
    private readonly int damageTakenFlatReduction;
    private readonly int positiveMomentumBonus;
    private readonly int prestigeGainFlatBonus;

    public override string MechanicName =>
        $"TEMP_BALANCE_V1 Emotion · {label}";

    public TempBalanceEmotionProxyMechanic(
        string label,
        int rollBonus,
        int damageDealtFlatBonus,
        int damageTakenFlatReduction,
        int positiveMomentumBonus,
        int prestigeGainFlatBonus)
    {
        this.label = string.IsNullOrWhiteSpace(label) ? "Proxy" : label;
        this.rollBonus = rollBonus;
        this.damageDealtFlatBonus = damageDealtFlatBonus;
        this.damageTakenFlatReduction = Mathf.Max(0, damageTakenFlatReduction);
        this.positiveMomentumBonus = Mathf.Max(0, positiveMomentumBonus);
        this.prestigeGainFlatBonus = Mathf.Max(0, prestigeGainFlatBonus);
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner)
            return roll;

        return Mathf.Max(1, roll + rollBonus);
    }

    public override int ModifyDamageDealt(DamageContext context, int damage)
    {
        if (context?.Attacker != owner || damage <= 0)
            return damage;

        return Mathf.Max(1, damage + damageDealtFlatBonus);
    }

    public override int ModifyDamageTaken(DamageContext context, int damage)
    {
        if (context?.Target != owner || damage <= 0)
            return damage;

        return Mathf.Max(1, damage - damageTakenFlatReduction);
    }

    public override int ModifyMomentumShift(ClashResultContext context, int shift)
    {
        if (shift <= 0 || positiveMomentumBonus <= 0)
            return shift;

        return shift + positiveMomentumBonus;
    }

    public override int ModifyPrestigeGain(ClashResultContext context, int prestigeGain)
    {
        if (prestigeGain <= 0)
            return prestigeGain;

        return prestigeGain + prestigeGainFlatBonus;
    }
}
