public enum SkillEffectTiming
{
    // 기존 SO 호환을 위해 반드시 0을 유지한다.
    OnExecute = 0,
    OnClashWin = 1,
    OnClashLose = 2,
    AfterDamage = 3,
    OnCritical = 4,
    OnKill = 5,
    OnActionEnd = 6
}
