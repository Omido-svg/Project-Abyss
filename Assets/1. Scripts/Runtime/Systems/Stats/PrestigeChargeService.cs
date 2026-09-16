using UnityEngine;

public enum PrestigeChargeReason
{
    // 기존 enum 값은 diagnostics/source compatibility를 위해 보존한다.
    ExchangeParticipant = 0,
    OneSidedParticipant = 1,
    ClashStart = 2,
    ClashWin = 3,
    Kill = 4
}

public sealed class PrestigeChargeContext
{
    public PrestigeChargeReason Reason;
    public Character Recipient;
    public Character OtherCharacter;
    public BattleAction SourceAction;
    public int BaseAmount;
}

public interface IPrestigeChargeModifier
{
    int ModifyPrestigeCharge(PrestigeChargeContext context, int currentAmount);
}

/// <summary>
/// 0916 정본 사건 기반 위세 충전 서비스.
/// 합 시작 +1 / 교환(일방 포함) +1 / 적 처치 +5.
/// 합 다수결 승리는 별도 위세 충전 사건이 아니다.
/// ChargeClashWinner API는 구 호출부/source compatibility를 위해 유지하되
/// PrestigeRuleSettings.ClashWinCharge=0이라 canonical runtime에서는 no-op이다.
/// </summary>
public sealed class PrestigeChargeService
{
    private readonly BattleContext battleContext;
    private readonly PrestigeRuleSettings settings;

    public PrestigeChargeService(BattleContext battleContext)
    {
        this.battleContext = battleContext;
        settings = battleContext?.Rules?.Prestige ?? new PrestigeRuleSettings();
        settings.Normalize();
    }

    public int ChargeClashStart(Character recipient, Character other, BattleAction sourceAction) =>
        Charge(recipient, other, sourceAction, PrestigeChargeReason.ClashStart, settings.ClashStartCharge);

    public int ChargeExchangeParticipant(Character recipient, Character other, BattleAction sourceAction) =>
        Charge(recipient, other, sourceAction, PrestigeChargeReason.ExchangeParticipant, settings.ExchangeCharge);

    public int ChargeOneSidedParticipant(Character recipient, Character other, BattleAction sourceAction) =>
        Charge(recipient, other, sourceAction, PrestigeChargeReason.OneSidedParticipant, settings.ExchangeCharge);

    public int ChargeClashWinner(Character recipient, Character other, BattleAction sourceAction) =>
        Charge(recipient, other, sourceAction, PrestigeChargeReason.ClashWin, settings.ClashWinCharge);

    public int ChargeKill(Character recipient, Character victim, BattleAction sourceAction) =>
        Charge(recipient, victim, sourceAction, PrestigeChargeReason.Kill, settings.KillCharge);

    private int Charge(
        Character recipient,
        Character other,
        BattleAction sourceAction,
        PrestigeChargeReason reason,
        int baseAmount)
    {
        if (recipient == null || recipient.IsDead || baseAmount <= 0)
            return 0;

        if (settings.ExcludePreparationActions &&
            sourceAction?.ActionType == ActionType.Preparation)
            return 0;

        PrestigeChargeContext context = new()
        {
            Reason = reason,
            Recipient = recipient,
            OtherCharacter = other,
            SourceAction = sourceAction,
            BaseAmount = baseAmount
        };

        int amount = baseAmount;
        if (recipient.Mechanics != null)
        {
            foreach (CombatMechanic mechanic in recipient.Mechanics)
            {
                if (mechanic is IPrestigeChargeModifier modifier)
                    amount = Mathf.Max(0, modifier.ModifyPrestigeCharge(context, amount));
            }
        }

        if (amount <= 0)
            return 0;

        bool applied =
            battleContext?.EffectResolver?.AddPrestige(
                EffectRequest.Prestige(
                    sourceAction?.Owner ?? recipient,
                    recipient,
                    amount)) == true;

        if (!applied)
            return 0;

        Debug.Log(
            $"[PrestigeCharge] Target={recipient.Data?.CharacterName}, " +
            $"Reason={reason}, Amount={amount}");

        return amount;
    }
}
