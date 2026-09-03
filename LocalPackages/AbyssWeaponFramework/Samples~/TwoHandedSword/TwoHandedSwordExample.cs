using System.Collections.Generic;
using ProjectAbyss.WeaponSystem;
using UnityEngine;

public sealed class TwoHandedSwordExample : MonoBehaviour
{
    [SerializeField] private WeaponMountController mountController;
    [SerializeField] private WeaponDefinition greatSword;
    private WeaponEquipHandle handle;

    public bool Draw()
    {
        if (mountController == null || greatSword == null) return false;
        return mountController.TryEquip(
            greatSword,
            out handle,
            "GreatSword",
            new List<WeaponGripSocketBinding>
            {
                new("Grip_Main", "RightHand"),
                new("Grip_Support", "LeftHand")
            },
            true);
    }

    public void BeginSlash()
    {
        handle?.Instance?.GetComponentInChildren<MeleeWeaponModule>()?.BeginAttackWindow("slash");
    }

    public void EndSlash()
    {
        handle?.Instance?.GetComponentInChildren<MeleeWeaponModule>()?.EndAttackWindow();
    }
}
