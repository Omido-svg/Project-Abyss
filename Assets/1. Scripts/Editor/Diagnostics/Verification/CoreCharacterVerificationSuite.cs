#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 역사적으로 Core 3 진입점이었던 클래스 이름은 기존 호출 호환을 위해 유지하지만,
/// v23부터 실제 의미는 "프로젝트에 등록된 모든 Character" 전수 검증이다.
///
/// 대상:
/// - 플레이어블: Olaf / Yujin / Hifumi
/// - 적: EliteEnemy / NormalEnemy
/// - 향후 추가되는 concrete CharacterAuthoringBundle
///
/// Full Coverage는 모든 Profile의 Data/Isolated 검증 후 CameraTest를
/// 플레이어별 MixedBattle로 자동 재시작하여 플레이어와 적 모두 LiveScene에서 관찰한다.
/// </summary>
public static class CoreCharacterVerificationSuite
{
    private const string FullMenu =
        "Tools/Project Abyss/Character Verification/Run ALL Characters Full Coverage (Playable + Enemy)";

    private const string DataMenu =
        "Tools/Project Abyss/Character Verification/Run ALL Characters Data Coverage";

    private static readonly CharacterAuthoringKind[] PreferredOrder =
    {
        CharacterAuthoringKind.Olaf,
        CharacterAuthoringKind.Yujin,
        CharacterAuthoringKind.Hifumi,
        CharacterAuthoringKind.EliteEnemy,
        CharacterAuthoringKind.NormalEnemy,
        CharacterAuthoringKind.Custom
    };

    [MenuItem(FullMenu, false, 120)]
    public static void RunFullCoverage()
    {
        CharacterVerificationProjectRepair.RepairAll(
            logResult: true);

        CharacterVerificationProfileBuilder
            .CreateOrRefreshAllProfiles();

        List<CharacterVerificationProfile> profiles =
            LoadAllCharacterProfiles();

        if (profiles.Count == 0)
        {
            string message =
                "실제 CharacterAuthoringBundle과 연결된 Verification Profile을 찾지 못했습니다.";

            Debug.LogError(
                "[CharacterVerificationSuite] " +
                message);

            EditorUtility.DisplayDialog(
                "All Character Coverage",
                message,
                "확인");

            return;
        }

        string roster =
            string.Join(
                ", ",
                profiles.Select(
                    DescribeProfile));

        Debug.Log(
            "[CharacterVerificationSuite] " +
            $"ALL Character Full Coverage 시작 / Profiles={profiles.Count} / {roster}");

        CharacterVerificationPlayModeQueue
            .QueueProfilesWithScenarioMatrix(
                profiles);
    }

    [MenuItem(DataMenu, false, 121)]
    public static void RunDataCoverage()
    {
        CharacterVerificationProjectRepair.RepairAll(
            logResult: true);

        CharacterVerificationProfileBuilder
            .CreateOrRefreshAllProfiles();

        List<CharacterVerificationProfile> profiles =
            LoadAllCharacterProfiles();

        if (profiles.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "All Character Data Coverage",
                "검증할 Character Profile이 없습니다.",
                "확인");

            return;
        }

        List<CharacterVerificationReport> reports =
            CharacterVerificationRunner.RunAll(
                profiles,
                includeRuntime: false,
                includeLiveScene: false,
                writeReports: true);

        int pass =
            reports.Sum(
                report =>
                    report?.PassCount ?? 0);

        int fail =
            reports.Sum(
                report =>
                    report?.FailCount ?? 0);

        int skip =
            reports.Sum(
                report =>
                    report?.SkipCount ?? 0);

        int error =
            reports.Sum(
                report =>
                    report?.ErrorCount ?? 0);

        string aggregateDirectory =
            CoreCharacterVerificationAggregateWriter.TryWrite(
                profiles,
                reports,
                "All Character Data Coverage");

        string summary =
            $"ALL Character Data Coverage 완료\n\n" +
            $"Profiles: {reports.Count}\n" +
            $"PASS: {pass}\n" +
            $"FAIL: {fail}\n" +
            $"SKIP: {skip}\n" +
            $"ERROR: {error}\n\n" +
            $"Aggregate: {aggregateDirectory}";

        if (fail == 0 &&
            error == 0)
        {
            Debug.Log(
                "[CharacterVerificationSuite] " +
                summary);
        }
        else
        {
            Debug.LogError(
                "[CharacterVerificationSuite] " +
                summary);
        }

        CharacterVerificationReport display =
            reports.FirstOrDefault(
                report =>
                    report != null &&
                    (report.FailCount > 0 ||
                     report.ErrorCount > 0)) ??
            reports.FirstOrDefault();

        if (display != null)
            CharacterVerificationWindow.ShowReport(display);

