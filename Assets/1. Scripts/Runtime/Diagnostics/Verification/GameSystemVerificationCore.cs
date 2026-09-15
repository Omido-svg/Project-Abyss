using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public sealed class GameSystemVerificationProbeResult
{
    public GameSystemVerificationStatus Status;
    public string Actual;
    public string Details;

    public static GameSystemVerificationProbeResult Pass(string actual, string details = null) =>
        new()
        {
            Status = GameSystemVerificationStatus.Pass,
            Actual = actual,
            Details = details
        };

    public static GameSystemVerificationProbeResult Fail(string actual, string details = null) =>
        new()
        {
            Status = GameSystemVerificationStatus.Fail,
            Actual = actual,
            Details = details
        };

    public static GameSystemVerificationProbeResult Skip(string actual, string details = null) =>
        new()
        {
            Status = GameSystemVerificationStatus.Skip,
            Actual = actual,
            Details = details
        };

    public static GameSystemVerificationProbeResult Pending(string actual, string details = null) =>
        new()
        {
            Status = GameSystemVerificationStatus.Pending,
            Actual = actual,
            Details = details
        };
}

public sealed class GameSystemVerificationCase
{
    public string CaseId { get; }
    public string RequirementId { get; }
    public string DisplayName { get; }
    public GameSystemVerificationCategory Category { get; }
    public GameSystemVerificationExecutionMode ExecutionMode { get; }
    public string Expected { get; }
    public bool Required { get; }
    public Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> Execute { get; }

    public GameSystemVerificationCase(
        string caseId,
        string requirementId,
        string displayName,
        GameSystemVerificationCategory category,
        GameSystemVerificationExecutionMode executionMode,
        string expected,
        Func<GameSystemVerificationContext, GameSystemVerificationProbeResult> execute,
        bool required = true)
    {
        CaseId = caseId ?? string.Empty;
        RequirementId = requirementId ?? string.Empty;
        DisplayName = displayName ?? caseId ?? string.Empty;
        Category = category;
        ExecutionMode = executionMode;
        Expected = expected ?? string.Empty;
        Execute = execute;
        Required = required;
    }
}

public interface IGameSystemVerificationModule
{
    string ModuleId { get; }
    int Order { get; }
    IEnumerable<GameSystemVerificationCase> BuildCases();
}

public sealed class GameSystemVerificationContext : IDisposable
{
    private GameSystemVerificationFixture fixture;

    public BattleManager LiveManager { get; }

    public GameSystemVerificationContext(BattleManager liveManager)
    {
        LiveManager = liveManager;
    }

    public bool TryGetFixture(
        out GameSystemVerificationFixture result,
        out string reason)
    {
        if (fixture != null)
        {
            result = fixture;
            reason = string.Empty;
            return true;
        }

        BattleContext liveContext = LiveManager?.BattleContext;
        Character player = liveContext?.Player;
        Character enemy = liveContext?.Enemies?
            .FirstOrDefault(candidate => candidate != null && !candidate.IsDead) ??
            liveContext?.Enemies?.FirstOrDefault(candidate => candidate != null);

        if (!UnityEngine.Application.isPlaying)
        {
            result = null;
            reason = "Isolated Runtime 검증은 Play Mode에서 실행합니다.";
            return false;
        }

        if (LiveManager == null || !LiveManager.IsInitialized || player == null || enemy == null)
        {
            result = null;
            reason = "초기화된 BattleManager의 Player/Enemy가 필요합니다.";
            return false;
        }

        try
        {
            fixture = GameSystemVerificationFixture.Create(
                player,
                enemy,
                LiveManager.BattleRules);
            result = fixture;
            reason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            result = null;
            reason = exception.Message;
            return false;
        }
    }

    public void Dispose()
    {
        fixture?.Dispose();
        fixture = null;
    }
}

public static class GameSystemVerificationModuleRegistry
{
    public static IReadOnlyList<IGameSystemVerificationModule> DiscoverModules()
    {
        List<IGameSystemVerificationModule> modules = new();
        Type contract = typeof(IGameSystemVerificationModule);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string assemblyName = assembly.GetName().Name ?? string.Empty;
            if (!assemblyName.StartsWith("ProjectAbyss", StringComparison.Ordinal))
                continue;

            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (type == null || type.IsAbstract || type.IsInterface ||
                    !contract.IsAssignableFrom(type) ||
                    type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is IGameSystemVerificationModule module)
                        modules.Add(module);
                }
                catch
                {
                }
            }
        }

        return modules
            .OrderBy(module => module.Order)
            .ThenBy(module => module.ModuleId, StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<GameSystemVerificationCase> BuildCases(
        IReadOnlyList<IGameSystemVerificationModule> modules)
    {
        List<GameSystemVerificationCase> cases = new();
        if (modules == null)
            return cases;

        foreach (IGameSystemVerificationModule module in modules)
        {
            if (module == null)
                continue;

            IEnumerable<GameSystemVerificationCase> built = null;
            try
            {
                built = module.BuildCases();
            }
            catch
            {
                continue;
            }

            if (built == null)
                continue;

            foreach (GameSystemVerificationCase verificationCase in built)
            {
                if (verificationCase != null)
                    cases.Add(verificationCase);
            }
        }

        return cases;
    }

    public static IReadOnlyDictionary<string, int> CountRequirementCoverage(
        IEnumerable<GameSystemVerificationCase> cases)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        if (cases == null)
            return result;

        foreach (GameSystemVerificationCase verificationCase in cases)
        {
            string requirement = verificationCase?.RequirementId;
            if (string.IsNullOrWhiteSpace(requirement))
                continue;

            result.TryGetValue(requirement, out int count);
            result[requirement] = count + 1;
        }

        return result;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }
}
