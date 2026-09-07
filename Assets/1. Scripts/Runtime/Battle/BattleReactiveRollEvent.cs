using System;

/// <summary>
/// 계획 단계 ActionSlot에는 없었지만 전투 결과/메커닉에 의해 런타임에 생성된 추가 굴림을
/// 전투 도메인과 Presentation 사이에서 전달하는 공용 이벤트 모델.
///
/// 예: 히후미의 뼈 반격, 향후 추가타/추가 흐트러짐 굴림/후속 굴림.
/// 이 객체는 새 ActionSlot을 위조하지 않는다. 원래 계획 행동(SourceAction)과 파생 굴림을
/// 명시적으로 분리해 행동순서 UI가 보라색 "추가 굴림"으로 표현할 수 있게 한다.
/// </summary>
public sealed class BattleReactiveRollEvent
{
    private static long nextEventId;

    public long EventId { get; } = ++nextEventId;

    public BattleReactiveRollSourceKind SourceKind { get; set; } =
        BattleReactiveRollSourceKind.Mechanic;

    public string DisplayName { get; set; } = "추가 굴림";

    public BattleAction SourceAction { get; set; }
    public Character Owner { get; set; }
    public BodyPart OwnerPart { get; set; }
    public Character Target { get; set; }
    public BodyPart TargetPart { get; set; }

    public CombatRollType RollType { get; set; } = CombatRollType.Attack;
    public PhysicalDamageType PhysicalType { get; set; } = PhysicalDamageType.Cut;

    public int SequenceIndex { get; set; }
    public int SequenceCount { get; set; } = 1;
    public int Power { get; set; }

    public DamageContext DamageContext { get; private set; }
    public bool IsResolved { get; private set; }

    public int DisplaySequenceNumber =>
        Math.Max(1, SequenceIndex + 1);

    public void Complete(DamageContext damageContext)
    {
        DamageContext = damageContext;
        IsResolved = true;
    }
}

public enum BattleReactiveRollSourceKind
{
    Mechanic = 0,
    Counter = 1,
    BonusRoll = 2,
    FollowUp = 3
}
