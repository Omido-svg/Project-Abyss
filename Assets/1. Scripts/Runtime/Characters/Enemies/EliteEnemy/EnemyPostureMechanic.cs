using System;
using UnityEngine;

public enum EnemyPosture
{
    Normal = 0,
    Crouching = 1,
    Offensive = 2
}

[Serializable]
public sealed class EnemyPostureSettings
{
    [Min(1)] public int MinimumTurns = 2;
    [Min(1)] public int MaximumTurns = 3;

    [Min(0)] public int NormalAttackSlotLimit = 3;
    [Min(0)] public int CrouchingAttackSlotLimit = 2;
    [Min(0)] public int OffensiveAttackSlotLimit = 4;

    [Tooltip(
        "자세가 만드는 예상 기세 드리프트를 UI/로그에 표시하기 위한 값입니다. " +
        "기세를 즉시 이동시키지 않으며 실제 히트 결과가 바를 움직입니다.")]
    [Min(0)]
    public int ExpectedMomentumDriftPerTurn = 15;

    public void Normalize()
    {
        MinimumTurns = Mathf.Max(1, MinimumTurns);
        MaximumTurns = Mathf.Max(MinimumTurns, MaximumTurns);
        NormalAttackSlotLimit = Mathf.Max(0, NormalAttackSlotLimit);
        CrouchingAttackSlotLimit = Mathf.Max(0, CrouchingAttackSlotLimit);
        OffensiveAttackSlotLimit = Mathf.Max(0, OffensiveAttackSlotLimit);
        ExpectedMomentumDriftPerTurn =
            Mathf.Max(0, ExpectedMomentumDriftPerTurn);
    }
}

/// <summary>
/// 보통 → 웅크림 → 공세 → 보통 순서로 회전하는 적 자세 정책.
/// 자세는 공격 COMBAT 슬롯 상한만 바꾸며 기세를 직접 수정하지 않는다.
/// 문서의 +15/-15는 공격 굴림 총량 차이에서 기대되는 드리프트다.
/// </summary>
public sealed class EnemyPostureMechanic : CombatMechanic
{
    private readonly EnemyPostureSettings settings;
    private EnemyPosture current = EnemyPosture.Normal;
    private int turnsRemaining;

    public EnemyPosture Current => current;
    public int TurnsRemaining => turnsRemaining;

    public int CurrentAttackSlotLimit => current switch
    {
        EnemyPosture.Crouching => settings.CrouchingAttackSlotLimit,
        EnemyPosture.Offensive => settings.OffensiveAttackSlotLimit,
        _ => settings.NormalAttackSlotLimit
    };

    /// <summary>
    /// 플레이어 관점의 이론상 기세 드리프트.
    /// 실제 CurrentMomentum에는 즉시 반영하지 않는다.
    /// </summary>
    public int ExpectedPlayerMomentumDrift => current switch
    {
        EnemyPosture.Crouching =>
            settings.ExpectedMomentumDriftPerTurn,
        EnemyPosture.Offensive =>
            -settings.ExpectedMomentumDriftPerTurn,
        _ => 0
    };

    public override string MechanicName => "적 자세 로테이션";

    public EnemyPostureMechanic(EnemyPostureSettings settings)
    {
        this.settings = settings ?? new EnemyPostureSettings();
        this.settings.Normalize();
    }

    public override void OnRegister()
    {
        current = EnemyPosture.Normal;
        RollDuration();

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");
    }

    public override void OnUnregister()
    {
        current = EnemyPosture.Normal;
        turnsRemaining = 0;
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null || owner.IsDead)
            return;

        turnsRemaining--;
        if (turnsRemaining > 0)
            return;

        current = current switch
        {
            EnemyPosture.Normal => EnemyPosture.Crouching,
            EnemyPosture.Crouching => EnemyPosture.Offensive,
            _ => EnemyPosture.Normal
        };

        RollDuration();

        Debug.Log(
            $"[EnemyPosture] {owner.Data?.CharacterName} / " +
            $"Turn={turn}, Posture={current}, " +
            $"AttackSlotLimit={CurrentAttackSlotLimit}, " +
            $"ExpectedPlayerMomentumDrift=" +
            $"{ExpectedPlayerMomentumDrift:+#;-#;0}, " +
            $"Duration={turnsRemaining}");
    }

    private void RollDuration()
    {
        turnsRemaining = UnityEngine.Random.Range(
            settings.MinimumTurns,
            settings.MaximumTurns + 1);
    }
}