        EditorUtility.DisplayDialog(
            "All Character Data Coverage",
            summary,
            "확인");
    }

    /// <summary>
    /// Editor Window의 현재 플레이어블 Baseline 표시에 사용.
    /// 적은 스킬/에셋 수가 데이터에 따라 달라질 수 있으므로 고정 숫자를 강제하지 않는다.
    /// </summary>
    public static int GetCurrentExpectedCaseCount(
        CharacterAuthoringKind kind)
    {
        return kind switch
        {
            CharacterAuthoringKind.Olaf => 41,
            CharacterAuthoringKind.Yujin => 43,
            CharacterAuthoringKind.Hifumi => 41,
            _ => 0
        };
    }

    public static List<CharacterVerificationProfile>
        LoadAllCharacterProfiles()
    {
        List<CharacterVerificationProfile> profiles =
            new List<CharacterVerificationProfile>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:CharacterVerificationProfile");

        for (int i = 0;
             i < guids.Length;
             i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]);

            CharacterVerificationProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    CharacterVerificationProfile>(
                        path);

            if (!IsConcreteCharacterProfile(profile))
                continue;

            profiles.Add(profile);
        }

        profiles =
            profiles
                .GroupBy(
                    profile =>
                        profile.Bundle,
                    ReferenceEqualityComparer<
                        CharacterAuthoringBundle>.Instance)
                .Select(
                    group =>
                        group
                            .OrderBy(
                                profile =>
                                    AssetDatabase.GetAssetPath(profile),
                                StringComparer.Ordinal)
                            .First())
                .OrderBy(
                    profile =>
                        GetOrder(
                            profile.Bundle.Kind))
                .ThenBy(
                    profile =>
                        profile.Bundle.DisplayName ??
                        profile.name,
                    StringComparer.Ordinal)
                .ToList();

        return profiles;
    }

    /// <summary>
    /// 이전 API 호환. Core 3만 요구하는 외부 호출이 있다면 여전히 사용할 수 있다.
    /// 새 Full Coverage 진입점은 LoadAllCharacterProfiles를 사용한다.
    /// </summary>
    public static List<CharacterVerificationProfile>
        LoadCoreProfiles(
            out List<CharacterAuthoringKind> missing)
    {
        CharacterAuthoringKind[] core =
        {
            CharacterAuthoringKind.Olaf,
            CharacterAuthoringKind.Yujin,
            CharacterAuthoringKind.Hifumi
        };

        List<CharacterVerificationProfile> all =
            LoadAllCharacterProfiles();

        List<CharacterVerificationProfile> result =
            new List<CharacterVerificationProfile>();

        missing =
            new List<CharacterAuthoringKind>();

        foreach (CharacterAuthoringKind kind in core)
        {
            CharacterVerificationProfile profile =
                all.FirstOrDefault(
                    candidate =>
                        candidate?.Bundle?.Kind == kind);

            if (profile == null)
                missing.Add(kind);
            else
                result.Add(profile);
        }

        return result;
    }

    private static bool IsConcreteCharacterProfile(
        CharacterVerificationProfile profile)
    {
        CharacterAuthoringBundle bundle =
            profile?.Bundle;

        if (bundle == null)
            return false;

        // 실제 캐릭터로 간주할 최소 Authoring 계약.
        // 향후 Custom Kind 캐릭터도 Data+Prefab이 있으면 자동으로 Suite에 포함된다.
        return bundle.CharacterData != null &&
               bundle.CharacterPrefab != null;
    }

    private static int GetOrder(
        CharacterAuthoringKind kind)
    {
        int index =
            Array.IndexOf(
                PreferredOrder,
                kind);

        return index >= 0
            ? index
            : int.MaxValue;
    }

    private static string DescribeProfile(
        CharacterVerificationProfile profile)
    {
        if (profile?.Bundle == null)
            return "NULL";

        string role =
            profile.Bundle.Kind == CharacterAuthoringKind.EliteEnemy ||
            profile.Bundle.Kind == CharacterAuthoringKind.NormalEnemy
                ? "ENEMY"
                : "CHARACTER";

        return
            $"{role}:{profile.Bundle.DisplayName ?? profile.name}" +
            $"<{profile.Bundle.Kind}>";
    }

    /// <summary>
    /// Reference 기준 GroupBy를 위한 comparer.
    /// </summary>
    private sealed class ReferenceEqualityComparer<T> :
        IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T>
            Instance =
                new ReferenceEqualityComparer<T>();

        public bool Equals(
            T x,
            T y)
        {
            return ReferenceEquals(
                x,
                y);
        }

        public int GetHashCode(
            T obj)
        {
            return
                obj == null
                    ? 0
                    : System.Runtime.CompilerServices
                        .RuntimeHelpers
                        .GetHashCode(obj);
        }
    }
}
#endif
