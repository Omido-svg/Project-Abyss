using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    public enum WeaponFamily
    {
        Unspecified = 0,
        Melee = 10,
        Firearm = 20,
        Bow = 30,
        Shield = 40,
        Magic = 50,
        Tool = 60,
        Throwable = 70,
        Flexible = 80,
        Custom = 100
    }

    public enum WeaponHandUsage
    {
        OneHanded = 0,
        TwoHanded = 10,
        Flexible = 20,
        Paired = 30,
        HandsFree = 40,
        Custom = 100
    }

    public enum WeaponSocketType
    {
        Any = 0,
        Hand = 10,
        Back = 20,
        Hip = 30,
        Chest = 40,
        Leg = 50,
        Shoulder = 60,
        Custom = 100
    }

    public enum WeaponHand
    {
        Any = 0,
        Left = 10,
        Right = 20
    }

    public enum WeaponGripRole
    {
        Primary = 0,
        Support = 10,
        Auxiliary = 20,
        Stow = 30
    }

    public enum WeaponPointType
    {
        Custom = 0,
        Muzzle = 10,
        Ejection = 20,
        Aim = 30,
        ProjectileOrigin = 40,
        BladeStart = 50,
        BladeEnd = 60,
        FX = 70,
        Audio = 80,
        ChainRoot = 90,
        ReturnTarget = 100
    }

    public enum WeaponAttackKind
    {
        Custom = 0,
        Melee = 10,
        Hitscan = 20,
        Projectile = 30
    }

    public enum WeaponQueryTriggerInteraction
    {
        UseGlobal = 0,
        Ignore = 10,
        Collide = 20
    }

    public enum WeaponBoneId
    {
        None = 0,
        LeftShoulder = 10,
        LeftUpperArm = 11,
        LeftLowerArm = 12,
        LeftHand = 13,
        RightShoulder = 20,
        RightUpperArm = 21,
        RightLowerArm = 22,
        RightHand = 23,
        LeftThumbProximal = 100,
        LeftThumbIntermediate = 101,
        LeftThumbDistal = 102,
        LeftIndexProximal = 110,
        LeftIndexIntermediate = 111,
        LeftIndexDistal = 112,
        LeftMiddleProximal = 120,
        LeftMiddleIntermediate = 121,
        LeftMiddleDistal = 122,
        LeftRingProximal = 130,
        LeftRingIntermediate = 131,
        LeftRingDistal = 132,
        LeftLittleProximal = 140,
        LeftLittleIntermediate = 141,
        LeftLittleDistal = 142,
        RightThumbProximal = 200,
        RightThumbIntermediate = 201,
        RightThumbDistal = 202,
        RightIndexProximal = 210,
        RightIndexIntermediate = 211,
        RightIndexDistal = 212,
        RightMiddleProximal = 220,
        RightMiddleIntermediate = 221,
        RightMiddleDistal = 222,
        RightRingProximal = 230,
        RightRingIntermediate = 231,
        RightRingDistal = 232,
        RightLittleProximal = 240,
        RightLittleIntermediate = 241,
        RightLittleDistal = 242
    }

    public static class WeaponPhysicsUtility
    {
        public static QueryTriggerInteraction ToUnity(WeaponQueryTriggerInteraction value)
        {
            return value switch
            {
                WeaponQueryTriggerInteraction.Ignore => QueryTriggerInteraction.Ignore,
                WeaponQueryTriggerInteraction.Collide => QueryTriggerInteraction.Collide,
                _ => QueryTriggerInteraction.UseGlobal
            };
        }
    }
}
