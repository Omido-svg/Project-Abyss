using UnityEngine;

[CreateAssetMenu(
    fileName = "BattleVfxDefinition",
    menuName = "Battle/Visual/VFX Definition")]
public class BattleVfxDefinition : ScriptableObject
{
    [Header("Prefab")]
    public GameObject EffectPrefab;

    [Header("Lifetime")]
    [Min(0f)] public float Lifetime = 1.0f;
    public bool DestroyAfterLifetime = true;
    public bool UseUnscaledLifetime = false;

    [Header("Pooling")]
    public bool UsePooling = true;
    [Min(0)] public int PrewarmCount = 0;
    [Min(0)] public int MaxPoolSize = 8;

    [Header("Legacy Cue Transform Defaults")]
    [Tooltip("Legacy BattleVfxCue 경로에서만 사용됩니다. Skill VFX Timeline Clip은 자체 Transform을 사용합니다.")]
    [HideInInspector] public Vector3 PositionOffset;
    [HideInInspector] public Vector3 RotationOffset;
    [HideInInspector] public Vector3 Scale = Vector3.one;

    [Header("Legacy Cue Follow Default")]
    [Tooltip("Legacy BattleVfxCue 경로에서만 사용됩니다. Skill VFX Timeline Clip은 자체 FollowMode를 사용합니다.")]
    [HideInInspector]
    public BattleVfxFollowMode FollowMode = BattleVfxFollowMode.SpawnWorldFixed;

    [Header("Common Parameters")]
    public Color Color = Color.white;
    public float Intensity = 1f;
    public float Radius = 1f;

    [Header("VFX Graph Option")]
    public string PlayEventName = "OnPlay";

    [Header("Debug")]
    public bool LogSpawn;

    private void OnValidate()
    {
        Lifetime = Mathf.Max(0f, Lifetime);
        PrewarmCount = Mathf.Max(0, PrewarmCount);
        MaxPoolSize = Mathf.Max(0, MaxPoolSize);

        if (MaxPoolSize > 0)
            PrewarmCount = Mathf.Min(PrewarmCount, MaxPoolSize);

        if (Scale == Vector3.zero)
            Scale = Vector3.one;
    }
}