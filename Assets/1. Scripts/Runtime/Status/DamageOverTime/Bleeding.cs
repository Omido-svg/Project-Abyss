using UnityEngine;

/// <summary>
/// 올라프 전용 고유 키워드 「혈상」.
/// 기존 Bleeding 타입/직렬화 참조는 마이그레이션 호환을 위해 보존한다.
/// 턴 종료 시 피해 = 현재 스택 × DamagePerStack, 처리 후 스택 -1.
/// </summary>
public sealed class Bleeding : StatusEffect, IUniqueKeywordStatus
{
    public const int ExplosionThreshold = 10;
    public const string KeywordId = "olaf.blood_wound";

    public string UniqueKeywordId => KeywordId;
    public override string EffectName => KeywordId;

    public int DamagePerStack { get; private set; }

    public bool CanExplode => Stack >= ExplosionThreshold;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.Permanent;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacks;

    public Bleeding(
        int stack = 1,
        int duration = -1,
        int damagePerStack = 1)
    {
        Name = "혈상";
        Stack = Mathf.Max(0, stack);
        Duration = -1;
        DamagePerStack = Mathf.Max(0, damagePerStack);
    }

    public void AddStacks(int amount)
    {
        Stack = Mathf.Max(0, Stack + Mathf.Max(0, amount));
    }

    public int ConsumeAll()
    {
        int consumed = Mathf.Max(0, Stack);
        Stack = 0;
        return consumed;
    }

    public override void Merge(StatusEffect other)
    {
        if (other is not Bleeding bleeding)
            return;

        AddStacks(bleeding.Stack);
        DamagePerStack = Mathf.Max(DamagePerStack, bleeding.DamagePerStack);
    }

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        if (context?.TargetCharacter == null ||
            context.TargetPart == null ||
            context.TargetPart.IsBroken ||
            Stack <= 0)
        {
            return;
        }

        int tickDamage =
            Mathf.Max(0, Stack * DamagePerStack);

        if (tickDamage > 0)
        {
            DamageRequest request =
                DamageRequest.Custom(
                    DamageType.StatusPart,
                    Source ?? Owner,
                    context.TargetCharacter,
                    context.TargetPart,
                    tickDamage,
                    1f,
                    canBreakPart: false,
                    applyMomentum: false,
                    applyGuard: false,
                    sourceAction: null,
                    sourceEffect: this);

            request.ApplyAttackerModifiers = false;
            request.ApplyTargetModifiers = false;
            request.WasCritical = false;

            context.ApplyDamage(request);
        }

        Stack = Mathf.Max(0, Stack - 1);
        if (Stack <= 0)
            RemoveStatus();
    }
}
