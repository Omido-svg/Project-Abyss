#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Project Abyss의 프로젝트 스크립트와 프로젝트가 직접 소유하는 UPM 패키지 소스를
/// AI 분석용 단일 텍스트로 내보냅니다.
///
/// 포함 범위:
/// - Assets/1. Scripts 아래의 소스 파일
/// - Packages/manifest.json
/// - Packages/packages-lock.json
/// - manifest.json의 file: 로 연결된 Local UPM Package
/// - Packages/ 아래에 직접 배치된 Embedded UPM Package
///
/// 의도적으로 제외:
/// - Library/PackageCache
/// - Unity Registry / Git / Built-in 패키지의 캐시 소스
/// - 바이너리 에셋
/// </summary>
public static class ProjectAbyssScriptExporter
{
    private const string SourceFolder =
        "Assets/1. Scripts";

    private const string PackagesFolder =
        "Packages";

    private const string ManifestRelativePath =
        "Packages/manifest.json";

    private const string LockRelativePath =
        "Packages/packages-lock.json";

    private const string OutputFolderName =
        "AllDataTXT/Scripts";

    private const string DefaultFileName =
        "Project_Abyss_All_Scripts_For_AI.txt";

    private const string MenuPath =
        "Tools/Project Abyss/Exports/Export All Scripts for AI";

    private const string Separator =
        "================================================================================";

