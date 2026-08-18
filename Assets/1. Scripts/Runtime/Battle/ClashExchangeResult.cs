using System.Collections.Generic;

public sealed class ClashExchangeResult
{
    public int ExchangeIndex;

    public BattleAction FirstAction;
    public BattleAction SecondAction;

    public bool IsOneSided;
    public bool IsTie;
    public bool WasCancelled;
    public bool WasDefenseResolution;
    public int TieRerollCount;
    public CombatRollType FirstRollType;
    public CombatRollType SecondRollType;

    public int FirstClashPower;
    public int SecondClashPower;

    public RollResult FirstRollResult;
    public RollResult SecondRollResult;

    public BattleAction WinnerAction;
    public BattleAction LoserAction;

    public DamageContext DamageContext;

    public List<DamageContext>
        SecondaryDamageContexts =
            new List<DamageContext>();

    public bool IsDuelExchange;

    public int MomentumBefore;
    public int MomentumAfter;
    public int MomentumShift;
    public int HitMomentumShift;
    public int DuelMomentumShift;

    public int FirstPrestigeGain;
    public int SecondPrestigeGain;

    public int Damage =>
        DamageContext?.GetDisplayDamage() ?? 0;

    public int SecondaryDamage
    {
        get
        {
            int total = 0;

            if (SecondaryDamageContexts == null)
                return total;

            foreach (DamageContext context
                     in SecondaryDamageContexts)
            {
                total +=
                    context?.GetDisplayDamage() ?? 0;
            }

            return total;
        }
    }

    public int TotalDamage =>
        Damage +
        SecondaryDamage;

    public int ResolvedAttackWeight =>
        DamageContext == null
            ? 0
            : 1 +
              (SecondaryDamageContexts?.Count ?? 0);

}
