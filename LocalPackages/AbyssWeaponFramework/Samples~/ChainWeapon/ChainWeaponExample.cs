using ProjectAbyss.WeaponSystem;
using UnityEngine;

public sealed class ChainWeaponExample : MonoBehaviour
{
    [SerializeField] private WeaponMountController mountController;
    [SerializeField] private WeaponDefinition chainWeapon;
    private WeaponEquipHandle handle;

    public bool Equip()
    {
        return mountController != null && chainWeapon != null && mountController.TryEquip(chainWeapon, out handle, "ChainWeapon");
    }

    public void DisableSimulation()
    {
        handle?.Instance?.GetComponentInChildren<ChainWeaponPhysicsModule>()?.SetSimulation(false);
    }

    public void EnableSimulation()
    {
        handle?.Instance?.GetComponentInChildren<ChainWeaponPhysicsModule>()?.SetSimulation(true);
    }
}
