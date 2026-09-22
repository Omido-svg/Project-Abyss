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
    [Min(1)] public int Stack = 1;
    [Min(1)] public int Duration = 3;
    [Tooltip("Regeneration 전용. 0이면 Stack 값을 legacy 회복량으로 사용합니다.")]
    [Min(0)] public int RegenerationHealAmount;
    [Tooltip("Regeneration 전용 회복 채널입니다.")]
    public RegenerationRecoveryChannel RegenerationChannel =
        RegenerationRecoveryChannel.HitPoints;
    [Tooltip("이번 턴 즉시 적용하거나 다음 TurnStart에 예약 적용합니다.")]
    [FormerlySerializedAs("Timing")]
    public StatusApplicationTiming ApplicationTiming =
        StatusApplicationTiming.Immediate;

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

        int resolvedStack =
            overrides?.ResolveStack(Stack) ??
            Stack;

        int resolvedDuration =
            overrides?.ResolveDuration(Duration) ??
            Duration;

        StatusEffect effect =
            ApplicationTiming == StatusApplicationTiming.NextTurn
                ? new DeferredStatusEffect(
                    StatusEffectId,
                    resolvedStack,
                    resolvedDuration,
                    RegenerationHealAmount,
                    RegenerationChannel)
                : StatusEffectFactory.Create(
                    StatusEffectId,
                    resolvedStack,
                    resolvedDuration,
                    RegenerationHealAmount,
                    RegenerationChannel);

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
