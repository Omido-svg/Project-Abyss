using UnityEngine;

public class Burn : DamageStatus
{
    public Burn(int stack = 1, int duration = 3)
        : base(stack, duration)
    {
        Name = "Burn";
        Stack = Mathf.Max(0, stack);
        Damage = Stack;
    }

    // 기존 규칙 유지: 부위에 붙어 있어도 화상 피해는 전체 HP 고정 피해다.
    protected override bool UsePartDamage => false;

    public override void Merge(StatusEffect other)
    {
        if (other is not Burn burn)
            return;

        Stack += Mathf.Max(0, burn.Stack);
        Damage = Stack;
        Duration = Mathf.Max(Duration, burn.Duration);
    }
}
