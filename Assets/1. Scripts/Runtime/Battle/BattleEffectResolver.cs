public class BattleEffectResolver
{
    private readonly BattleContext context;

    public BattleEffectResolver(BattleContext context)
    {
        this.context = context;
    }

    public bool ApplyDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request))
            return false;

        Character target = request.TargetCharacter;

        if (target.IsDead)
            return false;

        DamageContext damageContext = request.DamageContext;

        DamageType damageType =
            damageContext != null
                ? damageContext.DamageType
                : request.DamageType;

        int damage =
            damageContext != null
                ? damageContext.FinalDamage
                : request.Value;

        if (damage <= 0)
            return false;

        BodyPart targetPart =
            damageContext != null
                ? damageContext.TargetPart
                : request.TargetPart;

        Character source =
            damageContext != null
                ? damageContext.Attacker
                : request.SourceCharacter;

        BattleAction sourceAction =
            damageContext != null
                ? damageContext.Action
                : request.SourceAction;

        bool canBreakPart =
            damageContext != null
                ? damageContext.CanBreakPart
                : request.CanBreakPart;

        switch (damageType)
        {
            case DamageType.True:
                target.TakeTrueDamage(
                    damage,
                    request.SourceStatusEffect);
                return true;

            case DamageType.StatusPart:
                if (targetPart == null || targetPart.IsBroken)
                    return false;

                target.TakeStatusPartDamage(
                    targetPart,
                    damage,
                    request.SourceStatusEffect);
                return true;

            case DamageType.Direct:
            case DamageType.SelfCost:
            case DamageType.Execution:
                target.TakeDirectDamage(
                    damage,
                    source,
                    sourceAction);
                return true;
        }

        if (targetPart != null)
        {
            target.TakeDamage(
                targetPart,
                damage,
                canBreakPart);
            return true;
        }

        target.TakeDirectDamage(
            damage,
            source,
            sourceAction);
        return true;
    }

    public bool ApplyDirectDamage(EffectRequest request)
    {
        if (request == null)
            return false;

        request.DamageType = DamageType.Direct;
        request.TargetPart = null;
        return ApplyDamage(request);
    }

    public bool ApplyPartDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetPart == null ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        request.TargetCharacter.TakeDamage(
            request.TargetPart,
            request.Value,
            request.CanBreakPart);
        return true;
    }

    // 기존 호출부 호환. 가능하면 StatusEffectTickContext.ApplyDamage를 사용한다.
    public bool ApplyStatusPartDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetPart == null ||
            request.TargetPart.IsBroken ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        DamageRequest damageRequest =
            DamageRequest.Custom(
                DamageType.StatusPart,
                request.SourceCharacter,
                request.TargetCharacter,
                request.TargetPart,
                request.Value,
                1f,
                false,
                false,
                false,
                null,
                request.SourceStatusEffect);

        ConfigureDotRequest(ref damageRequest);

        DamageContext result =
            context?.ResolveDamageManager()?
                .ApplyDamageContext(damageRequest);

        if (result != null)
            return result.WasApplied;

        request.TargetCharacter.TakeStatusPartDamage(
            request.TargetPart,
            request.Value,
            request.SourceStatusEffect);

        return true;
    }

    public bool ApplyTrueDamage(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        DamageRequest damageRequest =
            DamageRequest.Custom(
                DamageType.True,
                request.SourceCharacter,
                request.TargetCharacter,
                null,
                request.Value,
                1f,
                false,
                false,
                false,
                null,
                request.SourceStatusEffect);

        ConfigureDotRequest(ref damageRequest);

        DamageContext result =
            context?.ResolveDamageManager()?
                .ApplyDamageContext(damageRequest);

        if (result != null)
            return result.WasApplied;

        request.TargetCharacter.TakeTrueDamage(
            request.Value,
            request.SourceStatusEffect);

        return true;
    }

    private static void ConfigureDotRequest(
        ref DamageRequest request)
    {
        request.ApplyMomentum = false;
        request.ApplyAttackerModifiers = false;
        request.ApplyGuard = false;
        request.ApplyTargetModifiers = false;
        request.WasCritical = false;
    }

    public bool ApplyCharacterStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.StatusEffect == null ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        StatusEffectApplyResult result =
            request.TargetCharacter.ApplyStatus(
                request.StatusEffect,
                request.SourceCharacter,
                request.SourcePart,
                request.SourceAction,
                request.SourceExchangeIndex,
                request.SourceEffectTiming,
                request.HasSourceEffectTiming);

        return result?.Succeeded == true;
    }

    public bool ApplyBodyPartStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.StatusEffect == null ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        // Single HP 대상은 TargetPart가 null이어도 캐릭터 상태로 정상 적용한다.
        if (request.TargetPart == null)
        {
            StatusEffectApplyResult characterResult =
                request.TargetCharacter.ApplyStatus(
                    request.StatusEffect,
                    request.SourceCharacter,
                    request.SourcePart,
                    request.SourceAction,
                    request.SourceExchangeIndex,
                    request.SourceEffectTiming,
                    request.HasSourceEffectTiming);

            return characterResult?.Succeeded == true;
        }

        if (request.TargetPart.IsBroken)
            return false;

        StatusEffectApplyResult partResult =
            request.TargetCharacter.ApplyPartStatus(
                request.TargetPart,
                request.StatusEffect,
                request.SourceCharacter,
                request.SourceAction,
                request.SourceExchangeIndex,
                request.SourceEffectTiming,
                request.HasSourceEffectTiming);

        return partResult?.Succeeded == true;
    }

    public bool ForceBreakPart(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetPart == null ||
            request.TargetCharacter.IsDead ||
            request.TargetPart.IsBroken)
        {
            return false;
        }

        request.TargetCharacter.ForceBreakPart(
            request.TargetPart,
            request.SourceCharacter,
            request.SourceAction);

        return true;
    }

    public bool BreakWeakenedPart(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetPart == null ||
            request.TargetCharacter.IsDead ||
            request.TargetPart.IsBroken ||
            !request.TargetPart.IsWeakened)
        {
            return false;
        }

        return request.TargetCharacter.TryBreakWeakenedPart(
            request.TargetPart,
            request.SourceCharacter,
            request.SourceAction);
    }

    public bool AddPrestige(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        request.TargetCharacter.AddPrestige(request.Value);
        return true;
    }

    public bool RemoveBodyPartStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetPart == null ||
            request.StatusEffect == null ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        request.TargetCharacter.RemovePartStatus(
            request.TargetPart,
            request.StatusEffect,
            StatusEffectRemoveReason.Manual);
        return true;
    }

    public bool RecoverPart(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetPart == null ||
            request.TargetCharacter.IsDead ||
            (!request.TargetPart.IsBroken &&
             !request.TargetPart.IsWeakened))
        {
            return false;
        }

        request.TargetCharacter.RecoverPart(
            request.TargetPart);
        return true;
    }

    public bool ForceKill(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        request.TargetCharacter.Die(
            request.SourceCharacter,
            request.SourceAction,
            null);

        return true;
    }

    public bool SetPrestigeToMax(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.TargetCharacter.IsDead ||
            request.TargetCharacter.RuntimeStatus == null ||
            request.TargetCharacter.CurrentStatus == null)
        {
            return false;
        }

        request.TargetCharacter.RuntimeStatus.currentPrestige =
            request.TargetCharacter.CurrentStatus.maxPrestige;
        return true;
    }

    private bool IsValidCommonRequest(EffectRequest request)
    {
        return request != null &&
               request.TargetCharacter != null &&
               request.Value >= 0;
    }
}