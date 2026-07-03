using UnityEngine;

// 올라프가 일반공격으로 피해를 주면
// 대상에게 출혈 1 추가 부여

[CreateAssetMenu(
    menuName = "Battle/Item/Olaf/Bloody Axe")]
public class OlafBloodyAxeItem : CharacterItem
{
    public override CombatMechanic CreateMechanic()
    {
        return new OlafBloodyAxeMechanic();
    }
}