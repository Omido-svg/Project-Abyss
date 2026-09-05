using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    internal static class WeaponHitQueryUtility
    {
        public static bool IsSelf(Collider collider, WeaponInstance weapon, GameObject owner)
        {
            if (collider == null) return true;
            Transform hit = collider.transform;
            if (weapon != null && (hit == weapon.transform || hit.IsChildOf(weapon.transform))) return true;
            if (owner != null)
            {
                Transform root = owner.transform;
                if (hit == root || hit.IsChildOf(root)) return true;
            }
            return false;
        }

        public static void RefreshOwnerBehaviours(GameObject owner, List<MonoBehaviour> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            if (owner != null)
                owner.GetComponentsInChildren(true, buffer);
        }

        public static bool PassesOwnerFilters(
            Collider collider,
            WeaponInstance weapon,
            IReadOnlyList<MonoBehaviour> ownerBehaviours)
        {
            if (ownerBehaviours == null) return true;
            for (int i = 0; i < ownerBehaviours.Count; i++)
            {
                if (ownerBehaviours[i] is IWeaponHitFilter filter &&
                    !filter.CanWeaponHit(weapon, collider))
                {
                    return false;
                }
            }
            return true;
        }

        // Compatibility path for external/internal callers that do not maintain a cache.
        public static bool PassesOwnerFilters(Collider collider, WeaponInstance weapon, GameObject owner)
        {
            if (owner == null) return true;
            MonoBehaviour[] behaviours = owner.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IWeaponHitFilter filter && !filter.CanWeaponHit(weapon, collider)) return false;
            return true;
        }

        public static int ResolveTargetId(Collider collider)
        {
            if (collider == null) return 0;
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IWeaponDamageReceiver) return behaviours[i].GetInstanceID();
            if (collider.attachedRigidbody != null) return collider.attachedRigidbody.GetInstanceID();
            return collider.GetInstanceID();
        }

        public static bool HasReceiver(Collider collider)
        {
            if (collider == null) return false;
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) if (behaviours[i] is IWeaponDamageReceiver) return true;
            return false;
        }
    }
}
