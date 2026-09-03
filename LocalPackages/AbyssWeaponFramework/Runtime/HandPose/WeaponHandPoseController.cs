using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.WeaponSystem
{
    [DisallowMultipleComponent]
    public sealed class WeaponHandPoseController : MonoBehaviour
    {
        private sealed class Layer
        {
            public object Source;
            public WeaponHand Hand;
            public HandPoseDefinition Pose;
            public float Weight;
            public int Priority;
        }

        [SerializeField] private WeaponSkeletonMap skeletonMap;
        [SerializeField, Range(0f, 1f)] private float globalWeight = 1f;
        private readonly List<Layer> layers = new();

        private void Reset() => skeletonMap = GetComponent<WeaponSkeletonMap>();

        public void SetPose(object source, WeaponHand hand, HandPoseDefinition pose, float weight = 1f, int priority = 0)
        {
            if (source == null || hand == WeaponHand.Any) return;
            RemovePose(source, hand);
            if (pose == null || weight <= 0f) return;
            layers.Add(new Layer { Source = source, Hand = hand, Pose = pose, Weight = Mathf.Clamp01(weight), Priority = priority });
        }

        public void RemovePose(object source, WeaponHand hand = WeaponHand.Any)
        {
            for (int i = layers.Count - 1; i >= 0; i--)
                if (ReferenceEquals(layers[i].Source, source) && (hand == WeaponHand.Any || layers[i].Hand == hand)) layers.RemoveAt(i);
        }

        private void LateUpdate()
        {
            if (skeletonMap == null || globalWeight <= 0f) return;
            ApplyHand(WeaponHand.Left);
            ApplyHand(WeaponHand.Right);
        }

        private void ApplyHand(WeaponHand hand)
        {
            Layer best = null;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i].Hand == hand && (best == null || layers[i].Priority > best.Priority)) best = layers[i];
            if (best == null || best.Pose == null) return;

            float weight = Mathf.Clamp01(best.Weight * globalWeight);
            IReadOnlyList<HandPoseBoneData> data = best.Pose.Get(hand);
            for (int i = 0; i < data.Count; i++)
            {
                HandPoseBoneData entry = data[i];
                Transform bone = skeletonMap.GetBone(entry.Bone);
                if (bone != null) bone.localRotation = Quaternion.Slerp(bone.localRotation, entry.LocalRotation, weight);
            }
        }
    }
}
