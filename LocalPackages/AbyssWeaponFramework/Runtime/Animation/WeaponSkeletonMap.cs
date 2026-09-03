using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [Serializable]
    public sealed class WeaponBoneBinding
    {
        [SerializeField] private WeaponBoneId bone;
        [SerializeField] private Transform transform;
        public WeaponBoneId Bone => bone;
        public Transform Transform => transform;

        public WeaponBoneBinding(WeaponBoneId bone, Transform transform) { this.bone = bone; this.transform = transform; }
    }

    [DisallowMultipleComponent]
    public sealed class WeaponSkeletonMap : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private List<WeaponBoneBinding> bindings = new();
        private readonly Dictionary<WeaponBoneId, Transform> cache = new();

        public Animator Animator => animator;
        public IReadOnlyList<WeaponBoneBinding> Bindings => bindings;
        public bool IsHumanoid => animator != null && animator.isHuman;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman) AutoMapHumanoid();
        }
        private void Awake() => RebuildCache();
        private void OnValidate() => RebuildCache();

        public Transform GetBone(WeaponBoneId id)
        {
            if (cache.Count == 0) RebuildCache();
            cache.TryGetValue(id, out Transform value);
            return value;
        }

        public bool TryGetBone(WeaponBoneId id, out Transform value)
        {
            value = GetBone(id);
            return value != null;
        }

        public void RebuildCache()
        {
            cache.Clear();
            if (bindings == null) return;
            for (int i = 0; i < bindings.Count; i++)
            {
                WeaponBoneBinding b = bindings[i];
                if (b != null && b.Bone != WeaponBoneId.None && b.Transform != null) cache[b.Bone] = b.Transform;
            }
        }

        public void AutoMapHumanoid()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;
            bindings.Clear();
            Array values = Enum.GetValues(typeof(WeaponBoneId));
            foreach (WeaponBoneId id in values)
            {
                if (id == WeaponBoneId.None) continue;
                HumanBodyBones human = WeaponBoneUtility.ToHumanBodyBone(id);
                if (human == HumanBodyBones.LastBone) continue;
                Transform bone = animator.GetBoneTransform(human);
                if (bone != null) bindings.Add(new WeaponBoneBinding(id, bone));
            }
            RebuildCache();
        }
    }
}
