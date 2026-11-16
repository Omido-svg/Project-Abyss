#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectAbyss.Abilities;

public static class NativeResourceUpgradeValues
{

    public static int ResolveAmount(SkillDefinition source,int level)
    {
        var entry=source.EffectEntries.Single();
        var proxy=(TempBalanceSkillProxyEffectDefinition)entry.Definition;
        int amount=entry.Overrides?.OverrideAmount==true
            ?entry.Overrides.Amount:proxy.BaseAmount;
        for(int i=1;i<=level;i++)
        {
            var step=source.UpgradeProfile?.GetStep(i);
            if(step?.EffectPayloads==null)continue;
            foreach(var payload in step.EffectPayloads)
            {
                if(payload==null||!string.Equals(payload.TargetEffectKey,
                    entry.UpgradeKey,StringComparison.Ordinal))continue;
                if(payload.OverrideStack||payload.OverrideDuration||
                   payload.OverrideMinimum||payload.OverrideMaximum||
                   payload.OverrideFlatValue||payload.OverrideMultiplier||
                   payload.OverrideResourceKey||
                   payload.OverrideForceCharacterStatus||
                   payload.OverrideGiveToSelectedTarget)
                    throw new InvalidOperationException(
                        "Unsupported resource upgrade: "+source.SkillId);
                if(payload.OverrideAmount)amount=payload.Amount;
            }
        }
        if(amount<=0)throw new InvalidOperationException(
            "Native resource amount must be positive "+source.SkillId+" lv"+level);
        return amount;
    }
}
#endif
