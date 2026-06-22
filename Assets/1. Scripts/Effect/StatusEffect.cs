public abstract class StatusEffect
{
    public string Name { get; protected set; }

    public int Stack { get; protected set; }

    public int Duration { get; protected set; }

    protected Character owner;

    protected Character source;

    public virtual void Initialize(
        Character owner,
        Character source)
    {
        this.owner = owner;
        this.source = source;
    }

    public virtual void OnApply() { }

    public virtual void OnTurnStart() { }

    public virtual void OnTurnEnd() { }

    public virtual void OnRemove() { }

    public virtual void Merge(StatusEffect other)
    {
    }

    public void DecreaseDuration()
    {
        Duration--;
    }

    public bool IsExpired()
    {
        return Duration <= 0;
    }
}