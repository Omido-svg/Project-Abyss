using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public static class WeaponBoneUtility
    {
        public static HumanBodyBones ToHumanBodyBone(WeaponBoneId id)
        {
            return id switch
            {
                WeaponBoneId.LeftShoulder => HumanBodyBones.LeftShoulder,
                WeaponBoneId.LeftUpperArm => HumanBodyBones.LeftUpperArm,
                WeaponBoneId.LeftLowerArm => HumanBodyBones.LeftLowerArm,
                WeaponBoneId.LeftHand => HumanBodyBones.LeftHand,
                WeaponBoneId.RightShoulder => HumanBodyBones.RightShoulder,
                WeaponBoneId.RightUpperArm => HumanBodyBones.RightUpperArm,
                WeaponBoneId.RightLowerArm => HumanBodyBones.RightLowerArm,
                WeaponBoneId.RightHand => HumanBodyBones.RightHand,
                WeaponBoneId.LeftThumbProximal => HumanBodyBones.LeftThumbProximal,
                WeaponBoneId.LeftThumbIntermediate => HumanBodyBones.LeftThumbIntermediate,
                WeaponBoneId.LeftThumbDistal => HumanBodyBones.LeftThumbDistal,
                WeaponBoneId.LeftIndexProximal => HumanBodyBones.LeftIndexProximal,
                WeaponBoneId.LeftIndexIntermediate => HumanBodyBones.LeftIndexIntermediate,
                WeaponBoneId.LeftIndexDistal => HumanBodyBones.LeftIndexDistal,
                WeaponBoneId.LeftMiddleProximal => HumanBodyBones.LeftMiddleProximal,
                WeaponBoneId.LeftMiddleIntermediate => HumanBodyBones.LeftMiddleIntermediate,
                WeaponBoneId.LeftMiddleDistal => HumanBodyBones.LeftMiddleDistal,
                WeaponBoneId.LeftRingProximal => HumanBodyBones.LeftRingProximal,
                WeaponBoneId.LeftRingIntermediate => HumanBodyBones.LeftRingIntermediate,
                WeaponBoneId.LeftRingDistal => HumanBodyBones.LeftRingDistal,
                WeaponBoneId.LeftLittleProximal => HumanBodyBones.LeftLittleProximal,
                WeaponBoneId.LeftLittleIntermediate => HumanBodyBones.LeftLittleIntermediate,
                WeaponBoneId.LeftLittleDistal => HumanBodyBones.LeftLittleDistal,
                WeaponBoneId.RightThumbProximal => HumanBodyBones.RightThumbProximal,
                WeaponBoneId.RightThumbIntermediate => HumanBodyBones.RightThumbIntermediate,
                WeaponBoneId.RightThumbDistal => HumanBodyBones.RightThumbDistal,
                WeaponBoneId.RightIndexProximal => HumanBodyBones.RightIndexProximal,
                WeaponBoneId.RightIndexIntermediate => HumanBodyBones.RightIndexIntermediate,
                WeaponBoneId.RightIndexDistal => HumanBodyBones.RightIndexDistal,
                WeaponBoneId.RightMiddleProximal => HumanBodyBones.RightMiddleProximal,
                WeaponBoneId.RightMiddleIntermediate => HumanBodyBones.RightMiddleIntermediate,
                WeaponBoneId.RightMiddleDistal => HumanBodyBones.RightMiddleDistal,
                WeaponBoneId.RightRingProximal => HumanBodyBones.RightRingProximal,
                WeaponBoneId.RightRingIntermediate => HumanBodyBones.RightRingIntermediate,
                WeaponBoneId.RightRingDistal => HumanBodyBones.RightRingDistal,
                WeaponBoneId.RightLittleProximal => HumanBodyBones.RightLittleProximal,
                WeaponBoneId.RightLittleIntermediate => HumanBodyBones.RightLittleIntermediate,
                WeaponBoneId.RightLittleDistal => HumanBodyBones.RightLittleDistal,
                _ => HumanBodyBones.LastBone
            };
        }

        public static bool IsLeft(WeaponBoneId id) => ((int)id >= 10 && (int)id < 20) || ((int)id >= 100 && (int)id < 200);
        public static bool IsRight(WeaponBoneId id) => ((int)id >= 20 && (int)id < 30) || ((int)id >= 200 && (int)id < 300);
    }
}
