#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project Abyss의 AI 분석용 Exporter를 한 번에 실행하고,
/// AllDataTXT 결과를 업로드 가능한 분할 ZIP으로 정리합니다.
/// 사용자가 요청한 모든 프로젝트 범위 Exporter를 순서대로 실행합니다.
/// </summary>
public static class ProjectAbyssExportAllForAI
{
    private const string MenuPath =
        "Tools/Project Abyss/Exports/Export Everything + Build Upload ZIPs";

    private const string OutputFolderName =
        "AllDataTXT/UploadPackages";

    private const long MaximumPackageInputBytes =
        450L * 1024L * 1024L;

    [MenuItem(MenuPath, false, 2090)]
    public static void ExportEverything()
    {
        string projectRoot = Path.GetFullPath(
            Path.Combine(Application.dataPath, ".."));
        string allDataRoot = Path.Combine(projectRoot, "AllDataTXT");
        string packageRoot = Path.Combine(projectRoot, OutputFolderName);

        try
        {
            AssetDatabase.SaveAssets();

            using (ProjectAbyssExportSession.BeginBatch())
            {
                ProjectAbyssScriptExporter.ExportAllScriptsForAI();
                ProjectAbyssHierarchyExporter.ExportCurrentHierarchyForAI();
                ProjectAbyssDataAssetExporter.ExportCoreDataAssetsForAI();
                ProjectAbyssDataAssetExporter.ExportModelAssetsForAI();
                ProjectAbyssDataAssetExporter.ExportAllDataAssetsForAI();
                ProjectAbyssVFXExporter.ExportVFXContextForAI();
            }

            IReadOnlyList<string> packages =
                BuildUploadPackages(allDataRoot, packageRoot);

            string message =
                $"모든 Exporter 실행과 분할 압축을 완료했습니다.\n\n" +
                $"Package Count: {packages.Count}\n" +
                $"Target Input Limit: {FormatBytes(MaximumPackageInputBytes)}\n\n" +
                string.Join("\n", packages.Select(Normalize));

            Debug.Log("[ProjectAbyssExportAllForAI]\n" + message);
            EditorUtility.DisplayDialog(
                "Project Abyss Export Complete",
                message,
                "확인");

            if (packages.Count > 0)
                EditorUtility.RevealInFinder(packages[0]);
            else
                EditorUtility.RevealInFinder(packageRoot);
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("[ProjectAbyssExportAllForAI] Export cancelled.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog(
                "Project Abyss Export Failed",
                exception.Message,
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static IReadOnlyList<string> BuildUploadPackages(
        string allDataRoot,
        string packageRoot)
    {
        if (!Directory.Exists(allDataRoot))
            throw new DirectoryNotFoundException(allDataRoot);

        RecreateDirectory(packageRoot);

        List<ExportFile> files = Directory
            .EnumerateFiles(allDataRoot, "*", SearchOption.AllDirectories)
            .Where(path => ShouldInclude(path, packageRoot))
            .Select(path => new ExportFile
            {
                AbsolutePath = Path.GetFullPath(path),
                RelativePath = Normalize(
                    MakeRelativePath(allDataRoot, path)),
                Size = new FileInfo(path).Length
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToList();

        List<List<ExportFile>> groups = new();
        List<ExportFile> current = new();
        long currentBytes = 0L;

        foreach (ExportFile file in files)
        {
            if (current.Count > 0 &&
                currentBytes + file.Size > MaximumPackageInputBytes)
            {
                groups.Add(current);
                current = new List<ExportFile>();
                currentBytes = 0L;
            }

            current.Add(file);
            currentBytes += file.Size;
        }

        if (current.Count > 0)
            groups.Add(current);

        ExportPackageManifest manifest = new()
        {
            formatVersion = "1.0",
            generatedAtLocal = DateTimeOffset.Now.ToString("O"),
            unityVersion = Application.unityVersion,
            sourceRoot = Normalize(allDataRoot),
            targetInputBytes = MaximumPackageInputBytes,
            fileCount = files.Count
        };

        for (int i = 0; i < groups.Count; i++)
        {
            List<ExportFile> group = groups[i];
            manifest.packages.Add(new ExportPackageRecord
            {
                index = i + 1,
                fileName = $"Project_Abyss_AI_Export_Part_{i + 1:000}.zip",
                inputBytes = group.Sum(file => file.Size),
                files = group.Select(file => file.RelativePath).ToList()
            });
        }

        string manifestJson = JsonUtility.ToJson(manifest, true);
        string manifestPath = Path.Combine(
            packageRoot,
            "Project_Abyss_AI_Export_Package_Manifest.json");
        File.WriteAllText(
            manifestPath,
            manifestJson,
            new UTF8Encoding(false));

        List<string> packagePaths = new();
        for (int i = 0; i < groups.Count; i++)
        {
            ExportPackageRecord record = manifest.packages[i];
            string zipPath = Path.Combine(packageRoot, record.fileName);

            EditorUtility.DisplayProgressBar(
                "Project Abyss Upload Package",
                $"[{i + 1}/{groups.Count}] {record.fileName}",
                groups.Count == 0 ? 1f : (float)i / groups.Count);

            using FileStream stream = new(
                zipPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None);
            using ZipArchive archive = new(
                stream,
                ZipArchiveMode.Create);

            foreach (ExportFile file in groups[i])
            {
                archive.CreateEntryFromFile(
                    file.AbsolutePath,
                    file.RelativePath,
                    System.IO.Compression.CompressionLevel.Optimal);
            }

            ZipArchiveEntry manifestEntry = archive.CreateEntry(
                "Project_Abyss_AI_Export_Package_Manifest.json",
                System.IO.Compression.CompressionLevel.Optimal);
            using StreamWriter writer = new(
                manifestEntry.Open(),
                new UTF8Encoding(false));
            writer.Write(manifestJson);

            packagePaths.Add(zipPath);
        }

        return packagePaths;
    }

    private static bool ShouldInclude(
        string path,
        string packageRoot)
    {
        string full = Path.GetFullPath(path);
        string normalized = Normalize(full);

        if (normalized.StartsWith(
                Normalize(Path.GetFullPath(packageRoot)) + "/",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.IndexOf(
                "/UploadZIP/",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        string extension = Path.GetExtension(full);
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".tmp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static void RecreateDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
    }

    private static string MakeRelativePath(
        string root,
        string path)
    {
        string rootWithSeparator =
            Path.GetFullPath(root)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        Uri rootUri = new(rootWithSeparator);
        Uri pathUri = new(Path.GetFullPath(path));

        return Uri.UnescapeDataString(
            rootUri.MakeRelativeUri(pathUri).ToString());
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private static string FormatBytes(long bytes)
    {
        const double MiB = 1024d * 1024d;
        return $"{bytes / MiB:0.00} MiB";
    }

    [Serializable]
    private sealed class ExportPackageManifest
    {
        public string formatVersion;
        public string generatedAtLocal;
        public string unityVersion;
        public string sourceRoot;
        public long targetInputBytes;
        public int fileCount;
        public List<ExportPackageRecord> packages = new();
    }

    [Serializable]
    private sealed class ExportPackageRecord
    {
        public int index;
        public string fileName;
        public long inputBytes;
        public List<string> files = new();
    }

    private sealed class ExportFile
    {
        public string AbsolutePath;
        public string RelativePath;
        public long Size;
    }
}
#endif
