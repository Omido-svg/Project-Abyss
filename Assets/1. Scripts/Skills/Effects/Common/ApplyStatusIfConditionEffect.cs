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
        if (context?.Resolver == null ||
            context.Owner == null ||
            context.Target == null)
        {
            return;
        }

        StatusEffect effect =
            StatusEffectFactory.Create(
                StatusEffectId,
                Stack,
                Duration);

        if (effect == null)
            return;

        if (!ForceCharacterStatus &&
            context.TargetPart != null)
        {
            context.Resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    context.Owner,
                    context.Target,
                    context.TargetPart,
                    effect));
            return;
        }

        context.Resolver.ApplyCharacterStatus(
            EffectRequest.CharacterStatus(
                context.Owner,
                context.Target,
                effect));
    }
}
