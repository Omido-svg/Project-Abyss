#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterVerificationPlayModeQueue
{
    private const string QueueKey =
        "ProjectAbyss.CharacterVerification.Queue";

    private const string StartedKey =
        "ProjectAbyss.CharacterVerification.Started";

    private const string ScenarioMatrixKey =
        "ProjectAbyss.CharacterVerification.ScenarioMatrix";

    private sealed class LiveScenario
    {
        public BattleTestPlayerMode Player;
        public BattleTestEncounterMode Encounter;
        public string Label;
    }

    private static bool running;
    private static bool awaitingAutoBattle;
    private static bool coveragePrepared;
    private static bool useScenarioMatrix;
    private static bool matrixPrepared;
    private static bool waitingForScenarioReload;

    private static int nextCoverageProfileIndex;
    private static int currentScenarioIndex;

    private static double readyAt;
    private static double deadlineAt;
    private static double nextCoverageProfileAt;

    private static List<CharacterVerificationProfile>
        pendingProfiles;

    private static List<CharacterVerificationReport>
        pendingReports;

    private static List<LiveScenario>
        liveScenarios;

    private static readonly HashSet<string>
        liveCoveredProfileIds =
            new HashSet<string>(
                StringComparer.Ordinal);

    private static BattleTestPlayerMode
        originalPlayer;

    private static BattleTestEncounterMode
        originalEncounter;

    private static bool originalScenarioCaptured;

    static CharacterVerificationPlayModeQueue()
    {
        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;

        EditorApplication.update +=
            Update;
    }

    /// <summary>
    /// 기존 단일 Roster Full Coverage.
    /// 개별 Profile 버튼과 호환하기 위해 유지한다.
    /// </summary>
    public static void QueueProfiles(
        IEnumerable<CharacterVerificationProfile> profiles)
    {
        QueueProfilesInternal(
            profiles,
            scenarioMatrix: false);
    }

    /// <summary>
    /// 프로젝트의 모든 Character를 대상으로 하는 Full Coverage.
    /// Data/Isolated 후 CameraTest를 플레이어별 MixedBattle로 재시작해
    /// 플레이어와 적 모두 LiveScene 검증한다.
    /// </summary>
    public static void QueueProfilesWithScenarioMatrix(
        IEnumerable<CharacterVerificationProfile> profiles)
    {
        QueueProfilesInternal(
            profiles,
            scenarioMatrix: true);
    }

    private static void QueueProfilesInternal(
        IEnumerable<CharacterVerificationProfile> profiles,
        bool scenarioMatrix)
    {
        if (profiles == null)
            return;

        if (!EditorApplication.isPlaying)
        {
            CharacterVerificationProjectRepair
                .RepairAll(logResult: false);

            AssetDatabase.SaveAssets();
        }

        List<string> paths =
            profiles
                .Where(
                    profile =>
                        profile != null)
                .Select(
                    AssetDatabase.GetAssetPath)
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(path))
                .Distinct(
                    StringComparer.Ordinal)
                .ToList();

        if (paths.Count == 0)
        {
            Debug.LogWarning(
                "[CharacterVerification] Queue할 Profile이 없습니다.");
            return;
        }

        SessionState.SetString(
            QueueKey,
            string.Join(
                "\n",
                paths));

        SessionState.SetBool(
            StartedKey,
            false);

        SessionState.SetBool(
            ScenarioMatrixKey,
            scenarioMatrix);

        ResetRuntimeState(
            preserveSessionKeys: true);

        useScenarioMatrix =
            scenarioMatrix;

        if (EditorApplication.isPlaying)
        {
            running = true;
            readyAt =
                EditorApplication.timeSinceStartup +
                0.5d;

            deadlineAt =
                EditorApplication.timeSinceStartup +
                15d;
        }
        else
        {
            EditorApplication.isPlaying = true;
        }
    }

    private static void OnPlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state ==
            PlayModeStateChange.EnteredPlayMode)
        {
            if (!string.IsNullOrWhiteSpace(
                    SessionState.GetString(
                        QueueKey,
                        string.Empty)))
            {
                ResetRuntimeState(
                    preserveSessionKeys: true);

                running = true;

                useScenarioMatrix =
                    SessionState.GetBool(
                        ScenarioMatrixKey,
                        false);

                readyAt =
                    EditorApplication.timeSinceStartup +
                    0.75d;

                deadlineAt =
                    EditorApplication.timeSinceStartup +
                    15d;
            }
        }
        else if (state ==
                 PlayModeStateChange.EnteredEditMode)
        {
            ResetRuntimeState(
                preserveSessionKeys: false);
        }
    }

    private static void Update()
    {
        if (!running ||
            !EditorApplication.isPlaying ||
            awaitingAutoBattle ||
            EditorApplication.timeSinceStartup <
                readyAt)
        {
            return;
        }

        BattleManager manager =
            UnityEngine.Object.FindFirstObjectByType<
                BattleManager>();

        if (!coveragePrepared)
        {
            if ((manager == null ||
                 !manager.IsInitialized ||
                 manager.TurnManager == null) &&
                EditorApplication.timeSinceStartup <
                    deadlineAt)
            {
                return;
            }

            if (SessionState.GetBool(
                    StartedKey,
                    false))
            {
                running = false;
                return;
            }

            SessionState.SetBool(
                StartedKey,
                true);

            pendingProfiles =
                LoadQueuedProfiles();

            pendingReports =
                new List<CharacterVerificationReport>();

            coveragePrepared = true;
            nextCoverageProfileIndex = 0;
            nextCoverageProfileAt =
                EditorApplication.timeSinceStartup;

            Debug.Log(
                "[CharacterVerification] Character Profile 단위 전수 검증 시작 / " +
                $"Profiles={pendingProfiles?.Count ?? 0} / " +
                $"LiveMatrix={(useScenarioMatrix ? "ON" : "OFF")}");

            return;
        }

        if (EditorApplication.timeSinceStartup <
            nextCoverageProfileAt)
        {
            return;
        }

        if (pendingProfiles != null &&
            nextCoverageProfileIndex <
                pendingProfiles.Count)
        {
            RunNextStaticProfile();
            return;
        }

        if (useScenarioMatrix)
        {
            UpdateScenarioMatrix(
                manager);
            return;
        }

        StartSingleRosterAutoBattle(
            manager);
    }

    private static void RunNextStaticProfile()
    {
        CharacterVerificationProfile profile =
            pendingProfiles[
                nextCoverageProfileIndex];

        nextCoverageProfileIndex++;

        try
        {
            CharacterVerificationReport report =
                CharacterVerificationRunner.Run(
                    profile,
                    includeRuntime: true,
                    includeLiveScene:
                        !useScenarioMatrix,
                    writeReport: false);

            if (report != null &&
                useScenarioMatrix &&
                report.Results != null)
            {
                // Matrix Full Coverage에서는 정적 단계의
                // "Live Scene 검증 비활성화" SKIP은 최종 결과가 아니다.
                // 실제 CameraTest 시나리오가 뒤에서 LiveScene Case를 추가하므로
                // placeholder SKIP을 제거해 최종 Coverage를 오염시키지 않는다.
                report.Results.RemoveAll(
                    result =>
                        result != null &&
                        result.ExecutionMode ==
                            CharacterVerificationExecutionMode.LiveScene);

                report.RecalculateCounts();
            }

            if (report != null)
                pendingReports.Add(report);
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }

        nextCoverageProfileAt =
            EditorApplication.timeSinceStartup +
            0.05d;

        Debug.Log(
            "[CharacterVerification] Static Coverage 진행 / " +
            $"{nextCoverageProfileIndex}/" +
            $"{pendingProfiles.Count}");
    }

    private static void StartSingleRosterAutoBattle(
        BattleManager manager)
    {
        try
        {
            if (!IsBattleReady(manager))
            {
                CompleteQueuedVerification(
                    CreateStartFailure(
                        "BattleManager 또는 TurnManager가 준비되지 않아 " +
                        "자동 실전 분석을 시작하지 못했습니다."));
                return;
            }

            if (!CharacterVerificationAutoBattleDriver
                    .TryStart(
                        manager,
                        OnSingleAutoBattleCompleted,
                        out string failure))
            {
                CompleteQueuedVerification(
                    CreateStartFailure(
                        failure));
                return;
            }

            awaitingAutoBattle = true;

            Debug.Log(
                "[CharacterVerification] 정적·격리 검증 완료. " +
                "현재 Roster 한 판 분석을 시작합니다.");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);

            CompleteQueuedVerification(
                CreateStartFailure(
                    exception.Message));
        }
    }

    private static void UpdateScenarioMatrix(
        BattleManager manager)
    {
        if (!matrixPrepared)
        {
            PrepareScenarioMatrix();
            return;
        }

        if (liveScenarios == null ||
            liveScenarios.Count == 0)
        {
            CompleteScenarioMatrix(
                "Live Scenario를 구성하지 못했습니다.");
            return;
        }

        if (waitingForScenarioReload)
        {
            if (!IsBattleReady(manager))
            {
                if (EditorApplication.timeSinceStartup <
                    deadlineAt)
                {
                    return;
                }

                CompleteScenarioMatrix(
                    $"Scenario reload timeout: {GetCurrentScenarioLabel()}");
                return;
            }

            BattleTestScenarioSwitcher switcher =
                UnityEngine.Object.FindFirstObjectByType<
                    BattleTestScenarioSwitcher>(
                        FindObjectsInactive.Include);

            LiveScenario scenario =
                liveScenarios[
                    currentScenarioIndex];

            if (switcher == null ||
                switcher.SelectedPlayer != scenario.Player ||
                switcher.SelectedEncounter != scenario.Encounter)
            {
                if (EditorApplication.timeSinceStartup <
                    deadlineAt)
                {
                    return;
                }

                CompleteScenarioMatrix(
                    $"ScenarioSwitcher 준비 실패: {scenario.Label}");
                return;
            }

            waitingForScenarioReload = false;

            RunLiveSceneCasesForCurrentRoster(
                manager,
                scenario);

            StartCurrentScenarioAutoBattle(
                manager,
                scenario);

            return;
        }

        // matrixPrepared 이후 waitingForScenarioReload/awaitingAutoBattle이 둘 다 false면
        // 다음 시나리오를 시작해야 한다.
        StartCurrentScenarioReload();
    }

    private static void PrepareScenarioMatrix()
    {
        BattleTestScenarioSwitcher switcher =
            UnityEngine.Object.FindFirstObjectByType<
                BattleTestScenarioSwitcher>(
                    FindObjectsInactive.Include);

        if (switcher == null)
        {
            CompleteScenarioMatrix(
                "CameraTest의 BattleTestScenarioSwitcher를 찾지 못했습니다.");
            return;
        }

        originalPlayer =
            switcher.SelectedPlayer;

        originalEncounter =
            switcher.SelectedEncounter;

        originalScenarioCaptured =
            true;

        liveScenarios =
            BuildLiveScenarios(
                switcher);

        currentScenarioIndex = 0;
        matrixPrepared = true;

        Debug.Log(
            "[CharacterVerification] Live Character Matrix 구성 / " +
            string.Join(
                " -> ",
                liveScenarios.Select(
                    item =>
                        item.Label)));

        StartCurrentScenarioReload();
    }

    private static List<LiveScenario>
        BuildLiveScenarios(
            BattleTestScenarioSwitcher switcher)
    {
        List<LiveScenario> result =
            new List<LiveScenario>();

        bool hasStandardEnemies =
            switcher.NormalEnemyPrefab != null ||
            switcher.EliteEnemyPrefab != null;

        if (hasStandardEnemies)
        {
            if (switcher.OlafPrefab != null)
            {
                result.Add(
                    CreateScenario(
                        BattleTestPlayerMode.Olaf,
                        BattleTestEncounterMode.MixedBattle));
            }

            if (switcher.YujinPrefab != null)
            {
                result.Add(
                    CreateScenario(
                        BattleTestPlayerMode.Yujin,
                        BattleTestEncounterMode.MixedBattle));
            }

            if (switcher.HifumiPrefab != null)
            {
                result.Add(
                    CreateScenario(
                        BattleTestPlayerMode.Hifumi,
                        BattleTestEncounterMode.MixedBattle));
            }
        }

        // Boss prefab이 실제로 존재하면 최소 한 번은 Live Roster에 올려
        // 향후 Custom/Boss Character Profile이 조용히 빠지지 않게 한다.
        if (switcher.BossEnemyPrefab != null)
        {
            BattleTestPlayerMode player =
                switcher.OlafPrefab != null
                    ? BattleTestPlayerMode.Olaf
                    : switcher.YujinPrefab != null
                        ? BattleTestPlayerMode.Yujin
                        : BattleTestPlayerMode.Hifumi;

            result.Add(
                CreateScenario(
                    player,
                    BattleTestEncounterMode.BossBattle));
        }

        return result;
    }

    private static LiveScenario CreateScenario(
        BattleTestPlayerMode player,
        BattleTestEncounterMode encounter)
    {
        return new LiveScenario
        {
            Player = player,
            Encounter = encounter,
            Label =
                $"{player}_{encounter}"
        };
    }

    private static void StartCurrentScenarioReload()
    {
        if (liveScenarios == null ||
            currentScenarioIndex >=
                liveScenarios.Count)
        {
            CompleteScenarioMatrix(
                failure: null);
            return;
        }

        BattleTestScenarioSwitcher switcher =
            UnityEngine.Object.FindFirstObjectByType<
                BattleTestScenarioSwitcher>(
                    FindObjectsInactive.Include);

        if (switcher == null)
        {
            CompleteScenarioMatrix(
                "BattleTestScenarioSwitcher가 Scene에서 사라졌습니다.");
            return;
        }

        LiveScenario scenario =
            liveScenarios[
                currentScenarioIndex];

        Debug.Log(
            "[CharacterVerification] Live Scenario 시작 / " +
            $"{currentScenarioIndex + 1}/{liveScenarios.Count} / " +
            scenario.Label);

        waitingForScenarioReload = true;
        readyAt =
            EditorApplication.timeSinceStartup +
            0.8d;

        deadlineAt =
            EditorApplication.timeSinceStartup +
            20d;

        switcher.ApplyVerificationScenario(
            scenario.Player,
            scenario.Encounter,
            reloadScene: true);
    }

    private static void RunLiveSceneCasesForCurrentRoster(
        BattleManager manager,
        LiveScenario scenario)
    {
        if (manager?.BattleContext == null)
            return;

        foreach (CharacterVerificationProfile profile
                 in pendingProfiles)
        {
            if (!ProfileParticipatesInCurrentRoster(
                    profile,
                    manager.BattleContext))
            {
                continue;
            }

            CharacterVerificationReport master =
                FindPendingReport(
                    profile);

            if (master == null)
                continue;

            liveCoveredProfileIds.Add(
                profile.ProfileId);

            CharacterVerificationReport liveReport =
                CharacterVerificationRunner.Run(
                    profile,
                    includeRuntime: false,
                    includeLiveScene: true,
                    writeReport: false);

            if (liveReport?.Results == null)
                continue;

            foreach (CharacterVerificationCaseResult result
                     in liveReport.Results)
            {
                if (result == null ||
                    result.ExecutionMode !=
                        CharacterVerificationExecutionMode.LiveScene)
                {
                    continue;
                }

                master.Results.Add(
                    CloneForScenario(
                        result,
                        scenario.Label));
            }

            master.RecalculateCounts();
        }
    }

    private static void StartCurrentScenarioAutoBattle(
        BattleManager manager,
        LiveScenario scenario)
    {
        if (!CharacterVerificationAutoBattleDriver
                .TryStart(
                    manager,
                    OnMatrixAutoBattleCompleted,
                    out string failure))
        {
            CompleteScenarioMatrix(
                $"AutoBattle 시작 실패 / {scenario.Label} / {failure}");
            return;
        }

        awaitingAutoBattle = true;

        Debug.Log(
            "[CharacterVerification] Live AutoBattle / " +
            scenario.Label);
    }

    private static void OnSingleAutoBattleCompleted(
        CharacterVerificationAutoBattleResult result)
    {
        awaitingAutoBattle = false;
        CompleteQueuedVerification(
            result);
    }

    private static void OnMatrixAutoBattleCompleted(
        CharacterVerificationAutoBattleResult result)
    {
        awaitingAutoBattle = false;

        LiveScenario scenario =
            liveScenarios != null &&
            currentScenarioIndex <
                liveScenarios.Count
                ? liveScenarios[
                    currentScenarioIndex]
                : null;

        AppendAutoBattleResults(
            result,
            scenario?.Label);

        currentScenarioIndex++;

        if (liveScenarios != null &&
            currentScenarioIndex <
                liveScenarios.Count)
        {
            StartCurrentScenarioReload();
            return;
        }

        CompleteScenarioMatrix(
            failure: null);
    }

    private static void AppendAutoBattleResults(
        CharacterVerificationAutoBattleResult autoResult,
        string scenarioLabel)
    {
        if (pendingProfiles == null ||
            pendingReports == null)
        {
            return;
        }

        for (int i = 0;
             i < pendingProfiles.Count;
             i++)
        {
            CharacterVerificationProfile profile =
                pendingProfiles[i];

            CharacterVerificationReport report =
                i < pendingReports.Count
                    ? pendingReports[i]
                    : null;

            if (profile == null ||
                report == null ||
                !IsProfileParticipant(
                    profile,
                    autoResult))
            {
                continue;
            }

            liveCoveredProfileIds.Add(
                profile.ProfileId);

            report.Results ??=
                new List<CharacterVerificationCaseResult>();

            report.Results.Add(
                BuildAutoBattleCase(
                    profile,
                    autoResult,
                    scenarioLabel));

            CharacterVerificationCaseResult
                skillObservation =
                    BuildParticipantSkillObservationCase(
                        profile,
                        autoResult,
                        scenarioLabel);

            if (skillObservation != null)
                report.Results.Add(skillObservation);

            report.RecalculateCounts();
        }
    }

    private static void CompleteScenarioMatrix(
        string failure)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(
                    failure))
            {
                Debug.LogError(
                    "[CharacterVerification] Live Matrix 실패 / " +
                    failure);
            }

            AddMissingLiveCoverageFailures(
                failure);

            WriteAllPendingReports();

            string aggregate =
                CoreCharacterVerificationAggregateWriter.TryWrite(
                    pendingProfiles,
                    pendingReports,
                    "All Character Full Coverage");

            int pass =
                pendingReports?
                    .Sum(
                        report =>
                            report?.PassCount ?? 0) ?? 0;

            int fail =
                pendingReports?
                    .Sum(
                        report =>
                            report?.FailCount ?? 0) ?? 0;

            int skip =
                pendingReports?
                    .Sum(
                        report =>
                            report?.SkipCount ?? 0) ?? 0;

            int error =
                pendingReports?
                    .Sum(
                        report =>
                            report?.ErrorCount ?? 0) ?? 0;

            bool allPassed =
                fail == 0 &&
                error == 0 &&
                string.IsNullOrWhiteSpace(
                    failure);

            string summary =
                "[CharacterVerification] ALL Character Coverage 완료 / " +
                $"Result={(allPassed ? "PASSED" : "FAILED")}, " +
                $"Profiles={pendingReports?.Count ?? 0}, " +
                $"LiveScenarios={liveScenarios?.Count ?? 0}, " +
                $"PASS={pass}, FAIL={fail}, SKIP={skip}, ERROR={error}, " +
                $"Aggregate={aggregate}";

            if (allPassed)
                Debug.Log(summary);
            else
                Debug.LogError(summary);

            CharacterVerificationReport display =
                pendingReports?
                    .FirstOrDefault(
                        report =>
                            report != null &&
                            (report.FailCount > 0 ||
                             report.ErrorCount > 0)) ??
                pendingReports?
                    .LastOrDefault();

            CharacterVerificationWindow.ShowReport(
                display);
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
        finally
        {
            RestoreOriginalScenarioAndReset();
        }
    }

    private static void AddMissingLiveCoverageFailures(
        string matrixFailure)
    {
        if (pendingProfiles == null ||
            pendingReports == null)
        {
            return;
        }

        for (int i = 0;
             i < pendingProfiles.Count;
             i++)
        {
            CharacterVerificationProfile profile =
                pendingProfiles[i];

            CharacterVerificationReport report =
                i < pendingReports.Count
                    ? pendingReports[i]
                    : null;

            if (profile == null ||
                report == null ||
                liveCoveredProfileIds.Contains(
                    profile.ProfileId))
            {
                continue;
            }

            report.Results ??=
                new List<CharacterVerificationCaseResult>();

            report.Results.Add(
                new CharacterVerificationCaseResult
                {
                    CaseId =
                        "live.matrix.character_not_covered",
                    DisplayName =
                        "Live Character Matrix 포함 여부",
                    Category =
                        CharacterVerificationCategory.Boundary,
                    ExecutionMode =
                        CharacterVerificationExecutionMode.LiveScene,
                    Status =
                        CharacterVerificationStatus.Fail,
                    Expected =
                        "모든 concrete Character Profile이 최소 한 번 실제 CameraTest Roster에 등장",
                    Actual =
                        "Live Scenario에서 해당 Character를 관찰하지 못함",
                    Details =
                        $"Profile={profile.ProfileId}\n" +
                        $"Bundle={profile.Bundle?.DisplayName}\n" +
                        $"Kind={profile.Bundle?.Kind}\n" +
                        (string.IsNullOrWhiteSpace(matrixFailure)
                            ? string.Empty
                            : $"MatrixFailure={matrixFailure}")
                });

            report.RecalculateCounts();
        }
    }

    private static void WriteAllPendingReports()
    {
        if (pendingReports == null)
            return;

        foreach (CharacterVerificationReport report
                 in pendingReports)
        {
            if (report == null)
                continue;

            report.FinishedAt =
                DateTime.Now.ToString("O");

            report.RecalculateCounts();

            report.OutputDirectory =
                CharacterVerificationReportWriter.Write(
                    report);
        }
    }

    private static void RestoreOriginalScenarioAndReset()
    {
        BattleTestPlayerMode restorePlayer =
            originalPlayer;

        BattleTestEncounterMode restoreEncounter =
            originalEncounter;

        bool shouldRestore =
            originalScenarioCaptured;

        ResetRuntimeState(
            preserveSessionKeys: false);

        if (!shouldRestore ||
            !EditorApplication.isPlaying)
        {
            return;
        }

        BattleTestScenarioSwitcher switcher =
            UnityEngine.Object.FindFirstObjectByType<
                BattleTestScenarioSwitcher>(
                    FindObjectsInactive.Include);

        if (switcher == null)
        {
            Debug.LogWarning(
                "[CharacterVerification] 원래 CameraTest Roster를 복구할 " +
                "BattleTestScenarioSwitcher를 찾지 못했습니다.");
            return;
        }

        Debug.Log(
            "[CharacterVerification] 원래 CameraTest 선택 복구 / " +
            $"{restorePlayer}_{restoreEncounter}");

        switcher.ApplyVerificationScenario(
            restorePlayer,
            restoreEncounter,
            reloadScene: true);
    }

    private static List<CharacterVerificationProfile>
        LoadQueuedProfiles()
    {
        string raw =
            SessionState.GetString(
                QueueKey,
                string.Empty);

        SessionState.EraseString(
            QueueKey);

        string[] paths =
            raw.Split(
                new[]
                {
                    '\n'
                },
                StringSplitOptions.RemoveEmptyEntries);

        List<CharacterVerificationProfile> profiles =
            new List<CharacterVerificationProfile>();

        for (int i = 0;
             i < paths.Length;
             i++)
        {
            CharacterVerificationProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    CharacterVerificationProfile>(
                        paths[i]);

            if (profile != null)
                profiles.Add(profile);
        }

        return profiles;
    }

    private static CharacterVerificationReport
        FindPendingReport(
            CharacterVerificationProfile profile)
    {
        if (profile == null ||
            pendingProfiles == null ||
            pendingReports == null)
        {
            return null;
        }

        int index =
            pendingProfiles.IndexOf(
                profile);

        return index >= 0 &&
               index < pendingReports.Count
            ? pendingReports[index]
            : null;
    }

    private static bool ProfileParticipatesInCurrentRoster(
        CharacterVerificationProfile profile,
        BattleContext context)
    {
        if (profile?.Bundle == null ||
            context == null)
        {
            return false;
        }

        if (MatchesProfileCharacter(
                profile,
                context.Player))
        {
            return true;
        }

        if (context.Enemies == null)
            return false;

        foreach (Character enemy in context.Enemies)
        {
            if (MatchesProfileCharacter(
                    profile,
                    enemy))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesProfileCharacter(
        CharacterVerificationProfile profile,
        Character character)
    {
        CharacterAuthoringBundle bundle =
            profile?.Bundle;

        if (bundle == null ||
            character == null)
        {
            return false;
        }

        // 가장 강한 식별자: 실제 CharacterData asset reference.
        if (bundle.CharacterData != null &&
            ReferenceEquals(
                bundle.CharacterData,
                character.Data))
        {
            return true;
        }

        // Live Matrix에서 EliteEnemy와 Boss는 같은 C# 타입/Kind를 사용할 수 있다.
        // 따라서 IsCompatibleWith 같은 "타입 호환" 판정으로 Roster 참가 여부를
        // 결정하면 Elite profile을 BossBattle에, Boss profile을 MixedBattle에
        // 잘못 넣게 된다. 실제 Data identity만 비교한다.
        string[] expectedIdentities =
        {
            bundle.CharacterData?.CharacterName,
            bundle.CharacterData?.name,
            bundle.CharacterPrefab?.Data?.CharacterName,
            bundle.CharacterPrefab?.Data?.name,
            bundle.DisplayName
        };

        string[] actualIdentities =
        {
            character.Data?.CharacterName,
            character.Data?.name,
            character.name
        };

        foreach (string expected in expectedIdentities)
        {
            if (string.IsNullOrWhiteSpace(
                    expected))
            {
                continue;
            }

            string cleanExpected =
                NormalizeIdentity(
                    expected);

            foreach (string actual in actualIdentities)
            {
                if (string.IsNullOrWhiteSpace(
                        actual))
                {
                    continue;
                }

                if (string.Equals(
                        cleanExpected,
                        NormalizeIdentity(actual),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizeIdentity(
        string value)
    {
        return
            (value ?? string.Empty)
                .Replace(
                    "(Clone)",
                    string.Empty)
                .Trim();
    }

    private static bool IsBattleReady(
        BattleManager manager)
    {
        return manager != null &&
               manager.IsInitialized &&
               manager.TurnManager != null &&
               manager.BattleContext?.Player != null;
    }

    private static CharacterVerificationAutoBattleResult
        CreateStartFailure(
            string failure)
    {
        return new CharacterVerificationAutoBattleResult
        {
            CompletedNormally = false,
            Outcome = "START_FAILED",
            FailureMessage =
                string.IsNullOrWhiteSpace(
                    failure)
                    ? "자동 실전 분석 시작 실패"
                    : failure
        };
    }

    private static void CompleteQueuedVerification(
        CharacterVerificationAutoBattleResult autoResult)
    {
        try
        {
            pendingReports ??=
                new List<CharacterVerificationReport>();

            for (int i = 0;
                 i < pendingReports.Count;
                 i++)
            {
                CharacterVerificationReport report =
                    pendingReports[i];

                CharacterVerificationProfile profile =
                    pendingProfiles != null &&
                    i < pendingProfiles.Count
                        ? pendingProfiles[i]
                        : null;

                if (report == null)
                    continue;

                report.Results ??=
                    new List<CharacterVerificationCaseResult>();

                report.Results.Add(
                    BuildAutoBattleCase(
                        profile,
                        autoResult,
                        scenarioLabel: null));

                CharacterVerificationCaseResult
                    skillObservation =
                        BuildParticipantSkillObservationCase(
                            profile,
                            autoResult,
                            scenarioLabel: null);

                if (skillObservation != null)
                    report.Results.Add(skillObservation);

                report.FinishedAt =
                    DateTime.Now.ToString("O");

                report.RecalculateCounts();

                report.OutputDirectory =
                    CharacterVerificationReportWriter.Write(
                        report);
            }

            string aggregate =
                CoreCharacterVerificationAggregateWriter.TryWrite(
                    pendingProfiles,
                    pendingReports,
                    "Single Roster Full Coverage");

            int pass =
                pendingReports.Sum(
                    report =>
                        report?.PassCount ?? 0);

            int fail =
                pendingReports.Sum(
                    report =>
                        report?.FailCount ?? 0);

            int skip =
                pendingReports.Sum(
                    report =>
                        report?.SkipCount ?? 0);

            int error =
                pendingReports.Sum(
                    report =>
                        report?.ErrorCount ?? 0);

            bool allPassed =
                fail == 0 &&
                error == 0 &&
                autoResult?.CompletedNormally == true;

            string summary =
                "[CharacterVerification] Full Character Coverage 완료 / " +
                $"Result={(allPassed ? "PASSED" : "FAILED")}, " +
                $"Profiles={pendingReports.Count}, " +
                $"PASS={pass}, FAIL={fail}, SKIP={skip}, ERROR={error}, " +
                $"Aggregate={aggregate}";

            if (allPassed)
                Debug.Log(summary);
            else
                Debug.LogError(summary);

            CharacterVerificationReport display =
                pendingReports.FirstOrDefault(
                    report =>
                        report != null &&
                        (report.FailCount > 0 ||
                         report.ErrorCount > 0)) ??
                pendingReports.LastOrDefault();

            CharacterVerificationWindow.ShowReport(
                display);
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
        finally
        {
            ResetRuntimeState(
                preserveSessionKeys: false);
        }
    }

    private static CharacterVerificationCaseResult
        BuildAutoBattleCase(
            CharacterVerificationProfile profile,
            CharacterVerificationAutoBattleResult result,
            string scenarioLabel)
    {
        bool participates =
            IsProfileParticipant(
                profile,
                result);

        CharacterVerificationStatus status;
        string actual;
        string details;

        bool rosterCaptured =
            result?.ParticipantNames != null &&
            result.ParticipantNames.Count > 0;

        if (!rosterCaptured &&
            result?.CompletedNormally != true)
        {
            status =
                CharacterVerificationStatus.Error;

            actual =
                result?.Outcome ??
                "Character Coverage 통합 전투 시작 실패";

            details =
                result?.BuildDetails() ??
                "CharacterVerificationAutoBattleResult가 null입니다.";
        }
        else if (!participates)
        {
            status =
                CharacterVerificationStatus.Skip;

            actual =
                "현재 전투 Roster에 해당 캐릭터 없음";

            details =
                result?.BuildDetails() ??
                "Character Coverage 통합 전투 결과 없음";
        }
        else if (result?.CompletedNormally == true)
        {
            status =
                CharacterVerificationStatus.Pass;

            actual =
                result.AnalysisTurnBudgetReached
                    ? $"실전 스모크 구간 정상 완료 · {result.Turns}턴"
                    : $"정상 종료 · {result.Outcome} · {result.Turns}턴";

            details =
                result.BuildDetails();
        }
        else if (result?.TurnLimitReached == true)
        {
            status =
                CharacterVerificationStatus.Fail;

            actual =
                $"턴 제한 도달 · {result.Turns}턴";

            details =
                result.BuildDetails();
        }
        else
        {
            status =
                CharacterVerificationStatus.Error;

            actual =
                result?.Outcome ??
                "Character Coverage 통합 전투 결과 없음";

            details =
                result?.BuildDetails() ??
                "CharacterVerificationAutoBattleResult가 null입니다.";
        }

        return new CharacterVerificationCaseResult
        {
            CaseId =
                BuildScenarioCaseId(
                    "live.autobattle.full_match",
                    scenarioLabel),
            DisplayName =
                string.IsNullOrWhiteSpace(
                    scenarioLabel)
                    ? "승률 자동계획 실전 스모크 전투"
                    : $"실전 스모크 전투 · {scenarioLabel}",
            Category =
                CharacterVerificationCategory.Boundary,
            ExecutionMode =
                CharacterVerificationExecutionMode.LiveScene,
            Status = status,
            Expected =
                "실제 Roster에 참가한 Character가 승률 자동계획 전투를 " +
                "정상 종료하거나 검증용 5턴 구간을 완주",
            Actual = actual,
            Details = details,
            ElapsedMilliseconds =
                result?.ElapsedMilliseconds ?? 0
        };
    }

    private static CharacterVerificationCaseResult
        BuildParticipantSkillObservationCase(
            CharacterVerificationProfile profile,
            CharacterVerificationAutoBattleResult result,
            string scenarioLabel)
    {
        if (profile?.Bundle == null ||
            result == null ||
            !IsProfileParticipant(
                profile,
                result))
        {
            return null;
        }

        List<CharacterVerificationObservedSkillUsage> usages =
            result.ObservedSkillUsages?
                .Where(
                    usage =>
                        MatchesProfileUsage(
                            profile,
                            usage))
                .ToList() ??
            new List<CharacterVerificationObservedSkillUsage>();

        CharacterVerificationStatus status;
        string actual;

        if (!result.CompletedNormally)
        {
            status =
                CharacterVerificationStatus.Error;

            actual =
                "통합 전투가 정상 완료되지 않아 ActionStart 관찰 무효";
        }
        else if (usages.Count == 0)
        {
            status =
                CharacterVerificationStatus.Fail;

            actual =
                "해당 Character의 Skill ActionStart가 한 번도 관찰되지 않음";
        }
        else
        {
            status =
                CharacterVerificationStatus.Pass;

            actual =
                $"{usages.Count}개 고유 Skill ActionStart 관찰";
        }

        string used =
            usages.Count == 0
                ? "NONE"
                : string.Join(
                    ", ",
                    usages.Select(
                        usage =>
                            usage.BuildLabel()));

        return new CharacterVerificationCaseResult
        {
            CaseId =
                BuildScenarioCaseId(
                    "live.autobattle.character_skill_observation",
                    scenarioLabel),
            DisplayName =
                string.IsNullOrWhiteSpace(
                    scenarioLabel)
                    ? "실전 Character 스킬 실행 관찰"
                    : $"실전 Character 스킬 실행 관찰 · {scenarioLabel}",
            Category =
                CharacterVerificationCategory.Boundary,
            ExecutionMode =
                CharacterVerificationExecutionMode.LiveScene,
            Status = status,
            Expected =
                "플레이어/적 구분 없이 해당 Character가 실제 ActionStart를 최소 1회 발생",
            Actual = actual,
            Details =
                $"Observed={used}\n" +
                "모든 후보 스킬의 개별 계약은 Isolated skill coverage가 담당하고, " +
                "이 Case는 실제 CameraTest AI/행동 파이프라인에서 캐릭터가 행동했는지를 검증합니다.",
            ElapsedMilliseconds =
                result.ElapsedMilliseconds
        };
    }

    private static CharacterVerificationCaseResult
        CloneForScenario(
            CharacterVerificationCaseResult source,
            string scenarioLabel)
    {
        return new CharacterVerificationCaseResult
        {
            CaseId =
                BuildScenarioCaseId(
                    source.CaseId,
                    scenarioLabel),
            DisplayName =
                $"{source.DisplayName} · {scenarioLabel}",
            Category =
                source.Category,
            ExecutionMode =
                source.ExecutionMode,
            Status =
                source.Status,
            Expected =
                source.Expected,
            Actual =
                source.Actual,
            Details =
                source.Details,
            InitialSnapshot =
                source.InitialSnapshot,
            FinalSnapshot =
                source.FinalSnapshot,
            ElapsedMilliseconds =
                source.ElapsedMilliseconds
        };
    }

    private static string BuildScenarioCaseId(
        string baseId,
        string scenarioLabel)
    {
        if (string.IsNullOrWhiteSpace(
                scenarioLabel))
        {
            return baseId;
        }

        string safe =
            scenarioLabel
                .Replace(
                    ' ',
                    '_')
                .Replace(
                    '/',
                    '_');

        return
            $"{baseId}@{safe}";
    }

    private static bool IsProfileParticipant(
        CharacterVerificationProfile profile,
        CharacterVerificationAutoBattleResult result)
    {
        if (profile?.Bundle == null ||
            result == null)
        {
            return false;
        }

        IReadOnlyList<string> identities =
            result.ParticipantIdentities != null &&
            result.ParticipantIdentities.Count > 0
                ? result.ParticipantIdentities
                : result.ParticipantNames;

        return MatchesAnyProfileIdentity(
            profile,
            identities);
    }

    private static bool MatchesProfileUsage(
        CharacterVerificationProfile profile,
        CharacterVerificationObservedSkillUsage usage)
    {
        if (usage == null)
            return false;

        return MatchesAnyProfileIdentity(
            profile,
            new[]
            {
                usage.OwnerName,
                usage.OwnerDataName,
                usage.OwnerObjectName
            });
    }

    private static bool MatchesAnyProfileIdentity(
        CharacterVerificationProfile profile,
        IReadOnlyList<string> runtimeIdentities)
    {
        if (profile?.Bundle == null ||
            runtimeIdentities == null ||
            runtimeIdentities.Count == 0)
        {
            return false;
        }

        List<string> identities =
            new List<string>
            {
                profile.Bundle.DisplayName,
                profile.Bundle.name,
                profile.Bundle.CharacterData?.CharacterName,
                profile.Bundle.CharacterData?.name,
                profile.Bundle.CharacterPrefab?.Data?.CharacterName,
                profile.Bundle.CharacterPrefab?.Data?.name,
                profile.Bundle.CharacterPrefab?.name
            };

        foreach (string identity in identities)
        {
            if (string.IsNullOrWhiteSpace(
                    identity))
            {
                continue;
            }

            for (int i = 0;
                 i < runtimeIdentities.Count;
                 i++)
            {
                string runtime =
                    runtimeIdentities[i];

                if (string.IsNullOrWhiteSpace(
                        runtime))
                {
                    continue;
                }

                string cleanRuntime =
                    runtime
                        .Replace(
                            "(Clone)",
                            string.Empty)
                        .Trim();

                string cleanIdentity =
                    identity
                        .Replace(
                            "(Clone)",
                            string.Empty)
                        .Trim();

                if (string.Equals(
                        cleanIdentity,
                        cleanRuntime,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetCurrentScenarioLabel()
    {
        return liveScenarios != null &&
               currentScenarioIndex >= 0 &&
               currentScenarioIndex <
                   liveScenarios.Count
            ? liveScenarios[
                currentScenarioIndex].Label
            : "NONE";
    }

    private static void ResetRuntimeState(
        bool preserveSessionKeys)
    {
        running = false;
        awaitingAutoBattle = false;
        coveragePrepared = false;
        useScenarioMatrix = false;
        matrixPrepared = false;
        waitingForScenarioReload = false;
        nextCoverageProfileIndex = 0;
        currentScenarioIndex = 0;
        pendingProfiles = null;
        pendingReports = null;
        liveScenarios = null;
        liveCoveredProfileIds.Clear();
        originalScenarioCaptured = false;

        if (!preserveSessionKeys)
        {
            SessionState.SetBool(
                StartedKey,
                false);

            SessionState.SetBool(
                ScenarioMatrixKey,
                false);

            SessionState.EraseString(
                QueueKey);
        }
    }
}
#endif
