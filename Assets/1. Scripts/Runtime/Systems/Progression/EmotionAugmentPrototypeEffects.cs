using UnityEngine;

/// <summary>
/// 감정 증강 데이터 제작을 빠르게 시작하기 위한 범용 프로토타입 Effect.
/// 즉시 자원 보상과 전투 종료까지 유지되는 단순 배율/가산 보정을 한 에셋에서 설정할 수 있다.
/// 이후 각 감정 전용 EffectDefinition으로 교체해도 EmotionAugmentDefinition/Manager/UI 구조는 그대로 유지된다.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Progression/Emotion Augment Prototype Effect",
    fileName = "NewEmotionAugmentPrototypeEffect")]
public sealed class EmotionAugmentPrototypeEffectDefinition :
    EmotionAugmentEffectDefinition
{
    [Header("Immediate")]
    [Min(0)] public int GainEnergy;
    [Min(0)] public int GainPrestige;
    [Range(0f, 1f)] public float RecoverStaggerRatio;

    [Header("Battle-long Modifier")]
    public int RollBonus;
    [Min(0f)] public float DamageDealtMultiplier = 1f;
    [Min(0f)] public float DamageTakenMultiplier = 1f;
    [Min(0)] public int PositiveMomentumBonus;
    [Min(0f)] public float PrestigeGainMultiplier = 1f;

    public override void Apply(
        EmotionAugmentRuntimeContext context)
    {
        Character owner = context?.Owner;
        if (owner == null)
            return;

        if (GainEnergy > 0)
            owner.AddEnergy(GainEnergy);

        if (GainPrestige > 0)
            owner.AddPrestige(GainPrestige);

        if (RecoverStaggerRatio > 0f)
        {
            StaggerGaugeMechanic stagger =
                owner.GetMechanic<StaggerGaugeMechanic>();

            if (stagger != null)
            {
                int amount = Mathf.CeilToInt(
                    stagger.MaxGauge *
                    Mathf.Clamp01(RecoverStaggerRatio));

                stagger.Recover(amount);
            }
        }
    }

    public override CombatMechanic CreateMechanic(
        EmotionAugmentRuntimeContext context)
    {
        bool hasPersistentModifier =
            RollBonus != 0 ||
            Mathf.Abs(DamageDealtMultiplier - 1f) > 0.0001f ||
            Mathf.Abs(DamageTakenMultiplier - 1f) > 0.0001f ||
            PositiveMomentumBonus > 0 ||
            Mathf.Abs(PrestigeGainMultiplier - 1f) > 0.0001f;

        if (!hasPersistentModifier)
            return null;

        string label =
            context?.Augment?.DisplayName ??
            "Emotion Augment";

        return new EmotionAugmentPrototypeModifierMechanic(
            label,
            RollBonus,
            DamageDealtMultiplier,
            DamageTakenMultiplier,
            PositiveMomentumBonus,
            PrestigeGainMultiplier);
    }
}

/// <summary>
/// 프로토타입 감정 증강이 전투 종료까지 유지하는 단순 수치 보정.
/// Character.AddRuntimeMechanic으로 등록되므로 기존 CombatMechanic 파이프라인을 그대로 탄다.
/// </summary>
internal sealed class EmotionAugmentPrototypeModifierMechanic :
    CombatMechanic
{
    private readonly string label;
    private readonly int rollBonus;
    private readonly float damageDealtMultiplier;
    private readonly float damageTakenMultiplier;
    private readonly int positiveMomentumBonus;
    private readonly float prestigeGainMultiplier;

    public override string MechanicName =>
        $"Emotion Augment · {label}";

    public EmotionAugmentPrototypeModifierMechanic(
        string label,
        int rollBonus,
        float damageDealtMultiplier,
        float damageTakenMultiplier,
        int positiveMomentumBonus,
        float prestigeGainMultiplier)
    {
        this.label =
            string.IsNullOrWhiteSpace(label)
                ? "Prototype"
                : label;
        this.rollBonus = rollBonus;
        this.damageDealtMultiplier =
            Mathf.Max(0f, damageDealtMultiplier);
        this.damageTakenMultiplier =
            Mathf.Max(0f, damageTakenMultiplier);
        this.positiveMomentumBonus =
            Mathf.Max(0, positiveMomentumBonus);
        this.prestigeGainMultiplier =
            Mathf.Max(0f, prestigeGainMultiplier);
    }

    public override int ModifyRoll(
        BattleAction action,
        int roll)
    {
        if (action?.Owner != owner)
            return roll;

        return Mathf.Max(0, roll + rollBonus);
    }

    public override int ModifyDamageDealt(
        DamageContext context,
        int damage)
    {
        if (context?.Attacker != owner || damage <= 0)
            return damage;

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                damage * damageDealtMultiplier));
    }

    public override int ModifyDamageTaken(
        DamageContext context,
        int damage)
    {
        if (context?.Target != owner || damage <= 0)
            return damage;

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                damage * damageTakenMultiplier));
    }

    public override int ModifyMomentumShift(
        ClashResultContext context,
        int shift)
    {
        if (shift <= 0 || positiveMomentumBonus <= 0)
            return shift;

        return shift + positiveMomentumBonus;
    }

    public override int ModifyPrestigeGain(
        ClashResultContext context,
        int prestigeGain)
    {
        if (prestigeGain <= 0)
            return prestigeGain;

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                prestigeGain * prestigeGainMultiplier));
    }
}
