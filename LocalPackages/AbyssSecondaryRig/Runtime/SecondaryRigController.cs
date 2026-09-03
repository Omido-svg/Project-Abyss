using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectAbyss.SecondaryRig
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Project Abyss/Secondary Rig/Secondary Rig Controller")]
    public sealed class SecondaryRigController : MonoBehaviour
    {
        private const int CurrentSerializedVersion = 2;

        [SerializeField, HideInInspector] private int serializedVersion = CurrentSerializedVersion;

        [Header("Authoring")]
        [SerializeField] private SecondaryRigPresetLibrary presetLibrary;
        [SerializeField] private Transform simulationRoot;
        [SerializeField] private Transform colliderRoot;
        [SerializeField] private List<SecondaryRigChainBinding> chains = new();

        [Header("Simulation")]
        [SerializeField] private SecondaryRigUpdateMode updateMode = SecondaryRigUpdateMode.LateUpdate;
        [SerializeField, Range(1, 8)] private int substeps = 2;
        [SerializeField, Range(1, 8)] private int constraintIterations = 2;
        [SerializeField, Min(0.001f)] private float maxDeltaTime = 0.05f;
        [SerializeField] private Vector3 gravityDirection = Vector3.down;
        [SerializeField] private bool enableSecondaryToSecondaryCollision = true;

        [Header("Distance LOD")]
        [SerializeField] private SecondaryRigLodSettings lod = new();

        [Header("Teleport / Spawn Reset")]
        [SerializeField] private bool detectTeleport = true;
        [SerializeField, Min(0f)] private float teleportDistance = 0.75f;
        [SerializeField, Range(0f, 180f)] private float teleportAngle = 75f;

        [Header("Debug / Diagnostics")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private bool logWarnings = true;
        [SerializeField] private bool collectPerformanceStats = true;

        private readonly List<SecondaryRigChainRuntime> runtimeChains = new();
        private readonly List<LateralConstraint> lateralConstraints = new();
        private SecondaryRigCollider[] colliders = Array.Empty<SecondaryRigCollider>();
        private bool runtimeReady;
        private Vector3 lastRootPosition;
        private Quaternion lastRootRotation;
        private int activeNodeCount;
        private Camera cachedMainCamera;
        private SecondaryRigLodLevel currentLodLevel = SecondaryRigLodLevel.Full;
        private float currentLodDistance;
        private bool lodStateInitialized;
        private float lastSimulationMs;
        private float smoothedSimulationMs;
        private int lastUsedSubsteps;
        private int lastUsedConstraintIterations;

        public SecondaryRigPresetLibrary PresetLibrary
        {
            get => presetLibrary;
            set => presetLibrary = value;
        }

        public Transform SimulationRoot
        {
            get => simulationRoot != null ? simulationRoot : transform;
            set => simulationRoot = value;
        }

        public Transform ColliderRoot
        {
            get => colliderRoot != null ? colliderRoot : transform;
            set => colliderRoot = value;
        }

        public SecondaryRigLodSettings LodSettings => lod;
        public SecondaryRigLodLevel CurrentLodLevel => currentLodLevel;
        public float CurrentLodDistance => currentLodDistance;
        public IReadOnlyList<SecondaryRigChainBinding> Chains => chains;
        public int RuntimeChainCount => runtimeChains.Count;
        public int RuntimeNodeCount => activeNodeCount;
        public int ColliderCount => colliders?.Length ?? 0;
        public int LateralConstraintCount => lateralConstraints.Count;
        public SecondaryRigPerformanceStats PerformanceStats => new(
            lastSimulationMs,
            smoothedSimulationMs,
            runtimeChains.Count,
            activeNodeCount,
            ColliderCount,
            lateralConstraints.Count,
            lastUsedSubsteps,
            lastUsedConstraintIterations,
            currentLodLevel,
            currentLodDistance);

        private void Reset()
        {
            simulationRoot = transform;
            colliderRoot = transform;
            lod = new SecondaryRigLodSettings();
        }

        private void Awake()
        {
            RebuildRuntime();
        }

        private void OnEnable()
        {
            if (!runtimeReady)
                RebuildRuntime();
            else
                ResetSimulation();
        }

        private void OnDisable()
        {
            runtimeReady = false;
            lodStateInitialized = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (Application.isPlaying)
                RefreshColliders();
        }

        private void LateUpdate()
        {
            if (updateMode != SecondaryRigUpdateMode.LateUpdate)
                return;

            Simulate(Time.deltaTime);
        }

        private void OnValidate()
        {
            serializedVersion = CurrentSerializedVersion;
            substeps = Mathf.Clamp(substeps, 1, 8);
            constraintIterations = Mathf.Clamp(constraintIterations, 1, 8);
            maxDeltaTime = Mathf.Max(0.001f, maxDeltaTime);
            teleportDistance = Mathf.Max(0f, teleportDistance);
            teleportAngle = Mathf.Clamp(teleportAngle, 0f, 180f);
            lod ??= new SecondaryRigLodSettings();
            lod.Sanitize();
        }

        public void Simulate(float deltaTime)
        {
            if (!isActiveAndEnabled)
                return;

            if (!runtimeReady && !RebuildRuntime())
                return;

            Transform root = SimulationRoot;
            if (root == null)
                return;

            if (detectTeleport && HasTeleported(root))
            {
                ResetSimulation();
                CaptureRootPose(root);
                return;
            }

            SecondaryRigLodLevel nextLod = EvaluateLod(out float lodDistance);
            currentLodDistance = lodDistance;

            if (!lodStateInitialized)
            {
                currentLodLevel = nextLod;
                lodStateInitialized = true;
            }

            if (nextLod == SecondaryRigLodLevel.Disabled)
            {
                if (currentLodLevel != SecondaryRigLodLevel.Disabled && lod != null && lod.ResetWhenDisabled)
                    ResetSimulation();

                currentLodLevel = SecondaryRigLodLevel.Disabled;
                lastUsedSubsteps = 0;
                lastUsedConstraintIterations = 0;
                lastSimulationMs = 0f;
                smoothedSimulationMs = Mathf.Lerp(smoothedSimulationMs, 0f, 0.1f);
                CaptureRootPose(root);
                return;
            }

            if (currentLodLevel == SecondaryRigLodLevel.Disabled && nextLod != SecondaryRigLodLevel.Disabled)
                ResetSimulation();

            currentLodLevel = nextLod;

            float clampedDelta = Mathf.Clamp(deltaTime, 0f, maxDeltaTime);
            if (clampedDelta <= 0f)
            {
                CaptureRootPose(root);
                return;
            }

            GetLodQuality(nextLod, out int steps, out int iterations, out bool bodyCollision, out bool secondaryCollision);
            lastUsedSubsteps = steps;
            lastUsedConstraintIterations = iterations;

            long startTicks = collectPerformanceStats ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
            float stepDelta = clampedDelta / steps;
            SecondaryRigCollider[] activeColliders = bodyCollision ? colliders : Array.Empty<SecondaryRigCollider>();

            for (int step = 0; step < steps; step++)
            {
                for (int i = 0; i < runtimeChains.Count; i++)
                    runtimeChains[i].Integrate(stepDelta, gravityDirection, activeColliders);

                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    SolveLateralConstraints();

                    if (secondaryCollision)
                        SolveSecondaryParticleCollisions();

                    for (int i = 0; i < runtimeChains.Count; i++)
                    {
                        runtimeChains[i].ConstrainAllLengthsAndAngles();
                        runtimeChains[i].ResolveBodyCollisions(activeColliders);
                        runtimeChains[i].ConstrainAllLengthsAndAngles();
                    }
                }
            }

            for (int i = 0; i < runtimeChains.Count; i++)
                runtimeChains[i].ApplyPose();

            if (collectPerformanceStats)
            {
                long endTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                double elapsedMs = (endTicks - startTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                lastSimulationMs = (float)elapsedMs;
                smoothedSimulationMs = smoothedSimulationMs <= 0f
                    ? lastSimulationMs
                    : Mathf.Lerp(smoothedSimulationMs, lastSimulationMs, 0.08f);
            }

            CaptureRootPose(root);
        }

        public bool RebuildRuntime()
        {
            runtimeChains.Clear();
            lateralConstraints.Clear();
            activeNodeCount = 0;
            RefreshColliders();

            HashSet<int> springRootIds = new();

            if (chains != null)
            {
                for (int i = 0; i < chains.Count; i++)
                {
                    SecondaryRigChainBinding binding = chains[i];
                    if (binding == null || !binding.HasUsableBones())
                        continue;

                    int rootId = binding.SpringRoot.GetInstanceID();
                    if (!springRootIds.Add(rootId))
                    {
                        Warn($"Duplicate spring root skipped: {binding.SpringRoot.name}");
                        continue;
                    }

                    SecondaryRigSettings settings = binding.ResolveSettings(presetLibrary);
                    SecondaryRigChainRuntime runtime = new(binding, settings);
                    runtimeChains.Add(runtime);
                    activeNodeCount += runtime.NodeCount;
                }
            }

            BuildLateralConstraints();
            runtimeReady = runtimeChains.Count > 0;
            lodStateInitialized = false;

            if (runtimeReady)
                ResetSimulation();

            return runtimeReady;
        }

        public void RefreshColliders()
        {
            Transform root = ColliderRoot;
            colliders = root != null
                ? root.GetComponentsInChildren<SecondaryRigCollider>(true)
                : Array.Empty<SecondaryRigCollider>();
        }

        public void ResetSimulation()
        {
            for (int i = 0; i < runtimeChains.Count; i++)
            {
                runtimeChains[i].ResetToTarget();
                runtimeChains[i].ApplyPose();
            }

            Transform root = SimulationRoot;
            if (root != null)
                CaptureRootPose(root);
        }

        public bool ValidateBindings(out string report)
        {
            List<string> lines = new();
            HashSet<int> roots = new();
            int usable = 0;

            if (chains == null || chains.Count == 0)
            {
                report = "No secondary rig chains are configured.";
                return false;
            }

            for (int i = 0; i < chains.Count; i++)
            {
                SecondaryRigChainBinding binding = chains[i];
                if (binding == null)
                {
                    lines.Add($"[{i}] Binding is null.");
                    continue;
                }

                if (!binding.HasUsableBones())
                {
                    lines.Add($"[{i}] Invalid chain: {binding.StableKey}");
                    continue;
                }

                int id = binding.SpringRoot.GetInstanceID();
                if (!roots.Add(id))
                {
                    lines.Add($"[{i}] Duplicate spring root: {binding.SpringRoot.name}");
                    continue;
                }

                usable++;
            }

            lines.Insert(0, $"Usable chains: {usable}/{chains.Count}, Colliders: {ColliderCount}");
            report = string.Join("\n", lines);
            return usable > 0 && lines.Count == 1;
        }

        public void EditorReplaceBindings(
            List<SecondaryRigChainBinding> newBindings,
            SecondaryRigPresetLibrary newPresetLibrary,
            Transform newSimulationRoot)
        {
            chains = newBindings ?? new List<SecondaryRigChainBinding>();
            presetLibrary = newPresetLibrary;
            simulationRoot = newSimulationRoot != null ? newSimulationRoot : transform;
            if (colliderRoot == null)
                colliderRoot = simulationRoot;
            serializedVersion = CurrentSerializedVersion;
            runtimeReady = false;
            lodStateInitialized = false;
        }

        private SecondaryRigLodLevel EvaluateLod(out float distance)
        {
            distance = 0f;
            if (lod == null || !lod.Enabled)
                return SecondaryRigLodLevel.Full;

            lod.Sanitize();
            Camera camera = lod.CameraOverride;
            if (camera == null)
            {
                if (cachedMainCamera == null || !cachedMainCamera.isActiveAndEnabled)
                    cachedMainCamera = Camera.main;
                camera = cachedMainCamera;
            }

            if (camera == null)
                return SecondaryRigLodLevel.Full;

            Transform reference = lod.DistanceReference != null ? lod.DistanceReference : SimulationRoot;
            if (reference == null)
                return SecondaryRigLodLevel.Full;

            distance = Vector3.Distance(camera.transform.position, reference.position);
            if (distance <= lod.FullDistance)
                return SecondaryRigLodLevel.Full;
            if (distance <= lod.ReducedDistance)
                return SecondaryRigLodLevel.Reduced;
            if (distance <= lod.DisableDistance)
                return SecondaryRigLodLevel.Minimal;
            return SecondaryRigLodLevel.Disabled;
        }

        private void GetLodQuality(
            SecondaryRigLodLevel level,
            out int steps,
            out int iterations,
            out bool bodyCollision,
            out bool secondaryCollision)
        {
            switch (level)
            {
                case SecondaryRigLodLevel.Reduced:
                    steps = Mathf.Clamp(lod?.ReducedSubsteps ?? 1, 1, 8);
                    iterations = Mathf.Clamp(lod?.ReducedConstraintIterations ?? 1, 1, 8);
                    bodyCollision = lod == null || lod.ReducedBodyCollision;
                    secondaryCollision = enableSecondaryToSecondaryCollision && lod != null && lod.ReducedSecondaryCollision;
                    break;

                case SecondaryRigLodLevel.Minimal:
                    steps = Mathf.Clamp(lod?.MinimalSubsteps ?? 1, 1, 8);
                    iterations = Mathf.Clamp(lod?.MinimalConstraintIterations ?? 1, 1, 8);
                    bodyCollision = lod != null && lod.MinimalBodyCollision;
                    secondaryCollision = enableSecondaryToSecondaryCollision && lod != null && lod.MinimalSecondaryCollision;
                    break;

                default:
                    steps = Mathf.Clamp(substeps, 1, 8);
                    iterations = Mathf.Clamp(constraintIterations, 1, 8);
                    bodyCollision = true;
                    secondaryCollision = enableSecondaryToSecondaryCollision;
                    break;
            }
        }

        private void BuildLateralConstraints()
        {
            lateralConstraints.Clear();

            Dictionary<string, List<SecondaryRigChainRuntime>> groups = new(StringComparer.Ordinal);
            for (int i = 0; i < runtimeChains.Count; i++)
            {
                SecondaryRigChainRuntime chain = runtimeChains[i];
                if (!chain.Settings.EnableCrossChainConstraint)
                    continue;

                string key = $"{chain.PartName}|{chain.RegionName}|{chain.PresetName}";
                if (!groups.TryGetValue(key, out List<SecondaryRigChainRuntime> list))
                {
                    list = new List<SecondaryRigChainRuntime>();
                    groups.Add(key, list);
                }

                list.Add(chain);
            }

            foreach (KeyValuePair<string, List<SecondaryRigChainRuntime>> pair in groups)
            {
                List<SecondaryRigChainRuntime> group = pair.Value;
                for (int chainIndex = 0; chainIndex < group.Count - 1; chainIndex++)
                {
                    SecondaryRigChainRuntime a = group[chainIndex];
                    SecondaryRigChainRuntime b = group[chainIndex + 1];
                    int count = Mathf.Min(a.NodeCount, b.NodeCount);

                    for (int nodeIndex = 0; nodeIndex < count; nodeIndex++)
                    {
                        float restDistance = Vector3.Distance(
                            a.GetNodePosition(nodeIndex),
                            b.GetNodePosition(nodeIndex));

                        if (restDistance <= 0.0001f)
                            continue;

                        float stiffness = Mathf.Min(
                            a.Settings.CrossChainStiffness,
                            b.Settings.CrossChainStiffness);

                        lateralConstraints.Add(new LateralConstraint(
                            a,
                            b,
                            nodeIndex,
                            restDistance,
                            stiffness));
                    }
                }
            }
        }

        private void SolveLateralConstraints()
        {
            for (int i = 0; i < lateralConstraints.Count; i++)
            {
                LateralConstraint constraint = lateralConstraints[i];
                Vector3 a = constraint.A.GetNodePosition(constraint.NodeIndex);
                Vector3 b = constraint.B.GetNodePosition(constraint.NodeIndex);
                Vector3 delta = b - a;
                float distance = delta.magnitude;
                if (distance <= 0.000001f)
                    continue;

                float error = distance - constraint.RestDistance;
                Vector3 correction = delta / distance * (error * 0.5f * constraint.Stiffness);

                constraint.A.SetNodePosition(constraint.NodeIndex, a + correction);
                constraint.B.SetNodePosition(constraint.NodeIndex, b - correction);
            }
        }

        private void SolveSecondaryParticleCollisions()
        {
            for (int chainAIndex = 0; chainAIndex < runtimeChains.Count; chainAIndex++)
            {
                SecondaryRigChainRuntime a = runtimeChains[chainAIndex];
                if (!a.Settings.EnableCollision)
                    continue;

                for (int chainBIndex = chainAIndex + 1; chainBIndex < runtimeChains.Count; chainBIndex++)
                {
                    SecondaryRigChainRuntime b = runtimeChains[chainBIndex];
                    if (!b.Settings.EnableCollision)
                        continue;

                    bool samePart = string.Equals(a.PartName, b.PartName, StringComparison.Ordinal);
                    bool sameRegion = samePart && string.Equals(a.RegionName, b.RegionName, StringComparison.Ordinal);

                    if (samePart && !a.Settings.CollideWithinSamePart && !b.Settings.CollideWithinSamePart)
                        continue;

                    if (sameRegion && !a.Settings.EnableSelfCollision && !b.Settings.EnableSelfCollision)
                        continue;

                    bool aWantsB = (a.Settings.CollidesWith & b.Settings.Layer) != 0;
                    bool bWantsA = (b.Settings.CollidesWith & a.Settings.Layer) != 0;
                    if (!aWantsB || !bWantsA)
                        continue;

                    ResolveChainPairCollision(a, b);
                }
            }
        }

        private static void ResolveChainPairCollision(
            SecondaryRigChainRuntime a,
            SecondaryRigChainRuntime b)
        {
            for (int i = 0; i < a.NodeCount; i++)
            {
                for (int j = 0; j < b.NodeCount; j++)
                {
                    Vector3 pa = a.GetNodePosition(i);
                    Vector3 pb = b.GetNodePosition(j);
                    float minimumDistance = a.GetNodeRadius(i) + b.GetNodeRadius(j);
                    if (minimumDistance <= 0f)
                        continue;

                    Vector3 delta = pb - pa;
                    float sqrDistance = delta.sqrMagnitude;
                    float minSqr = minimumDistance * minimumDistance;
                    if (sqrDistance >= minSqr)
                        continue;

                    Vector3 direction;
                    float distance;

                    if (sqrDistance <= 0.00000001f)
                    {
                        direction = Vector3.right;
                        distance = 0f;
                    }
                    else
                    {
                        distance = Mathf.Sqrt(sqrDistance);
                        direction = delta / distance;
                    }

                    float penetration = minimumDistance - distance;
                    Vector3 correction = direction * (penetration * 0.5f);
                    a.SetNodePosition(i, pa - correction);
                    b.SetNodePosition(j, pb + correction);
                }
            }
        }

        private bool HasTeleported(Transform root)
        {
            float distance = Vector3.Distance(root.position, lastRootPosition);
            if (teleportDistance > 0f && distance > teleportDistance)
                return true;

            float angle = Quaternion.Angle(root.rotation, lastRootRotation);
            return teleportAngle > 0f && angle > teleportAngle;
        }

        private void CaptureRootPose(Transform root)
        {
            lastRootPosition = root.position;
            lastRootRotation = root.rotation;
        }

        private void Warn(string message)
        {
            if (logWarnings)
                Debug.LogWarning($"[SecondaryRig] {message}", this);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || chains == null)
                return;

            for (int i = 0; i < chains.Count; i++)
            {
                SecondaryRigChainBinding chain = chains[i];
                if (chain?.SpringBones == null)
                    continue;

                for (int j = 0; j < chain.SpringBones.Length - 1; j++)
                {
                    Transform a = chain.SpringBones[j];
                    Transform b = chain.SpringBones[j + 1];
                    if (a != null && b != null)
                        Gizmos.DrawLine(a.position, b.position);
                }
            }
        }

        private readonly struct LateralConstraint
        {
            public readonly SecondaryRigChainRuntime A;
            public readonly SecondaryRigChainRuntime B;
            public readonly int NodeIndex;
            public readonly float RestDistance;
            public readonly float Stiffness;

            public LateralConstraint(
                SecondaryRigChainRuntime a,
                SecondaryRigChainRuntime b,
                int nodeIndex,
                float restDistance,
                float stiffness)
            {
                A = a;
                B = b;
                NodeIndex = nodeIndex;
                RestDistance = restDistance;
                Stiffness = Mathf.Clamp01(stiffness);
            }
        }
    }
}
