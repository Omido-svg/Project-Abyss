#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class NativeStatusUpgradeValues
{
    public static int ResolveStacks(SkillDefinition d,int level)
    {
        var entry=d.EffectEntries.Single();
        var effect=(TempBalanceSkillProxyEffectDefinition)entry.Definition;
        int amount=entry.Overrides?.OverrideAmount==true
            ?entry.Overrides.Amount:effect.BaseAmount;
        for(int l=1;l<=level;l++)
        {
            var payloads=d.UpgradeProfile?.GetStep(l)?.EffectPayloads;
            if(payloads==null)continue;
            foreach(var p in payloads)
            {
                if(p==null||p.TargetEffectKey!=entry.UpgradeKey)continue;
                if(p.OverrideStack||p.OverrideDuration||
                   p.OverrideMinimum||p.OverrideMaximum||
                   p.OverrideFlatValue||p.OverrideMultiplier||
                   p.OverrideResourceKey||p.OverrideForceCharacterStatus||
                   p.OverrideGiveToSelectedTarget)
                    throw new InvalidOperationException(
                        "Unsupported bleeding upgrade: "+d.SkillId);
                if(p.OverrideAmount)amount=p.Amount;
            }
        }
        if(amount<=0)throw new InvalidOperationException(
            "Nonpositive native status amount "+d.SkillId);
        return amount;
    }
}
#endif
