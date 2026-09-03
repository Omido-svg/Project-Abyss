using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    /// <summary>
    /// Put this next to the character Animator. Animation Events can pass "InstanceKey|AttackId".
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponAnimationEventRelay : MonoBehaviour
    {
        [SerializeField] private WeaponMountController mountController;
        private void Reset() => mountController = GetComponent<WeaponMountController>();

        public void FireWeaponAttack(string route)
        {
            if (!TryResolve(route, out WeaponEquipHandle handle, out string attackId)) return;
            handle.Instance?.GetComponent<WeaponAttackController>()?.TryAttack(attackId);
        }

        public void BeginMeleeWindow(string route)
        {
            if (!TryResolve(route, out WeaponEquipHandle handle, out string attackId)) return;
            WeaponAttackModule module = handle.Instance?.FindAttackModule(attackId);
            if (module is MeleeWeaponModule melee) melee.BeginAttackWindow(attackId);
        }

        public void EndMeleeWindow(string route)
        {
            if (!TryResolve(route, out WeaponEquipHandle handle, out string attackId)) return;
            WeaponAttackModule module = handle.Instance?.FindAttackModule(attackId);
            if (module is MeleeWeaponModule melee) melee.EndAttackWindow();
        }

        private bool TryResolve(string route, out WeaponEquipHandle handle, out string attackId)
        {
            handle = null; attackId = "primary";
            if (mountController == null || string.IsNullOrWhiteSpace(route)) return false;
            int separator = route.IndexOf('|');
            string key = separator >= 0 ? route.Substring(0, separator) : route;
            if (separator >= 0 && separator + 1 < route.Length) attackId = route.Substring(separator + 1);
            handle = mountController.FindByKey(key);
            return handle != null && handle.Instance != null;
        }
    }
}
