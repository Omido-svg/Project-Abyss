using UnityEngine;

// 올라프가 일반공격으로 실제 피해를 준 뒤
// 대상에게 혈상을 추가 부여한다.

[CreateAssetMenu(
    menuName = "Battle/Item/Olaf/Bloody Axe")]
public class OlafBloodyAxeItem : CharacterItem
{
    [Header("Bleeding")]
    [SerializeField, Min(1)]
    private int bleedingAmount = 1;

    [Header("Trigger Conditions")]
    [SerializeField]
    private bool requireNormalAttack = true;

    [SerializeField]
    private bool requirePositiveDamage = true;

    [SerializeField]
    private bool ignoreStatusDamage = true;

    [SerializeField]
    private bool triggerOncePerAction = true;

    [Header("Target Fallback")]
    [SerializeField]
    private bool applyToCharacterWhenPartUnavailable = true;

    public override CombatMechanic CreateMechanic()
    {
        return new OlafBloodyAxeMechanic(
            bleedingAmount,
            requireNormalAttack,
            requirePositiveDamage,
            ignoreStatusDamage,
            triggerOncePerAction,
            applyToCharacterWhenPartUnavailable);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        bleedingAmount =
            Mathf.Max(
                1,
                bleedingAmount);
    }
#endif
}
