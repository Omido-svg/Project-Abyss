using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public sealed class WeaponRuntimeContext
    {
        public WeaponMountController MountController { get; }
        public WeaponEquipHandle EquipHandle { get; }
        public WeaponInstance Instance => EquipHandle?.Instance;
        public WeaponDefinition Definition => EquipHandle?.Definition;
        public IWeaponOwner OwnerProvider { get; }
        public GameObject OwnerObject => OwnerProvider?.WeaponOwnerObject ?? MountController?.gameObject;
        public Transform WeaponRoot => OwnerProvider?.WeaponRoot ?? MountController?.transform;

        public WeaponRuntimeContext(
            WeaponMountController mountController,
            WeaponEquipHandle equipHandle,
            IWeaponOwner ownerProvider)
        {
            MountController = mountController;
            EquipHandle = equipHandle;
            OwnerProvider = ownerProvider;
        }
    }
}
