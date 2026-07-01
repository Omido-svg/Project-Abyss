using UnityEngine;

// 전투 본능
// - 공격력 +3
// - 위세 획득량 +20%

[CreateAssetMenu(
    menuName = "Battle/Augment/Common/Battle Instinct")]
public class BattleInstinctAugment : CharacterAugment
{
    [SerializeField] private int bonusAttack = 3;
    [SerializeField] private float prestigeGainBonusRate = 0.2f;

    public override void ModifyStatus(
        Character owner,
        CurrentStatus status)
    {
        if (owner == null)
            return;

        if (status == null)
            return;

        status.flatDamageBonus += bonusAttack;

        status.prestigeGainMultiplier +=
            prestigeGainBonusRate;

        Debug.Log(
            $"{owner.Data.CharacterName} 공용 증강 적용 : 전투 본능 / " +
            $"공격력 +{bonusAttack}, 위세 획득량 +{prestigeGainBonusRate * 100}%");
    }
}
