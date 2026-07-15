public interface IBattleVfxPlayable
{
    void Play(BattleVfxPlayData playData);
}

public interface IBattleVfxPoolLifecycle
{
    void OnBattleVfxTakenFromPool();
    void OnBattleVfxReturnedToPool();
}
