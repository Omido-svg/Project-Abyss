public class Bleeding : StatusEffect
{
    public Bleeding(int stack)
    {
        Name = "출혈";

        Stack = stack;

        Duration = 999;
    }

    public override void OnTurnEnd()
    {
        owner.TakeDamage(Stack);

        Stack--;

        if (Stack <= 0)
        {
            owner.RemoveStatus(this);
        }
    }

    public override void Merge(StatusEffect other)
    {
        Bleeding bleeding = (Bleeding)other;

        Stack += bleeding.Stack;
    }
}