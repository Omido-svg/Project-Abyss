using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public static class WeaponTransformUtility
    {
        public static void AlignGripToTarget(
            Transform weaponRoot,
            Transform grip,
            Transform target)
        {
            if (weaponRoot == null || grip == null || target == null)
                return;

            Quaternion rotationDelta =
                target.rotation * Quaternion.Inverse(grip.rotation);

            weaponRoot.rotation = rotationDelta * weaponRoot.rotation;
            weaponRoot.position += target.position - grip.position;
        }
    }
}
