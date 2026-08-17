using System.Collections.Generic;
using UnityEngine;

public class DamageContext
{
    public DamageRequest Request;

    public BattleAction Action;

    public Character Attacker;
    public Character Target;
    public BodyPart TargetPart;

    public DamageType DamageType;
    public PhysicalDamageType PhysicalType;
    public float PhysicalResistanceMultiplier = 1f;

    public DamageStage CurrentStage;
    public readonly List<DamageSnapshot> StageSnapshots = new();

    //--------------------------------
    // 피해 계산 파이프라인
    //--------------------------------

    // 합 결과로 확정된 피해 기준 위력.
    public int RawPower;

    // 표준 행동은 1.0. ActionType별 보정은 존재하지 않는다.
    // 보조 대상/Custom 피해처럼 명시적으로 요청된 경우에만 다른 값이 들어온다.
    public float DamageCoefficient = 1f;
    public float MomentumMultiplier = 1f;

    // RawPower × DamageCoefficient × MomentumMultiplier를 버림한 값.
    public int BaseDamage;

    // 공격자 고유 효과 적용 후 값.
    public int AttackerModifiedDamage;

    // 크리티컬 여부는 굴림 결과 메타데이터다.
    // 별도의 추가 곱연산은 수행하지 않는다.
    public bool WasCritical;

    public int GuardBefore;
    public int GuardAbsorbed;
    public int GuardAfter;
    public int GuardValue => GuardBefore;

    // 대상 고유 효과 적용 후 값.
    public int TargetModifiedDamage;

    // 대상 보정까지 끝난 피해를 가드가 흡수한 뒤의 값.
    public int DamageAfterGuard;

    // 실제 적용 요청값.
    public int FinalDamage;

    //--------------------------------
    // 실제 적용 결과
    //--------------------------------

    public bool WasApplied;

    public int AppliedDamage;
    public int AppliedHpDamage;
    public int AppliedPartDamage;

    public int FinalHpDamage;
    public int PartHpDamage;
    public int DirectHpDamage;

    public int TargetHpBefore;
    public int TargetHpAfter;

    public bool HasTargetPartSnapshot;
    public int TargetPartHpBefore;
    public int TargetPartHpAfter;

    public BodyPartState TargetPartStateBefore;
    public BodyPartState TargetPartStateAfter;

    public bool TargetWasDeadBefore;
    public bool TargetWasDeadAfter;

    public bool WasWeakened;
    public bool WasBroken;
    public bool WasKilled;
    public bool WasDirectHPDamage;

    public bool WeakenedPart => WasWeakened;
    public bool BrokePart => WasBroken;

    public bool CanBreakPart;
    public bool IsClashDamage;
    public bool TargetLostClash;
    public bool IsPrestigeClash;

    public bool WasDispatched;

    public DamageResult Result;
    public DamageEventResult EventResult;

    public DamageContext()
    {
    }

    public DamageContext(
        DamageRequest request)
    {
        Request = request;

        Action = request.SourceAction;
        Attacker = request.SourceCharacter;
        Target = request.TargetCharacter;
        TargetPart = request.TargetPart;

        DamageType = request.Type;
        PhysicalType = request.PhysicalType;

        RawPower = Mathf.Max(
            0,
            request.RawPower > 0
                ? request.RawPower
                : request.Damage);

        DamageCoefficient =
            request.DamageCoefficient > 0f
                ? request.DamageCoefficient
                : 0f;

        WasCritical = request.WasCritical;
        CanBreakPart = request.CanBreakPart;
        IsClashDamage = request.IsClashDamage;
        TargetLostClash = request.TargetLostClash;
        IsPrestigeClash = request.IsPrestigeClash;

        CurrentStage = DamageStage.Created;
        RecordStage(
            DamageStage.Created,
            RawPower);
    }


    public void RecordStage(
        DamageStage stage,
        int damage)
    {
        CurrentStage = stage;

        StageSnapshots.Add(
            new DamageSnapshot(
                stage,
                Mathf.Max(0, damage),
                Target,
                TargetPart,
                GuardBefore));
    }

    public DamageSnapshot GetSnapshot(
        DamageStage stage)
    {
        for (int i = StageSnapshots.Count - 1;
             i >= 0;
             i--)
        {
            DamageSnapshot snapshot =
                StageSnapshots[i];

            if (snapshot != null &&
                snapshot.Stage == stage)
            {
                return snapshot;
            }
        }

        return null;
    }

    public int GetDisplayDamage()
    {
        if (Result != null)
            return Result.GetDisplayDamage();

        if (DirectHpDamage > 0)
            return DirectHpDamage;

        if (PartHpDamage > 0)
            return PartHpDamage;

        if (FinalHpDamage > 0)
            return FinalHpDamage;

        if (AppliedDamage > 0)
            return AppliedDamage;

        return 0;
    }

    public int GetPrimaryHpBefore()
    {
        return HasTargetPartSnapshot
            ? TargetPartHpBefore
            : TargetHpBefore;
    }

    public int GetPrimaryHpAfter()
    {
        return HasTargetPartSnapshot
            ? TargetPartHpAfter
            : TargetHpAfter;
    }
}
