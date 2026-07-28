using UnityEngine;

public class BattleVfxPlayData
{
    public BattleVfxDefinition Definition;
    public BattleVfxContext Context;

    public GameObject Instance;
    public BattleVfxInstance PooledInstance;
    public Transform Anchor;

    public Vector3 Position;
    public Quaternion Rotation;

    public float Lifetime;
    public Color Color;
    public float Intensity;
    public float Radius;
    public float PlaybackSpeed = 1f;
}