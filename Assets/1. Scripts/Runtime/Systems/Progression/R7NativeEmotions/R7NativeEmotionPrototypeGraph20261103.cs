using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class R7NativeEmotionPrototypeGraph20261103 : R7NativeEmotionGraph20261103
{
    [Min(0)] public int GainEnergy;
    [Min(0)] public int GainPrestige;
    [Range(0f,1f)] public float RecoverStaggerRatio;
    public int RollBonus;
    [Min(0f)] public float DamageDealtMultiplier=1f;
    [Min(0f)] public float DamageTakenMultiplier=1f;
    [Min(0)] public int PositiveMomentumBonus;
    [Min(0f)] public float PrestigeGainMultiplier=1f;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        Character owner=context?.Owner;
        if(owner == null)return;
        if(GainEnergy>0)owner.AddEnergy(GainEnergy);
        if(GainPrestige>0)owner.AddPrestige(GainPrestige);
        if(RecoverStaggerRatio>0f)
        {
            var stagger=owner.GetMechanic<StaggerGaugeMechanic>();
            if(stagger!=null)stagger.Recover(
                Mathf.CeilToInt(stagger.MaxGauge*Mathf.Clamp01(RecoverStaggerRatio)));
        }
    }

    public override CombatMechanic CreateMechanic(EmotionAugmentRuntimeContext context)
    {
        bool persistent=RollBonus!=0||
            Mathf.Abs(DamageDealtMultiplier-1f)>0.0001f||
            Mathf.Abs(DamageTakenMultiplier-1f)>0.0001f||
            PositiveMomentumBonus>0||
            Mathf.Abs(PrestigeGainMultiplier-1f)>0.0001f;
        if(!persistent)return null;
        return new R7NativeEmotionPrototypeMechanic20261103(
            context?.Augment?.DisplayName??SourceDisplayName,
            RollBonus,DamageDealtMultiplier,DamageTakenMultiplier,
            PositiveMomentumBonus,PrestigeGainMultiplier);
    }
}

