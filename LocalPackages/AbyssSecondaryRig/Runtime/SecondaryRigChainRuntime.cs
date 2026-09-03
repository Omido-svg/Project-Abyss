using System;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    internal sealed class SecondaryRigChainRuntime
    {
        internal sealed class Node
        {
            public Vector3 Position;
            public Vector3 PreviousPosition;
            public float Length;
            public Vector3 SpringAxisLocal;
            public Vector3 TargetAxisLocal;
            public Quaternion SpringFromTargetRotation;
        }

        public readonly SecondaryRigChainBinding Binding;
        public readonly SecondaryRigSettings Settings;
        public readonly Node[] Nodes;

        public int NodeCount => Nodes.Length;
        public string PartName => Binding.PartName ?? string.Empty;
        public string RegionName => Binding.RegionName ?? string.Empty;
        public string PresetName => Binding.PresetName ?? string.Empty;

        public SecondaryRigChainRuntime(
            SecondaryRigChainBinding binding,
            SecondaryRigSettings settings)
        {
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Nodes = new Node[binding.SpringBones.Length];

            InitializeNodes();
            ResetToTarget();
        }

        public void ResetToTarget()
        {
            for (int i = 0; i < Nodes.Length; i++)
            {
                Vector3 targetTip = GetTargetTip(i);
                Nodes[i].Position = targetTip;
                Nodes[i].PreviousPosition = targetTip;
            }
        }

        public void Integrate(
            float deltaTime,
            Vector3 gravityDirection,
            SecondaryRigCollider[] colliders)
        {
            Vector3 head = Binding.SpringBones[0].position;

            float dampingFactor = Mathf.Exp(-Mathf.Clamp01(Settings.Damping) * 12f * deltaTime);
            float stiffnessAlpha = 1f - Mathf.Exp(-Mathf.Clamp01(Settings.Stiffness) * 18f * deltaTime);
            Vector3 gravityStep = gravityDirection.normalized * (Settings.Gravity * 9.81f * deltaTime * deltaTime);

            for (int i = 0; i < Nodes.Length; i++)
            {
                Node node = Nodes[i];
                Vector3 current = node.Position;
                Vector3 velocity = (node.Position - node.PreviousPosition) * dampingFactor;
                Vector3 next = node.Position + velocity + gravityStep;
                Vector3 targetTip = GetTargetTip(i);

                next = Vector3.Lerp(next, targetTip, stiffnessAlpha);
                node.PreviousPosition = current;
                node.Position = next;

                ConstrainLengthAndAngle(i, head);
                ResolveBodyCollisions(i, colliders);
                ConstrainLengthAndAngle(i, head);

                head = node.Position;
            }
        }

        public void ConstrainAllLengthsAndAngles()
        {
            Vector3 head = Binding.SpringBones[0].position;
            for (int i = 0; i < Nodes.Length; i++)
            {
                ConstrainLengthAndAngle(i, head);
                head = Nodes[i].Position;
            }
        }

        public void ResolveBodyCollisions(SecondaryRigCollider[] colliders)
        {
            if (!Settings.EnableCollision || colliders == null || colliders.Length == 0)
                return;

            for (int i = 0; i < Nodes.Length; i++)
                ResolveBodyCollisions(i, colliders);
        }

        public void ApplyPose()
        {
            for (int i = 0; i < Nodes.Length; i++)
            {
                Transform springBone = Binding.SpringBones[i];
                Transform targetBone = Binding.TargetBones[i];
                if (springBone == null || targetBone == null)
                    continue;

                Vector3 head = i == 0
                    ? Binding.SpringBones[0].position
                    : Nodes[i - 1].Position;

                Vector3 desired = Nodes[i].Position - head;
                if (desired.sqrMagnitude <= 0.00000001f)
                    continue;

                desired.Normalize();

                Quaternion baseRotation = targetBone.rotation * Nodes[i].SpringFromTargetRotation;
                Vector3 baseAxis = baseRotation * Nodes[i].SpringAxisLocal;
                if (baseAxis.sqrMagnitude <= 0.00000001f)
                    continue;

                Quaternion correction = Quaternion.FromToRotation(baseAxis.normalized, desired);
                springBone.rotation = correction * baseRotation;
            }
        }

        public Vector3 GetNodePosition(int index) => Nodes[index].Position;

        public void SetNodePosition(int index, Vector3 position)
        {
            Nodes[index].Position = position;
        }

        public float GetNodeRadius(int index) => Settings.ParticleRadius;

        public Vector3 GetNodeHead(int index)
        {
            return index == 0
                ? Binding.SpringBones[0].position
                : Nodes[index - 1].Position;
        }

        private void InitializeNodes()
        {
            Transform[] springBones = Binding.SpringBones;
            Transform[] targetBones = Binding.TargetBones;

            for (int i = 0; i < springBones.Length; i++)
            {
                float length = EstimateBoneLength(springBones, i);
                Vector3 springAxisLocal = EstimateBoneAxisLocal(springBones, i);
                Vector3 targetAxisLocal = EstimateBoneAxisLocal(targetBones, i);

                Nodes[i] = new Node
                {
                    Length = Mathf.Max(0.0001f, length),
                    SpringAxisLocal = springAxisLocal,
                    TargetAxisLocal = targetAxisLocal,
                    SpringFromTargetRotation = Quaternion.Inverse(targetBones[i].rotation) * springBones[i].rotation
                };
            }
        }

        private Vector3 GetTargetTip(int index)
        {
            Transform[] targetBones = Binding.TargetBones;
            if (index < targetBones.Length - 1)
                return targetBones[index + 1].position;

            Transform target = targetBones[index];
            Vector3 direction = target.TransformDirection(Nodes[index].TargetAxisLocal);
            if (direction.sqrMagnitude <= 0.00000001f)
                direction = target.up;

            return target.position + direction.normalized * Nodes[index].Length;
        }

        private void ConstrainLengthAndAngle(int index, Vector3 head)
        {
            Node node = Nodes[index];
            Vector3 direction = node.Position - head;
            if (direction.sqrMagnitude <= 0.00000001f)
                direction = GetTargetDirection(index);

            direction.Normalize();

            float maxAngle = Mathf.Clamp(Settings.MaxAngle, 0f, 180f);
            if (maxAngle < 179.999f)
            {
                Vector3 targetDirection = GetTargetDirection(index);
                float angle = Vector3.Angle(targetDirection, direction);
                if (angle > maxAngle)
                {
                    direction = Vector3.RotateTowards(
                        targetDirection,
                        direction,
                        maxAngle * Mathf.Deg2Rad,
                        0f).normalized;
                }
            }

            node.Position = head + direction * node.Length;
        }

        private Vector3 GetTargetDirection(int index)
        {
            Transform target = Binding.TargetBones[index];
            Vector3 tip = GetTargetTip(index);
            Vector3 direction = tip - target.position;
            if (direction.sqrMagnitude <= 0.00000001f)
                direction = target.TransformDirection(Nodes[index].TargetAxisLocal);
            if (direction.sqrMagnitude <= 0.00000001f)
                direction = Vector3.down;
            return direction.normalized;
        }

        private void ResolveBodyCollisions(int index, SecondaryRigCollider[] colliders)
        {
            if (!Settings.EnableCollision || colliders == null)
                return;

            Node node = Nodes[index];

            for (int i = 0; i < colliders.Length; i++)
            {
                SecondaryRigCollider collider = colliders[i];
                if (collider == null || !collider.CollisionEnabled)
                    continue;

                if ((Settings.CollidesWith & collider.Layer) == 0)
                    continue;

                Vector3 position = node.Position;
                if (collider.ProjectOutside(ref position, Settings.ParticleRadius))
                    node.Position = position;
            }
        }

        private static float EstimateBoneLength(Transform[] bones, int index)
        {
            if (bones == null || bones.Length == 0 || bones[index] == null)
                return 0.05f;

            if (index < bones.Length - 1 && bones[index + 1] != null)
            {
                float direct = Vector3.Distance(bones[index].position, bones[index + 1].position);
                if (direct > 0.0001f)
                    return direct;
            }

            if (index > 0 && bones[index - 1] != null)
            {
                float previous = Vector3.Distance(bones[index - 1].position, bones[index].position);
                if (previous > 0.0001f)
                    return previous;
            }

            return 0.05f;
        }

        private static Vector3 EstimateBoneAxisLocal(Transform[] bones, int index)
        {
            Transform bone = bones[index];

            if (index < bones.Length - 1 && bones[index + 1] != null)
            {
                Vector3 worldDirection = bones[index + 1].position - bone.position;
                if (worldDirection.sqrMagnitude > 0.00000001f)
                    return bone.InverseTransformDirection(worldDirection.normalized).normalized;
            }

            Vector3 referenceWorld = Vector3.zero;
            if (index > 0 && bones[index - 1] != null)
                referenceWorld = bone.position - bones[index - 1].position;

            if (referenceWorld.sqrMagnitude <= 0.00000001f)
                referenceWorld = bone.up;

            Vector3 localReference = bone.InverseTransformDirection(referenceWorld.normalized);
            return DominantSignedAxis(localReference);
        }

        private static Vector3 DominantSignedAxis(Vector3 localDirection)
        {
            Vector3 abs = new(
                Mathf.Abs(localDirection.x),
                Mathf.Abs(localDirection.y),
                Mathf.Abs(localDirection.z));

            if (abs.x >= abs.y && abs.x >= abs.z)
                return localDirection.x >= 0f ? Vector3.right : Vector3.left;

            if (abs.y >= abs.z)
                return localDirection.y >= 0f ? Vector3.up : Vector3.down;

            return localDirection.z >= 0f ? Vector3.forward : Vector3.back;
        }
    }
}
