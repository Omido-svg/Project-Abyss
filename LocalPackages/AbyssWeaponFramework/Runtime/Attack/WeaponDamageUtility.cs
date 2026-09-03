using System;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public static class WeaponDamageUtility
    {
        public static event Action<WeaponHitInfo> AnyHitDelivered;

        public static bool Deliver(in WeaponHitInfo hit)
        {
            Collider collider = hit.HitCollider;
            if (collider == null) return false;

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IWeaponDamageReceiver receiver)
                {
                    receiver.ReceiveWeaponHit(in hit);
                    AnyHitDelivered?.Invoke(hit);
                    return true;
                }
            }

            AnyHitDelivered?.Invoke(hit);
            return false;
        }
    }
}
