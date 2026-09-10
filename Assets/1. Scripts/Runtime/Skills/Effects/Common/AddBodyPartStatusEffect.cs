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
    [Tooltip("이번 턴 즉시 적용하거나 다음 TurnStart에 예약 적용합니다.")]
    [FormerlySerializedAs("Timing")]
    public StatusApplicationTiming ApplicationTiming =
        StatusApplicationTiming.Immediate;

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

        int resolvedStack = overrides?.ResolveStack(Stack) ?? Stack;
        int resolvedDuration = overrides?.ResolveDuration(Duration) ?? Duration;

        StatusEffect effect = ApplicationTiming == StatusApplicationTiming.NextTurn
            ? new DeferredStatusEffect(
                StatusEffectId,
                resolvedStack,
                resolvedDuration)
            : StatusEffectFactory.Create(
                StatusEffectId,
                resolvedStack,
                resolvedDuration);

        if (effect == null)
            return;

        if (context.TargetPart != null)
        {
            context.Resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    context.Owner,
                    context.Target,
                    context.TargetPart,
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
}