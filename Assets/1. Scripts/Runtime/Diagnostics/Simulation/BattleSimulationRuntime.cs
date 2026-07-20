using UnityEngine;

public static class BattleSimulationRuntime
{
    public static bool IsBatchSimulation { get; private set; }
    public static bool SuppressDetailedTrace { get; private set; }

    private static bool suppressNextDynamicAnalysisSession;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        IsBatchSimulation = false;
        SuppressDetailedTrace = false;
        suppressNextDynamicAnalysisSession = false;
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

    /// <summary>
    /// 배치 완료 후 깨끗한 씬을 한 번 다시 불러올 때,
    /// 자동 Recorder가 의미 없는 INVALID 세션을 만들지 않도록 한다.
    /// </summary>
    public static void SuppressNextDynamicAnalysisSession()
    {
        suppressNextDynamicAnalysisSession = true;
    }

    public static bool ConsumeSuppressNextDynamicAnalysisSession()
    {
        bool suppress = suppressNextDynamicAnalysisSession;
        suppressNextDynamicAnalysisSession = false;
        return suppress;
    }
}
