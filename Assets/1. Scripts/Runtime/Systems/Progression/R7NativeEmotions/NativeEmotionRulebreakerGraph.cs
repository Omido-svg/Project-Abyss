using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NativeEmotionRulebreakerEntry
{
    public EmotionRulebreakerOperation Operation;
    public PartType TargetPartType = PartType.HEAD;
    public bool AnyPart;
    [Min(0)] public int Amount = 1;
    public float Value = 1f;
    [Min(1)] public int RequiredConsecutiveTurns = 1;
    public bool BooleanValue = true;

    public void Apply(EmotionAugmentRuntimeContext context)
    {
        Character owner=context?.Owner;
        var service=context?.BattleContext?.Services?.EmotionRulebreakerService;
        if (owner == null || service == null) return;

        switch(Operation)
        {
            case EmotionRulebreakerOperation.PreserveNextBrokenPartSlots:
                service.GrantBrokenPartSlotPreservation(owner,Mathf.Max(1,Amount));
                break;
            case EmotionRulebreakerOperation.AddPartSlots:
                service.AddPartSlots(owner,TargetPartType,Amount);
                break;
            case EmotionRulebreakerOperation.SuppressPartSlots:
                service.SetPartSlotsSuppressed(owner,TargetPartType,BooleanValue);
                break;
            case EmotionRulebreakerOperation.RegenerateBrokenPartAsWeakened:
                service.RegenerateOneBrokenPartAsWeakened(owner,
                    AnyPart?(PartType?)null:TargetPartType);
                break;
            case EmotionRulebreakerOperation.OverrideHpResistance:
                service.SetHpResistanceOverride(owner,Mathf.Max(0f,Value));
                break;
            case EmotionRulebreakerOperation.DisableStaggerGauge:
                service.SetStaggerGaugeSuppressed(owner,BooleanValue);
                break;
            case EmotionRulebreakerOperation.CarryPositiveMomentum:
                service.ConfigureMomentumCarry(BooleanValue,false);
                break;
            case EmotionRulebreakerOperation.CarryNegativeMomentum:
                service.ConfigureMomentumCarry(false,BooleanValue);
                break;
            case EmotionRulebreakerOperation.ConfigureLastStandRevive:
                service.ConfigureLastStandStreakRevive(owner,
                    RequiredConsecutiveTurns,Mathf.Max(1,Amount),Mathf.Clamp01(Value));
                break;
            case EmotionRulebreakerOperation.ConfigureBalanceResolutionRepeat:
                service.ConfigureBalanceResolutionRepeat(owner,RequiredConsecutiveTurns,
                    BooleanValue);
                break;
            case EmotionRulebreakerOperation.ConfigureOverwhelmAscension:
                service.ConfigureOverwhelmAscension(owner,
                    RequiredConsecutiveTurns,Mathf.Max(0f,Value));
                break;
            default:
                throw new InvalidOperationException(
                    "R7-10 unknown native rulebreaker opcode: "+Operation);
        }
    }
}

public sealed class NativeEmotionRulebreakerGraph : NativeEmotionGraph
{
    public List<NativeEmotionRulebreakerEntry> Operations = new();

    public override bool IsValid => base.IsValid &&
        Operations != null && Operations.Count > 0 &&
        Operations.TrueForAll(x=>x!=null);

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        foreach (var operation in Operations)
            operation.Apply(context);
    }
}

