using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponOwnerProxy : MonoBehaviour, IWeaponOwner
    {
        [SerializeField] private GameObject ownerObject;
        [SerializeField] private Transform weaponRoot;

        public GameObject WeaponOwnerObject => ownerObject != null ? ownerObject : gameObject;
        public Transform WeaponRoot => weaponRoot != null ? weaponRoot : transform;

        private void Reset()
        {
            ownerObject = gameObject;
            weaponRoot = transform;
        }
    }
}
