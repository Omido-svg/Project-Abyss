#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class CharacterVerificationWindow :
    EditorWindow
{
    private readonly List<CharacterVerificationProfile>
        profiles = new();

    private int selectedIndex;
    private Vector2 caseScroll;
    private Vector2 resultScroll;

    private CharacterVerificationReport lastReport;

    [MenuItem(
        "Tools/Project Abyss/Character Verification/Open Verification Window")]
    public static void Open()
    {
        CharacterVerificationWindow window =
            GetWindow<CharacterVerificationWindow>(
                "Character Verification");

        window.minSize =
            new Vector2(
                720f,
                560f);

        window.RefreshProfiles();
        window.Show();
    }

    public static void ShowReport(
        CharacterVerificationReport report)
    {
        if (report == null)
            return;

        CharacterVerificationWindow window =
            Resources
                .FindObjectsOfTypeAll<
                    CharacterVerificationWindow>()
                .FirstOrDefault();

        // Full Coverage 완료 callback은 Play Mode의 EditorApplication.update에서 온다.
        // 그 순간 새 dockable EditorWindow를 강제로 만들면 Unity가 layout을
        // persistence 처리하면서 임시 검증 Object와 충돌할 수 있다.
        // 테스트를 시작한 기존 창이 있으면 그 창만 갱신한다.
        if (window == null)
        {
            if (Application.isPlaying)
            {
                Debug.Log(
                    "[CharacterVerification] 검증 Report가 생성되었습니다. " +
                    "결과 창이 닫혀 있어 자동 생성하지 않습니다. " +
                    "Character Verification Window를 다시 열면 최신 Report 폴더를 확인할 수 있습니다.");
                return;
            }

            Open();
            window =
                GetWindow<CharacterVerificationWindow>();
        }

        window.lastReport =
            report;

        window.Repaint();
    }

    public static void Open(
        CharacterAuthoringBundle bundle)
    {
        Open();

        CharacterVerificationWindow window =
            GetWindow<CharacterVerificationWindow>();

        CharacterVerificationProfile profile =
            CharacterVerificationProfileBuilder
                .GetOrCreateProfile(bundle);

        AssetDatabase.SaveAssets();

        window.RefreshProfiles();
        window.SelectProfile(profile);
        window.Repaint();
    }

    private void OnEnable()
    {
        RefreshProfiles();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (profiles.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "CharacterVerificationProfile이 없습니다. " +
                "Create or Refresh Default Profiles를 실행하세요.",
                MessageType.Warning);

            return;
        }

        selectedIndex =
            Mathf.Clamp(
                selectedIndex,
                0,
                profiles.Count - 1);

        CharacterVerificationProfile profile =
            profiles[selectedIndex];

        DrawProfileSelection(profile);
        DrawExecutionButtons(profile);
        DrawCaseList(profile);
        DrawLastResult();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(
            EditorStyles.toolbar);

        if (GUILayout.Button(
                "Refresh",
                EditorStyles.toolbarButton,
                GUILayout.Width(72f)))
        {
            RefreshProfiles();
        }

        if (GUILayout.Button(
                "Create / Refresh Profiles",
                EditorStyles.toolbarButton,
                GUILayout.Width(170f)))
        {
            CharacterVerificationProfileBuilder
                .CreateOrRefreshAllProfiles();

            RefreshProfiles();
        }

        if (GUILayout.Button(
                "Repair All Dependencies",
                EditorStyles.toolbarButton,
                GUILayout.Width(135f)))
        {
            CharacterVerificationProjectRepair
                .RepairAll(logResult: true);

            CharacterVerificationProfileBuilder
                .CreateOrRefreshAllProfiles();

            RefreshProfiles();
        }

        GUILayout.FlexibleSpace();

        GUILayout.Label(
            Application.isPlaying
                ? "PLAY MODE"
                : "EDIT MODE",
            EditorStyles.miniBoldLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawProfileSelection(
        CharacterVerificationProfile profile)
    {
        EditorGUILayout.Space(8f);

        string[] labels =
            profiles
                .Select(
                    item =>
                        item == null
                            ? "NULL"
                            : $"{item.Bundle?.DisplayName ?? item.name} · {item.name}")
                .ToArray();

        selectedIndex =
            EditorGUILayout.Popup(
                "Character",
                selectedIndex,
                labels);

        profile =
            profiles[selectedIndex];

        EditorGUILayout.ObjectField(
            "Profile",
            profile,
            typeof(CharacterVerificationProfile),
            false);

        EditorGUILayout.ObjectField(
            "Bundle",
            profile?.Bundle,
            typeof(CharacterAuthoringBundle),
            false);

        CharacterAuthoringBundle bundle =
            profile?.Bundle;

        EditorGUILayout.ObjectField(
            "Character Prefab",
            bundle?.CharacterPrefab,
            typeof(Character),
            false);

        if (bundle != null &&
            bundle.CharacterPrefab == null)
        {
            EditorGUILayout.HelpBox(
                "Bundle의 Character Prefab 참조가 비어 있습니다. " +
                "이 상태에서는 Prefab 호환성·Presentation·런타임 검증을 실행할 수 없습니다.",
                MessageType.Error);

            if (GUILayout.Button(
                    "Repair Selected Bundle Dependencies",
                    GUILayout.Height(28f)))
            {
                if (CharacterVerificationProjectRepair.RepairBundle(
                        bundle,
                        out CharacterVerificationBundleRepairResult repairResult))
                {
                    Debug.Log(
                        "[CharacterVerification] 선택 Bundle Dependency 복구 / " +
                        repairResult.BuildLine(),
                        bundle);

                    CharacterVerificationProfileBuilder
                        .GetOrCreateProfile(bundle);

                    AssetDatabase.SaveAssets();
                    RefreshProfiles();
                }
                else
                {
                    Debug.LogWarning(
                        "[CharacterVerification] 선택 Bundle Dependency 복구 실패 / " +
                        repairResult.BuildLine(),
                        bundle);
                }
            }
        }

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Full Character Coverage는 플레이어블과 적을 모두 Character로 취급해 " +
                  "각 Profile의 Skill/Effect/Mechanic을 검증하고, 플레이어별 MixedBattle을 자동 재시작해 " +
                  "적 캐릭터의 실제 AI 행동까지 함께 관찰합니다."
                : "Data 검증은 즉시 실행됩니다. ALL Character Full Coverage는 Play Mode에 진입해 " +
                  "플레이어블/적 전체의 결정론적 검증을 먼저 수행한 뒤 Olaf→Yujin→Hifumi MixedBattle을 " +
                  "자동 재시작해 모든 현재 캐릭터의 LiveScene 연결과 행동을 검사합니다.",
            MessageType.Info);
    }

    private void DrawExecutionButtons(
        CharacterVerificationProfile profile)
    {
        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Run Data Checks",
                GUILayout.Height(34f)))
        {
            RunProfile(
                profile,
                includeRuntime: false,
                includeLiveScene: false);
        }

        if (Application.isPlaying)
        {
            if (GUILayout.Button(
                    "Run Full Character Coverage",
                    GUILayout.Height(34f)))
            {
                CharacterVerificationPlayModeQueue
                    .QueueProfiles(
                        new[] { profile });
            }
        }
        else
        {
            if (GUILayout.Button(
                    "Enter Play + Full Character Coverage",
                    GUILayout.Height(34f)))
            {
                CharacterVerificationPlayModeQueue
                    .QueueProfiles(
                        new[] { profile });
            }
        }

        if (GUILayout.Button(
                "Run ALL Character Coverage",
                GUILayout.Height(34f)))
        {
            CoreCharacterVerificationSuite
                .RunFullCoverage();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Run ALL Characters Full Coverage (Playable + Enemy)",
                GUILayout.Height(30f)))
        {
            CoreCharacterVerificationSuite.RunFullCoverage();
        }

        if (GUILayout.Button(
                "Run ALL Character Data Coverage",
                GUILayout.Height(30f),
                GUILayout.Width(215f)))
        {
            CoreCharacterVerificationSuite.RunDataCoverage();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Coverage 범위는 Player/Enemy 구분이 아니라 CharacterAuthoringBundle 전체입니다. " +
            "모든 스킬의 자원·굴림·피해 대상·Effect Entry, 필수 Mechanic 이벤트를 강제 검증하고, " +
            "NormalEnemy SingleHP·3회 굴림과 EliteEnemy 4부위·자세 슬롯 계약도 별도 Assert합니다. " +
            "Full Suite는 CameraTest를 Olaf→Yujin→Hifumi MixedBattle로 자동 재시작하며 " +
            "각 전투에서 Normal/Elite 적의 실제 ActionStart도 기록합니다.",
            MessageType.None);
    }

    private void DrawCaseList(
        CharacterVerificationProfile profile)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Verification Cases",
            EditorStyles.boldLabel);

        caseScroll =
            EditorGUILayout.BeginScrollView(
                caseScroll,
                GUILayout.Height(220f));

        IReadOnlyList<CharacterVerificationCaseDefinition>
            cases =
                CharacterVerificationRunner
                    .BuildEffectiveDefinitions(profile);

        int expectedCoreCount =
            profile?.Bundle == null
                ? 0
                : CoreCharacterVerificationSuite
                    .GetCurrentExpectedCaseCount(
                        profile.Bundle.Kind);

        if (expectedCoreCount > 0)
        {
            EditorGUILayout.LabelField(
                $"Core 3 Coverage Baseline: {cases?.Count ?? 0}/{expectedCoreCount} cases",
                EditorStyles.miniBoldLabel);
        }

        if (cases != null)
        {
            for (int i = 0;
                 i < cases.Count;
                 i++)
            {
                CharacterVerificationCaseDefinition definition =
                    cases[i];

                if (definition == null)
                    continue;

                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(
                    definition.Enabled
                        ? "●"
                        : "○",
                    GUILayout.Width(18f));

                GUILayout.Label(
                    definition.DisplayName,
                    EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                GUILayout.Label(
                    $"{definition.Category} · {definition.ExecutionMode}",
                    EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(
                    definition.CaseId,
                    EditorStyles.miniLabel);

                EditorGUILayout.LabelField(
                    definition.Description ?? string.Empty,
                    EditorStyles.wordWrappedLabel);

                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawLastResult()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Last Result",
            EditorStyles.boldLabel);

        if (lastReport == null)
        {
            EditorGUILayout.HelpBox(
                "아직 이 창에서 실행한 결과가 없습니다.",
                MessageType.None);

            DrawReportFolderButton();
            return;
        }

        MessageType type =
            lastReport.Succeeded
                ? MessageType.Info
                : MessageType.Error;

        EditorGUILayout.HelpBox(
            $"{lastReport.CharacterName}\n" +
            $"PASS {lastReport.PassCount} / " +
            $"FAIL {lastReport.FailCount} / " +
            $"SKIP {lastReport.SkipCount} / " +
            $"ERROR {lastReport.ErrorCount}",
            type);

        resultScroll =
            EditorGUILayout.BeginScrollView(
                resultScroll,
                GUILayout.Height(150f));

        for (int i = 0;
             i < lastReport.Results.Count;
             i++)
        {
            CharacterVerificationCaseResult result =
                lastReport.Results[i];

            if (result == null)
                continue;

            EditorGUILayout.LabelField(
                $"[{result.Status}] {result.DisplayName} · {result.ElapsedMilliseconds}ms");

            if (result.Status ==
                    CharacterVerificationStatus.Fail ||
                result.Status ==
                    CharacterVerificationStatus.Error)
            {
                EditorGUILayout.LabelField(
                    result.Actual,
                    EditorStyles.wordWrappedLabel);
            }
        }

        EditorGUILayout.EndScrollView();

        DrawReportFolderButton();
    }

    private void DrawReportFolderButton()
    {
        string directory =
            lastReport?.OutputDirectory;

        if (string.IsNullOrWhiteSpace(directory))
        {
            directory =
                CharacterVerificationReportWriter
                    .ReadLatestDirectory();
        }

        EditorGUI.BeginDisabledGroup(
            string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory));

        if (GUILayout.Button(
                "Open Latest Report Folder"))
        {
            EditorUtility.RevealInFinder(
                directory);
        }

        EditorGUI.EndDisabledGroup();
    }

    private void RunProfile(
        CharacterVerificationProfile profile,
        bool includeRuntime,
        bool includeLiveScene)
    {
        if (!Application.isPlaying &&
            profile?.Bundle != null)
        {
            CharacterVerificationProjectRepair.RepairBundle(
                profile.Bundle,
                out _,
                saveAssets: true);
        }

        lastReport =
            CharacterVerificationRunner.Run(
                profile,
                includeRuntime,
                includeLiveScene,
                writeReport: true);

        Repaint();
    }

    private void RunAllProfiles()
    {
        if (!Application.isPlaying)
        {
            CharacterVerificationProjectRepair
                .RepairAll(logResult: false);
        }

        List<CharacterVerificationReport> reports =
            CharacterVerificationRunner.RunAll(
                profiles,
                includeRuntime: true,
                includeLiveScene: true,
                writeReports: true);

        lastReport =
            reports.LastOrDefault();

        Repaint();
    }

    private void RefreshProfiles()
    {
        CharacterVerificationProfile selected =
            profiles.Count > 0 &&
            selectedIndex >= 0 &&
            selectedIndex < profiles.Count
                ? profiles[selectedIndex]
                : null;

        profiles.Clear();

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

            if (profile != null)
                profiles.Add(profile);
        }

        profiles.Sort(
            (left, right) =>
                string.Compare(
                    left?.Bundle?.DisplayName ?? left?.name,
                    right?.Bundle?.DisplayName ?? right?.name,
                    StringComparison.Ordinal));

        SelectProfile(selected);
    }

    private void SelectProfile(
        CharacterVerificationProfile profile)
    {
        if (profile == null ||
            profiles.Count == 0)
        {
            selectedIndex =
                Mathf.Clamp(
                    selectedIndex,
                    0,
                    Mathf.Max(
                        0,
                        profiles.Count - 1));

            return;
        }

        int index =
            profiles.IndexOf(profile);

        if (index >= 0)
            selectedIndex = index;
    }
}
#endif