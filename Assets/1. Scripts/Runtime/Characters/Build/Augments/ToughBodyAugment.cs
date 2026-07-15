using UnityEngine;

// 강인한 육체
// - 모든 부위 최대 체력 +10%
// - 방어력 +3

[CreateAssetMenu(
    menuName = "Battle/Augment/Common/Tough Body")]
public class ToughBodyAugment : CharacterAugment
{
    [SerializeField] private float bodyPartHpBonusRate = 0.1f;
    [SerializeField] private int bonusDefense = 3;

    public override void ModifyBodyPart(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return;

        if (part == null)
            return;

        part.IncreaseMaxHPPercent(
            bodyPartHpBonusRate,
            true);
    }

    public override void ModifyStatus(
        Character owner,
        CurrentStatus status)
    {
        if (owner == null)
            return;

        if (status == null)
            return;

        status.defense += bonusDefense;

        Debug.Log(
            $"{owner.Data.CharacterName} 공용 증강 적용 : 강인한 육체 / " +
            $"부위 최대 체력 +{bodyPartHpBonusRate * 100}%, 방어력 +{bonusDefense}");
    }
}