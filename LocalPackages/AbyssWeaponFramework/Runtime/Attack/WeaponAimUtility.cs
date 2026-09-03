using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    internal static class WeaponAimUtility
    {
        public static Ray ResolveAim(WeaponAttackModule module, in WeaponAttackRequest request, Transform fallback)
        {
            Vector3 origin = request.Origin ?? (fallback != null ? fallback.position : module.transform.position);
            Vector3 direction;

            if (request.TargetPoint.HasValue)
                direction = request.TargetPoint.Value - origin;
            else if (request.Direction.HasValue)
                direction = request.Direction.Value;
            else if (TryProvider(module, out Ray providerRay))
            {
                origin = request.Origin ?? providerRay.origin;
                direction = providerRay.direction;
            }
            else
                direction = fallback != null ? fallback.forward : module.transform.forward;

            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.forward;
            return new Ray(origin, direction.normalized);
        }

        private static bool TryProvider(WeaponAttackModule module, out Ray ray)
        {
            GameObject owner = module.RuntimeContext?.OwnerObject;
            if (owner != null)
            {
                MonoBehaviour[] behaviours = owner.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IWeaponAimProvider provider && provider.TryGetWeaponAim(module.GetComponentInParent<WeaponInstance>(), out ray))
                        return true;
                }
            }
            ray = default;
            return false;
        }
    }
}
