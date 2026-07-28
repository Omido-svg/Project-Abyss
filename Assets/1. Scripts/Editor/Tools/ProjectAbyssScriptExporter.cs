#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProjectAbyssScriptExporter
{
    private const string SourceFolder =
        "Assets/1. Scripts";

    private const string OutputFolderName =
        "AllDataTXT/Scripts";

    private const string DefaultFileName =
        "Project_Abyss_All_Scripts_For_AI.txt";

    private const string MenuPath =
        "Tools/Project Abyss/Export All Scripts for AI";

    private const string Separator =
        "================================================================================";

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
        List<ScriptFileInfo> scripts =
            FindScripts(
                projectRoot,
                absoluteSourceFolder);

        if (scripts.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Script Export",
                $"{SourceFolder} 아래에서 C# 스크립트를 찾지 못했습니다.",
                "확인");

            return;
        }

        List<string> readErrors =
            new List<string>();

        StringBuilder output =
            new StringBuilder(
                CalculateInitialCapacity(scripts));

        AppendDocumentHeader(
            output,
            projectRoot,
            scripts);

        AppendTableOfContents(
            output,
            scripts);

        for (int i = 0;
             i < scripts.Count;
             i++)
        {
            ScriptFileInfo script =
                scripts[i];

            float progress =
                scripts.Count == 0
                    ? 1f
                    : (float)i / scripts.Count;

            bool cancelled =
                EditorUtility.DisplayCancelableProgressBar(
                    "AI 분석용 스크립트 내보내기",
                    $"[{i + 1}/{scripts.Count}] {script.AssetPath}",
                    progress);

            if (cancelled)
            {
                EditorUtility.DisplayDialog(
                    "Script Export Cancelled",
                    "스크립트 내보내기를 취소했습니다.",
                    "확인");

                return;
            }

            AppendScriptSection(
                output,
                script,
                i + 1,
                scripts.Count,
                readErrors);
        }

        AppendDocumentFooter(
            output,
            scripts.Count,
            readErrors);

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

        string normalizedOutputPath =
            NormalizePath(outputPath);

        Debug.Log(
            $"[ProjectAbyssScriptExporter] Export complete\n" +
            $"Scripts : {scripts.Count}\n" +
            $"Errors  : {readErrors.Count}\n" +
            $"Output  : {normalizedOutputPath}");

        string resultMessage =
            readErrors.Count == 0
                ? $"총 {scripts.Count}개의 C# 스크립트를 내보냈습니다."
                : $"총 {scripts.Count}개 중 {readErrors.Count}개 파일을 읽지 못했습니다.\n" +
                  "출력 파일 마지막의 READ_ERRORS 항목을 확인하세요.";

        EditorUtility.DisplayDialog(
            "Script Export Complete",
            $"{resultMessage}\n\n{normalizedOutputPath}",
            "확인");

        EditorUtility.RevealInFinder(
            outputPath);
    }

    private static List<ScriptFileInfo> FindScripts(
        string projectRoot,
        string absoluteSourceFolder)
    {
        string[] files =
            Directory.GetFiles(
                absoluteSourceFolder,
                "*.cs",
                SearchOption.AllDirectories);

        List<ScriptFileInfo> result =
            new List<ScriptFileInfo>(
                files.Length);

        foreach (string absolutePath in files)
        {
            string normalizedAbsolutePath =
                NormalizePath(
                    Path.GetFullPath(
                        absolutePath));

            string assetPath =
                MakeRelativePath(
                    projectRoot,
                    normalizedAbsolutePath);

            result.Add(
                new ScriptFileInfo(
                    normalizedAbsolutePath,
                    assetPath));
        }

        return result
            .OrderBy(
                script => script.AssetPath,
                StringComparer.Ordinal)
            .ToList();
    }

    private static void AppendDocumentHeader(
        StringBuilder output,
        string projectRoot,
        IReadOnlyList<ScriptFileInfo> scripts)
    {
        output.AppendLine(
            "PROJECT_ABYSS_SCRIPT_EXPORT");

        output.AppendLine(
            "FORMAT_VERSION: 1");

        output.AppendLine(
            "PURPOSE: AI_CODE_ANALYSIS");

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
            $"SCRIPT_COUNT: {scripts.Count}");

        output.AppendLine(
            "ENCODING: UTF-8_NO_BOM");

        output.AppendLine(
            "NEWLINE: LF");

        output.AppendLine(
            "SORT_ORDER: ASSET_PATH_ORDINAL_ASCENDING");

        output.AppendLine(
            "CONTENT_POLICY: ORIGINAL_SOURCE_WITH_NORMALIZED_NEWLINES");

        output.AppendLine();

        output.AppendLine(
            "AI_READING_GUIDE:");

        output.AppendLine(
            "- 각 파일은 FILE_BEGIN / FILE_END 마커로 완전히 분리되어 있다.");

        output.AppendLine(
            "- FILE_PATH가 Unity 프로젝트 내부의 실제 상대 경로다.");

        output.AppendLine(
            "- FILE_GUID는 Unity Asset GUID이며 빈 값일 수 있다.");

        output.AppendLine(
            "- CONTENT_BEGIN과 CONTENT_END 사이만 실제 C# 원본 코드다.");

        output.AppendLine(
            "- 파일 간 동일 클래스명이나 참조 관계를 분석할 때 FILE_PATH를 기준으로 구분한다.");

        output.AppendLine(
            "- 코드 내용의 줄바꿈만 LF로 정규화하며 그 외 내용은 변경하지 않는다.");

        output.AppendLine();

        output.AppendLine(
            Separator);
    }

    private static void AppendTableOfContents(
        StringBuilder output,
        IReadOnlyList<ScriptFileInfo> scripts)
    {
        output.AppendLine(
            "TABLE_OF_CONTENTS_BEGIN");

        for (int i = 0;
             i < scripts.Count;
             i++)
        {
            ScriptFileInfo script =
                scripts[i];

            output.Append(
                (i + 1).ToString("D4"));

            output.Append(" | ");
            output.AppendLine(
                script.AssetPath);
        }

        output.AppendLine(
            "TABLE_OF_CONTENTS_END");

        output.AppendLine(
            Separator);

        output.AppendLine();
    }

    private static void AppendScriptSection(
        StringBuilder output,
        ScriptFileInfo script,
        int index,
        int totalCount,
        List<string> readErrors)
    {
        output.AppendLine(
            "FILE_BEGIN");

        output.AppendLine(
            $"FILE_INDEX: {index}/{totalCount}");

        output.AppendLine(
            $"FILE_PATH: {script.AssetPath}");

        output.AppendLine(
            $"FILE_NAME: {Path.GetFileName(script.AssetPath)}");

        output.AppendLine(
            $"FILE_GUID: {GetAssetGuid(script.AssetPath)}");

        output.AppendLine(
            "LANGUAGE: CSharp");

        try
        {
            string content =
                ReadTextFile(
                    script.AbsolutePath);

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
                $"{script.AssetPath} | " +
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
        int scriptCount,
        IReadOnlyList<string> readErrors)
    {
        output.AppendLine(
            "EXPORT_SUMMARY_BEGIN");

        output.AppendLine(
            $"SCRIPT_COUNT: {scriptCount}");

        output.AppendLine(
            $"READ_ERROR_COUNT: {readErrors.Count}");

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
        IReadOnlyList<ScriptFileInfo> scripts)
    {
        long totalLength = 0;

        foreach (ScriptFileInfo script in scripts)
        {
            try
            {
                totalLength +=
                    new FileInfo(
                        script.AbsolutePath)
                        .Length;
            }
            catch
            {
                // 용량 추정 실패는 무시한다.
            }
        }

        long estimated =
            totalLength +
            scripts.Count * 512L +
            8192L;

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

    private sealed class ScriptFileInfo
    {
        public string AbsolutePath { get; }
        public string AssetPath { get; }

        public ScriptFileInfo(
            string absolutePath,
            string assetPath)
        {
            AbsolutePath =
                absolutePath;

            AssetPath =
                assetPath;
        }
    }
}

#endif
