public enum DamageType
{
    SkillPart = 0,
    StatusPart = 1,
    Direct = 2,
    True = 3,

    SelfCost = 4,
    Counter = 5,
    Prestige = 6,
    Execution = 7,
    BleedExplosion = 8,

    // 규칙서 §10.2 고정 피해 — 2~4 · 6 · 7단계(내성 · 창 · 균열 · 보호 · 고유 증감 · 최소 1 · 방어도)를
    // 모두 무시하고 8단계(부위 + 전체 이중 차감)부터 처리한다. 흐트러짐 피해 · 교환 · 히트 없음.
    Fixed = 9
}
