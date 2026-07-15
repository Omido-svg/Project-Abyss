public readonly struct TargetSelectionRule
{
    public TargetSelectionRule(
        bool allowBrokenParts,
        bool allowDeadCharacters = false)
    {
        AllowBrokenParts = allowBrokenParts;
        AllowDeadCharacters = allowDeadCharacters;
    }

    public bool AllowBrokenParts { get; }
    public bool AllowDeadCharacters { get; }

    public static TargetSelectionRule StandardAttack =>
        new TargetSelectionRule(
            allowBrokenParts: true);

    public static TargetSelectionRule LivingPartOnly =>
        new TargetSelectionRule(
            allowBrokenParts: false);
}
