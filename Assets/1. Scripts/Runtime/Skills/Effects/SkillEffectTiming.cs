public enum SkillEffectTiming
{
    // 기존 SO 호환을 위해 0~6 값은 변경하지 않는다.
    OnExecute = 0,
    OnClashWin = 1,
    OnClashLose = 2,
    AfterDamage = 3,
    OnCritical = 4,
    OnKill = 5,
    OnActionEnd = 6,

    // 굴림 소모전 확장 타이밍.
    OnExchangeWin = 7,
    OnExchangeLose = 8,
    OnOneSideHit = 9,
    OnClashDraw = 10
}
