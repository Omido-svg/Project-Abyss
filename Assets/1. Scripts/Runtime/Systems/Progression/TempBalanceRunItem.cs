using UnityEngine;

/// <summary>
/// C-33의 실제 60개 아이템 정식 설계가 확정되기 전 TEMP_BALANCE_V1용 범용 아이템.
/// 공통 스탯/CombatMechanic 축만 사용하며, 정식 아이템 타입이 생기면 통째로 제거 가능하다.
/// </summary>
[CreateAssetMenu(
    menuName = "Run/Items/TEMP Balance Item",
    fileName = "TempBalanceRunItem")]
public sealed class TempBalanceRunItem : CharacterItem
{
    [Header("Static status")]
    public int MaxEnergyBonus;
    public int MaxSpeedBonus;

    [Header("Battle-long flat modifier")]
    public int RollBonus;
    public int DamageDealtFlatBonus;
    [Min(0)] public int DamageTakenFlatReduction;
    [Min(0)] public int PositiveMomentumBonus;
    [Min(0)] public int PrestigeGainFlatBonus;

    public override void ModifyStatus(Character owner, CurrentStatus status)
    {
        if (owner == null || status == null)
            return;

        status.maxEnergy = Mathf.Max(1, status.maxEnergy + MaxEnergyBonus);
        status.maxSpeed = Mathf.Max(status.minSpeed, status.maxSpeed + MaxSpeedBonus);
    }

    public override CombatMechanic CreateMechanic()
    {
        return new TempBalanceRunItemMechanic(
            RollBonus,
            DamageDealtFlatBonus,
            DamageTakenFlatReduction,
            PositiveMomentumBonus,
            PrestigeGainFlatBonus);
    }
}

internal sealed class TempBalanceRunItemMechanic : CombatMechanic
{
    private readonly int rollBonus;
    private readonly int damageDealtFlatBonus;
    private readonly int damageTakenFlatReduction;
    private readonly int positiveMomentumBonus;
    private readonly int prestigeGainFlatBonus;

    public override string MechanicName => "TEMP_BALANCE_V1 Run Item";

    public TempBalanceRunItemMechanic(
        int rollBonus,
        int damageDealtFlatBonus,
        int damageTakenFlatReduction,
        int positiveMomentumBonus,
        int prestigeGainFlatBonus)
    {
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
