using UnityEngine;

public class Bleeding : DamageStatus
{
    public const int ExplosionThreshold = 3;

    public bool CanExplode => Stack >= ExplosionThreshold;

    public Bleeding(int stack = 1, int duration = 3)
        : base(stack, duration)
    {
        Name = "Bleeding";
        Stack = stack;
        Duration = duration;

        // 출혈 피해량 = 스택
        Damage = Stack;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not Bleeding bleeding)
            return;

        Stack += bleeding.Stack;

        // 출혈 피해량 = 현재 스택 수
        Damage = Stack;

        // 새로 부여되면 지속시간 갱신
        Duration = Mathf.Max(Duration, bleeding.Duration);
    }

    public override void OnTurnEnd()
    {
        if (Owner == null)
            return;

        if (Owner.IsDead)
            return;

        if (Stack <= 0)
            return;

        //--------------------------------
        // 부위 출혈
        // 부위 HP를 깎고 약화까지 가능
        // 파괴는 불가능
        //--------------------------------

        if (IsPartEffect)
        {
            Owner.TakeStatusPartDamage(
                OwnerPart,
                Stack,
                this);

            return;
        }

        //--------------------------------
        // 캐릭터 출혈
        // 부위가 없는 출혈이면 기존처럼 캐릭터 피해
        //--------------------------------

        Owner.TakeTrueDamage(
            Stack,
            this);
    }
}