using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Skill Effect/Olaf/Normal Bleed",
    fileName = "OlafNormalBleedEffect")]
public class OlafNormalBleedEffect : SkillEffectDefinition
{
    [SerializeField, Min(1)]
    private int stack = 1;

    [SerializeField, Min(1)]
    private int duration = 3;

    public override void Apply(
        SkillEffectContext context)
    {
        Apply(context, null);
    }

    public override void Apply(
        SkillEffectContext context,
        SkillEffectOverrides overrides)
    {
        if (context?.Owner is not Olaf ||
            context.Target == null ||
            context.Resolver == null ||
            context.Action?.ActionType !=
                ActionType.NormalAttack)
        {
            return;
        }

        // SO Timing을 AfterDamage로 설정한 경우에는
        // 실제 피해를 준 공격에서만 혈상을 부여한다.
        if (context.Timing ==
                SkillEffectTiming.AfterDamage &&
            (context.DamageContext == null ||
             context.DamageContext.GetDisplayDamage() <= 0))
        {
            return;
        }

        OlafMadnessMechanic madness =
            context.Owner.GetMechanic<OlafMadnessMechanic>();

        // 새 규칙은 OlafMadnessMechanic이 합에서 이긴 교환마다
        // 혈상을 직접 부여한다. 기존 SO가 OnExecute/AfterDamage 어느 타이밍으로
        // 남아 있어도 중복 또는 일방 공격 혈상이 생기지 않도록 완전히 위임한다.
        if (madness != null)
            return;

        int bleedAmount =
            overrides?.ResolveStack(stack) ?? stack;
        int bleedDuration =
            overrides?.ResolveDuration(duration) ?? duration;

        Bleeding bleeding =
            new Bleeding(
                bleedAmount,
                bleedDuration);

        bool applied;

        if (context.TargetPart != null &&
            !context.TargetPart.IsBroken)
        {
            applied =
                context.Resolver.ApplyBodyPartStatus(
                    EffectRequest.BodyPartStatus(
                        context.Owner,
                        context.Target,
                        context.TargetPart,
                        bleeding,
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
                        bleeding,
                        context.Action,
                        context.RollIndex,
                        context.Timing,
                        true));
        }

        if (!applied)
            return;

        Debug.Log(
            $"{context.Owner.Data.CharacterName} 일반공격 효과 : " +
            $"혈상 {bleedAmount}, {bleedDuration}턴 부여");
    }
}