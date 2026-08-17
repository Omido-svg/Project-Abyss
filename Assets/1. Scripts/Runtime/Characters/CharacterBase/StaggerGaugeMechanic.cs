using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공용 무력화/흐트러짐 게이지.
/// 공격으로 실제 적용된 피해를 기본 1:1로 게이지에 누적 감산하고,
/// 0이 되면 머리를 제외한 정상 부위 하나를 무작위로 다음 한 턴 동안 약화한다.
/// 발동 직후 게이지는 최대치로 복구된다.
/// </summary>
public sealed class StaggerGaugeMechanic : ReactiveCombatMechanic,
    ICharacterUniqueGaugeProvider
{
    private readonly int maxGauge;
    private readonly float damageRatio;
    private int currentGauge;
    private BodyPart temporaryWeakenedPart;
    private float hpBeforeTemporaryWeaken;
    private int recoverAfterTurn = -1;

    public int CurrentGauge => currentGauge;
    public int MaxGauge => maxGauge;
    public string GaugeLabel => "무력화";
    public float GaugeNormalized => maxGauge <= 0 ? 0f : (float)currentGauge / maxGauge;
    public string GaugeValueText => $"{currentGauge}/{maxGauge}";

    protected override ReactiveCombatEventMask EventMask =>
        ReactiveCombatEventMask.DamageResolved |
        ReactiveCombatEventMask.TurnEnd;

    public override string MechanicName => "Stagger Gauge";

    public StaggerGaugeMechanic(int maxGauge, float damageRatio)
    {
        this.maxGauge = Mathf.Max(1, maxGauge);
        this.damageRatio = Mathf.Max(0f, damageRatio);
        currentGauge = this.maxGauge;
    }

    protected override void OnReactiveRegistered()
    {
        currentGauge = maxGauge;
        temporaryWeakenedPart = null;
        hpBeforeTemporaryWeaken = 0f;
        recoverAfterTurn = -1;
    }

    protected override void OnDamageResolved(DamageEventResult result)
    {
        DamageContext context = result?.Context;
        if (context == null || context.Target != owner)
            return;

        if (context.Action == null ||
            context.DamageType == DamageType.StatusPart ||
            context.DamageType == DamageType.True ||
            context.DamageType == DamageType.SelfCost)
        {
            return;
        }

        int applied = context.GetDisplayDamage();
        if (applied <= 0)
            return;

        int staggerDamage = Mathf.Max(
            1,
            Mathf.FloorToInt(applied * damageRatio));

        currentGauge = Mathf.Max(0, currentGauge - staggerDamage);
        if (currentGauge > 0)
            return;

        TriggerStagger();
        currentGauge = maxGauge;
    }

    protected override void OnTurnEnded(int turn)
    {
        if (temporaryWeakenedPart == null || turn < recoverAfterTurn)
            return;

        if (temporaryWeakenedPart.IsWeakened)
        {
            owner.RestoreTemporaryWeakenedPart(
                temporaryWeakenedPart,
                hpBeforeTemporaryWeaken);
        }

        temporaryWeakenedPart = null;
        hpBeforeTemporaryWeaken = 0f;
        recoverAfterTurn = -1;
    }

    private void TriggerStagger()
    {
        if (owner?.BodyParts == null)
            return;

        List<BodyPart> candidates = new();
        foreach (BodyPart part in owner.BodyParts)
        {
            if (part == null ||
                part.Type == PartType.HEAD ||
                part.IsBroken ||
                part.IsWeakened)
            {
                continue;
            }

            candidates.Add(part);
        }

        if (candidates.Count == 0)
            return;

        temporaryWeakenedPart =
            candidates[UnityEngine.Random.Range(0, candidates.Count)];
        hpBeforeTemporaryWeaken = temporaryWeakenedPart.PartHP;

        owner.WeakenPart(temporaryWeakenedPart, owner, null);

        int currentTurn =
            battleContext?.battleManager?.TurnManager?.CurrentTurn ?? 1;

        // 현재 해결 단계에서 발생한 약화가 다음 턴 전체를 유지한 뒤 해제된다.
        recoverAfterTurn = currentTurn + 1;
    }
}
