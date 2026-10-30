namespace ProjectAbyss.Abilities
{
    /// <summary>
    /// Reuses the existing execution path. It is NOT automatically registered
    /// alongside SkillEffectDispatcher: simultaneous registration would
    /// execute the same legacy effect twice.
    /// </summary>
    public sealed class LegacySkillEffectEntryAdapter
    {
        private readonly SkillEffectEntry entry;
        public LegacySkillEffectEntryAdapter(SkillEffectEntry entry)
        {
            this.entry = entry;
        }
        public SkillEffectResult Execute(SkillEffectContext context, SkillEffectTiming timing)
        {
            return entry?.TryApply(context, timing);
        }
    }
}
