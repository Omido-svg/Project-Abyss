#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProjectAbyssFolderStructureMigrator
{
    private const string MenuRoot =
        "Tools/Project Abyss/Folder Structure/";

    private const string ScriptRoot =
        "Assets/1. Scripts";

    private const string RuntimeRoot =
        ScriptRoot + "/Runtime";

    private const string EditorRoot =
        ScriptRoot + "/Editor";

    private const string DataRoot =
        "Assets/2. Data";

    [MenuItem(MenuRoot + "1. Dry Run", priority = 2002)]
    public static void DryRun()
    {
        string report =
            BuildReport();

        EditorGUIUtility.systemCopyBuffer =
            report;

        Debug.Log(report);

        EditorUtility.DisplayDialog(
            "Folder Migration Dry Run",
            "이동 계획을 Console에 출력하고 클립보드에 복사했습니다.\n\n" +
            "CONFLICT가 0인지 확인한 뒤 Execute Migration을 실행하세요.",
            "확인");
    }

    [MenuItem(MenuRoot + "2. Execute Migration", priority = 2003)]
    public static void ExecuteMigration()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Migration Blocked",
                "Play Mode에서는 폴더를 이동할 수 없습니다.",
                "확인");

            return;
        }

        List<MoveOperation> plan =
            BuildPlan();

        List<MoveOperation> conflicts =
            plan.Where(
                    operation =>
                        Evaluate(operation) ==
                        MoveState.Conflict)
                .ToList();

        if (conflicts.Count > 0)
        {
            DryRun();

            EditorUtility.DisplayDialog(
                "Migration Blocked",
                $"대상 경로 충돌이 {conflicts.Count}개 있습니다.\n" +
                "Console의 Dry Run 결과를 확인하세요.",
                "확인");

            return;
        }

        List<MoveOperation> ready =
            plan.Where(
                    operation =>
                        Evaluate(operation) ==
                        MoveState.Ready)
                .ToList();

        if (ready.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Folder Migration",
                "이동할 항목이 없습니다. 이미 정리되었을 수 있습니다.",
                "확인");

            return;
        }

        if (!EditorSceneManager
                .SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        bool confirmed =
            EditorUtility.DisplayDialog(
                "Execute Folder Migration",
                $"{ready.Count}개 항목을 이동합니다.\n\n" +
                "AssetDatabase.MoveAsset을 사용하므로 GUID와 일반 Unity 참조는 유지됩니다.\n" +
                "그래도 실행 전 Git commit 또는 백업을 권장합니다.",
                "실행",
                "취소");

        if (!confirmed)
            return;

        List<string> successes =
            new List<string>();

        List<string> errors =
            new List<string>();

        try
        {
            AssetDatabase.SaveAssets();

            for (int i = 0;
                 i < ready.Count;
                 i++)
            {
                MoveOperation operation =
                    ready[i];

                EditorUtility.DisplayProgressBar(
                    "Project Abyss Folder Migration",
                    $"[{i + 1}/{ready.Count}] {operation.Source}",
                    (float)i / ready.Count);

                string destinationParent =
                    Normalize(
                        Path.GetDirectoryName(
                            operation.Destination));

                EnsureFolder(destinationParent);

                string error =
                    AssetDatabase.MoveAsset(
                        operation.Source,
                        operation.Destination);

                if (string.IsNullOrWhiteSpace(error))
                {
                    successes.Add(
                        $"{operation.Source} -> {operation.Destination}");
                }
                else
                {
                    errors.Add(
                        $"{operation.Source} -> {operation.Destination}\n" +
                        $"  {error}");
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add(
                exception.ToString());
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        DeleteEmptyLegacyFolders();
        AssetDatabase.Refresh();

        string result =
            BuildResultReport(
                successes,
                errors);

        EditorGUIUtility.systemCopyBuffer =
            result;

        Debug.Log(result);

        EditorUtility.DisplayDialog(
            errors.Count == 0
                ? "Migration Complete"
                : "Migration Completed With Errors",
            $"성공: {successes.Count}\n" +
            $"실패: {errors.Count}\n\n" +
            "결과를 Console에 출력하고 클립보드에 복사했습니다.",
            "확인");
    }

    [MenuItem(MenuRoot + "3. Print Target Structure", priority = 2004)]
    public static void PrintTargetStructure()
    {
        EditorGUIUtility.systemCopyBuffer =
            TargetStructure;

        Debug.Log(
            TargetStructure);
    }

    private static List<MoveOperation> BuildPlan()
    {
        List<MoveOperation> plan =
            new List<MoveOperation>();

        // 비스크립트와 Editor 전용 파일을 먼저 분리한다.
        plan.Add(new MoveOperation(
            ScriptRoot + "/Character/Olaf/Olaf Status Data.asset",
            DataRoot + "/Characters/Olaf/Olaf Status Data.asset",
            "CharacterData Asset을 스크립트 폴더 밖으로 이동"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/BattleVisual/Editor",
            EditorRoot + "/Visual",
            "BattleVisual Editor 도구 분리"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Utils/Tools",
            EditorRoot + "/Tools",
            "Exporter Editor 도구 분리"));

        // 주요 Runtime 도메인.
        plan.Add(new MoveOperation(
            ScriptRoot + "/Battle",
            RuntimeRoot + "/Battle",
            "전투 액션, 데미지, 타게팅"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Character",
            RuntimeRoot + "/Characters",
            "캐릭터 공통, 적, 올라프"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Skills",
            RuntimeRoot + "/Skills",
            "스킬 코어, 데이터 정의, 효과"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/StatusEffects",
            RuntimeRoot + "/Status",
            "상태이상 시스템"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Managers",
            RuntimeRoot + "/Systems",
            "AI, BattleFlow, Resolution, Stats"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/BattleUI",
            RuntimeRoot + "/Presentation/UI",
            "전투 UI"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/BattleVisual",
            RuntimeRoot + "/Presentation/Visual",
            "전투 연출과 VFX"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Camera",
            RuntimeRoot + "/Presentation/Camera",
            "카메라 호환 코드와 카메라 포인트"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Event",
            RuntimeRoot + "/Battle/Events",
            "BattleEvent와 구독 수명주기"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Roll",
            RuntimeRoot + "/Battle/Roll",
            "Dice, Coin, Slot Roll"));

        // 캐릭터 빌드와 중복 최상위 폴더.
        plan.Add(new MoveOperation(
            ScriptRoot + "/Augments",
            RuntimeRoot + "/Characters/Build/Augments",
            "캐릭터 증강"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/ItemScript",
            RuntimeRoot + "/Characters/Build/Items",
            "공통 캐릭터 아이템"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Enemy",
            RuntimeRoot + "/Characters/Enemies/Base",
            "중복 최상위 Enemy 폴더 제거"));

        // 진단과 공용 코드.
        plan.Add(new MoveOperation(
            ScriptRoot + "/Debug",
            RuntimeRoot + "/Diagnostics/Debug",
            "디버그 UI와 튜너"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Utils/BattleLogBuilder.cs",
            RuntimeRoot + "/Diagnostics/Logging/BattleLogBuilder.cs",
            "구조화 전투 로그"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Utils/BattleLogEntry.cs",
            RuntimeRoot + "/Diagnostics/Logging/BattleLogEntry.cs",
            "구조화 전투 로그"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Utils/BattleLogger.cs",
            RuntimeRoot + "/Diagnostics/Logging/BattleLogger.cs",
            "구조화 전투 로그"));

        plan.Add(new MoveOperation(
            ScriptRoot + "/Utils/Utils.cs",
            RuntimeRoot + "/Shared/Utils.cs",
            "공용 유틸리티"));

        return plan;
    }

    private static string BuildReport()
    {
        List<MoveOperation> plan =
            BuildPlan();

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "PROJECT ABYSS FOLDER MIGRATION — DRY RUN");

        builder.AppendLine(
            "============================================================");

        int ready = 0;
        int alreadyMoved = 0;
        int missing = 0;
        int conflicts = 0;

        for (int i = 0;
             i < plan.Count;
             i++)
        {
            MoveOperation operation =
                plan[i];

            MoveState state =
                Evaluate(operation);

            switch (state)
            {
                case MoveState.Ready:
                    ready++;
                    break;

                case MoveState.AlreadyMoved:
                    alreadyMoved++;
                    break;

                case MoveState.SourceMissing:
                    missing++;
                    break;

                case MoveState.Conflict:
                    conflicts++;
                    break;
            }

            builder.AppendLine(
                $"[{i + 1:D2}] {state}");

            builder.AppendLine(
                $"  FROM: {operation.Source}");

            builder.AppendLine(
                $"    TO: {operation.Destination}");

            builder.AppendLine(
                $"  NOTE: {operation.Description}");

            builder.AppendLine();
        }

        builder.AppendLine(
            "============================================================");

        builder.AppendLine(
            $"READY          : {ready}");

        builder.AppendLine(
            $"ALREADY_MOVED  : {alreadyMoved}");

        builder.AppendLine(
            $"SOURCE_MISSING : {missing}");

        builder.AppendLine(
            $"CONFLICT       : {conflicts}");

        builder.AppendLine();
        builder.AppendLine(TargetStructure);

        return builder.ToString();
    }

    private static string BuildResultReport(
        IReadOnlyList<string> successes,
        IReadOnlyList<string> errors)
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "PROJECT ABYSS FOLDER MIGRATION — RESULT");

        builder.AppendLine(
            "============================================================");

        builder.AppendLine(
            $"SUCCESS: {successes.Count}");

        builder.AppendLine(
            $"ERROR  : {errors.Count}");

        builder.AppendLine();

        foreach (string success in successes)
            builder.AppendLine(success);

        if (errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("ERRORS");

            foreach (string error in errors)
            {
                builder.AppendLine(error);
                builder.AppendLine();
            }
        }

        builder.AppendLine();
        builder.AppendLine(TargetStructure);

        return builder.ToString();
    }

    private static MoveState Evaluate(
        MoveOperation operation)
    {
        bool sourceExists =
            AssetExists(operation.Source);

        bool destinationExists =
            AssetExists(operation.Destination);

        if (sourceExists &&
            destinationExists)
        {
            return MoveState.Conflict;
        }

        if (sourceExists)
            return MoveState.Ready;

        if (destinationExists)
            return MoveState.AlreadyMoved;

        return MoveState.SourceMissing;
    }

    private static bool AssetExists(
        string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
            return true;

        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            return true;

        return File.Exists(
            ToAbsolutePath(assetPath));
    }

    private static void EnsureFolder(
        string assetFolder)
    {
        assetFolder =
            Normalize(assetFolder);

        if (string.IsNullOrWhiteSpace(assetFolder) ||
            assetFolder == "Assets" ||
            AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent =
            Normalize(
                Path.GetDirectoryName(assetFolder));

        string folderName =
            Path.GetFileName(assetFolder);

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(assetFolder))
        {
            AssetDatabase.CreateFolder(
                parent,
                folderName);
        }
    }

    private static void DeleteEmptyLegacyFolders()
    {
        string[] folders =
        {
            ScriptRoot + "/Utils",
            ScriptRoot + "/Enemy",
            ScriptRoot + "/Augments",
            ScriptRoot + "/ItemScript",
            ScriptRoot + "/BattleVisual/Editor"
        };

        foreach (string folder in folders)
            DeleteFolderIfEmpty(folder);
    }

    private static void DeleteFolderIfEmpty(
        string assetFolder)
    {
        if (!AssetDatabase.IsValidFolder(assetFolder))
            return;

        string absolutePath =
            ToAbsolutePath(assetFolder);

        if (!Directory.Exists(absolutePath))
            return;

        if (Directory
            .EnumerateFileSystemEntries(absolutePath)
            .Any())
        {
            return;
        }

        AssetDatabase.DeleteAsset(assetFolder);
    }

    private static string ToAbsolutePath(
        string assetPath)
    {
        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."));

        return Path.GetFullPath(
            Path.Combine(
                projectRoot,
                assetPath));
    }

    private static string Normalize(
        string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/');
    }

    private const string TargetStructure =
