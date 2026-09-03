using System.Collections.Generic;
using ProjectAbyss.WeaponSystem;
using UnityEngine;

public sealed class DualPistolExample : MonoBehaviour
{
    [SerializeField] private WeaponMountController mountController;
    [SerializeField] private WeaponDefinition pistol;

    public bool EquipDualPistols()
    {
        if (mountController == null || pistol == null) return false;
        bool left = mountController.TryEquip(
            pistol,
            out _,
            "Pistol_L",
            new List<WeaponGripSocketBinding> { new("Grip_Main", "LeftHand") },
            true);
        bool right = mountController.TryEquip(
            pistol,
            out _,
            "Pistol_R",
            new List<WeaponGripSocketBinding> { new("Grip_Main", "RightHand") },
            true);
        return left && right;
    }

    public void FireLeft() => Fire("Pistol_L");
    public void FireRight() => Fire("Pistol_R");

    private void Fire(string key)
    {
        WeaponEquipHandle handle = mountController != null ? mountController.FindByKey(key) : null;
        handle?.Instance?.GetComponent<WeaponAttackController>()?.TryAttack("primary");
    }
}
