using UnityEngine;

/// <summary>
/// 올라프 전용 고유 키워드 「출혈」.
/// 기존 Bleeding 타입/직렬화 참조는 마이그레이션 호환을 위해 보존한다.
/// 0922 canonical: N/T 상태가 아닌 bespoke stack.
/// 턴 종료 시 피해 = 현재 스택, 처리 후 스택 -1, 0이면 제거한다.
/// 폭발/카드 소비는 현재 스택을 참조하고 필요량 또는 전량을 직접 소비한다.
/// </summary>
public sealed class Bleeding : StatusEffect, IUniqueKeywordStatus
{
    // 구 외부 호출 호환용 이름. 0922에는 별도 폭발 스택 문턱이 없으며 1 이상이면 소비 가능하다.
    public const int ExplosionThreshold = 1;
    public const int CanonicalDamagePerStack = 1;
    public const string KeywordId = "olaf.blood_wound";

    public string UniqueKeywordId => KeywordId;
    public override string EffectName => KeywordId;

    public override StatusEffectStorageKind StorageKind =>
        StatusEffectStorageKind.Bespoke;

    public int DamagePerStack => CanonicalDamagePerStack;

    public int CurrentTurnEndDamage =>
        Mathf.Max(0, Stack);

    public bool CanExplode =>
        Stack > 0;

    public override StatusEffectDurationPolicy DurationPolicy =>
        StatusEffectDurationPolicy.Permanent;

    public override StatusEffectStackPolicy StackPolicy =>
        StatusEffectStackPolicy.AddStacks;

    public Bleeding(
        int stack = 1,
        int duration = -1,
        int damagePerStack = 1)
    {
        Name = "출혈";
        Stack = Mathf.Max(0, stack);
        Duration = InfiniteDuration;

        // 0922 이후 출혈 피해 계수는 고정 1:1이다.
        // 인자는 구 호출부의 named-argument 소스 호환을 위해서만 남긴다.
        _ = duration;
        _ = damagePerStack;
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
    }

    public override void OnTurnEnd(StatusEffectTickContext context)
    {
        if (context?.TargetCharacter == null ||
            Stack <= 0)
        {
            return;
        }

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(
                context.TargetCharacter,
                context.TargetPart);

        int tickDamage =
            CurrentTurnEndDamage;

        if (tickDamage > 0)
        {
            DamageRequest request =
                DamageRequest.Custom(
                    anchor.StatusDamageType,
                    Source ?? Owner,
                    context.TargetCharacter,
                    anchor.Part,
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