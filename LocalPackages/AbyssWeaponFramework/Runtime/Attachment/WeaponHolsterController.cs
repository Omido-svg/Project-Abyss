using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponHolsterController : MonoBehaviour
    {
        [SerializeField] private WeaponMountController mountController;
        private void Reset() => mountController = GetComponent<WeaponMountController>();

        public bool TryStow(WeaponEquipHandle handle, string stowGripId, string socketId)
        {
            if (mountController == null || handle == null) return false;
            List<WeaponGripSocketBinding> bindings = new() { new WeaponGripSocketBinding(stowGripId, socketId) };
            return mountController.TryRemount(handle, bindings, true);
        }

        public bool TryDraw(WeaponEquipHandle handle, string primaryGripId, string handSocketId, string supportGripId = null, string supportSocketId = null)
        {
            if (mountController == null || handle == null) return false;
            List<WeaponGripSocketBinding> bindings = new() { new WeaponGripSocketBinding(primaryGripId, handSocketId) };
            if (!string.IsNullOrWhiteSpace(supportGripId) && !string.IsNullOrWhiteSpace(supportSocketId)) bindings.Add(new WeaponGripSocketBinding(supportGripId, supportSocketId));
            return mountController.TryRemount(handle, bindings, true);
        }
    }
}
