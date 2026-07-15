using System;
using UnityEngine;

[Serializable]
public readonly struct BattleVfxPoolKey : IEquatable<BattleVfxPoolKey>
{
    public BattleVfxPoolKey(GameObject prefab)
    {
        Prefab = prefab;
        PrefabInstanceId = prefab != null ? prefab.GetInstanceID() : 0;
    }

    public GameObject Prefab { get; }
    public int PrefabInstanceId { get; }

    public bool IsValid => Prefab != null && PrefabInstanceId != 0;

    public bool Equals(BattleVfxPoolKey other)
    {
        return PrefabInstanceId == other.PrefabInstanceId;
    }

    public override bool Equals(object obj)
    {
        return obj is BattleVfxPoolKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return PrefabInstanceId;
    }

    public static bool operator ==(BattleVfxPoolKey left, BattleVfxPoolKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BattleVfxPoolKey left, BattleVfxPoolKey right)
    {
        return !left.Equals(right);
    }
}
