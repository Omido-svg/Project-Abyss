using UnityEngine;

/// <summary>
/// Gameplay v5 흐트러짐 게이지.
/// HP와 별개이며 성공한 Attack/Stagger 굴림의 최종 굴림값에 별도 흐트러짐 내성을 적용한다.
/// 0이 되면 HP 내성을 전 타입 x2로 덮어쓰는 취약 창을 열고, 다음 턴 종료에 100% 복구한다.
/// </summary>
public sealed class StaggerGaugeMechanic : ReactiveCombatMechanic,
    ICharacterUniqueGaugeProvider
{
    private readonly int maxGauge;
    private int currentGauge;
    private bool vulnerabilityWindowOpen;
    private int recoverAfterTurn = -1;

    public int CurrentGauge => currentGauge;
    public int MaxGauge => maxGauge;
    public bool IsVulnerabilityWindowOpen => vulnerabilityWindowOpen;
    public string GaugeLabel => "흐트러짐";
    public float GaugeNormalized => maxGauge <= 0 ? 0f : (float)currentGauge / maxGauge;
    public string GaugeValueText => vulnerabilityWindowOpen
        ? $"취약 · HP 내성 ×{GetVulnerabilityMultiplier():0.##}"
        : $"{currentGauge}/{maxGauge}";
    public int GaugeStateVersion => (currentGauge * 2) + (vulnerabilityWindowOpen ? 1 : 0);

    protected override ReactiveCombatEventMask EventMask =>
        ReactiveCombatEventMask.ExchangeResolved |
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
    }

    protected override void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (exchange == null || exchange.WasCancelled || exchange.WinnerAction == null)
            return;

        BattleAction winner = exchange.WinnerAction;
        if (winner.CurrentRollType != CombatRollType.Attack &&
            winner.CurrentRollType != CombatRollType.Stagger)
        {
            return;
        }

        Character target = winner.Target ?? exchange.LoserAction?.Owner;
        if (target == null || target.IsDead)
            return;

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

        // 흐트러짐 공격 굴림은 HP를 주지 않는 대신 자신 흐트러짐을 회복한다.
        if (winner.Owner == owner &&
            winner.CurrentRollType == CombatRollType.Stagger)
        {
            float ratio = battleContext?.Rules?.Stagger?.StaggerRollSelfRecoveryRatio ?? 1f;
            Recover(Mathf.FloorToInt(staggerDamage * Mathf.Max(0f, ratio)));
        }
    }

    protected override void OnTurnEnded(int turn)
    {
        if (!vulnerabilityWindowOpen || turn < recoverAfterTurn)
            return;

        vulnerabilityWindowOpen = false;
        currentGauge = maxGauge;
        recoverAfterTurn = -1;
    }

    public void Recover(int amount)
    {
        if (amount <= 0 || vulnerabilityWindowOpen)
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
        int raw = Mathf.Max(0, action.GetDamagePower());
        if (raw <= 0)
            return 0;

        PhysicalDamageType type = PhysicalDamageResolver.Resolve(action);
        float multiplier = target.Data?.StaggerResistances?.GetMultiplier(type) ?? 1f;
        return Mathf.Max(0, Mathf.FloorToInt(raw * multiplier));
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
