using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    /// <summary>
    /// Reusable sorted cast helpers. The buffer grows and retries when saturated so a
    /// NonAlloc query never silently drops hits. A rare alloc fallback preserves
    /// correctness at the hard safety ceiling.
    /// </summary>
    internal static class WeaponPhysicsQueryUtility
    {
        private const int MinimumCapacity = 16;
        private const int MaximumNonAllocCapacity = 4096;

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new();
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

        public static int CastSorted(
            Vector3 origin,
            float radius,
            Vector3 direction,
            float distance,
            LayerMask hitMask,
            QueryTriggerInteraction triggerInteraction,
            ref RaycastHit[] buffer)
        {
            EnsureCapacity(ref buffer, MinimumCapacity);

            while (true)
            {
                int count = radius > 0f
                    ? Physics.SphereCastNonAlloc(
                        origin,
                        radius,
                        direction,
                        buffer,
                        distance,
                        hitMask,
                        triggerInteraction)
                    : Physics.RaycastNonAlloc(
                        origin,
                        direction,
                        buffer,
                        distance,
                        hitMask,
                        triggerInteraction);

                if (count < buffer.Length)
                {
                    if (count > 1)
                        Array.Sort(buffer, 0, count, RaycastHitDistanceComparer.Instance);
                    return count;
                }

                if (buffer.Length < MaximumNonAllocCapacity)
                {
                    EnsureCapacity(
                        ref buffer,
                        Math.Min(buffer.Length * 2, MaximumNonAllocCapacity));
                    continue;
                }

                // Saturated even at the safety ceiling. Preserve exact hit coverage rather
                // than silently accepting an unspecified subset from NonAlloc.
                RaycastHit[] exact = radius > 0f
                    ? Physics.SphereCastAll(
                        origin,
                        radius,
                        direction,
                        distance,
                        hitMask,
                        triggerInteraction)
                    : Physics.RaycastAll(
                        origin,
                        direction,
                        distance,
                        hitMask,
                        triggerInteraction);

                Array.Sort(exact, RaycastHitDistanceComparer.Instance);
                buffer = exact;
                return exact.Length;
            }
        }

        private static void EnsureCapacity(ref RaycastHit[] buffer, int capacity)
        {
            if (buffer != null && buffer.Length >= capacity)
                return;

            buffer = new RaycastHit[Mathf.NextPowerOfTwo(Mathf.Max(MinimumCapacity, capacity))];
        }
    }
}
