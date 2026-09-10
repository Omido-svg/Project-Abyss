using UnityEngine;

/// <summary>
/// Game View에 디버그 패널을 띄우지 않고, Scene Inspector 한 곳에서
/// 테스트 전투 / Performance / 동적 분석을 제어하기 위한 참조 허브다.
/// 실제 게임 로직은 소유하지 않고 기존 Diagnostics 컴포넌트에 위임한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattleLiveDebugInspector : MonoBehaviour
{
    [Header("Inspector-Controlled Diagnostics")]
    [SerializeField] private BattleTestScenarioSwitcher testScenarioSwitcher;
    [SerializeField] private BattlePerformanceDiagnostics performanceDiagnostics;
    [SerializeField] private BattleBatchSimulationRunner batchSimulationRunner;

    public BattleTestScenarioSwitcher TestScenarioSwitcher
    {
        get
        {
            if (testScenarioSwitcher == null)
            {
                testScenarioSwitcher =
                    FindFirstObjectByType<BattleTestScenarioSwitcher>(
                        FindObjectsInactive.Include);
            }

            return testScenarioSwitcher;
        }
    }

    public BattlePerformanceDiagnostics PerformanceDiagnostics
    {
        get
        {
            if (performanceDiagnostics == null)
            {
                performanceDiagnostics =
                    FindFirstObjectByType<BattlePerformanceDiagnostics>(
                        FindObjectsInactive.Include);
            }

            return performanceDiagnostics;
        }
    }

    public BattleBatchSimulationRunner BatchSimulationRunner
    {
        get
        {
            if (BattleBatchSimulationRunner.Instance != null)
                return BattleBatchSimulationRunner.Instance;

            if (batchSimulationRunner == null)
            {
                batchSimulationRunner =
                    FindFirstObjectByType<BattleBatchSimulationRunner>(
                        FindObjectsInactive.Include);
            }

            return batchSimulationRunner;
        }
    }

    public void AssignReferences(
        BattleTestScenarioSwitcher scenarioSwitcher,
        BattlePerformanceDiagnostics performance,
        BattleBatchSimulationRunner simulationRunner)
    {
        testScenarioSwitcher = scenarioSwitcher;
        performanceDiagnostics = performance;
        batchSimulationRunner = simulationRunner;
    }
}
