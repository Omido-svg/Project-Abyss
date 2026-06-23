public class BattleAction
{
    public Character Owner;
    public Character Target;

    public BodyPart OwnerPart;
    public BodyPart TargetPart;

    public Skill Skill;

    public int Speed;

    public void CalculateSpeed()
    {
        Speed = OwnerPart.CurrentSpeed;
    }
}