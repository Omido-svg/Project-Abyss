public class Burn : StatusEffect
{
    public Burn(int stack)
    {
        Stack = stack;

        Duration = 3;
    }

    public override void OnTurnEnd()
    {
        BodyPart part = GetRandomAlivePart();

        owner.TakeDamage(
            part,
            Stack * 2);
    }
}