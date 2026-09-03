using ProjectAbyss.WeaponSystem;
using UnityEngine;

public sealed class RifleExample : MonoBehaviour, IWeaponAimProvider
{
    [SerializeField] private Camera aimCamera;
    [SerializeField] private WeaponMountController mountController;
    [SerializeField] private WeaponDefinition rifle;
    private WeaponEquipHandle handle;

    public bool Equip()
    {
        return mountController != null && rifle != null && mountController.TryEquip(rifle, out handle, "Rifle");
    }

    public void Fire()
    {
        handle?.Instance?.GetComponent<WeaponAttackController>()?.TryAttack("primary");
    }

    public bool TryGetWeaponAim(WeaponInstance weapon, out Ray ray)
    {
        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        if (cameraToUse != null)
        {
            ray = new Ray(cameraToUse.transform.position, cameraToUse.transform.forward);
            return true;
        }
        ray = default;
        return false;
    }
}
