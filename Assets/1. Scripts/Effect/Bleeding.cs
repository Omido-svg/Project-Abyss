public class Bleeding : StatusEffect
{
    public Bleeding(int stack)
    {
        Name = "Bleeding";

        Stack = stack;

        Duration = 999;
    }

    public override void OnTurnEnd(Character owner)
    {
        owner.TakeDamage(Stack);

        Stack--;

        if (Stack <= 0)
            Duration = 0;
    }
}