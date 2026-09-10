using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Apply Status If Condition",
    fileName = "ApplyStatusIfConditionEffect")]
public class ApplyStatusIfConditionEffect : SkillEffectDefinition
{
    public StatusEffectId StatusEffectId;
    [Min(1)] public int Stack = 1;
    [Min(1)] public int Duration = 3;

    [Tooltip("선택된 대상에 부위가 있어도 캐릭터 상태로 적용합니다.")]
    public bool ForceCharacterStatus;

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

        StatusEffect effect =
            StatusEffectFactory.Create(
                StatusEffectId,
                overrides?.ResolveStack(Stack) ?? Stack,
                overrides?.ResolveDuration(Duration) ?? Duration);

        if (effect == null)
            return;

        bool forceCharacterStatus =
            overrides?.ResolveForceCharacterStatus(ForceCharacterStatus) ??
            ForceCharacterStatus;

        if (!forceCharacterStatus &&
            context.TargetPart != null)
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