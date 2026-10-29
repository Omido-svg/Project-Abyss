/// <summary>
/// 1029 정본 §4.2~4.4 / DMG-01:
/// 굴림 보정을 "굴림 자체(피해 기준)"와 "합 판정 전용"으로 분리한다.
///
/// RollPowerModifier:
///   힘 / 쇠약 / 공포 / 부위 파괴 / 광기 등 실제 굴림값을 바꾸는 보정.
///   정본 '굴림 위력'과 피해 기준값에 반영된다.
///
/// JudgmentOnlyModifier:
///   합 판정에만 반영되고 HP/흐트러짐 피해에는 들어가지 않는 보정.
///   속도차, 합 전용 보너스는 이 축에 해당한다.
/// </summary>
public readonly struct RollModification
{
    public int RollPowerModifier { get; }
    public int JudgmentOnlyModifier { get; }

    public RollModification(
        int rollPowerModifier,
        int judgmentOnlyModifier = 0)
    {
        RollPowerModifier = rollPowerModifier;
        JudgmentOnlyModifier = judgmentOnlyModifier;
    }

    public int TotalModifier =>
        RollPowerModifier +
        JudgmentOnlyModifier;

    public static RollModification None => default;
}
