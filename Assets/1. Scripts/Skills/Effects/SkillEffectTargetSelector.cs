public enum SkillEffectTargetSelector
{
    // 기존 SO 호환을 위해 반드시 0을 유지한다.
    CurrentTarget = 0,
    Owner = 1,
    OwnerCharacter = 2,
    Opponent = 3,
    DamageTarget = 4,
    KillVictim = 5,
    RandomAliveEnemy = 6,
    RandomAliveEnemyPart = 7,
    RandomUsableOwnerPart = 8
}
