public enum CombatRollType
{
    Attack = 0,
    Defense = 1
}

public enum RollRngSource
{
    CharacterDefault = 0,
    Dice = 1,
    Coin = 2,
    Chinchiro = 3,

    // 기존 직렬화 값을 보존하기 위해 맨 뒤에 추가한다.
    Slot = 4
}

public enum CombatantTier
{
    Player = 0,
    NormalEnemy = 1,
    EliteEnemy = 2,
    Boss = 3
}

public enum MultiRollPenaltyTiming
{
    OnActionStart = 0,
    AfterRoll = 1,
    OnActionEnd = 2
}

public enum BossPhaseQueuedActionPolicy
{
    KeepAlreadyPlanned = 0,
    CancelNotStarted = 1,
    RebuildNotStarted = 2
}