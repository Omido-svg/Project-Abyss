using UnityEngine;

/// <summary>
/// C-35 authoring bridge. Phase E emotion assets can use this definition without adding
/// character-specific runtime branches.
/// </summary>
[CreateAssetMenu(
    menuName = "Battle/Progression/Emotion Rulebreaker Effect",
    fileName = "NewEmotionRulebreakerEffect")]
public sealed class EmotionRulebreakerEffectDefinition : EmotionAugmentEffectDefinition
{
    public EmotionRulebreakerOperation Operation;
    public PartType TargetPartType = PartType.HEAD;
    [Tooltip("부위 재생처럼 특정 부위를 고르지 않는 효과는 true. 이 경우 서비스가 정본 우선순위로 1곳을 선택합니다.")]
    public bool AnyPart;
    [Min(0)] public int Amount = 1;
    public float Value = 1f;
    [Min(1)] public int RequiredConsecutiveTurns = 1;
    public bool BooleanValue = true;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        Character owner = context?.Owner;
        EmotionRulebreakerService service = context?.BattleContext?.Services?.EmotionRulebreakerService;
        if (owner == null || service == null) return;

        switch (Operation)
        {
            case EmotionRulebreakerOperation.PreserveNextBrokenPartSlots:
                service.GrantBrokenPartSlotPreservation(owner, Mathf.Max(1, Amount)); break;
            case EmotionRulebreakerOperation.AddPartSlots:
                service.AddPartSlots(owner, TargetPartType, Amount); break;
            case EmotionRulebreakerOperation.SuppressPartSlots:
                service.SetPartSlotsSuppressed(owner, TargetPartType, BooleanValue); break;
            case EmotionRulebreakerOperation.RegenerateBrokenPartAsWeakened:
                service.RegenerateOneBrokenPartAsWeakened(owner, AnyPart ? (PartType?)null : TargetPartType); break;
            case EmotionRulebreakerOperation.OverrideHpResistance:
                service.SetHpResistanceOverride(owner, Mathf.Max(0f, Value)); break;
            case EmotionRulebreakerOperation.DisableStaggerGauge:
                service.SetStaggerGaugeSuppressed(owner, BooleanValue); break;
            case EmotionRulebreakerOperation.CarryPositiveMomentum:
                service.ConfigureMomentumCarry(BooleanValue, false); break;
            case EmotionRulebreakerOperation.CarryNegativeMomentum:
                service.ConfigureMomentumCarry(false, BooleanValue); break;
            case EmotionRulebreakerOperation.ConfigureLastStandRevive:
                service.ConfigureLastStandStreakRevive(owner, RequiredConsecutiveTurns, Mathf.Max(1, Amount), Mathf.Clamp01(Value)); break;
            case EmotionRulebreakerOperation.ConfigureBalanceResolutionRepeat:
                service.ConfigureBalanceResolutionRepeat(owner, RequiredConsecutiveTurns, BooleanValue); break;
            case EmotionRulebreakerOperation.ConfigureOverwhelmAscension:
                service.ConfigureOverwhelmAscension(owner, RequiredConsecutiveTurns, Mathf.Max(0f, Value)); break;
        }
    }
}
