using UnityEngine;

[CreateAssetMenu(
    menuName = "Battle/Progression/Emotion Augment Prototype Effect",
    fileName = "NewEmotionAugmentPrototypeEffect")]
public sealed class EmotionAugmentPrototypeEffectDefinition : EmotionAugmentEffectDefinition
{
    [Header("Immediate")]
    [Min(0)] public int GainEnergy;
    [Min(0)] public int GainPrestige;
    [Range(0f, 1f)] public float RecoverStaggerRatio;

    [Header("Battle-long Modifier")]
    public int RollBonus;
    [Min(0f)] public float DamageDealtMultiplier = 1f;
    [Min(0f)] public float DamageTakenMultiplier = 1f;
    [Min(0)] public int PositiveMomentumBonus;
    [Min(0f)] public float PrestigeGainMultiplier = 1f;

    public override void Apply(EmotionAugmentRuntimeContext context)
    {
        Character owner = context?.Owner;
        if (owner == null) return;
        if (GainEnergy > 0) owner.AddEnergy(GainEnergy);
        if (GainPrestige > 0) owner.AddPrestige(GainPrestige);
        if (RecoverStaggerRatio > 0f)
        {
            StaggerGaugeMechanic stagger = owner.GetMechanic<StaggerGaugeMechanic>();
            if (stagger != null)
            {
                int amount = Mathf.CeilToInt(stagger.MaxGauge * Mathf.Clamp01(RecoverStaggerRatio));
                stagger.Recover(amount);
            }
        }
    }

    public override CombatMechanic CreateMechanic(EmotionAugmentRuntimeContext context)
    {
        bool hasPersistentModifier =
            RollBonus != 0 ||
            Mathf.Abs(DamageDealtMultiplier - 1f) > 0.0001f ||
            Mathf.Abs(DamageTakenMultiplier - 1f) > 0.0001f ||
            PositiveMomentumBonus > 0 ||
            Mathf.Abs(PrestigeGainMultiplier - 1f) > 0.0001f;
        if (!hasPersistentModifier) return null;
        string label = context?.Augment?.DisplayName ?? "Emotion Augment";
        return new EmotionAugmentPrototypeModifierMechanic(
            label, RollBonus, DamageDealtMultiplier, DamageTakenMultiplier, PositiveMomentumBonus, PrestigeGainMultiplier);
    }
}
