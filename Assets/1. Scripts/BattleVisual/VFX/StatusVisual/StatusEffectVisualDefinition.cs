using UnityEngine;

[CreateAssetMenu(
    fileName = "StatusEffectVisualDefinition",
    menuName = "Battle/Visual/Status Effect Visual Definition")]
public class StatusEffectVisualDefinition : ScriptableObject
{
    [Header("Identity")]
    public string StatusKey;

    [Header("Tick Damage")]
    public BattleVfxDefinition TickDamageVfx;

    [Header("Apply")]
    public BattleVfxDefinition ApplyVfx;

    [Header("Remove")]
    public BattleVfxDefinition RemoveVfx;

    [Header("Damage Number")]
    public Color DamageNumberColor = Color.white;
}