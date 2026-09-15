using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 모듈형 Project Abyss Game System Verification 진입점.
/// Editor Window/Test Runner는 이 API만 호출하고 개별 게임 규칙을 직접 알지 않는다.
/// </summary>
public static class GameSystemVerificationRunner
{
    private sealed class RegisteredCase
    {
        public string ModuleId;
        public GameSystemVerificationCase Case;
    }

    public static GameSystemVerificationReport RunDataChecks() =>
        RunInternal(null, true, false, false);

    public static GameSystemVerificationReport RunContracts() =>
        RunDataChecks();

    public static GameSystemVerificationReport RunLivePlanAudit(
        BattleManager manager) =>
        RunInternal(manager, false, false, true);

    public static GameSystemVerificationReport RunPhaseA(
        BattleManager manager) =>
        RunInternal(manager, true, true, true);

    public static GameSystemVerificationReport RunFull(
        BattleManager manager) =>
        RunPhaseA(manager);

    public static IReadOnlyList<IGameSystemVerificationModule> DiscoverModules() =>
        GameSystemVerificationModuleRegistry.DiscoverModules();

    public static IReadOnlyList<GameSystemVerificationCase> DiscoverCases()
    {
        IReadOnlyList<IGameSystemVerificationModule> modules = DiscoverModules();
        return GameSystemVerificationModuleRegistry.BuildCases(modules);
    }

    private static GameSystemVerificationReport RunInternal(
        BattleManager manager,
        bool includeStatic,
        bool includeIsolated,
        bool includeLive)
    {
        DateTime started = DateTime.Now;
        GameSystemVerificationReport report = CreateReport(started, manager);

        IReadOnlyList<IGameSystemVerificationModule> modules =
            GameSystemVerificationModuleRegistry.DiscoverModules();
        List<RegisteredCase> registered = BuildRegisteredCases(modules, report);

        AddCoverageResults(report, registered);

        using GameSystemVerificationContext context =
            new(manager);

        foreach (RegisteredCase registeredCase in registered)
        {
            if (registeredCase?.Case == null)
                continue;

            if (!ShouldRun(
                    registeredCase.Case.ExecutionMode,
                    includeStatic,
                    includeIsolated,
                    includeLive))
            {
                continue;
            }

            ExecuteCase(
                report,
                registeredCase.ModuleId,
                registeredCase.Case,
                context);
        }

        Complete(report);
        return report;
    }

