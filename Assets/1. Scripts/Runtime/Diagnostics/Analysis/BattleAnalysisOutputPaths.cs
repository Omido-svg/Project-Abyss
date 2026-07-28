using System.IO;
using UnityEngine;

/// <summary>
/// 런타임 분석 결과의 저장 위치를 관리한다.
///
/// 분석 출력은 Unity의 Assets 폴더 내부가 아니라,
/// Assets와 같은 부모를 가지는 Unity 프로젝트 루트에 저장한다.
///
/// <ProjectRoot>/BattleAnalysis
/// <ProjectRoot>/BattleSimulation
/// </summary>
public static class BattleAnalysisOutputPaths
{
    public const string BattleAnalysisFolderName =
        "BattleAnalysis";

    public const string BattleSimulationFolderName =
        "BattleSimulation";

    public static string ProjectRootDirectory =>
        Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                ".."));

    public static string BattleAnalysisDirectory =>
        Path.GetFullPath(
            Path.Combine(
                ProjectRootDirectory,
                BattleAnalysisFolderName));

    public static string BattleSimulationDirectory =>
        Path.GetFullPath(
            Path.Combine(
                ProjectRootDirectory,
                BattleSimulationFolderName));

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(
            BattleAnalysisDirectory);

        Directory.CreateDirectory(
            BattleSimulationDirectory);
    }
}
