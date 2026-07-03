public class SkillEffectContext
{
    public BattleAction Action { get; }
    public SkillDefinition SkillDefinition { get; }

    public Character Owner => Action?.Owner;
    public BodyPart OwnerPart => Action?.OwnerPart;

    public Character Target => Action?.Target;
    public BodyPart TargetPart => Action?.TargetPart;

    public BattleContext BattleContext => Owner?.BattleContext;
    public BattleEffectResolver Resolver => BattleContext?.EffectResolver;

    public SkillEffectContext(
        BattleAction action,
        SkillDefinition skillDefinition)
    {
        Action = action;
        SkillDefinition = skillDefinition;
    }
}