    private static List<RegisteredCase> BuildRegisteredCases(
        IReadOnlyList<IGameSystemVerificationModule> modules,
        GameSystemVerificationReport report)
    {
        List<RegisteredCase> result = new();

        if (modules == null || modules.Count == 0)
        {
            AddInfrastructureResult(
                report,
                "system.coverage.module_discovery",
                GameSystemVerificationStatus.Fail,
                "최소 1개 IGameSystemVerificationModule 자동 발견",
                "0 modules",
                "ProjectAbyss assembly의 module discovery 결과가 비었습니다.",
                true);
            return result;
        }

        foreach (IGameSystemVerificationModule module in modules)
        {
            if (module == null)
                continue;

            IEnumerable<GameSystemVerificationCase> built;
            try
            {
                built = module.BuildCases();
            }
            catch (Exception exception)
            {
                AddInfrastructureResult(
                    report,
                    $"system.coverage.module_build.{module.ModuleId}",
                    GameSystemVerificationStatus.Error,
                    "Module BuildCases 예외 없음",
                    exception.Message,
                    exception.ToString(),
                    true);
                continue;
            }

            if (built == null)
                continue;

            foreach (GameSystemVerificationCase verificationCase in built)
            {
                if (verificationCase == null)
                    continue;

                result.Add(new RegisteredCase
                {
                    ModuleId = module.ModuleId,
                    Case = verificationCase
                });
            }
        }

        List<string> duplicates = result
            .Where(item => !string.IsNullOrWhiteSpace(item.Case?.CaseId))
            .GroupBy(item => item.Case.CaseId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        AddInfrastructureResult(
            report,
            "system.coverage.unique_case_ids",
            duplicates.Count == 0
                ? GameSystemVerificationStatus.Pass
                : GameSystemVerificationStatus.Fail,
            "모든 CaseId가 전역에서 유일",
            duplicates.Count == 0
                ? $"{result.Count} unique cases"
                : "Duplicates=" + string.Join(", ", duplicates),
            duplicates.Count == 0 ? null : "중복 CaseId는 결과 추적을 불가능하게 만듭니다.",
            true);

        return result;
    }

    private static void AddCoverageResults(
        GameSystemVerificationReport report,
        IReadOnlyList<RegisteredCase> registered)
    {
        IEnumerable<GameSystemVerificationCase> cases =
            registered?.Select(item => item.Case) ??
            Enumerable.Empty<GameSystemVerificationCase>();

        IReadOnlyDictionary<string, int> coverage =
            GameSystemVerificationModuleRegistry.CountRequirementCoverage(cases);

        List<string> missingA = CanonicalGameSystemVerificationSpec.PhaseARequirements
            .Where(id => !coverage.ContainsKey(id))
            .ToList();

        AddInfrastructureResult(
            report,
            "system.coverage.phase_a",
            missingA.Count == 0
                ? GameSystemVerificationStatus.Pass
                : GameSystemVerificationStatus.Fail,
            "Phase A 모든 Requirement에 최소 1개 자동 검증 Case 연결",
            missingA.Count == 0
                ? $"{CanonicalGameSystemVerificationSpec.PhaseARequirements.Count}/" +
                  $"{CanonicalGameSystemVerificationSpec.PhaseARequirements.Count} covered"
                : "Missing=" + string.Join(", ", missingA),
            null,
            true);

        List<string> missingB = CanonicalGameSystemVerificationSpec.PhaseBRequirements
            .Where(id => !coverage.ContainsKey(id))
            .ToList();

        AddInfrastructureResult(
            report,
            "system.coverage.phase_b",
            missingB.Count == 0
                ? GameSystemVerificationStatus.Pass
                : GameSystemVerificationStatus.Pending,
            "Phase B 18개 Requirement에 검증 Case 연결",
            $"Covered={CanonicalGameSystemVerificationSpec.PhaseBRequirements.Count - missingB.Count}/" +
            $"{CanonicalGameSystemVerificationSpec.PhaseBRequirements.Count}",
            missingB.Count == 0
                ? null
                : "Pending=" + string.Join(", ", missingB),
            false);
    }

    private static bool ShouldRun(
        GameSystemVerificationExecutionMode mode,
        bool includeStatic,
        bool includeIsolated,
        bool includeLive)
    {
        return mode switch
        {
            GameSystemVerificationExecutionMode.StaticContract => includeStatic,
            GameSystemVerificationExecutionMode.IsolatedRuntime => includeIsolated,
            GameSystemVerificationExecutionMode.LiveScene => includeLive,
            _ => false
        };
    }

    private static void ExecuteCase(
        GameSystemVerificationReport report,
        string moduleId,
        GameSystemVerificationCase verificationCase,
        GameSystemVerificationContext context)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        GameSystemVerificationProbeResult probe;

        try
        {
            probe = verificationCase.Execute?.Invoke(context) ??
                    GameSystemVerificationProbeResult.Fail("Probe=NULL");
        }
        catch (Exception exception)
        {
            probe = new GameSystemVerificationProbeResult
            {
                Status = GameSystemVerificationStatus.Error,
                Actual = exception.Message,
                Details = exception.ToString()
            };
        }
        finally
        {
            stopwatch.Stop();
        }

        report.Results.Add(new GameSystemVerificationCaseResult
        {
            CaseId = verificationCase.CaseId,
            RequirementId = verificationCase.RequirementId,
            ModuleId = moduleId,
            DisplayName = verificationCase.DisplayName,
            Category = verificationCase.Category,
            ExecutionMode = verificationCase.ExecutionMode,
            Status = probe.Status,
            Required = verificationCase.Required,
            Expected = verificationCase.Expected,
            Actual = probe.Actual,
            Details = probe.Details,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        });
    }

    private static void AddInfrastructureResult(
        GameSystemVerificationReport report,
        string caseId,
        GameSystemVerificationStatus status,
        string expected,
        string actual,
        string details,
        bool required)
    {
        report.Results.Add(new GameSystemVerificationCaseResult
        {
            CaseId = caseId,
            ModuleId = "verification.infrastructure",
            DisplayName = caseId,
            Category = GameSystemVerificationCategory.Coverage,
            ExecutionMode = GameSystemVerificationExecutionMode.StaticContract,
            Status = status,
            Required = required,
            Expected = expected,
            Actual = actual,
            Details = details
        });
    }

    private static GameSystemVerificationReport CreateReport(
        DateTime started,
        BattleManager manager)
    {
        return new GameSystemVerificationReport
        {
            SessionId = started.ToString("yyyyMMdd_HHmmss_fff"),
            SpecId = CanonicalGameSystemVerificationSpec.SpecId,
            StartedAt = started.ToString("O"),
            SceneName = manager != null && manager.gameObject.scene.IsValid()
                ? manager.gameObject.scene.name
                : SceneManager.GetActiveScene().name,
            PlayerName = manager?.BattleContext?.Player?.Data?.CharacterName ??
                         manager?.BattleContext?.Player?.name ??
                         "NONE"
        };
    }

    private static void Complete(GameSystemVerificationReport report)
    {
        report.FinishedAt = DateTime.Now.ToString("O");
        report.RecalculateCounts();
    }
}
