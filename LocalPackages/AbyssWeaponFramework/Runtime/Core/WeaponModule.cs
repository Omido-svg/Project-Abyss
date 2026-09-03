using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public abstract class WeaponModule : MonoBehaviour
    {
        public WeaponRuntimeContext RuntimeContext { get; private set; }
        public bool IsEquipped => RuntimeContext != null;

        public virtual void OnWeaponEquipped(WeaponEquipContext context)
        {
            RuntimeContext = context.Runtime;
        }

        public virtual void OnWeaponUnequipped(WeaponEquipContext context)
        {
            RuntimeContext = null;
        }

        public virtual void OnWeaponRemounted(WeaponEquipContext context)
        {
            RuntimeContext = context.Runtime;
        }
    }

    public readonly struct WeaponEquipContext
    {
        public WeaponMountController Controller { get; }
        public WeaponEquipHandle Handle { get; }
        public WeaponRuntimeContext Runtime { get; }

        public WeaponInstance Instance => Handle?.Instance;
        public WeaponDefinition Definition => Handle?.Definition;
        public GameObject OwnerObject => Runtime?.OwnerObject;

        public WeaponEquipContext(WeaponMountController controller, WeaponEquipHandle handle)
        {
            Controller = controller;
            Handle = handle;
            Runtime = handle?.RuntimeContext;
        }
    }
}
