using UnityEngine;

public enum PrestigeChargeReason
{
    ExchangeParticipant,
    OneSidedParticipant,

    // Legacy compatibility
    ClashStart,
    HitDealt,
    HitTaken,
    ClashVictory
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
    int ModifyPrestigeCharge(
        PrestigeChargeContext context,
        int currentAmount);
}

public sealed class PrestigeChargeService
{
    private readonly BattleContext battleContext;
    private readonly PrestigeRuleSettings settings;

    public PrestigeChargeService(
        BattleContext battleContext)
    {
        this.battleContext = battleContext;
        settings = battleContext?.Rules?.Prestige ??
                   new PrestigeRuleSettings();
        settings.Normalize();
    }

    public int ChargeExchangeParticipant(
        Character recipient,
        Character other,
        BattleAction sourceAction)
    {
        return Charge(
            recipient,
            other,
            sourceAction,
            PrestigeChargeReason.ExchangeParticipant,
            settings.ExchangeParticipantCharge);
    }

    public int ChargeOneSidedParticipant(
        Character recipient,
        Character other,
        BattleAction sourceAction)
    {
        return Charge(
            recipient,
            other,
            sourceAction,
            PrestigeChargeReason.OneSidedParticipant,
            settings.OneSidedParticipantCharge);
    }

    public int ChargeClashStart(
        Character recipient,
        Character other,
        BattleAction sourceAction) =>
        Charge(
            recipient,
            other,
            sourceAction,
            PrestigeChargeReason.ClashStart,
            settings.ClashStartCharge);

    public int ChargeHitDealt(
        Character recipient,
        Character target,
        BattleAction sourceAction) =>
        Charge(
            recipient,
            target,
            sourceAction,
            PrestigeChargeReason.HitDealt,
            settings.HitDealtCharge);

    public int ChargeHitTaken(
        Character recipient,
        Character attacker,
        BattleAction sourceAction) =>
        Charge(
            recipient,
            attacker,
            sourceAction,
            PrestigeChargeReason.HitTaken,
            settings.HitTakenCharge);

    public int ChargeClashVictory(
        Character recipient,
        Character loser,
        BattleAction sourceAction) =>
        Charge(
            recipient,
            loser,
            sourceAction,
            PrestigeChargeReason.ClashVictory,
            settings.ClashVictoryCharge);

    private int Charge(
        Character recipient,
        Character other,
        BattleAction sourceAction,
        PrestigeChargeReason reason,
        int baseAmount)
    {
        if (recipient == null ||
            recipient.IsDead ||
            baseAmount <= 0)
        {
            return 0;
        }

        if (settings.PreparationDoesNotCharge &&
            sourceAction?.ActionType == ActionType.Preparation)
        {
            return 0;
        }

        PrestigeChargeContext context =
            new PrestigeChargeContext
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
            foreach (CombatMechanic mechanic
                     in recipient.Mechanics)
            {
                if (mechanic is not
                    IPrestigeChargeModifier modifier)
                {
                    continue;
                }

                amount = Mathf.Max(
                    0,
                    modifier.ModifyPrestigeCharge(
                        context,
                        amount));
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
            $"[PrestigeCharge] " +
            $"Target={recipient.Data?.CharacterName}, " +
            $"Reason={reason}, Amount={amount}");

        return amount;
    }
}
