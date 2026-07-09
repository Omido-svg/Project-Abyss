using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleVfxDefinition",
    menuName = "Battle/Visual/VFX Definition")]
public class BattleVfxDefinition : ScriptableObject
{
    [Header("Prefab")]
    public GameObject EffectPrefab;

    [Header("Lifetime")]
    public float Lifetime = 1.0f;
    public bool DestroyAfterLifetime = true;

    [Header("Transform")]
    public Vector3 PositionOffset;
    public Vector3 RotationOffset;
    public Vector3 Scale = Vector3.one;

    [Header("Follow")]
    public BattleVfxFollowMode FollowMode = BattleVfxFollowMode.SpawnWorldFixed;

    [Header("Common Parameters")]
    public Color Color = Color.white;
    public float Intensity = 1f;
    public float Radius = 1f;

    [Header("VFX Graph Option")]
    public string PlayEventName = "OnPlay";

    [Header("Debug")]
    public bool LogSpawn;
}