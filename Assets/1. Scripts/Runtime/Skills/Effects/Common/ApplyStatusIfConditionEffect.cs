using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Apply Status If Condition",
    fileName = "ApplyStatusIfConditionEffect")]
public class ApplyStatusIfConditionEffect : SkillEffectDefinition
{
    public StatusEffectId StatusEffectId;

    [Tooltip("Legacy=기존 직렬화 의미 유지. Canonical0922=N/T/∞ 및 Presence T-only 문법을 명시적으로 사용합니다.")]
    public StatusEffectAuthoringSchema AuthoringSchema =
        StatusEffectAuthoringSchema.Legacy;

    [Tooltip("Legacy에서는 Stack. Canonical0922 NumericTimed에서는 N(Value). PresenceTimed에서는 gameplay 값으로 사용하지 않습니다.")]
    [Min(1)] public int Stack = 1;

    [Tooltip("유한 지속시간 T. Canonical0922에서 Infinite Duration이 켜지면 이 값 대신 ∞를 사용합니다.")]
    [Min(1)] public int Duration = 3;

    [Tooltip("Canonical0922 전용. true면 자연 감소하지 않는 ∞ Duration으로 authoring합니다.")]
    public bool InfiniteDuration;

    [Tooltip("Regeneration legacy 전용. Canonical0922 재생은 Stack=N 하나로 HP+Stagger를 둘 다 회복합니다.")]
    [Min(0)] public int RegenerationHealAmount;

    [Tooltip("Regeneration legacy 호환 metadata. Canonical0922 gameplay에서는 사용하지 않습니다.")]
    public RegenerationRecoveryChannel RegenerationChannel =
        RegenerationRecoveryChannel.HitPoints;

    [Tooltip("선택된 대상에 부위가 있어도 캐릭터 상태로 적용합니다.")]
    public bool ForceCharacterStatus;

    public StatusEffectStorageKind AuthoredStorageKind =>
        StatusEffectFactory.GetStorageKind(StatusEffectId);

    public bool UsesCanonical0922 =>
        AuthoringSchema == StatusEffectAuthoringSchema.Canonical0922;

    public override void Apply(
        SkillEffectContext context)
    {
        Apply(context, null);
    }

    public override void Apply(
        SkillEffectContext context,
        SkillEffectOverrides overrides)
    {
        if (context?.Resolver == null ||
            context.Owner == null ||
            context.Target == null)
        {
            return;
        }

        int resolvedValue =
            ResolveAuthoredValue(overrides);

        int resolvedDuration =
            ResolveAuthoredDuration(overrides);

        if (UsesCanonical0922 &&
            !StatusEffectFactory.IsCanonicalGenericAuthorable(StatusEffectId))
        {
            Debug.LogWarning(
                $"[0922 Status Authoring] {name}: " +
                $"{StatusEffectId}는 Bespoke라 generic conditional status authoring을 사용할 수 없습니다.");
            return;
        }

        StatusEffect effect =
            StatusEffectFactory.CreateForAuthoring(
                AuthoringSchema,
                StatusEffectId,
                resolvedValue,
                resolvedDuration,
                UsesCanonical0922
                    ? 0
                    : RegenerationHealAmount,
                UsesCanonical0922
                    ? RegenerationRecoveryChannel.HitPoints
                    : RegenerationChannel);

        if (effect == null)
            return;

        bool forceCharacterStatus =
            overrides?.ResolveForceCharacterStatus(ForceCharacterStatus) ??
            ForceCharacterStatus;

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(context.Target, context.TargetPart);

        if (!forceCharacterStatus && anchor.IsPartAnchor)
        {
            context.Resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    context.Owner,
                    context.Target,
                    anchor.Part,
                    effect,
                    context.Action,
                    context.RollIndex,
                    context.Timing,
                    true));
            return;
        }

        context.Resolver.ApplyCharacterStatus(
            EffectRequest.CharacterStatus(
                context.Owner,
                context.Target,
                effect,
                context.Action,
                context.RollIndex,
                context.Timing,
                true));
    }

    public int ResolveAuthoredValue(
        SkillEffectOverrides overrides)
    {
        int value =
            overrides?.ResolveStack(Stack) ??
            Mathf.Max(1, Stack);

        if (UsesCanonical0922 &&
            AuthoredStorageKind == StatusEffectStorageKind.PresenceTimed)
        {
            return 1;
        }

        return Mathf.Max(1, value);
    }

    public int ResolveAuthoredDuration(
        SkillEffectOverrides overrides)
    {
        int finiteDuration =
            overrides?.ResolveDuration(Duration) ??
            Mathf.Max(1, Duration);

        if (!UsesCanonical0922)
            return finiteDuration;

        bool infinite =
            overrides?.ResolveInfiniteDuration(InfiniteDuration) ??
            InfiniteDuration;

        return infinite
            ? StatusEffect.InfiniteDuration
            : Mathf.Max(1, finiteDuration);
    }
}
