using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [Serializable]
    public sealed class HandPoseBoneData
    {
        [SerializeField] private WeaponBoneId bone;
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        public WeaponBoneId Bone => bone;
        public Quaternion LocalRotation => localRotation;
        public HandPoseBoneData(WeaponBoneId bone, Quaternion localRotation) { this.bone = bone; this.localRotation = localRotation; }
    }

    [CreateAssetMenu(fileName = "HandPose", menuName = "Abyss Weapon Framework/Hand Pose")]
    public sealed class HandPoseDefinition : ScriptableObject
    {
        [SerializeField] private List<HandPoseBoneData> left = new();
        [SerializeField] private List<HandPoseBoneData> right = new();
        public IReadOnlyList<HandPoseBoneData> Left => left;
        public IReadOnlyList<HandPoseBoneData> Right => right;
        public IReadOnlyList<HandPoseBoneData> Get(WeaponHand hand) => hand == WeaponHand.Left ? left : right;

#if UNITY_EDITOR
        public int EditorCapture(WeaponSkeletonMap map, WeaponHand hand)
        {
            if (map == null) return 0;
            List<HandPoseBoneData> target = hand == WeaponHand.Left ? left : right;
            target.Clear();
            foreach (WeaponBoneBinding binding in map.Bindings)
            {
                if (binding == null || binding.Transform == null) continue;
                if (hand == WeaponHand.Left && !WeaponBoneUtility.IsLeft(binding.Bone)) continue;
                if (hand == WeaponHand.Right && !WeaponBoneUtility.IsRight(binding.Bone)) continue;
                if (binding.Bone == WeaponBoneId.LeftShoulder || binding.Bone == WeaponBoneId.LeftUpperArm || binding.Bone == WeaponBoneId.LeftLowerArm || binding.Bone == WeaponBoneId.LeftHand ||
                    binding.Bone == WeaponBoneId.RightShoulder || binding.Bone == WeaponBoneId.RightUpperArm || binding.Bone == WeaponBoneId.RightLowerArm || binding.Bone == WeaponBoneId.RightHand) continue;
                target.Add(new HandPoseBoneData(binding.Bone, binding.Transform.localRotation));
            }
            return target.Count;
        }
#endif
    }
}
