public abstract class PrestigeSkill : Skill
{
    public override ActionType ActionType => ActionType.Prestige;

    public override bool CanClash => false;

    public override bool GainPrestige => false;
}