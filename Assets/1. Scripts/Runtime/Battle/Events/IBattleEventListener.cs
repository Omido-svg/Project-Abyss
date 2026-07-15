public interface IBattleEventListener
{
    bool IsSubscribed { get; }

    void Subscribe();
    void Unsubscribe();
}
