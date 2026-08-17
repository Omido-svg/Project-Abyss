using UnityEngine;

public class BattleEffectResolver
{
    private readonly BattleContext context;
    private BattleStatusVisualDirector statusVisualDirector;

    public BattleEffectResolver(BattleContext context)
    {
        this.context = context;

        if (!IsPresentationSuppressed)
            ResolveStatusVisualDirector();
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
                false,
                false,
                null,
                request.SourceStatusEffect);

        ConfigureDotRequest(ref damageRequest);

        DamageContext result =
            context?.ResolveDamageManager()?
                .ApplyDamageContext(damageRequest);

        if (result != null)
        {
            ShowStatusTickVisual(
                request.SourceStatusEffect,
                request.TargetCharacter,
                request.TargetPart,
                result.GetDisplayDamage());

            return result.WasApplied;
        }

        request.TargetCharacter.TakeStatusPartDamage(
            request.TargetPart,
            request.Value,
            request.SourceStatusEffect);

        ShowStatusTickVisual(
            request.SourceStatusEffect,
            request.TargetCharacter,
            request.TargetPart,
            request.Value);

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
                false,
                false,
                null,
                request.SourceStatusEffect);

        ConfigureDotRequest(ref damageRequest);

        DamageContext result =
            context?.ResolveDamageManager()?
                .ApplyDamageContext(damageRequest);

        if (result != null)
        {
            ShowStatusTickVisual(
                request.SourceStatusEffect,
                request.TargetCharacter,
                null,
                result.GetDisplayDamage());

            return result.WasApplied;
        }

        request.TargetCharacter.TakeTrueDamage(
            request.Value,
            request.SourceStatusEffect);

        ShowStatusTickVisual(
            request.SourceStatusEffect,
            request.TargetCharacter,
            null,
            request.Value);

        return true;
    }

    private static void ConfigureDotRequest(
        ref DamageRequest request)
    {
        request.ApplyFlatDamageBonus = false;
        request.ApplyOwnerMultiplier = false;
        request.ApplyMomentum = false;
        request.ApplyAttackerModifiers = false;
        request.ApplyDefense = false;
        request.ApplyGuard = false;
        request.ApplyTargetModifiers = false;
        request.ApplyProtection = false;
        request.WasCritical = false;
    }

    public void ShowStatusTickVisual(
        StatusEffect effect,
        Character target,
        BodyPart targetPart,
        int damage)
    {
        if (IsPresentationSuppressed)
            return;

        if (effect == null ||
            target == null ||
            damage <= 0)
        {
            return;
        }

        ResolveStatusVisualDirector();

        statusVisualDirector?.ShowStatusDamage(
            new StatusDamageVisualRequest
            {
                Target = target,
                TargetPart = targetPart,
                Damage = damage,
                StatusKey = effect.EffectName
            });
    }

    public void ShowStatusApplyVisual(
        StatusEffectApplyResult result)
    {
        if (IsPresentationSuppressed)
            return;

        if (result?.Effect == null ||
            result.TargetCharacter == null ||
            result.Kind == StatusEffectApplyKind.Ignored ||
            result.Kind == StatusEffectApplyKind.Rejected ||
            result.WasTransferred)
        {
            return;
        }

        ResolveStatusVisualDirector();

        StatusEffectVisualPhase phase =
            result.Kind switch
            {
                StatusEffectApplyKind.Stacked =>
                    StatusEffectVisualPhase.Stacked,
                StatusEffectApplyKind.Refreshed =>
                    StatusEffectVisualPhase.Refreshed,
                _ =>
                    StatusEffectVisualPhase.Applied
            };

        statusVisualDirector?.ShowStatusLifecycle(
            new StatusEffectLifecycleVisualRequest
            {
                Target = result.TargetCharacter,
                TargetPart = result.TargetPart,
                StatusKey = result.Effect.EffectName,
                Phase = phase,
                Stack = result.Effect.Stack,
                Duration = result.Effect.Duration
            });
    }

    public void ShowStatusRemoveVisual(
        Character target,
        BodyPart part,
        StatusEffect effect,
        StatusEffectRemoveReason reason)
    {
        if (IsPresentationSuppressed)
            return;

        if (target == null ||
            effect == null ||
            reason == StatusEffectRemoveReason.Transferred)
        {
            return;
        }

        ResolveStatusVisualDirector();

        statusVisualDirector?.ShowStatusLifecycle(
            new StatusEffectLifecycleVisualRequest
            {
                Target = target,
                TargetPart = part,
                StatusKey = effect.EffectName,
                Phase = reason == StatusEffectRemoveReason.Expired
                    ? StatusEffectVisualPhase.Expired
                    : StatusEffectVisualPhase.Removed,
                Stack = effect.Stack,
                Duration = effect.Duration,
                RemoveReason = reason
            });
    }

    public bool ApplyCharacterStatus(EffectRequest request)
    {
        if (!IsValidCommonRequest(request) ||
            request.StatusEffect == null ||
            request.TargetCharacter.IsDead)
        {
            return false;
        }

        request.TargetCharacter.AddStatus(
            request.StatusEffect,
            request.SourceCharacter);
        return true;
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
            request.TargetCharacter.AddStatus(
                request.StatusEffect,
                request.SourceCharacter);
            return true;
        }

        if (request.TargetPart.IsBroken)
            return false;

        request.TargetCharacter.AddPartStatus(
            request.TargetPart,
            request.StatusEffect,
            request.SourceCharacter);
        return true;
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

    private bool IsPresentationSuppressed =>
        context?.SuppressPresentation == true;

    private void ResolveStatusVisualDirector()
    {
        if (IsPresentationSuppressed)
        {
            statusVisualDirector = null;
            return;
        }

        if (statusVisualDirector == null)
        {
            statusVisualDirector =
                Object.FindFirstObjectByType<
                    BattleStatusVisualDirector>();
        }
    }

    private bool IsValidCommonRequest(EffectRequest request)
    {
        return request != null &&
               request.TargetCharacter != null &&
               request.Value >= 0;
    }
}