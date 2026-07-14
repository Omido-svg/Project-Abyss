using UnityEngine;

[CreateAssetMenu(
    fileName = "StatusEffectVisualDefinition",
    menuName = "Battle/Visual/Status Effect Visual Definition")]
public class StatusEffectVisualDefinition : ScriptableObject
{
    [Header("Identity")]
    public string StatusKey;

    [Header("Lifecycle")]
    public BattleVfxDefinition ApplyVfx;
    public BattleVfxDefinition RefreshVfx;
    public BattleVfxDefinition StackVfx;
    public BattleVfxDefinition TickDamageVfx;
    public BattleVfxDefinition RemoveVfx;
    public BattleVfxDefinition ExpireVfx;

    [Header("Persistent")]
    public PersistentBattleVfxDefinition PersistentVfx;

    [Header("Damage Number")]
    public Color DamageNumberColor = Color.white;

    public BattleVfxDefinition GetLifecycleVfx(StatusEffectVisualPhase phase)
    {
        return phase switch
        {
            StatusEffectVisualPhase.Applied => ApplyVfx,
            StatusEffectVisualPhase.Refreshed => RefreshVfx != null ? RefreshVfx : ApplyVfx,
            StatusEffectVisualPhase.Stacked => StackVfx != null ? StackVfx : ApplyVfx,
            StatusEffectVisualPhase.Removed => RemoveVfx,
            StatusEffectVisualPhase.Expired => ExpireVfx != null ? ExpireVfx : RemoveVfx,
            _ => null
        };
    }
}
