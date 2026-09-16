using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gameplay v5 흐트러짐 게이지.
/// HP와 별개이며 성공한 Attack/Stagger 굴림의 최종 굴림값에 별도 흐트러짐 내성을 적용한다.
/// 0이 되면 HP 내성을 전 타입 x2로 덮어쓰는 취약 창을 열고, 다음 턴 종료에 100% 복구한다.
/// </summary>
public sealed class StaggerGaugeMechanic : ReactiveCombatMechanic,
    ICharacterUniqueGaugeProvider,
    IRequiredExchangeReaction
{
    private readonly int maxGauge;
    private int currentGauge;
    private bool vulnerabilityWindowOpen;
    private int recoverAfterTurn = -1;

    // P0 D-09: RED/Attack 한 타격 안에서는 흐트러짐을 HP보다 먼저 계산한다.
    // DamagePipeline이 HP 내성을 계산하기 전에 이 snapshot을 만든 뒤 Exchange에 붙인다.
    private long pendingAttackActionId = -1;
    private int pendingAttackBefore = -1;
    private int pendingAttackAfter = -1;
    private int pendingAttackApplied;

    private bool IsSuppressed =>
        owner?.BattleContext?.Services?.EmotionRulebreakerService?
            .IsStaggerGaugeSuppressed(owner) == true;

    public int CurrentGauge => IsSuppressed ? 0 : currentGauge;
    public int MaxGauge => IsSuppressed ? 0 : maxGauge;
    public bool IsVulnerabilityWindowOpen => !IsSuppressed && vulnerabilityWindowOpen;
    public string GaugeLabel => IsSuppressed ? "흐트러짐 제거" : "흐트러짐";
    public float GaugeNormalized =>
        IsSuppressed || maxGauge <= 0
            ? 0f
            : (float)currentGauge / maxGauge;
    public string GaugeValueText => IsSuppressed
        ? "제거됨"
        : vulnerabilityWindowOpen
            ? $"취약 · HP 내성 ×{GetVulnerabilityMultiplier():0.##}"
            : $"{currentGauge}/{maxGauge}";
    public int GaugeStateVersion =>
        IsSuppressed
            ? int.MinValue
            : (currentGauge * 2) + (vulnerabilityWindowOpen ? 1 : 0);

    protected override ReactiveCombatEventMask EventMask =>
        ReactiveCombatEventMask.TurnEnd;

    public override string MechanicName => "Stagger Gauge v5";

    public StaggerGaugeMechanic(int maxGauge)
    {
        this.maxGauge = Mathf.Max(1, maxGauge);
        currentGauge = this.maxGauge;
    }

    protected override void OnReactiveRegistered()
    {
        currentGauge = maxGauge;
        vulnerabilityWindowOpen = false;
        recoverAfterTurn = -1;
        ClearPendingAttackSnapshot();
    }

    public void ApplyBeforeHealthDamage(
        BattleAction action,
        int rawRollPower,
        PhysicalDamageType physicalType)
    {
        if (IsSuppressed ||
            action == null || action.Skill?.IsRed != true ||
            owner == null || owner.IsDead || vulnerabilityWindowOpen)
        {
            return;
        }

        int raw = Mathf.Max(
            0,
            rawRollPower + ResolveAdditionalStaggerDamage(action, owner));
        if (raw <= 0)
            return;

        float multiplier = owner.Data?.StaggerResistances?.GetMultiplier(physicalType) ?? 1f;
        int staggerDamage = Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));
        if (staggerDamage <= 0)
            return;

        int before = currentGauge;
        currentGauge = Mathf.Max(0, currentGauge - staggerDamage);
        int applied = Mathf.Max(0, before - currentGauge);

        pendingAttackActionId = action.ActionId;
        pendingAttackBefore = before;
        pendingAttackAfter = currentGauge;
        pendingAttackApplied = applied;

        if (currentGauge <= 0)
            OpenVulnerabilityWindow();
    }

    public void ApplyRequiredExchangeReaction(
        ClashExchangeResult exchange)
    {
        if (IsSuppressed ||
            exchange == null || exchange.WasCancelled || exchange.WinnerAction == null)
            return;

        BattleAction winner = exchange.WinnerAction;
        if (winner.Skill == null)
            return;

        Character target = winner.Target ?? exchange.LoserAction?.Owner;
        if (target == null || target.IsDead)
            return;

        if (winner.Skill.IsRed && target == owner)
        {
            if (pendingAttackActionId == winner.ActionId && pendingAttackApplied > 0)
            {
                exchange.StaggerDamage = pendingAttackApplied;
                exchange.StaggerGaugeBefore = pendingAttackBefore;
                exchange.StaggerGaugeAfter = pendingAttackAfter;
            }
            ClearPendingAttackSnapshot();
            return;
        }

        int staggerDamage = CalculateStaggerDamage(winner, target);
        if (staggerDamage <= 0)
            return;

        if (target == owner && !vulnerabilityWindowOpen)
        {
            int before =
                currentGauge;

            currentGauge =
                Mathf.Max(
                    0,
                    currentGauge - staggerDamage);

            int applied =
                Mathf.Max(
                    0,
                    before - currentGauge);

            if (applied > 0)
            {
                exchange.StaggerDamage = applied;
                exchange.StaggerGaugeBefore = before;
                exchange.StaggerGaugeAfter = currentGauge;
            }

            if (currentGauge <= 0)
                OpenVulnerabilityWindow();
        }

        // C-10 / 0915 정본 BLUE:
        // 상대에게 주는 흐트러짐 피해는 상대 내성을 적용하지만, 자기 회복은
        // "굴림 값만큼"의 고정값이며 상대 내성/규칙 배율의 영향을 받지 않는다.
        if (winner.Owner == owner &&
            winner.Skill.IsBlue)
        {
            int fixedSelfRecovery =
                Mathf.Max(0, winner.GetDamagePower());

            Recover(fixedSelfRecovery);
        }
    }

    protected override void OnTurnEnded(int turn)
    {
        if (IsSuppressed)
        {
            vulnerabilityWindowOpen = false;
            currentGauge = maxGauge;
            recoverAfterTurn = -1;
            ClearPendingAttackSnapshot();
            return;
        }

        if (!vulnerabilityWindowOpen || turn < recoverAfterTurn)
            return;

        vulnerabilityWindowOpen = false;
        currentGauge = maxGauge;
        recoverAfterTurn = -1;
    }

    public void Recover(int amount)
    {
        if (IsSuppressed || amount <= 0 || vulnerabilityWindowOpen)
            return;
        currentGauge = Mathf.Clamp(currentGauge + amount, 0, maxGauge);
    }

    private float GetVulnerabilityMultiplier()
    {
        return battleContext?.Rules?.Stagger?
                   .VulnerabilityHpResistanceOverride ?? 2f;
    }

    private int CalculateStaggerDamage(BattleAction action, Character target)
    {
        int raw = Mathf.Max(
            0,
            action.GetDamagePower() + ResolveAdditionalStaggerDamage(action, target));
        if (raw <= 0)
            return 0;

        PhysicalDamageType type = PhysicalDamageResolver.Resolve(action);
        float multiplier = target.Data?.StaggerResistances?.GetMultiplier(type) ?? 1f;
        return Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));
    }

    private static int ResolveAdditionalStaggerDamage(
        BattleAction action,
        Character target)
    {
        IReadOnlyList<CombatMechanic> mechanics = action?.Owner?.Mechanics;
        if (mechanics == null)
            return 0;

        int total = 0;
        foreach (CombatMechanic mechanic in mechanics)
        {
            if (mechanic is IAdditionalStaggerDamageProvider provider)
            {
                total += Mathf.Max(
                    0,
                    provider.GetAdditionalStaggerDamage(action, target));
            }
        }

        return Mathf.Max(0, total);
    }

    private void ClearPendingAttackSnapshot()
    {
        pendingAttackActionId = -1;
        pendingAttackBefore = -1;
        pendingAttackAfter = -1;
        pendingAttackApplied = 0;
    }

    private void OpenVulnerabilityWindow()
    {
        vulnerabilityWindowOpen = true;
        currentGauge = 0;
        int currentTurn = battleContext?.Services?.TurnManager?.CurrentTurn ?? 1;
        // 발동한 턴의 잔여 구간 + 다음 한 턴을 보장하고 다음 턴 종료에 복구한다.
        recoverAfterTurn = currentTurn + 1;
        Debug.Log($"[Stagger] {owner?.Data?.CharacterName ?? owner?.name} 취약 창 OPEN / RecoverAfterTurn={recoverAfterTurn}");
    }
}
