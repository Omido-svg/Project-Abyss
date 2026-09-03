using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponAttackController : MonoBehaviour
    {
        [SerializeField] private WeaponInstance weapon;
        private readonly Dictionary<string, WeaponAttackModule> modules = new(StringComparer.Ordinal);

        private void Reset() => weapon = GetComponent<WeaponInstance>();
        private void Awake() => Refresh();
        private void OnValidate() { if (!Application.isPlaying) Refresh(); }

        public void Refresh()
        {
            modules.Clear();
            weapon ??= GetComponent<WeaponInstance>();
            if (weapon == null) return;
            WeaponAttackModule[] found = weapon.GetComponentsInChildren<WeaponAttackModule>(true);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null && !string.IsNullOrWhiteSpace(found[i].AttackId) && !modules.ContainsKey(found[i].AttackId)) modules.Add(found[i].AttackId, found[i]);
        }

        public WeaponAttackModule Find(string attackId)
        {
            if (string.IsNullOrWhiteSpace(attackId)) return null;
            if (modules.Count == 0) Refresh();
            modules.TryGetValue(attackId, out WeaponAttackModule module);
            return module;
        }

        public bool TryAttack(string attackId) { WeaponAttackRequest request = default; return TryAttack(attackId, in request); }
        public bool TryAttack(string attackId, in WeaponAttackRequest request) => Find(attackId)?.TryAttack(in request) == true;
    }
}
