using System.IO;
using UnityEngine;

/// <summary>
/// 런타임 분석 결과의 저장 위치를 관리한다.
///
/// 분석 도중 Assets 아래에 JSON을 반복 기록하면 Unity AssetDatabase가
/// 매 실행마다 파일과 .meta를 Import할 수 있다. 배치 분석이 멈춘 것처럼
/// 보이는 Editor stall을 피하기 위해 persistentDataPath를 사용한다.
/// </summary>
public static class BattleAnalysisOutputPaths
{
    public const string DiagnosticsRootFolderName =
        "ProjectAbyssDiagnostics";

    public const string BattleAnalysisFolderName =
        "BattleAnalysis";

    public const string BattleSimulationFolderName =
        "BattleSimulation";

    public static string DiagnosticsRootDirectory =>
        Path.Combine(
            Application.persistentDataPath,
            DiagnosticsRootFolderName);

    public static string BattleAnalysisDirectory =>
        Path.Combine(
            DiagnosticsRootDirectory,
            BattleAnalysisFolderName);

    public static string BattleSimulationDirectory =>
        Path.Combine(
            DiagnosticsRootDirectory,
            BattleSimulationFolderName);

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(
            BattleAnalysisDirectory);

        Directory.CreateDirectory(
            BattleSimulationDirectory);
    }
}
