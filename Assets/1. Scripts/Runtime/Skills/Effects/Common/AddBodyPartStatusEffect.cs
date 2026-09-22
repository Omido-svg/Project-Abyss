using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum StatusApplicationTiming
{
    Immediate = 0,
    NextTurn = 1
}

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Add Status",
    fileName = "AddStatusEffect")]
public class AddBodyPartStatusEffect : SkillEffectDefinition
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

    [Tooltip("이번 턴 즉시 적용하거나 다음 TurnStart에 예약 적용합니다.")]
    [FormerlySerializedAs("Timing")]
    public StatusApplicationTiming ApplicationTiming =
        StatusApplicationTiming.Immediate;

    public StatusEffectStorageKind AuthoredStorageKind =>
        StatusEffectFactory.GetStorageKind(StatusEffectId);

    public bool UsesCanonical0922 =>
        AuthoringSchema == StatusEffectAuthoringSchema.Canonical0922;

    public override void Apply(
        SkillEffectContext context)
    {
        Apply(
            context,
            null);
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
                $"{StatusEffectId}는 Bespoke라 generic Add Status로 authoring할 수 없습니다.");
            return;
        }

        int legacyRegenerationHeal =
            UsesCanonical0922
                ? 0
                : RegenerationHealAmount;

        RegenerationRecoveryChannel legacyRegenerationChannel =
            UsesCanonical0922
                ? RegenerationRecoveryChannel.HitPoints
                : RegenerationChannel;

        StatusEffect effect =
            ApplicationTiming == StatusApplicationTiming.NextTurn
                ? new DeferredStatusEffect(
                    StatusEffectId,
                    resolvedValue,
                    resolvedDuration,
                    legacyRegenerationHeal,
                    legacyRegenerationChannel)
                : StatusEffectFactory.CreateForAuthoring(
                    AuthoringSchema,
                    StatusEffectId,
                    resolvedValue,
                    resolvedDuration,
                    legacyRegenerationHeal,
                    legacyRegenerationChannel);

        if (effect == null)
            return;

        CombatStatusAnchor anchor =
            CombatStatusAnchor.Resolve(
                context.Target,
                context.TargetPart);

        // [0922_PHASE4_EXACT_STATUS_UNDO]
        // Planning 단계 Preparation이 기존 mergeable 상태를 변경한 경우
        // 그 상태의 정확한 직전 Stack/Duration을 저장한다.
        StatusEffect existingBefore =
            FindMergeTarget(
                anchor,
                effect);

        int stackBefore =
            existingBefore?.Stack ??
            0;

        int durationBefore =
            existingBefore is DeferredStatusEffect deferredBefore
                ? deferredBefore.PendingDuration
                : existingBefore?.Duration ?? 0;

        bool applied;

        if (anchor.IsPartAnchor)
        {
            applied =
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
        }
        else
        {
            applied =
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

        if (!applied ||
            context.Action?.ActionType != ActionType.Preparation)
        {
            return;
        }

        PlanningUndoJournal undo =
            context.Action.Slot?.PlanningUndo;

        if (undo == null)
            return;

        if (existingBefore != null)
        {
            undo.Record(
                () =>
                    existingBefore.RestoreStateForPlanning(
                        stackBefore,
                        durationBefore));

            return;
        }

        Character target =
            context.Target;

        BodyPart targetPart =
            anchor.IsPartAnchor
                ? anchor.Part
                : null;

        // 새 Entry가 삽입된 경우 그 exact instance만 제거한다.
        undo.Record(
            () =>
            {
                if (target == null)
                    return;

                if (targetPart != null)
                {
                    target.RemovePartStatus(
                        targetPart,
                        effect,
                        StatusEffectRemoveReason.Cleared);
                }
                else
                {
                    target.RemoveStatus(
                        effect,
                        StatusEffectRemoveReason.Cleared);
                }
            });
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
            // Presence는 legacy marker 1만 저장하고 gameplay N은 없다.
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

    private static StatusEffect FindMergeTarget(
        CombatStatusAnchor anchor,
        StatusEffect incoming)
    {
        if (!anchor.IsValid ||
            incoming == null)
        {
            return null;
        }

        IReadOnlyList<StatusEffect> statuses =
            anchor.IsPartAnchor
                ? anchor.Part?.StatusEffects
                : anchor.Character?.StatusEffects;

        if (statuses == null)
            return null;

        foreach (StatusEffect existing in statuses)
        {
            if (existing != null &&
                existing.CanMergeWith(incoming))
            {
                return existing;
            }
        }

        return null;
    }
}
