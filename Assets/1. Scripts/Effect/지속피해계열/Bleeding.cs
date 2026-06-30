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
        if (owner == null)
            return;

        // 매 턴 출혈 스택만큼 고정 피해
        owner.TakeTrueDamage(Stack, this);

        DecreaseDuration();

        if (IsExpired())
            RemoveStatus();
    }
}