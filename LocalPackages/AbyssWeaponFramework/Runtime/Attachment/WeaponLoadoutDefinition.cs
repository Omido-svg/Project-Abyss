using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [Serializable]
    public sealed class WeaponLoadoutEntry
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string instanceKey = "Weapon";
        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private bool useOnlyExplicitBindings;
        [SerializeField] private List<WeaponGripSocketBinding> bindings = new();

        public bool Enabled => enabled;
        public string InstanceKey => instanceKey;
        public WeaponDefinition Weapon => weapon;
        public bool UseOnlyExplicitBindings => useOnlyExplicitBindings;
        public IReadOnlyList<WeaponGripSocketBinding> Bindings => bindings;
    }

    [CreateAssetMenu(
        fileName = "WeaponLoadout",
        menuName = "Project Abyss/Weapon System/Weapon Loadout")]
    public sealed class WeaponLoadoutDefinition : ScriptableObject
    {
        [SerializeField] private List<WeaponLoadoutEntry> entries = new();

        public IReadOnlyList<WeaponLoadoutEntry> Entries => entries;
    }
}