@"TARGET STRUCTURE
------------------------------------------------------------
Assets/
├─ 1. Scripts/
│  ├─ Runtime/
│  │  ├─ Battle/
│  │  │  ├─ Damage/
│  │  │  ├─ Targeting/
│  │  │  ├─ Events/
│  │  │  └─ Roll/
│  │  ├─ Characters/
│  │  │  ├─ CharacterBase/
│  │  │  ├─ BodyParts/
│  │  │  ├─ Build/
│  │  │  │  ├─ Augments/
│  │  │  │  └─ Items/
│  │  │  ├─ Enemies/
│  │  │  │  ├─ Base/
│  │  │  │  ├─ EliteEnemy/
│  │  │  │  └─ NormalEnemy/
│  │  │  └─ Olaf/
│  │  │     ├─ Items/
│  │  │     └─ Mechanics/
│  │  ├─ Skills/
│  │  ├─ Status/
│  │  ├─ Systems/
│  │  │  ├─ AI/
│  │  │  ├─ BattleFlow/
│  │  │  ├─ Resolution/
│  │  │  └─ Stats/
│  │  ├─ Presentation/
│  │  │  ├─ UI/
│  │  │  ├─ Camera/
│  │  │  └─ Visual/
│  │  ├─ Diagnostics/
│  │  │  ├─ Debug/
│  │  │  └─ Logging/
│  │  └─ Shared/
│  └─ Editor/
│     ├─ Tools/
│     └─ Visual/
└─ 2. Data/
   └─ Characters/
      └─ Olaf/
         └─ Olaf Status Data.asset";

    private sealed class MoveOperation
    {
        public string Source { get; }
        public string Destination { get; }
        public string Description { get; }

        public MoveOperation(
            string source,
            string destination,
            string description)
        {
            Source = Normalize(source);
            Destination = Normalize(destination);
            Description = description;
        }
    }

    private enum MoveState
    {
        Ready,
        AlreadyMoved,
        SourceMissing,
        Conflict
    }
}

#endif
