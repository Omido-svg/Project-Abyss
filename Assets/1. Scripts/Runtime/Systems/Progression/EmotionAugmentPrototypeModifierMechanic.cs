using UnityEngine;

/// <summary>
/// 프로토타입 감정 증강이 전투 종료까지 유지하는 단순 수치 보정.
/// Character.AddRuntimeMechanic으로 등록되는 순수 런타임 객체이며 UnityEngine.Object가 아니다.
/// ScriptableObject 직렬화 대상과 파일을 분리해 MonoScript 참조가 섞이지 않게 한다.
/// </summary>
internal sealed class EmotionAugmentPrototypeModifierMechanic : CombatMechanic
{
    private readonly string label;
    private readonly int rollBonus;
    private readonly float damageDealtMultiplier;
    private readonly float damageTakenMultiplier;
    private readonly int positiveMomentumBonus;
    private readonly float prestigeGainMultiplier;

    public override string MechanicName => $"Emotion Augment · {label}";

    public EmotionAugmentPrototypeModifierMechanic(
        string label,
        int rollBonus,
        float damageDealtMultiplier,
        float damageTakenMultiplier,
        int positiveMomentumBonus,
        float prestigeGainMultiplier)
    {
        this.label = string.IsNullOrWhiteSpace(label) ? "Prototype" : label;
        this.rollBonus = rollBonus;
        this.damageDealtMultiplier = Mathf.Max(0f, damageDealtMultiplier);
        this.damageTakenMultiplier = Mathf.Max(0f, damageTakenMultiplier);
        this.positiveMomentumBonus = Mathf.Max(0, positiveMomentumBonus);
        this.prestigeGainMultiplier = Mathf.Max(0f, prestigeGainMultiplier);
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner)
            return roll;
        return Mathf.Max(0, roll + rollBonus);
    }

    public override int ModifyDamageDealt(DamageContext context, int damage)
    {
        if (context?.Attacker != owner || damage <= 0)
            return damage;
        return Mathf.Max(0, Mathf.RoundToInt(damage * damageDealtMultiplier));
    }

    public override int ModifyDamageTaken(DamageContext context, int damage)
    {
        if (context?.Target != owner || damage <= 0)
            return damage;
        return Mathf.Max(0, Mathf.RoundToInt(damage * damageTakenMultiplier));
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
        return Mathf.Max(0, Mathf.RoundToInt(prestigeGain * prestigeGainMultiplier));
    }
}
