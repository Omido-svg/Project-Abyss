public abstract class PreparationSkill : Skill
{
    public override ActionType ActionType => ActionType.Preparation;

    public override bool CanClash => false;

    public override bool GainPrestige => false;
}