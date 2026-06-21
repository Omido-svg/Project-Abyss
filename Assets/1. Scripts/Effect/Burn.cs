public class Burn : StatusEffect
{
    public Burn(int stack)
    {
        Stack = stack;

        Duration = 3;
    }

    public override void OnTurnEnd(Character owner)
    {
        owner.TakeDamage(Stack * 2);
    }
}