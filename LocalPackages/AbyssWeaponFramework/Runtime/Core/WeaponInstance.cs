using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponInstance : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;
        [SerializeField] private bool includeInactiveChildren = true;

        private WeaponGripPoint[] grips = Array.Empty<WeaponGripPoint>();
        private WeaponPoint[] points = Array.Empty<WeaponPoint>();
        private WeaponModule[] modules = Array.Empty<WeaponModule>();

        public WeaponDefinition Definition => definition;
        public IReadOnlyList<WeaponGripPoint> Grips => grips;
        public IReadOnlyList<WeaponPoint> Points => points;
        public IReadOnlyList<WeaponModule> Modules => modules;
        public WeaponRuntimeContext RuntimeContext { get; private set; }

        private void Awake() => RefreshCache();
        private void OnValidate() => RefreshCache();

        public void RefreshCache()
        {
            grips = GetComponentsInChildren<WeaponGripPoint>(includeInactiveChildren);
            points = GetComponentsInChildren<WeaponPoint>(includeInactiveChildren);
            modules = GetComponentsInChildren<WeaponModule>(includeInactiveChildren);
        }

        public WeaponGripPoint FindGrip(string gripId)
        {
            if (string.IsNullOrWhiteSpace(gripId)) return null;
            EnsureCache();
            for (int i = 0; i < grips.Length; i++)
                if (grips[i] != null && string.Equals(grips[i].GripId, gripId, StringComparison.Ordinal)) return grips[i];
            return null;
        }

        public WeaponPoint FindPoint(string pointId)
        {
            if (string.IsNullOrWhiteSpace(pointId)) return null;
            EnsureCache();
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null && string.Equals(points[i].PointId, pointId, StringComparison.Ordinal)) return points[i];
            return null;
        }

        public WeaponPoint FindPoint(WeaponPointType pointType)
        {
            EnsureCache();
            for (int i = 0; i < points.Length; i++)
                if (points[i] != null && points[i].PointType == pointType) return points[i];
            return null;
        }

        public T FindModule<T>() where T : WeaponModule
        {
            EnsureCache();
            for (int i = 0; i < modules.Length; i++) if (modules[i] is T typed) return typed;
            return null;
        }

        public WeaponAttackModule FindAttackModule(string attackId)
        {
            EnsureCache();
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] is WeaponAttackModule attack && string.Equals(attack.AttackId, attackId, StringComparison.Ordinal))
                    return attack;
            }
            return null;
        }

        internal void BindRuntimeDefinition(WeaponDefinition runtimeDefinition)
        {
            if (runtimeDefinition != null) definition = runtimeDefinition;
        }

        internal void BindRuntimeContext(WeaponRuntimeContext context) => RuntimeContext = context;

        internal void NotifyEquipped(WeaponEquipContext context)
        {
            RuntimeContext = context.Runtime;
            EnsureCache();
            for (int i = 0; i < modules.Length; i++) modules[i]?.OnWeaponEquipped(context);
        }

        internal void NotifyUnequipped(WeaponEquipContext context)
        {
            EnsureCache();
            for (int i = 0; i < modules.Length; i++) modules[i]?.OnWeaponUnequipped(context);
            RuntimeContext = null;
        }

        internal void NotifyRemounted(WeaponEquipContext context)
        {
            RuntimeContext = context.Runtime;
            EnsureCache();
            for (int i = 0; i < modules.Length; i++) modules[i]?.OnWeaponRemounted(context);
        }

        private void EnsureCache()
        {
            if (grips == null || points == null || modules == null) RefreshCache();
        }
    }
}
