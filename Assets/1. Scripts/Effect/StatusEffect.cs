public abstract class StatusEffect
{
    public string Name { get; protected set; }

    public int Stack { get; protected set; }

    public int Duration { get; protected set; }

    /// 상태이상이 부여될 때
    public virtual void OnApply(Character owner) { }

    /// 턴 시작
    public virtual void OnTurnStart(Character owner) { }

    /// 턴 종료
    public virtual void OnTurnEnd(Character owner) { }

    /// 제거될 때
    public virtual void OnRemove(Character owner) { }

    public void DecreaseDuration()
    {
        Duration--;
    }

    public bool IsExpired()
    {
        return Duration <= 0;
    }
}