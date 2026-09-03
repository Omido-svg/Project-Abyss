using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public interface IWeaponAimProvider
    {
        bool TryGetWeaponAim(WeaponInstance weapon, out Ray ray);
    }
}
