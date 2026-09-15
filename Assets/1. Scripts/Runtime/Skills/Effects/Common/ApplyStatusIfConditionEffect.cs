using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Common/Apply Status If Condition",
    fileName = "ApplyStatusIfConditionEffect")]
public class ApplyStatusIfConditionEffect : SkillEffectDefinition
{
    public StatusEffectId StatusEffectId;
    [Min(1)] public int Stack = 1;
    [Min(1)] public int Duration = 3;
    [Tooltip("Regeneration 전용. 0이면 Stack 값을 legacy 회복량으로 사용합니다.")]
    [Min(0)] public int RegenerationHealAmount;
    [Tooltip("Regeneration 전용 회복 채널입니다.")]
    public RegenerationRecoveryChannel RegenerationChannel = RegenerationRecoveryChannel.HitPoints;

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
                overrides?.ResolveDuration(Duration) ?? Duration,
                RegenerationHealAmount,
                RegenerationChannel);

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
}