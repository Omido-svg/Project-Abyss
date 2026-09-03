namespace ProjectAbyss.SecondaryRig
{
    public readonly struct SecondaryRigPerformanceStats
    {
        public readonly float LastSimulationMs;
        public readonly float SmoothedSimulationMs;
        public readonly int ActiveChains;
        public readonly int ActiveNodes;
        public readonly int BodyColliders;
        public readonly int LateralConstraints;
        public readonly int Substeps;
        public readonly int ConstraintIterations;
        public readonly SecondaryRigLodLevel LodLevel;
        public readonly float LodDistance;

        public SecondaryRigPerformanceStats(
            float lastSimulationMs,
            float smoothedSimulationMs,
            int activeChains,
            int activeNodes,
            int bodyColliders,
            int lateralConstraints,
            int substeps,
            int constraintIterations,
            SecondaryRigLodLevel lodLevel,
            float lodDistance)
        {
            LastSimulationMs = lastSimulationMs;
            SmoothedSimulationMs = smoothedSimulationMs;
            ActiveChains = activeChains;
            ActiveNodes = activeNodes;
            BodyColliders = bodyColliders;
            LateralConstraints = lateralConstraints;
            Substeps = substeps;
            ConstraintIterations = constraintIterations;
            LodLevel = lodLevel;
            LodDistance = lodDistance;
        }
    }
}
