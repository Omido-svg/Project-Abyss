using UnityEngine;

public enum PrestigeChargeReason
{
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

/// <summary>
/// 김삿갓처럼 표준 위세 게이지 대신 고유 자원을 사용하는 메커닉이 구현한다.
/// 0을 반환하면 해당 표준 충전을 완전히 차단할 수 있다.
/// </summary>
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

    public int ChargeClashStart(
        Character recipient,
        Character other,
        BattleAction sourceAction)
    {
        return Charge(
            recipient,
            other,
            sourceAction,
            PrestigeChargeReason.ClashStart,
            settings.ClashStartCharge);
    }

    public int ChargeHitDealt(
        Character recipient,
        Character target,
        BattleAction sourceAction)
    {
        return Charge(
            recipient,
            target,
            sourceAction,
            PrestigeChargeReason.HitDealt,
            settings.HitDealtCharge);
    }

    public int ChargeHitTaken(
        Character recipient,
        Character attacker,
        BattleAction sourceAction)
    {
        return Charge(
            recipient,
            attacker,
            sourceAction,
            PrestigeChargeReason.HitTaken,
            settings.HitTakenCharge);
    }

    public int ChargeClashVictory(
        Character recipient,
        Character loser,
        BattleAction sourceAction)
    {
        return Charge(
            recipient,
            loser,
            sourceAction,
            PrestigeChargeReason.ClashVictory,
            settings.ClashVictoryCharge);
    }

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

                amount = modifier.ModifyPrestigeCharge(
                    context,
                    amount);

                amount = Mathf.Max(0, amount);
            }
        }

        if (amount <= 0)
            return 0;

        // 위세 획득 배율은 CharacterResourceController.AddPrestige에서
        // 중앙 적용한다. 이 서비스에서 다시 곱하면 배율이 제곱된다.
        int finalAmount = Mathf.Max(0, amount);

        if (finalAmount <= 0)
            return 0;

        bool applied =
            battleContext?.EffectResolver?.AddPrestige(
                EffectRequest.Prestige(
                    sourceAction?.Owner ?? recipient,
                    recipient,
                    finalAmount)) == true;

        if (!applied)
            return 0;

        Debug.Log(
            $"[PrestigeCharge] " +
            $"Target={recipient.Data?.CharacterName}, " +
            $"Reason={reason}, Amount={finalAmount}");

        return finalAmount;
    }
}