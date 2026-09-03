using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    /// <summary>
    /// Optional owner-side filter for teams, invulnerability, friendly-fire, temporary ignore rules, etc.
    /// Return false to make the framework ignore the candidate collider and continue the query.
    /// </summary>
    public interface IWeaponHitFilter
    {
        bool CanWeaponHit(WeaponInstance weapon, Collider candidate);
    }
}
