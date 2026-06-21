public abstract class Passive
{
    protected Character owner;
    protected string passiveName;

    protected Passive(Character owner)
    {
        this.owner = owner;
    }

    public abstract void Register();
    public abstract void Unregister();
}