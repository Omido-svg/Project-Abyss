using System;
using System.Linq;

public interface ICombatDamageNegationRule { bool NegatesDamage(DamageContext context); }
public interface ICombatEnergyGainRule { int ModifyEnergyGain(int amount); }
public interface ICombatSpeedRangeRule { void ModifySpeedRange(ref int minimum,ref int maximum); }

public static class CombatRuleHooks
{
    public static bool NegatesDamage(DamageContext context)=>context?.Action?.Owner?.Mechanics?
        .OfType<ICombatDamageNegationRule>().Any(rule=>rule.NegatesDamage(context))==true;
    public static int ModifyEnergyGain(Character owner,int amount)
    {
        foreach(var rule in owner?.Mechanics?.OfType<ICombatEnergyGainRule>()??Enumerable.Empty<ICombatEnergyGainRule>())
            amount=rule.ModifyEnergyGain(amount);
        return Math.Max(0,amount);
    }
    public static void ModifySpeedRange(Character owner,ref int minimum,ref int maximum)
    {
        foreach(var rule in owner?.Mechanics?.OfType<ICombatSpeedRangeRule>()??Enumerable.Empty<ICombatSpeedRangeRule>())
            rule.ModifySpeedRange(ref minimum,ref maximum);
    }
}
