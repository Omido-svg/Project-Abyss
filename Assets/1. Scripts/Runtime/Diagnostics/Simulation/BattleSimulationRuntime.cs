using UnityEngine;

public static class BattleSimulationRuntime
{
    public static bool IsBatchSimulation { get; private set; }
    public static bool SuppressDetailedTrace { get; private set; }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        IsBatchSimulation = false;
        SuppressDetailedTrace = false;
    }

    public static void BeginBatch(
        bool suppressDetailedTrace = true)
    {
        IsBatchSimulation = true;
        SuppressDetailedTrace = suppressDetailedTrace;
    }

    public static void EndBatch()
    {
        IsBatchSimulation = false;
        SuppressDetailedTrace = false;
    }
}
