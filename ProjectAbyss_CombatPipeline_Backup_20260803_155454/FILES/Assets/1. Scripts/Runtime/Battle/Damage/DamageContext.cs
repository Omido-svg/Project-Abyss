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

    public DamageStage CurrentStage;
    public readonly List<DamageSnapshot> StageSnapshots = new();

    //--------------------------------
    // 계산 단계
    //--------------------------------

    public int RawPower;

    public float SkillMultiplier = 1f;
    public int FlatDamageBonus;
    public float OwnerDamageMultiplier = 1f;
    public float MomentumMultiplier = 1f;

    public int BaseDamage;
    public int RawDamage;
    public int AttackerModifiedDamage;

    public bool WasCritical;
    public float CriticalMultiplier = 1f;
    public int DamageAfterCritical;

    public int DefenseValue;
    public float PenetrationRate;
    public int PiercingDamage;
    public int BlockableDamage;
    public int DamageAfterArmor;

    public int GuardBefore;
    public int GuardAbsorbed;
    public int GuardAfter;

    public int GuardValue => GuardBefore;

    public int DamageAfterDefense;

    public int TargetModifiedDamage;

    public int ProtectionValue;
    public int ProtectionAbsorbed;
    public int DamageAfterProtection;

    // 기존 호출부 호환용 최종 수정값
    public int ModifiedDamage;

    // 실제 적용 요청값
    public int FinalDamage;

    //--------------------------------
    // 실제 적용 결과
    //--------------------------------

    public bool WasApplied;

    public int AppliedDamage;
    public int AppliedHpDamage;
    public int AppliedPartDamage;

    // 신규 표준 결과 이름
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

        RawPower = Mathf.Max(
            0,
            request.RawPower > 0
                ? request.RawPower
                : request.Damage);

        SkillMultiplier = request.SkillMultiplier;

        CriticalMultiplier =
            request.CriticalMultiplier > 0f
                ? request.CriticalMultiplier
                : 1f;

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

    public void AddProtection(
        int amount)
    {
        if (amount <= 0)
            return;

        ProtectionValue += amount;
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
                GuardBefore,
                ProtectionValue));
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
