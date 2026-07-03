public class StatusEffectTickContext
{
    public Character TargetCharacter { get; }
    public BodyPart TargetPart { get; }
    public StatusEffect SourceEffect { get; }

    public BattleEffectResolver Resolver
    {
        get
        {
            if (TargetCharacter == null)
                return null;

            if (TargetCharacter.BattleContext == null)
                return null;

            return TargetCharacter.BattleContext.EffectResolver;
        }
    }

    public bool IsPartStatus => TargetPart != null;
    public bool IsCharacterStatus => TargetPart == null;

    public StatusEffectTickContext(
        Character targetCharacter,
        BodyPart targetPart,
        StatusEffect sourceEffect)
    {
        TargetCharacter = targetCharacter;
        TargetPart = targetPart;
        SourceEffect = sourceEffect;
    }
}