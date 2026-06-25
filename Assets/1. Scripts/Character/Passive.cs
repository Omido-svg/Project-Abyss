using UnityEngine;

public abstract class Passive
{
    protected Character owner;
    protected string passiveName;
    protected BattleEvent battleEvent;

    public virtual void Initialize(
        Character owner,
        BattleEvent battleEvent)
    {
        this.owner = owner;
        this.battleEvent = battleEvent;
    }
    public abstract void Register();
    public abstract void Unregister();
}