    private static readonly HashSet<string> SupportedExtensions =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".json",
            ".asmdef",
            ".asmref",
            ".uxml",
            ".uss",
            ".shader",
            ".hlsl",
            ".compute",
            ".cginc",
            ".md",
            ".txt"
        };

    private static readonly string[] ExcludedPackageDirectoryNames =
    {
        ".git",
        ".svn",
        ".hg",
        "Library",
        "Temp",
        "Obj",
        "Logs",
        "node_modules"
    };

    [MenuItem(MenuPath, priority = 2000)]
    public static void ExportAllScriptsForAI()
    {
        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."));

        string absoluteSourceFolder =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    SourceFolder));

        if (!Directory.Exists(absoluteSourceFolder))
        {
            EditorUtility.DisplayDialog(
                "Script Export Failed",
                $"스크립트 폴더를 찾을 수 없습니다.\n\n{absoluteSourceFolder}",
                "확인");

            return;
        }

        string outputDirectory =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    OutputFolderName));

        string outputPath =
            Path.GetFullPath(
                Path.Combine(
                    outputDirectory,
                    DefaultFileName));

        string temporaryPath =
            outputPath + ".tmp";

        try
        {
            Export(
                projectRoot,
                absoluteSourceFolder,
                outputPath,
                temporaryPath);
        }
        catch (Exception exception)
        {
            DeleteFileIfExists(
                temporaryPath);

            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Script Export Failed",
                $"스크립트 내보내기에 실패했습니다.\n\n{exception.Message}",
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void Export(
        string projectRoot,
        string absoluteSourceFolder,
        string outputPath,
        string temporaryPath)
    {
        List<string> discoveryWarnings =
            new List<string>();

        List<PackageExportInfo> packages =
            DiscoverProjectOwnedPackages(
                projectRoot,
                discoveryWarnings);

        List<SourceFileInfo> files =
            CollectExportFiles(
                projectRoot,
                absoluteSourceFolder,
                packages,
                discoveryWarnings);

        if (files.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Script Export",
                "내보낼 프로젝트/패키지 소스 파일을 찾지 못했습니다.",
                "확인");

            return;
        }

        List<string> readErrors =
            new List<string>();

        StringBuilder output =
            new StringBuilder(
                CalculateInitialCapacity(files));

        AppendDocumentHeader(
            output,
            projectRoot,
            files,
            packages,
            discoveryWarnings);

        AppendPackageCatalog(
            output,
            packages);

        AppendTableOfContents(
            output,
            files);

        for (int i = 0;
             i < files.Count;
             i++)
        {
            SourceFileInfo file =
                files[i];

            float progress =
                files.Count == 0
                    ? 1f
                    : (float)i / files.Count;

            bool cancelled =
                EditorUtility.DisplayCancelableProgressBar(
                    "AI 분석용 프로젝트/패키지 소스 내보내기",
                    $"[{i + 1}/{files.Count}] {file.AssetPath}",
                    progress);

            if (cancelled)
            {
                EditorUtility.DisplayDialog(
                    "Script Export Cancelled",
                    "스크립트 내보내기를 취소했습니다.",
                    "확인");

                return;
            }

            AppendFileSection(
                output,
                file,
                i + 1,
                files.Count,
                readErrors);
        }

        AppendDocumentFooter(
            output,
            files,
            packages,
            readErrors,
            discoveryWarnings);

        string outputDirectory =
            Path.GetDirectoryName(outputPath);

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new DirectoryNotFoundException(
                $"출력 폴더를 결정할 수 없습니다: {outputPath}");
        }

        Directory.CreateDirectory(
            outputDirectory);

        DeleteFileIfExists(
            temporaryPath);

        File.WriteAllText(
            temporaryPath,
            output.ToString(),
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false));

        ReplaceOutputFile(
            temporaryPath,
            outputPath);

        int localPackageCount =
            packages.Count(
                item => item.Kind == PackageKind.Local);

        int embeddedPackageCount =
            packages.Count(
                item => item.Kind == PackageKind.Embedded);

        int packageFileCount =
            files.Count(
                item => item.Origin == SourceOrigin.LocalPackage ||
                        item.Origin == SourceOrigin.EmbeddedPackage);

        string normalizedOutputPath =
            NormalizePath(outputPath);

        Debug.Log(
            $"[ProjectAbyssScriptExporter] Export complete\n" +
            $"Files             : {files.Count}\n" +
            $"Package Files     : {packageFileCount}\n" +
            $"Local Packages    : {localPackageCount}\n" +
            $"Embedded Packages : {embeddedPackageCount}\n" +
            $"Discovery Warnings: {discoveryWarnings.Count}\n" +
            $"Read Errors       : {readErrors.Count}\n" +
            $"Output            : {normalizedOutputPath}");

        string resultMessage =
            $"총 {files.Count}개의 프로젝트/패키지 소스 파일을 내보냈습니다.\n" +
            $"Local Packages: {localPackageCount}\n" +
            $"Embedded Packages: {embeddedPackageCount}\n" +
            $"Package Source Files: {packageFileCount}";

        if (discoveryWarnings.Count > 0)
        {
            resultMessage +=
                $"\nDiscovery Warnings: {discoveryWarnings.Count}";
        }

        if (readErrors.Count > 0)
        {
            resultMessage +=
                $"\nRead Errors: {readErrors.Count}\n" +
                "출력 파일 마지막의 EXPORT_SUMMARY를 확인하세요.";
        }

        if (!ProjectAbyssExportSession.IsBatch)
        {
            EditorUtility.DisplayDialog(
                "Script Export Complete",
                $"{resultMessage}\n\n{normalizedOutputPath}",
                "확인");

            EditorUtility.RevealInFinder(
                outputPath);
        }
    }

    private static List<SourceFileInfo> CollectExportFiles(
        string projectRoot,
        string absoluteSourceFolder,
        IReadOnlyList<PackageExportInfo> packages,
        List<string> warnings)
    {
        List<SourceFileInfo> result =
            new List<SourceFileInfo>();

        AddDirectoryFiles(
            result,
            absoluteSourceFolder,
            absoluteSourceFolder,
            SourceOrigin.ProjectSource,
            package: null,
            virtualRoot: SourceFolder,
            warnings);

        AddProjectMetadataFile(
            result,
            projectRoot,
            ManifestRelativePath);

        AddProjectMetadataFile(
            result,
            projectRoot,
            LockRelativePath);

        foreach (PackageExportInfo package in packages)
        {
            if (!package.IsResolved)
                continue;

            SourceOrigin origin =
                package.Kind == PackageKind.Embedded
                    ? SourceOrigin.EmbeddedPackage
                    : SourceOrigin.LocalPackage;

            string virtualRoot =
                $"Packages/{package.Name}";

            AddDirectoryFiles(
                result,
                package.ResolvedPath,
                package.ResolvedPath,
                origin,
                package,
                virtualRoot,
                warnings);
        }

        return result
            .GroupBy(
                item => item.AssetPath,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group => group.First())
            .OrderBy(
                item => item.AssetPath,
                StringComparer.Ordinal)
            .ToList();
    }

    private static void AddDirectoryFiles(
        List<SourceFileInfo> result,
        string searchRoot,
        string relativeRoot,
        SourceOrigin origin,
        PackageExportInfo package,
        string virtualRoot,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(searchRoot) ||
            !Directory.Exists(searchRoot))
        {
            return;
        }

        try
        {
            IEnumerable<string> files =
                Directory
                    .EnumerateFiles(
                        searchRoot,
                        "*",
                        SearchOption.AllDirectories)
                    .Where(IsSupportedSourceFile)
                    .Where(
                        path =>
                            !IsExcludedPackagePath(
                                relativeRoot,
                                path));

            foreach (string absolutePath in files)
            {
                string normalizedAbsolutePath =
                    NormalizePath(
                        Path.GetFullPath(
                            absolutePath));

                string relativePath =
                    MakeRelativePath(
                        relativeRoot,
                        normalizedAbsolutePath);

                string assetPath =
                    NormalizePath(
                        virtualRoot.TrimEnd('/', '\\') +
                        "/" +
                        relativePath.TrimStart('/', '\\'));

                result.Add(
                    new SourceFileInfo(
                        normalizedAbsolutePath,
                        assetPath,
                        origin,
                        package));
            }
        }
        catch (Exception exception)
        {
            warnings.Add(
                $"SOURCE_ENUMERATION_FAILED | {NormalizePath(searchRoot)} | " +
                $"{exception.GetType().Name}: {SanitizeSingleLine(exception.Message)}");
        }
    }

    private static void AddProjectMetadataFile(
        List<SourceFileInfo> result,
        string projectRoot,
        string relativePath)
    {
        string absolutePath =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    relativePath));

        if (!File.Exists(absolutePath))
            return;

        result.Add(
            new SourceFileInfo(
                NormalizePath(absolutePath),
                NormalizePath(relativePath),
                SourceOrigin.ProjectMetadata,
                package: null));
    }

    private static bool IsSupportedSourceFile(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return SupportedExtensions.Contains(
            Path.GetExtension(path));
    }

    private static bool IsExcludedPackagePath(
        string root,
        string path)
    {
        string relative;

        try
        {
            relative =
                MakeRelativePath(
                    root,
                    path);
        }
        catch
        {
            return false;
        }

        string[] segments =
            NormalizePath(relative)
                .Split(
                    new[] { '/' },
                    StringSplitOptions.RemoveEmptyEntries);

        foreach (string segment in segments)
        {
            for (int i = 0;
                 i < ExcludedPackageDirectoryNames.Length;
                 i++)
            {
                if (string.Equals(
                        segment,
                        ExcludedPackageDirectoryNames[i],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<PackageExportInfo> DiscoverProjectOwnedPackages(
        string projectRoot,
        List<string> warnings)
    {
        List<PackageExportInfo> result =
            new List<PackageExportInfo>();

        string manifestPath =
            Path.Combine(
                projectRoot,
                ManifestRelativePath);

        if (File.Exists(manifestPath))
        {
            string manifestText =
                ReadTextFile(manifestPath);

            foreach (PackageReference packageReference
                     in ParseFilePackageReferences(manifestText))
            {
                string resolvedPath =
                    ResolveLocalPackagePath(
                        projectRoot,
                        Path.GetDirectoryName(manifestPath),
                        packageReference.Reference);

                PackageExportInfo package =
                    CreatePackageInfo(
                        packageReference.Name,
                        PackageKind.Local,
                        packageReference.Reference,
                        resolvedPath);

                result.Add(package);

                if (!package.IsResolved)
                {
                    warnings.Add(
                        $"LOCAL_PACKAGE_NOT_FOUND | " +
                        $"name={packageReference.Name} | " +
                        $"reference={packageReference.Reference}");
                }
            }
        }
        else
        {
            warnings.Add(
                $"MANIFEST_NOT_FOUND | {NormalizePath(manifestPath)}");
        }

        string embeddedRoot =
            Path.Combine(
                projectRoot,
                PackagesFolder);

        if (Directory.Exists(embeddedRoot))
        {
            foreach (string directory
                     in Directory.EnumerateDirectories(
                         embeddedRoot,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                string packageJson =
                    Path.Combine(
                        directory,
                        "package.json");

                if (!File.Exists(packageJson))
                    continue;

                PackageExportInfo package =
                    CreatePackageInfo(
                        fallbackName: Path.GetFileName(directory),
                        kind: PackageKind.Embedded,
                        reference: "embedded",
                        resolvedPath: directory);

                result.Add(package);
            }
        }

        // Local/Embedded 패키지 자체가 또 다른 file: 패키지에 의존하는 경우도 추적한다.
        DiscoverTransitiveLocalPackages(
            projectRoot,
            result,
            warnings);

        return result
            .GroupBy(
                item =>
                    $"{item.Name}|{NormalizePath(item.ResolvedPath)}",
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group => group.First())
            .OrderBy(
                item => item.Name,
                StringComparer.Ordinal)
            .ThenBy(
                item => item.Kind)
            .ToList();
    }

    private static void DiscoverTransitiveLocalPackages(
        string projectRoot,
        List<PackageExportInfo> packages,
        List<string> warnings)
    {
        Queue<PackageExportInfo> queue =
            new Queue<PackageExportInfo>(
                packages.Where(item => item.IsResolved));

        HashSet<string> visitedRoots =
            new HashSet<string>(
                packages
                    .Where(item => item.IsResolved)
                    .Select(item => NormalizePath(item.ResolvedPath)),
                StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            PackageExportInfo parent =
                queue.Dequeue();

            string packageJsonPath =
                Path.Combine(
                    parent.ResolvedPath,
                    "package.json");

            if (!File.Exists(packageJsonPath))
                continue;

            string json;

            try
            {
                json =
                    ReadTextFile(packageJsonPath);
            }
            catch (Exception exception)
            {
                warnings.Add(
                    $"PACKAGE_JSON_READ_FAILED | {NormalizePath(packageJsonPath)} | " +
                    $"{exception.GetType().Name}: {SanitizeSingleLine(exception.Message)}");

                continue;
            }

            foreach (PackageReference dependency
                     in ParseFilePackageReferences(json))
            {
                string resolvedPath =
                    ResolveLocalPackagePath(
                        projectRoot,
                        parent.ResolvedPath,
                        dependency.Reference);

                PackageExportInfo package =
                    CreatePackageInfo(
                        dependency.Name,
                        PackageKind.Local,
                        dependency.Reference,
                        resolvedPath);

                if (!package.IsResolved)
                {
                    warnings.Add(
                        $"TRANSITIVE_LOCAL_PACKAGE_NOT_FOUND | " +
                        $"parent={parent.Name} | " +
                        $"name={dependency.Name} | " +
                        $"reference={dependency.Reference}");

                    continue;
                }

                string normalizedRoot =
                    NormalizePath(package.ResolvedPath);

                if (!visitedRoots.Add(normalizedRoot))
                    continue;

                packages.Add(package);
                queue.Enqueue(package);
            }
        }
    }

    private static IReadOnlyList<PackageReference> ParseFilePackageReferences(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<PackageReference>();

        MatchCollection matches =
            Regex.Matches(
                json,
                "\"(?<name>[^\"]+)\"\\s*:\\s*\"(?<reference>file:[^\"]+)\"",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

        List<PackageReference> result =
            new List<PackageReference>(
                matches.Count);

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            string name =
                UnescapeJsonString(
                    match.Groups["name"].Value);

            string reference =
                UnescapeJsonString(
                    match.Groups["reference"].Value);

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            result.Add(
                new PackageReference(
                    name,
                    reference));
        }

        return result;
    }

    private static string ResolveLocalPackagePath(
        string projectRoot,
        string declaringDirectory,
        string packageReference)
    {
        if (string.IsNullOrWhiteSpace(packageReference) ||
            !packageReference.StartsWith(
                "file:",
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        string referencePath =
            packageReference.Substring(
                "file:".Length);

        try
        {
            referencePath =
                Uri.UnescapeDataString(
                    referencePath);
        }
        catch
        {
            // 잘못된 URI escape가 있어도 원문 경로로 계속 시도한다.
        }

        if (Uri.TryCreate(
                packageReference,
                UriKind.Absolute,
                out Uri fileUri) &&
            fileUri.IsFile)
        {
            string uriLocalPath =
                fileUri.LocalPath;

            if (Directory.Exists(uriLocalPath))
            {
                return NormalizePath(
                    Path.GetFullPath(
                        uriLocalPath));
            }
        }

        if (Path.IsPathRooted(referencePath))
        {
            if (Directory.Exists(referencePath))
            {
                return NormalizePath(
                    Path.GetFullPath(
                        referencePath));
            }

            return string.Empty;
        }

        List<string> candidates =
            new List<string>();

        // Unity Local Package의 일반적인 기준은 프로젝트 Root다.
        AddCandidate(
            candidates,
            projectRoot,
            referencePath);

        // package.json에서 발견한 transitive file: 의 경우 선언 패키지 Root 기준도 지원한다.
        AddCandidate(
            candidates,
            declaringDirectory,
            referencePath);

        // 일부 수동 manifest 구성의 호환성을 위해 Packages 폴더 기준도 마지막에 시도한다.
        AddCandidate(
            candidates,
            Path.Combine(projectRoot, PackagesFolder),
            referencePath);

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return NormalizePath(
                    Path.GetFullPath(
                        candidate));
            }
        }

        return string.Empty;
    }

    private static void AddCandidate(
        List<string> candidates,
        string basePath,
        string relativePath)
    {
        if (string.IsNullOrWhiteSpace(basePath) ||
            string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            string candidate =
                Path.GetFullPath(
                    Path.Combine(
                        basePath,
                        relativePath));

            if (!candidates.Contains(
                    candidate,
                    StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(candidate);
            }
        }
        catch
        {
            // 다른 기준 경로를 계속 시도한다.
        }
    }

    private static PackageExportInfo CreatePackageInfo(
        string fallbackName,
        PackageKind kind,
        string reference,
        string resolvedPath)
    {
        string name =
            fallbackName ?? string.Empty;

        string version =
            string.Empty;

        if (!string.IsNullOrWhiteSpace(resolvedPath) &&
            Directory.Exists(resolvedPath))
        {
            string packageJsonPath =
                Path.Combine(
                    resolvedPath,
                    "package.json");

            if (File.Exists(packageJsonPath))
            {
                try
                {
                    string json =
                        ReadTextFile(
                            packageJsonPath);

                    string jsonName =
                        ReadJsonStringProperty(
                            json,
                            "name");

                    string jsonVersion =
                        ReadJsonStringProperty(
                            json,
                            "version");

                    if (!string.IsNullOrWhiteSpace(jsonName))
                        name = jsonName;

                    if (!string.IsNullOrWhiteSpace(jsonVersion))
                        version = jsonVersion;
                }
                catch
                {
                    // package.json 메타데이터 실패는 소스 export 자체를 막지 않는다.
                }
            }
        }

        return new PackageExportInfo(
            kind,
            name,
            version,
            reference,
            resolvedPath);
    }

    private static string ReadJsonStringProperty(
        string json,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json) ||
            string.IsNullOrWhiteSpace(propertyName))
        {
            return string.Empty;
        }

        Match match =
            Regex.Match(
                json,
                "\"" + Regex.Escape(propertyName) +
                "\"\\s*:\\s*\"(?<value>[^\"]*)\"",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant);

        return match.Success
            ? UnescapeJsonString(
                match.Groups["value"].Value)
            : string.Empty;
    }

    private static string UnescapeJsonString(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        StringBuilder builder =
            new StringBuilder(
                value.Length);

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char current =
                value[i];

            if (current != '\\' ||
                i + 1 >= value.Length)
            {
                builder.Append(current);
                continue;
            }

            char escaped =
                value[++i];

            switch (escaped)
            {
                case '"':
                    builder.Append('"');
                    break;

                case '\\':
                    builder.Append('\\');
                    break;

                case '/':
                    builder.Append('/');
                    break;

                case 'b':
                    builder.Append('\b');
                    break;

                case 'f':
                    builder.Append('\f');
                    break;

                case 'n':
                    builder.Append('\n');
                    break;

                case 'r':
                    builder.Append('\r');
                    break;

                case 't':
                    builder.Append('\t');
                    break;

                case 'u':
                    if (i + 4 < value.Length)
                    {
                        string hex =
                            value.Substring(
                                i + 1,
                                4);

                        if (ushort.TryParse(
                                hex,
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out ushort unicode))
                        {
                            builder.Append(
                                (char)unicode);

                            i += 4;
                            break;
                        }
                    }

                    builder.Append("\\u");
                    break;

                default:
                    builder.Append(escaped);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void AppendDocumentHeader(
        StringBuilder output,
        string projectRoot,
        IReadOnlyList<SourceFileInfo> files,
        IReadOnlyList<PackageExportInfo> packages,
        IReadOnlyList<string> discoveryWarnings)
    {
        output.AppendLine(
            "PROJECT_ABYSS_SCRIPT_EXPORT");

        output.AppendLine(
            "FORMAT_VERSION: 2");

        output.AppendLine(
            "PURPOSE: AI_CODE_ANALYSIS_WITH_PROJECT_OWNED_UPM_PACKAGES");

        output.AppendLine(
            $"GENERATED_AT_UTC: {DateTime.UtcNow:O}");

        output.AppendLine(
            $"GENERATED_AT_LOCAL: {DateTimeOffset.Now:O}");

        output.AppendLine(
            $"LOCAL_TIME_ZONE: {TimeZoneInfo.Local.Id}");

        output.AppendLine(
            $"UNITY_PROJECT_ROOT: {NormalizePath(projectRoot)}");

        output.AppendLine(
            $"SOURCE_ROOT: {SourceFolder}");

        output.AppendLine(
            $"SOURCE_FILE_COUNT: {files.Count}");

        output.AppendLine(
            $"PROJECT_SOURCE_FILE_COUNT: {files.Count(item => item.Origin == SourceOrigin.ProjectSource)}");

        output.AppendLine(
            $"PROJECT_METADATA_FILE_COUNT: {files.Count(item => item.Origin == SourceOrigin.ProjectMetadata)}");

        output.AppendLine(
            $"LOCAL_PACKAGE_COUNT: {packages.Count(item => item.Kind == PackageKind.Local)}");

        output.AppendLine(
            $"EMBEDDED_PACKAGE_COUNT: {packages.Count(item => item.Kind == PackageKind.Embedded)}");

        output.AppendLine(
            $"LOCAL_PACKAGE_FILE_COUNT: {files.Count(item => item.Origin == SourceOrigin.LocalPackage)}");

        output.AppendLine(
            $"EMBEDDED_PACKAGE_FILE_COUNT: {files.Count(item => item.Origin == SourceOrigin.EmbeddedPackage)}");

        output.AppendLine(
            $"CSHARP_FILE_COUNT: {files.Count(item => item.Language == "CSharp")}");

        output.AppendLine(
            $"JSON_FILE_COUNT: {files.Count(item => item.Language == "JSON")}");

        output.AppendLine(
            $"DISCOVERY_WARNING_COUNT: {discoveryWarnings.Count}");

        output.AppendLine(
            $"PROJECT_MANIFEST_INCLUDED: {files.Any(item => string.Equals(item.AssetPath, ManifestRelativePath, StringComparison.OrdinalIgnoreCase))}");

        output.AppendLine(
            $"PROJECT_LOCK_INCLUDED: {files.Any(item => string.Equals(item.AssetPath, LockRelativePath, StringComparison.OrdinalIgnoreCase))}");

        output.AppendLine(
            "ENCODING: UTF-8_NO_BOM");

        output.AppendLine(
            "NEWLINE: LF");

        output.AppendLine(
            "SORT_ORDER: FILE_PATH_ORDINAL_ASCENDING");

        output.AppendLine(
            "CONTENT_POLICY: ORIGINAL_TEXT_WITH_NORMALIZED_NEWLINES");

        output.AppendLine(
            "PACKAGE_POLICY: EXPORT_FILE_AND_EMBEDDED_PROJECT_PACKAGES_ONLY; PACKAGECACHE_EXCLUDED");

        output.AppendLine();

        output.AppendLine(
            "AI_READING_GUIDE:");

        output.AppendLine(
            "- 각 파일은 FILE_BEGIN / FILE_END 마커로 완전히 분리되어 있다.");

        output.AppendLine(
            "- FILE_PATH는 Unity에서 보이는 논리 경로다.");

        output.AppendLine(
            "- 외부 Local UPM 패키지도 FILE_PATH를 Packages/<package-name>/... 형태로 기록한다.");

        output.AppendLine(
            "- 실제 Local Package의 물리 경로와 버전은 PACKAGE_CATALOG에서 확인한다.");

        output.AppendLine(
            "- FILE_ORIGIN으로 PROJECT_SOURCE / PROJECT_METADATA / LOCAL_PACKAGE / EMBEDDED_PACKAGE를 구분한다.");

        output.AppendLine(
            "- FILE_GUID는 Unity Asset GUID이며 manifest/lock 등 AssetDatabase 대상이 아니면 빈 값일 수 있다.");

        output.AppendLine(
            "- CONTENT_BEGIN과 CONTENT_END 사이만 실제 원본 텍스트 내용이다.");

        output.AppendLine(
            "- Library/PackageCache의 Unity/Third-party registry package 소스는 의도적으로 포함하지 않는다.");

        output.AppendLine(
            "- 코드 내용의 줄바꿈만 LF로 정규화하며 그 외 내용은 변경하지 않는다.");

        output.AppendLine();

        output.AppendLine(
            Separator);
    }

    private static void AppendPackageCatalog(
        StringBuilder output,
        IReadOnlyList<PackageExportInfo> packages)
    {
        output.AppendLine(
            "PACKAGE_CATALOG_BEGIN");

        if (packages.Count == 0)
        {
            output.AppendLine(
                "(none)");
        }
        else
        {
            for (int i = 0;
                 i < packages.Count;
                 i++)
            {
                PackageExportInfo package =
                    packages[i];

                output.Append(
                    (i + 1).ToString("D4"));

                output.Append(" | ");

                output.Append(
                    $"KIND={package.Kind.ToString().ToUpperInvariant()} | ");

                output.Append(
                    $"NAME={SanitizeSingleLine(package.Name)} | ");

                output.Append(
                    $"VERSION={SanitizeSingleLine(package.Version)} | ");

                output.Append(
                    $"STATUS={(package.IsResolved ? "RESOLVED" : "UNRESOLVED")} | ");

                output.Append(
                    $"REFERENCE={SanitizeSingleLine(package.Reference)} | ");

                output.AppendLine(
                    $"RESOLVED_PATH={NormalizePath(package.ResolvedPath)}");
            }
        }

        output.AppendLine(
            "PACKAGE_CATALOG_END");

        output.AppendLine(
            Separator);

        output.AppendLine();
    }

    private static void AppendTableOfContents(
        StringBuilder output,
        IReadOnlyList<SourceFileInfo> files)
    {
        output.AppendLine(
            "TABLE_OF_CONTENTS_BEGIN");

        for (int i = 0;
             i < files.Count;
             i++)
        {
            SourceFileInfo file =
                files[i];

            output.Append(
                (i + 1).ToString("D4"));

            output.Append(" | ");

            output.Append(
                GetOriginLabel(file.Origin));

            output.Append(" | ");

            output.AppendLine(
                file.AssetPath);
        }

        output.AppendLine(
            "TABLE_OF_CONTENTS_END");

        output.AppendLine(
            Separator);

        output.AppendLine();
    }

    private static void AppendFileSection(
        StringBuilder output,
        SourceFileInfo file,
        int index,
        int totalCount,
        List<string> readErrors)
    {
        output.AppendLine(
            "FILE_BEGIN");

        output.AppendLine(
            $"FILE_INDEX: {index}/{totalCount}");

        output.AppendLine(
            $"FILE_PATH: {file.AssetPath}");

        output.AppendLine(
            $"FILE_NAME: {Path.GetFileName(file.AssetPath)}");

        output.AppendLine(
            $"FILE_ORIGIN: {GetOriginLabel(file.Origin)}");

        if (file.Package != null)
        {
            output.AppendLine(
                $"PACKAGE_NAME: {file.Package.Name}");

            output.AppendLine(
                $"PACKAGE_VERSION: {file.Package.Version}");

            output.AppendLine(
                $"PACKAGE_KIND: {file.Package.Kind.ToString().ToUpperInvariant()}");

            output.AppendLine(
                $"PACKAGE_REFERENCE: {file.Package.Reference}");
        }

        output.AppendLine(
            $"FILE_GUID: {GetAssetGuid(file.AssetPath)}");

        output.AppendLine(
            $"LANGUAGE: {file.Language}");

        try
        {
            string content =
                ReadTextFile(
                    file.AbsolutePath);

            string normalizedContent =
                NormalizeNewlines(content);

            int lineCount =
                CountLines(
                    normalizedContent);

            long utf8ByteCount =
                Encoding.UTF8.GetByteCount(
                    normalizedContent);

            string sha256 =
                CalculateSha256(
                    normalizedContent);

            output.AppendLine(
                $"LINE_COUNT: {lineCount}");

            output.AppendLine(
                $"UTF8_BYTE_COUNT: {utf8ByteCount}");

            output.AppendLine(
                $"SHA256: {sha256}");

            output.AppendLine(
                "READ_STATUS: OK");

            output.AppendLine(
                "CONTENT_BEGIN");

            output.Append(
                normalizedContent);

            if (!normalizedContent.EndsWith(
                    "\n",
                    StringComparison.Ordinal))
            {
                output.AppendLine();
            }

            output.AppendLine(
                "CONTENT_END");
        }
        catch (Exception exception)
        {
            string errorMessage =
                $"{file.AssetPath} | " +
                $"{exception.GetType().Name}: " +
                $"{exception.Message}";

            readErrors.Add(
                errorMessage);

            output.AppendLine(
                "LINE_COUNT: 0");

            output.AppendLine(
                "UTF8_BYTE_COUNT: 0");

            output.AppendLine(
                "SHA256:");

            output.AppendLine(
                "READ_STATUS: ERROR");

            output.AppendLine(
                $"READ_ERROR: {SanitizeSingleLine(exception.Message)}");

            output.AppendLine(
                "CONTENT_BEGIN");

            output.AppendLine(
                "// FILE_READ_FAILED");

            output.AppendLine(
                "CONTENT_END");
        }

        output.AppendLine(
            "FILE_END");

        output.AppendLine(
            Separator);

        output.AppendLine();
    }

    private static void AppendDocumentFooter(
        StringBuilder output,
        IReadOnlyList<SourceFileInfo> files,
        IReadOnlyList<PackageExportInfo> packages,
        IReadOnlyList<string> readErrors,
        IReadOnlyList<string> discoveryWarnings)
    {
        output.AppendLine(
            "EXPORT_SUMMARY_BEGIN");

        output.AppendLine(
            $"SOURCE_FILE_COUNT: {files.Count}");

        output.AppendLine(
            $"PROJECT_SOURCE_FILE_COUNT: {files.Count(item => item.Origin == SourceOrigin.ProjectSource)}");

        output.AppendLine(
            $"PACKAGE_SOURCE_FILE_COUNT: {files.Count(item => item.Origin == SourceOrigin.LocalPackage || item.Origin == SourceOrigin.EmbeddedPackage)}");

        output.AppendLine(
            $"LOCAL_PACKAGE_COUNT: {packages.Count(item => item.Kind == PackageKind.Local)}");

        output.AppendLine(
            $"EMBEDDED_PACKAGE_COUNT: {packages.Count(item => item.Kind == PackageKind.Embedded)}");

        output.AppendLine(
            $"DISCOVERY_WARNING_COUNT: {discoveryWarnings.Count}");

        output.AppendLine(
            $"READ_ERROR_COUNT: {readErrors.Count}");

        if (discoveryWarnings.Count > 0)
        {
            output.AppendLine(
                "DISCOVERY_WARNINGS_BEGIN");

            for (int i = 0;
                 i < discoveryWarnings.Count;
                 i++)
            {
                output.Append(
                    (i + 1).ToString("D4"));

                output.Append(" | ");

                output.AppendLine(
                    discoveryWarnings[i]);
            }

            output.AppendLine(
                "DISCOVERY_WARNINGS_END");
        }

        if (readErrors.Count > 0)
        {
            output.AppendLine(
                "READ_ERRORS_BEGIN");

            for (int i = 0;
                 i < readErrors.Count;
                 i++)
            {
                output.Append(
                    (i + 1).ToString("D4"));

                output.Append(" | ");

                output.AppendLine(
                    readErrors[i]);
            }

            output.AppendLine(
                "READ_ERRORS_END");
        }

        output.AppendLine(
            "EXPORT_SUMMARY_END");

        output.AppendLine(
            "PROJECT_ABYSS_SCRIPT_EXPORT_END");
    }

    private static string GetOriginLabel(
        SourceOrigin origin)
    {
        switch (origin)
        {
            case SourceOrigin.ProjectSource:
                return "PROJECT_SOURCE";

            case SourceOrigin.ProjectMetadata:
                return "PROJECT_METADATA";

            case SourceOrigin.LocalPackage:
                return "LOCAL_PACKAGE";

            case SourceOrigin.EmbeddedPackage:
                return "EMBEDDED_PACKAGE";

            default:
                return origin.ToString().ToUpperInvariant();
        }
    }

    private static string GetLanguage(
        string path)
    {
        string extension =
            Path.GetExtension(path);

        if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
            return "CSharp";

        if (string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
            return "JSON";

        if (string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase))
            return "AssemblyDefinition";

        if (string.Equals(extension, ".asmref", StringComparison.OrdinalIgnoreCase))
            return "AssemblyReference";

        if (string.Equals(extension, ".uxml", StringComparison.OrdinalIgnoreCase))
            return "UXML";

        if (string.Equals(extension, ".uss", StringComparison.OrdinalIgnoreCase))
            return "USS";

        if (string.Equals(extension, ".shader", StringComparison.OrdinalIgnoreCase))
            return "Shader";

        if (string.Equals(extension, ".hlsl", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".cginc", StringComparison.OrdinalIgnoreCase))
        {
            return "HLSL";
        }

        if (string.Equals(extension, ".compute", StringComparison.OrdinalIgnoreCase))
            return "ComputeShader";

        return "Text";
    }

    private static string ReadTextFile(
        string path)
    {
        byte[] bytes =
            File.ReadAllBytes(
                path);

        if (HasUtf8Bom(bytes))
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: true,
                    throwOnInvalidBytes: true)
                .GetString(
                    bytes,
                    3,
                    bytes.Length - 3);
        }

        try
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(
                    bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding koreanEncoding =
                Encoding.GetEncoding(949);

            return koreanEncoding.GetString(
                bytes);
        }
    }

    private static bool HasUtf8Bom(
        IReadOnlyList<byte> bytes)
    {
        return
            bytes != null &&
            bytes.Count >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF;
    }

    private static string NormalizeNewlines(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace(
                "\r\n",
                "\n")
            .Replace(
                "\r",
                "\n");
    }

    private static int CountLines(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int lineCount = 1;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            if (value[i] == '\n')
                lineCount++;
        }

        if (value.EndsWith(
                "\n",
                StringComparison.Ordinal))
        {
            lineCount--;
        }

        return Math.Max(
            0,
            lineCount);
    }

    private static string CalculateSha256(
        string value)
    {
        byte[] bytes =
            Encoding.UTF8.GetBytes(
                value ?? string.Empty);

        using (SHA256 sha256 =
               SHA256.Create())
        {
            byte[] hash =
                sha256.ComputeHash(
                    bytes);

            StringBuilder builder =
                new StringBuilder(
                    hash.Length * 2);

            foreach (byte item in hash)
            {
                builder.Append(
                    item.ToString("x2"));
            }

            return builder.ToString();
        }
    }

    private static string GetAssetGuid(
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return string.Empty;

        return AssetDatabase.AssetPathToGUID(
            assetPath) ??
            string.Empty;
    }

    private static string MakeRelativePath(
        string basePath,
        string fullPath)
    {
        Uri baseUri =
            new Uri(
                AppendDirectorySeparator(
                    NormalizePath(
                        Path.GetFullPath(
                            basePath))));

        Uri fullUri =
            new Uri(
                NormalizePath(
                    Path.GetFullPath(
                        fullPath)));

        string relative =
            Uri.UnescapeDataString(
                baseUri.MakeRelativeUri(
                        fullUri)
                    .ToString());

        return NormalizePath(
            relative);
    }

    private static string AppendDirectorySeparator(
        string path)
    {
        if (string.IsNullOrEmpty(path))
            return "/";

        return path.EndsWith(
                "/",
                StringComparison.Ordinal)
            ? path
            : path + "/";
    }

    private static string NormalizePath(
        string path)
    {
        return string.IsNullOrEmpty(path)
            ? string.Empty
            : path.Replace(
                '\\',
                '/');
    }

    private static string SanitizeSingleLine(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace(
                "\r",
                " ")
            .Replace(
                "\n",
                " ");
    }

    private static int CalculateInitialCapacity(
        IReadOnlyList<SourceFileInfo> files)
    {
        long totalLength = 0;

        foreach (SourceFileInfo file in files)
        {
            try
            {
                totalLength +=
                    new FileInfo(
                        file.AbsolutePath)
                        .Length;
            }
            catch
            {
                // 용량 추정 실패는 무시한다.
            }
        }

        long estimated =
            totalLength +
            files.Count * 768L +
            16 * 1024L;

        return (int)Math.Min(
            int.MaxValue,
            Math.Max(
                16 * 1024L,
                estimated));
    }

    private static void ReplaceOutputFile(
        string temporaryPath,
        string outputPath)
    {
        if (!File.Exists(temporaryPath))
        {
            throw new FileNotFoundException(
                "임시 출력 파일을 찾을 수 없습니다.",
                temporaryPath);
        }

        DeleteFileIfExists(
            outputPath);

        File.Move(
            temporaryPath,
            outputPath);
    }

    private static void DeleteFileIfExists(
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (File.Exists(path))
            File.Delete(path);
    }

    private enum SourceOrigin
    {
        ProjectSource = 0,
        ProjectMetadata = 1,
        LocalPackage = 2,
        EmbeddedPackage = 3
    }

    private enum PackageKind
    {
        Local = 0,
        Embedded = 1
    }

    private sealed class PackageReference
    {
        public string Name { get; }
        public string Reference { get; }

        public PackageReference(
            string name,
            string reference)
        {
            Name =
                name;

            Reference =
                reference;
        }
    }

    private sealed class PackageExportInfo
    {
        public PackageKind Kind { get; }
        public string Name { get; }
        public string Version { get; }
        public string Reference { get; }
        public string ResolvedPath { get; }

        public bool IsResolved =>
            !string.IsNullOrWhiteSpace(ResolvedPath) &&
            Directory.Exists(ResolvedPath);

        public PackageExportInfo(
            PackageKind kind,
            string name,
            string version,
            string reference,
            string resolvedPath)
        {
            Kind =
                kind;

            Name =
                string.IsNullOrWhiteSpace(name)
                    ? "unknown-package"
                    : name;

            Version =
                version ?? string.Empty;

            Reference =
                reference ?? string.Empty;

            ResolvedPath =
                resolvedPath ?? string.Empty;
        }
    }

    private sealed class SourceFileInfo
    {
        public string AbsolutePath { get; }
        public string AssetPath { get; }
        public string Language { get; }
        public SourceOrigin Origin { get; }
        public PackageExportInfo Package { get; }

        public SourceFileInfo(
            string absolutePath,
            string assetPath,
            SourceOrigin origin,
            PackageExportInfo package)
        {
            AbsolutePath =
                absolutePath;

            AssetPath =
                assetPath;

            Origin =
                origin;

            Package =
                package;

            Language =
                GetLanguage(
                    assetPath);
        }
    }
}

#endif
