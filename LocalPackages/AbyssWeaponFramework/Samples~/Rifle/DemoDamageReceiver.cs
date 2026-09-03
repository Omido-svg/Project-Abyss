using ProjectAbyss.WeaponSystem;
using UnityEngine;

public sealed class DemoDamageReceiver : MonoBehaviour, IWeaponDamageReceiver
{
    [SerializeField] private float health = 100f;
    public void ReceiveWeaponHit(in WeaponHitInfo hit)
    {
        health -= hit.Power;
        Debug.Log($"{name} received {hit.AttackKind}/{hit.DamageChannel}, raw power={hit.Power}, health={health}", this);
    }
}
