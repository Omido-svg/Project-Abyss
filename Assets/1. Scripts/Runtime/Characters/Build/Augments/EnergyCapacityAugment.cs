
using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Augment/Common/Energy Capacity",
    fileName = "EnergyCapacityAugment")]
public sealed class EnergyCapacityAugment : CharacterAugment
{
    [SerializeField, Min(1)]
    private int bonusMaxEnergy = 1;

    public override void ModifyStatus(
        Character owner,
        CurrentStatus status)
    {
        if (owner == null || status == null)
            return;

        int amount = Mathf.Max(1, bonusMaxEnergy);
        status.maxEnergy += amount;

        Debug.Log(
            $"{owner.Data?.CharacterName ?? owner.name} 증강 적용 : " +
            $"최대 에너지 +{amount}");
    }
}
