using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "Abyss Weapon Framework/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private string weaponId = "weapon.id";
        [SerializeField] private string displayName = "Weapon";
        [SerializeField] private WeaponFamily family = WeaponFamily.Unspecified;
        [SerializeField] private WeaponHandUsage handUsage = WeaponHandUsage.OneHanded;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private WeaponAttackProfile[] attackProfiles = Array.Empty<WeaponAttackProfile>();

        public string WeaponId => weaponId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public WeaponFamily Family => family;
        public WeaponHandUsage HandUsage => handUsage;
        public GameObject Prefab => prefab;
        public IReadOnlyList<string> Tags => tags;
        public IReadOnlyList<WeaponAttackProfile> AttackProfiles => attackProfiles;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
                return false;

            for (int index = 0; index < tags.Length; index++)
            {
                if (string.Equals(tags[index], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public WeaponAttackProfile FindAttack(string attackId)
        {
            if (string.IsNullOrWhiteSpace(attackId) || attackProfiles == null)
                return null;

            for (int i = 0; i < attackProfiles.Length; i++)
            {
                WeaponAttackProfile profile = attackProfiles[i];
                if (profile != null && string.Equals(profile.AttackId, attackId, StringComparison.Ordinal))
                    return profile;
            }
            return null;
        }

#if UNITY_EDITOR
        public void EditorConfigure(string id, string newDisplayName, GameObject newPrefab, WeaponFamily newFamily, WeaponHandUsage newHandUsage)
        {
            weaponId = id;
            displayName = newDisplayName;
            prefab = newPrefab;
            family = newFamily;
            handUsage = newHandUsage;
        }
#endif
    }
}
