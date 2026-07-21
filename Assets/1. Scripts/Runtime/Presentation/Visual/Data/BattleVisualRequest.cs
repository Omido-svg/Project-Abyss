using System.Collections.Generic;
using UnityEngine;

public class BattleVisualRequest
{
    private static int nextRequestId;

    public int RequestId;

    public BattleAction SourceAction;
    public BattleAction OpponentAction;

    public Character Attacker;
    public BodyPart AttackerPart;
    public Character Target;
    public BodyPart TargetPart;
    public TargetPoint TargetPoint;

    public ActionType ActionType;
    public SkillVisualDefinition VisualDefinition;

    public List<ClashRollVisualStep> ClashSteps = new();
    public List<int> HitDamages = new();

    [Header("Continuous Clash Sequence")]
    public bool IsClashSequence;
    public List<BattleClashVisualExchange> ClashExchanges = new();

    public bool HasMomentumTimeline;
    public int MomentumAtSequenceStart;
    public int MomentumAfterSequence;

    public bool HasClashSequence =>
        IsClashSequence &&
        ClashExchanges != null &&
        ClashExchanges.Count > 0;

    public int FallbackDamage;

    public bool HasWorldPosition;
    public Vector3 WorldPosition;

    public DamageContext DamageContext;
    public DamageResult DamageResult;

    public int RawPower;
    public int RawDamage;
    public int DefenseValue;
    public int GuardValue;
    public int ProtectionValue;
    public int DamageAfterDefense;

    public int FinalHpDamage;
    public int PartHpDamage;
    public int DirectHpDamage;

    public bool WasCritical;
    public bool WasKilled;
    public bool BrokePart;
    public bool WeakenedPart;

    public bool HasTargetPartHpSnapshot;
    public int TargetPartHpBefore;
    public int TargetPartHpAfter;

    public bool HasTargetCharacterHpSnapshot;
    public int TargetCharacterHpBefore;
    public int TargetCharacterHpAfter;
    public int TargetCharacterMaxHp;

    public bool IsCharacterLevelTarget =>
        Target != null && TargetPart == null;

    public bool IsSelfTarget =>
        Attacker != null && Attacker == Target;

    public static BattleVisualRequest FromAction(
        BattleAction action,
        SkillVisualDefinition visualDefinition)
    {
        BattleVisualRequest request = new BattleVisualRequest
        {
            RequestId = ++nextRequestId,
            SourceAction = action,
            VisualDefinition = visualDefinition
        };

        if (action == null)
            return request;

        request.Attacker = action.Owner;
        request.AttackerPart = action.OwnerPart;
        request.Target = action.Target;
        request.TargetPart = action.TargetPart;
        request.TargetPoint = new TargetPoint(action.Target, action.TargetPart);

        if (action.Skill != null)
            request.ActionType = action.Skill.ActionType;

        return request;
    }

    public void ApplyDamageContext(DamageContext context)
    {
        DamageContext = context;
        DamageResult = context?.Result;

        if (context == null)
            return;

        if (context.Attacker != null)
            Attacker = context.Attacker;

        if (context.Target != null)
            Target = context.Target;

        if (context.TargetPart != null || TargetPart == null)
            TargetPart = context.TargetPart;

        if (context.Action != null)
        {
            SourceAction = context.Action;
            AttackerPart = context.Action.OwnerPart;
        }

        TargetPoint = new TargetPoint(Target, TargetPart);

        RawPower = context.RawPower;
        RawDamage = context.RawDamage;
        DefenseValue = context.DefenseValue;
        GuardValue = context.GuardValue;
        ProtectionValue = context.ProtectionValue;
        DamageAfterDefense = context.DamageAfterDefense;

        FinalHpDamage = context.FinalHpDamage;
        PartHpDamage = context.PartHpDamage;
        DirectHpDamage = context.DirectHpDamage;

        WasCritical = context.WasCritical;
        WasKilled = context.WasKilled;
        BrokePart = context.BrokePart;
        WeakenedPart = context.WeakenedPart;

        HasTargetCharacterHpSnapshot = true;
        TargetCharacterHpBefore = context.TargetHpBefore;
        TargetCharacterHpAfter = context.TargetHpAfter;
        TargetCharacterMaxHp = context.Target?.MaxCombatHP ?? 0;

        if (!context.HasTargetPartSnapshot)
            return;

        HasTargetPartHpSnapshot = true;
        TargetPartHpBefore = context.TargetPartHpBefore;
        TargetPartHpAfter = context.TargetPartHpAfter;
    }

    public int GetDamageForHitIndex(int hitIndex)
    {
        if (HitDamages == null || HitDamages.Count == 0)
            return FallbackDamage;

        if (hitIndex < 0)
            return HitDamages[0];

        if (hitIndex < HitDamages.Count)
            return HitDamages[hitIndex];

        return 0;
    }
}