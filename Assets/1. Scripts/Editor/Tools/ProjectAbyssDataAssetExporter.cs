#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

public static class ProjectAbyssDataAssetExporter
{
    private const string SourceFolder =
        "Assets/2. Data";

    private const string OutputFolder =
        "Assets/AllDataTXT";

    private const string LegacySingleFileName =
        "Project_Abyss_All_Data_Assets_For_AI.txt";

    private const string LegacyManifestFileName =
        "Project_Abyss_All_Data_Manifest_For_AI.txt";

    private const string PartsFolderName =
        "Parts";

    private const string UploadZipFolderName =
        "UploadZIP";

    private const string CoreMenuPath =
        "Tools/Project Abyss/Export Core Data Assets for AI";

    private const string ModelMenuPath =
        "Tools/Project Abyss/Export Model Assets for AI";

    private const string AllMenuPath =
        "Tools/Project Abyss/Export All Data Assets for AI";

    private const string FormatVersion =
        "3.0.0";

    private const string Separator =
        "================================================================================";

    // 각 TXT Part 자체를 400 MiB 이하로 제한한다.
    // ZIP은 원본 TXT보다 커질 수 없으므로 500 MiB 업로드 제한에도 안전하다.
    private const long MaximumPartBytes =
        400L * 1024L * 1024L;

    // Part 종료 마커와 UTF-8 경계 보정을 위한 여유 공간.
    private const long PartFooterReserveBytes =
        1L * 1024L * 1024L;

    private const long MinimumContinuationPayloadBytes =
        64L * 1024L;

    private const int CopyBufferBytes =
        1024 * 1024;

    private static readonly CultureInfo Invariant =
        CultureInfo.InvariantCulture;

    private static readonly HashSet<string>
        RawTextExtensions =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".asset",
                ".anim",
                ".controller",
                ".overrideController",
                ".mat",
                ".prefab",
                ".playable",
                ".shader",
                ".compute",
                ".hlsl",
                ".cginc",
                ".json",
                ".xml",
                ".txt",
                ".csv",
                ".uss",
                ".uxml",
                ".asmdef",
                ".asmref",
                ".shadergraph",
                ".shadersubgraph",
                ".vfx",
                ".vfxoperator",
                ".vfxblock",
                ".inputactions"
            };

    [MenuItem(CoreMenuPath, priority = 2002)]
    public static void ExportCoreDataAssetsForAI()
    {
        RunExport(
            ExportProfile.CreateCore());
    }

    [MenuItem(ModelMenuPath, priority = 2003)]
    public static void ExportModelAssetsForAI()
    {
        RunExport(
            ExportProfile.CreateModels());
    }

    [MenuItem(AllMenuPath, priority = 2004)]
    public static void ExportAllDataAssetsForAI()
    {
        RunExport(
            ExportProfile.CreateAll());
    }

    private static void RunExport(
        ExportProfile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        if (!AssetDatabase.IsValidFolder(SourceFolder))
        {
            EditorUtility.DisplayDialog(
                "Data Asset Export Failed",
                $"데이터 폴더를 찾을 수 없습니다.\n\n{SourceFolder}",
                "확인");

            return;
        }

        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."));

        string outputDirectory =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    profile.RelativeOutputFolder));

        string partsDirectory =
            Path.Combine(
                outputDirectory,
                PartsFolderName);

        string uploadZipDirectory =
            Path.Combine(
                outputDirectory,
                UploadZipFolderName);

        string manifestPath =
            Path.Combine(
                outputDirectory,
                profile.ManifestFileName);

        string temporaryDirectory =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    "Library",
                    "ProjectAbyssDataExport",
                    profile.Mode.ToString()));

        try
        {
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(
                outputDirectory);

            RecreateDirectory(
                partsDirectory);

            RecreateDirectory(
                uploadZipDirectory);

            RecreateDirectory(
                temporaryDirectory);

            CleanupLegacyRootExport(
                projectRoot);

            ExportSummary summary =
                Export(
                    projectRoot,
                    partsDirectory,
                    uploadZipDirectory,
                    temporaryDirectory,
                    manifestPath,
                    profile);

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceUpdate);

            TextAsset manifest =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"{profile.RelativeOutputFolder}/{profile.ManifestFileName}");

            if (manifest != null)
            {
                Selection.activeObject =
                    manifest;

                EditorGUIUtility.PingObject(
                    manifest);
            }

            EditorUtility.DisplayDialog(
                $"{profile.DisplayName} Export Complete",
                $"AI 분석용 데이터 에셋 내보내기가 완료되었습니다.\n\n" +
                $"Mode: {profile.Mode}\n" +
                $"Manifest: {profile.RelativeOutputFolder}/{profile.ManifestFileName}\n" +
                $"상세 에셋: {summary.DetailAssetCount}개\n" +
                $"TXT Part: {summary.PartCount}개\n" +
                $"ZIP Part: {summary.PartCount}개\n" +
                $"최대 TXT 크기: {FormatBytes(MaximumPartBytes)}",
                "확인");

            EditorUtility.RevealInFinder(
                manifestPath);
        }
        catch (OperationCanceledException)
        {
            RecreateDirectory(
                partsDirectory);

            RecreateDirectory(
                uploadZipDirectory);

            DeleteFileIfExists(
                manifestPath);

            DeleteFileIfExists(
                manifestPath + ".meta");

            EditorUtility.DisplayDialog(
                $"{profile.DisplayName} Export Cancelled",
                "데이터 에셋 내보내기를 취소했습니다.\n" +
                "완성되지 않은 Part 파일은 제거했습니다.",
                "확인");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);

            EditorUtility.DisplayDialog(
                $"{profile.DisplayName} Export Failed",
                $"데이터 에셋 내보내기에 실패했습니다.\n\n{exception.Message}",
                "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void CleanupLegacyRootExport(
        string projectRoot)
    {
        string legacyRoot =
            Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    OutputFolder));

        DeleteDirectoryIfExists(
            Path.Combine(
                legacyRoot,
                PartsFolderName));

        DeleteDirectoryIfExists(
            Path.Combine(
                legacyRoot,
                UploadZipFolderName));

        DeleteFileIfExists(
            Path.Combine(
                legacyRoot,
                PartsFolderName + ".meta"));

        DeleteFileIfExists(
            Path.Combine(
                legacyRoot,
                UploadZipFolderName + ".meta"));

        string[] legacyFiles =
        {
            LegacySingleFileName,
            LegacyManifestFileName,
            LegacyManifestFileName + ".zip"
        };

        foreach (string legacyFile
                 in legacyFiles)
        {
            DeleteFileIfExists(
                Path.Combine(
                    legacyRoot,
                    legacyFile));

            DeleteFileIfExists(
                Path.Combine(
                    legacyRoot,
                    legacyFile + ".meta"));
        }
    }

    private static ExportSummary Export(
        string projectRoot,
        string partsDirectory,
        string uploadZipDirectory,
        string temporaryDirectory,
        string manifestPath,
        ExportProfile profile)
    {
        List<DataAssetInfo> assets =
            CollectDataAssets(
                projectRoot);

        if (assets.Count == 0)
        {
            throw new InvalidOperationException(
                $"{SourceFolder} 아래에서 내보낼 에셋을 찾지 못했습니다.");
        }

        List<DataAssetInfo> detailAssets =
            assets
                .Where(
                    profile.ShouldExportDetail)
                .ToList();

        if (detailAssets.Count == 0)
        {
            throw new InvalidOperationException(
                $"{profile.DisplayName} 모드에서 상세 출력할 에셋을 찾지 못했습니다.");
        }

        ExportContext context =
            new ExportContext(
                projectRoot,
                assets,
                detailAssets,
                profile);

        BuildDependencyGraph(
            context);

        List<ExportError> errors =
            new List<ExportError>();

        List<PartInfo> parts =
            new List<PartInfo>();

        Dictionary<string, AssetPlacementInfo>
            placements =
                detailAssets.ToDictionary(
                    asset => asset.AssetPath,
                    asset =>
                        new AssetPlacementInfo(
                            asset),
                    StringComparer.Ordinal);

        string assetTemporaryDirectory =
            Path.Combine(
                temporaryDirectory,
                "AssetDetails");

        RecreateDirectory(
            assetTemporaryDirectory);

        PartOutput currentPart =
            null;

        int nextPartIndex =
            1;

        try
        {
            for (int i = 0;
                 i < detailAssets.Count;
                 i++)
            {
                DataAssetInfo asset =
                    detailAssets[i];

                bool cancelled =
                    EditorUtility.DisplayCancelableProgressBar(
                        "AI 분석용 데이터 에셋 내보내기",
                        $"{profile.DisplayName} 상세 정보 " +
                        $"[{i + 1}/{detailAssets.Count}] {asset.AssetPath}",
                        detailAssets.Count == 0
                            ? 1f
                            : (float)i / detailAssets.Count);

                if (cancelled)
                    throw new OperationCanceledException();

                string assetTemporaryPath =
                    Path.Combine(
                        assetTemporaryDirectory,
                        $"Asset_{asset.Index:D6}.txt.tmp");

                try
                {
                    using (StreamWriter assetWriter =
                           CreateUtf8Writer(
                               assetTemporaryPath))
                    {
                        try
                        {
                            WriteAssetDetail(
                                assetWriter,
                                context,
                                asset);
                        }
                        catch (Exception exception)
                        {
                            errors.Add(
                                new ExportError(
                                    asset.AssetPath,
                                    exception));

                            WriteAssetError(
                                assetWriter,
                                asset,
                                exception);
                        }
                    }

                    long assetDetailBytes =
                        new FileInfo(
                            assetTemporaryPath)
                        .Length;

                    AssetPlacementInfo placement =
                        placements[asset.AssetPath];

                    if (assetDetailBytes <=
                        MaximumPartBytes -
                        PartFooterReserveBytes)
                    {
                        if (currentPart == null)
                        {
                            currentPart =
                                CreatePartOutput(
                                    partsDirectory,
                                    nextPartIndex,
                                    context,
                                    asset);

                            nextPartIndex++;
                        }

                        if (!currentPart.CanFit(
                                assetDetailBytes) &&
                            currentPart.AssetCount > 0)
                        {
                            FinalizePart(
                                ref currentPart,
                                parts,
                                uploadZipDirectory,
                                context);

                            currentPart =
                                CreatePartOutput(
                                    partsDirectory,
                                    nextPartIndex,
                                    context,
                                    asset);

                            nextPartIndex++;
                        }

                        if (!currentPart.CanFit(
                                assetDetailBytes))
                        {
                            AppendOversizedAsset(
                                assetTemporaryPath,
                                asset,
                                placement,
                                ref currentPart,
                                ref nextPartIndex,
                                partsDirectory,
                                uploadZipDirectory,
                                context,
                                parts);
                        }
                        else
                        {
                            currentPart.AddAsset(
                                asset,
                                placement);

                            currentPart.AppendFile(
                                assetTemporaryPath);
                        }
                    }
                    else
                    {
                        if (currentPart != null &&
                            currentPart.AssetCount > 0)
                        {
                            FinalizePart(
                                ref currentPart,
                                parts,
                                uploadZipDirectory,
                                context);
                        }

                        AppendOversizedAsset(
                            assetTemporaryPath,
                            asset,
                            placement,
                            ref currentPart,
                            ref nextPartIndex,
                            partsDirectory,
                            uploadZipDirectory,
                            context,
                            parts);
                    }
                }
                finally
                {
                    DeleteFileIfExists(
                        assetTemporaryPath);
                }

                if (profile.SplitEachDetailAsset &&
                    currentPart != null)
                {
                    FinalizePart(
                        ref currentPart,
                        parts,
                        uploadZipDirectory,
                        context);
                }
            }

            FinalizePart(
                ref currentPart,
                parts,
                uploadZipDirectory,
                context);

            string manifestTemporaryPath =
                Path.Combine(
                    temporaryDirectory,
                    profile.ManifestFileName + ".tmp");

            using (StreamWriter writer =
                   CreateUtf8Writer(
                       manifestTemporaryPath))
            {
                WriteDocumentHeader(
                    writer,
                    context);

                writer.WriteLine(
                    "DOCUMENT_ROLE: MANIFEST");

                writer.WriteLine(
                    $"PART_MAX_BYTES: {MaximumPartBytes}");

                writer.WriteLine(
                    $"PART_MAX_HUMAN_READABLE: {FormatBytes(MaximumPartBytes)}");

                writer.WriteLine(
                    $"PARTS_FOLDER: {profile.RelativeOutputFolder}/{PartsFolderName}");

                writer.WriteLine(
                    $"UPLOAD_ZIP_FOLDER: {profile.RelativeOutputFolder}/{UploadZipFolderName}");

                writer.WriteLine();

                WriteAiReadingGuide(
                    writer,
                    context);

                WriteExportScopeSummary(
                    writer,
                    context);

                WriteStatistics(
                    writer,
                    context);

                WriteFolderTree(
                    writer,
                    context);

                WriteAssetIndex(
                    writer,
                    context);

                WriteReferenceGraph(
                    writer,
                    context);

                if (profile.Mode == DataExportMode.Core)
                {
                    WriteLightweightModelSummary(
                        writer,
                        context);
                }

                WritePartManifest(
                    writer,
                    parts,
                    context);

                WriteAssetPlacementManifest(
                    writer,
                    placements.Values);

                WriteErrorSummary(
                    writer,
                    errors);

                WriteDocumentFooter(
                    writer,
                    context,
                    errors);
            }

            ReplaceFileAtomically(
                manifestTemporaryPath,
                manifestPath);

            string manifestZipPath =
                Path.Combine(
                    uploadZipDirectory,
                    profile.ManifestZipFileName);

            CreateZipForFile(
                manifestPath,
                manifestZipPath,
                profile.ManifestFileName);

            return
                new ExportSummary(
                    detailAssets.Count,
                    parts.Count,
                    parts.Sum(
                        part => part.TextBytes),
                    parts.Sum(
                        part => part.ZipBytes));
        }
        finally
        {
            currentPart?.Dispose();

            DeleteDirectoryIfExists(
                assetTemporaryDirectory);
        }
    }

    private static StreamWriter CreateUtf8Writer(
        string path)
    {
        StreamWriter writer =
            new StreamWriter(
                path,
                false,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                bufferSize: 64 * 1024);

        writer.NewLine =
            "\n";

        return writer;
    }

    private static PartOutput CreatePartOutput(
        string partsDirectory,
        int partIndex,
        ExportContext context,
        DataAssetInfo currentAsset)
    {
        string partFileName =
            context.Profile.GetPartFileName(
                partIndex,
                currentAsset);

        string partPath =
            Path.Combine(
                partsDirectory,
                partFileName);

        return
            new PartOutput(
                partIndex,
                partFileName,
                partPath,
                context);
    }

    private static void FinalizePart(
        ref PartOutput currentPart,
        List<PartInfo> parts,
        string uploadZipDirectory,
        ExportContext context)
    {
        if (currentPart == null)
            return;

        PartInfo info =
            currentPart.Complete(
                context);

        currentPart =
            null;

        if (info.TextBytes >
            MaximumPartBytes)
        {
            throw new InvalidOperationException(
                $"Part TXT 크기 제한을 초과했습니다. " +
                $"Part={info.PartIndex}, " +
                $"Size={FormatBytes(info.TextBytes)}, " +
                $"Limit={FormatBytes(MaximumPartBytes)}");
        }

        string zipFileName =
            Path.GetFileNameWithoutExtension(
                info.TextFileName) +
            ".zip";

        string zipPath =
            Path.Combine(
                uploadZipDirectory,
                zipFileName);

        CreateZipForFile(
            info.TextAbsolutePath,
            zipPath,
            info.TextFileName);

        info.ZipFileName =
            zipFileName;

        info.ZipAbsolutePath =
            zipPath;

        info.ZipBytes =
            new FileInfo(
                zipPath)
            .Length;

        info.ZipSha256 =
            ComputeSha256(
                zipPath);

        if (info.ZipBytes >
            500L * 1024L * 1024L)
        {
            throw new InvalidOperationException(
                $"ZIP 크기가 500 MiB를 초과했습니다. " +
                $"Part={info.PartIndex}, " +
                $"Size={FormatBytes(info.ZipBytes)}");
        }

        parts.Add(
            info);
    }

    private static void AppendOversizedAsset(
        string assetTemporaryPath,
        DataAssetInfo asset,
        AssetPlacementInfo placement,
        ref PartOutput currentPart,
        ref int nextPartIndex,
        string partsDirectory,
        string uploadZipDirectory,
        ExportContext context,
        List<PartInfo> parts)
    {
        using (FileStream source =
               new FileStream(
                   assetTemporaryPath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   bufferSize: CopyBufferBytes,
                   options: FileOptions.SequentialScan))
        {
            bool firstFragment =
                true;

            while (source.Position <
                   source.Length)
            {
                if (currentPart == null)
                {
                    currentPart =
                        CreatePartOutput(
                            partsDirectory,
                            nextPartIndex,
                            context,
                            asset);

                    nextPartIndex++;
                }

                if (currentPart.RemainingPayloadBytes <
                    MinimumContinuationPayloadBytes)
                {
                    FinalizePart(
                        ref currentPart,
                        parts,
                        uploadZipDirectory,
                        context);

                    continue;
                }

                currentPart.AddAsset(
                    asset,
                    placement);

                if (!firstFragment)
                {
                    currentPart.WriteLine(
                        "ASSET_CONTINUATION_FROM_PREVIOUS_PART_BEGIN");

                    currentPart.WriteLine(
                        $"ASSET_PATH: {asset.AssetPath}");

                    currentPart.WriteLine(
                        $"SOURCE_BYTE_OFFSET: {source.Position}");

                    currentPart.WriteLine(
                        "ASSET_CONTINUATION_FROM_PREVIOUS_PART_END");

                    currentPart.WriteLine();
                }

                long availableBytes =
                    currentPart.RemainingPayloadBytes -
                    16L * 1024L;

                if (availableBytes <= 0)
                {
                    FinalizePart(
                        ref currentPart,
                        parts,
                        uploadZipDirectory,
                        context);

                    continue;
                }

                long copiedBytes =
                    CopyUtf8SafeBytes(
                        source,
                        currentPart,
                        availableBytes);

                if (copiedBytes <= 0)
                {
                    throw new InvalidOperationException(
                        $"UTF-8 안전 분할에 실패했습니다. Asset={asset.AssetPath}");
                }

                bool hasMore =
                    source.Position <
                    source.Length;

                if (hasMore)
                {
                    currentPart.WriteLine();

                    currentPart.WriteLine(
                        "ASSET_CONTINUATION_TO_NEXT_PART_BEGIN");

                    currentPart.WriteLine(
                        $"ASSET_PATH: {asset.AssetPath}");

                    currentPart.WriteLine(
                        $"NEXT_SOURCE_BYTE_OFFSET: {source.Position}");

                    currentPart.WriteLine(
                        "ASSET_CONTINUATION_TO_NEXT_PART_END");

                    FinalizePart(
                        ref currentPart,
                        parts,
                        uploadZipDirectory,
                        context);
                }
                else
                {
                    currentPart.WriteLine();

                    if (!firstFragment)
                    {
                        currentPart.WriteLine(
                            "ASSET_CONTINUATION_COMPLETE");
                    }
                }

                firstFragment =
                    false;
            }
        }
    }

    private static long CopyUtf8SafeBytes(
        FileStream source,
        PartOutput destination,
        long maximumBytes)
    {
        destination.FlushText();

        byte[] buffer =
            new byte[CopyBufferBytes];

        long copied =
            0;

        while (copied < maximumBytes &&
               source.Position < source.Length)
        {
            int requested =
                (int)Math.Min(
                    buffer.Length,
                    maximumBytes - copied);

            int read =
                source.Read(
                    buffer,
                    0,
                    requested);

            if (read <= 0)
                break;

            int safeCount =
                read;

            bool reachesBudget =
                copied + read >= maximumBytes;

            bool hasMoreSource =
                source.Position < source.Length;

            if (reachesBudget &&
                hasMoreSource)
            {
                safeCount =
                    FindUtf8SafePrefixLength(
                        buffer,
                        read);
            }

            if (safeCount <= 0)
            {
                source.Position -=
                    read;

                break;
            }

            destination.AppendRawBytes(
                buffer,
                safeCount);

            copied +=
                safeCount;

            if (safeCount < read)
            {
                source.Position -=
                    read - safeCount;

                break;
            }
        }

        return copied;
    }

    private static int FindUtf8SafePrefixLength(
        byte[] buffer,
        int length)
    {
        if (length <= 0)
            return 0;

        int leadIndex =
            length - 1;

        while (leadIndex >= 0 &&
               IsUtf8ContinuationByte(
                   buffer[leadIndex]))
        {
            leadIndex--;
        }

        if (leadIndex < 0)
            return 0;

        int expectedLength =
            GetUtf8SequenceLength(
                buffer[leadIndex]);

        if (expectedLength <= 1)
            return length;

        int actualLength =
            length - leadIndex;

        return
            actualLength < expectedLength
                ? leadIndex
                : length;
    }

    private static bool IsUtf8ContinuationByte(
        byte value)
    {
        return
            (value & 0xC0) ==
            0x80;
    }

    private static int GetUtf8SequenceLength(
        byte leadByte)
    {
        if ((leadByte & 0x80) == 0)
            return 1;

        if ((leadByte & 0xE0) == 0xC0)
            return 2;

        if ((leadByte & 0xF0) == 0xE0)
            return 3;

        if ((leadByte & 0xF8) == 0xF0)
            return 4;

        return 1;
    }

    private static void CreateZipForFile(
        string sourcePath,
        string zipPath,
        string entryName)
    {
        DeleteFileIfExists(
            zipPath);

        using (FileStream zipStream =
               new FileStream(
                   zipPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        using (ZipArchive archive =
               new ZipArchive(
                   zipStream,
                   ZipArchiveMode.Create,
                   leaveOpen: false))
        {
            ZipArchiveEntry entry =
                archive.CreateEntry(
                    entryName,
                    System.IO.Compression.CompressionLevel.Optimal);

            using (Stream entryStream =
                   entry.Open())
            using (FileStream sourceStream =
                   new FileStream(
                       sourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: CopyBufferBytes,
                       options: FileOptions.SequentialScan))
            {
                sourceStream.CopyTo(
                    entryStream,
                    CopyBufferBytes);
            }
        }
    }

    private static void WritePartManifest(
        StreamWriter writer,
        IReadOnlyList<PartInfo> parts,
        ExportContext context)
    {
        writer.WriteLine(
            Separator);

        writer.WriteLine(
            "PART_MANIFEST_BEGIN");

        writer.WriteLine(
            $"PART_COUNT: {parts.Count}");

        writer.WriteLine(
            $"MAXIMUM_PART_BYTES: {MaximumPartBytes}");

        writer.WriteLine(
            $"MAXIMUM_PART_HUMAN_READABLE: {FormatBytes(MaximumPartBytes)}");

        writer.WriteLine(
            $"TOTAL_TEXT_BYTES: {parts.Sum(part => part.TextBytes)}");

        writer.WriteLine(
            $"TOTAL_ZIP_BYTES: {parts.Sum(part => part.ZipBytes)}");

        foreach (PartInfo part
                 in parts.OrderBy(
                     item => item.PartIndex))
        {
            writer.WriteLine(
                "PART_BEGIN");

            writer.WriteLine(
                $"PART_INDEX: {part.PartIndex}");

            writer.WriteLine(
                $"TXT_PATH: {context.Profile.RelativeOutputFolder}/" +
                $"{PartsFolderName}/{part.TextFileName}");

            writer.WriteLine(
                $"TXT_BYTES: {part.TextBytes}");

            writer.WriteLine(
                $"TXT_SIZE: {FormatBytes(part.TextBytes)}");

            writer.WriteLine(
                $"TXT_SHA256: {part.TextSha256}");

            writer.WriteLine(
                $"ZIP_PATH: {context.Profile.RelativeOutputFolder}/" +
                $"{UploadZipFolderName}/{part.ZipFileName}");

            writer.WriteLine(
                $"ZIP_BYTES: {part.ZipBytes}");

            writer.WriteLine(
                $"ZIP_SIZE: {FormatBytes(part.ZipBytes)}");

            writer.WriteLine(
                $"ZIP_SHA256: {part.ZipSha256}");

            writer.WriteLine(
                $"ASSET_COUNT: {part.AssetPaths.Count}");

            writer.WriteLine(
                $"FIRST_ASSET_INDEX: {part.FirstAssetIndex}");

            writer.WriteLine(
                $"LAST_ASSET_INDEX: {part.LastAssetIndex}");

            for (int i = 0;
                 i < part.AssetPaths.Count;
                 i++)
            {
                writer.WriteLine(
                    $"PART_ASSET: " +
                    $"index={i}; " +
                    $"path={Escape(part.AssetPaths[i])}");
            }

            writer.WriteLine(
                "PART_END");

            writer.WriteLine();
        }

        writer.WriteLine(
            "PART_MANIFEST_END");

        writer.WriteLine();
    }

    private static void WriteAssetPlacementManifest(
        StreamWriter writer,
        IEnumerable<AssetPlacementInfo> placements)
    {
        writer.WriteLine(
            Separator);

        writer.WriteLine(
            "ASSET_PART_MAP_BEGIN");

        AssetPlacementInfo[] ordered =
            placements
                .OrderBy(
                    placement => placement.Asset.Index)
                .ToArray();

        writer.WriteLine(
            $"ASSET_PART_MAP_COUNT: {ordered.Length}");

        foreach (AssetPlacementInfo placement
                 in ordered)
        {
            int[] partIndexes =
                placement.PartIndexes
                    .Distinct()
                    .OrderBy(
                        index => index)
                    .ToArray();

            writer.WriteLine(
                $"ASSET_PART_MAP: " +
                $"asset_index={placement.Asset.Index}; " +
                $"asset_path={Escape(placement.Asset.AssetPath)}; " +
                $"part_count={partIndexes.Length}; " +
                $"parts=[{string.Join(", ", partIndexes)}]");
        }

        writer.WriteLine(
            "ASSET_PART_MAP_END");

        writer.WriteLine();
    }

    private static List<DataAssetInfo> CollectDataAssets(
        string projectRoot)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    SourceFolder
                });

        List<string> assetPaths =
            guids
                .Select(
                    AssetDatabase.GUIDToAssetPath)
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(path) &&
                        !AssetDatabase.IsValidFolder(path) &&
                        path.StartsWith(
                            SourceFolder + "/",
                            StringComparison.Ordinal))
                .Distinct(
                    StringComparer.Ordinal)
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToList();

        List<DataAssetInfo> assets =
            new List<DataAssetInfo>(
                assetPaths.Count);

        for (int i = 0;
             i < assetPaths.Count;
             i++)
        {
            string assetPath =
                assetPaths[i];

            bool cancelled =
                EditorUtility.DisplayCancelableProgressBar(
                    "데이터 에셋 목록 구성",
                    $"[{i + 1}/{assetPaths.Count}] {assetPath}",
                    assetPaths.Count == 0
                        ? 1f
                        : (float)i / assetPaths.Count);

            if (cancelled)
                throw new OperationCanceledException();

            string absolutePath =
                Path.GetFullPath(
                    Path.Combine(
                        projectRoot,
                        assetPath));

            Type mainType =
                AssetDatabase.GetMainAssetTypeAtPath(
                    assetPath);

            AssetImporter importer =
                AssetImporter.GetAtPath(
                    assetPath);

            DataAssetInfo info =
                new DataAssetInfo
                {
                    Index =
                        i + 1,
                    AssetPath =
                        assetPath,
                    AbsolutePath =
                        absolutePath,
                    Guid =
                        AssetDatabase.AssetPathToGUID(
                            assetPath),
                    Extension =
                        Path.GetExtension(
                            assetPath),
                    FileName =
                        Path.GetFileName(
                            assetPath),
                    MainType =
                        mainType,
                    MainTypeName =
                        GetTypeName(
                            mainType),
                    ImporterTypeName =
                        importer == null
                            ? "NONE"
                            : GetTypeName(
                                importer.GetType()),
                    Category =
                        ClassifyAsset(
                            assetPath,
                            mainType)
                };

            if (File.Exists(absolutePath))
            {
                FileInfo fileInfo =
                    new FileInfo(
                        absolutePath);

                info.FileSizeBytes =
                    fileInfo.Length;

                info.LastWriteUtc =
                    fileInfo.LastWriteTimeUtc;

                info.Sha256 =
                    ComputeSha256(
                        absolutePath);
            }

            UnityEngine.Object mainObject =
                AssetDatabase.LoadMainAssetAtPath(
                    assetPath);

            if (mainObject != null)
            {
                info.MainObjectName =
                    mainObject.name;

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    mainObject,
                    out string objectGuid,
                    out long localId);

                info.MainObjectGuid =
                    objectGuid;

                info.MainObjectLocalId =
                    localId;

                info.Labels =
                    AssetDatabase.GetLabels(
                        mainObject)
                    ?? Array.Empty<string>();
            }

            assets.Add(
                info);
        }

        return assets;
    }

    private static void BuildDependencyGraph(
        ExportContext context)
    {
        Dictionary<string, DataAssetInfo> byPath =
            context.AssetsByPath;

        for (int i = 0;
             i < context.Assets.Count;
             i++)
        {
            DataAssetInfo asset =
                context.Assets[i];

            bool cancelled =
                EditorUtility.DisplayCancelableProgressBar(
                    "데이터 에셋 의존성 분석",
                    $"출력 의존성 [{i + 1}/{context.Assets.Count}] {asset.AssetPath}",
                    context.Assets.Count == 0
                        ? 1f
                        : (float)i / context.Assets.Count);

            if (cancelled)
                throw new OperationCanceledException();

            asset.DirectDependencies =
                SafeGetDependencies(
                    asset.AssetPath,
                    recursive: false);

            asset.RecursiveDependencies =
                SafeGetDependencies(
                    asset.AssetPath,
                    recursive: true);

            foreach (string dependency
                     in asset.DirectDependencies)
            {
                if (string.Equals(
                        dependency,
                        asset.AssetPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!byPath.TryGetValue(
                        dependency,
                        out DataAssetInfo target))
                {
                    continue;
                }

                target.InternalIncomingReferences.Add(
                    asset.AssetPath);
            }
        }

        string[] projectAssetPaths =
            AssetDatabase.GetAllAssetPaths()
                .Where(
                    path =>
                        path.StartsWith(
                            "Assets/",
                            StringComparison.Ordinal) &&
                        !path.StartsWith(
                            OutputFolder + "/",
                            StringComparison.Ordinal) &&
                        !AssetDatabase.IsValidFolder(path))
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToArray();

        for (int i = 0;
             i < projectAssetPaths.Length;
             i++)
        {
            string projectAssetPath =
                projectAssetPaths[i];

            bool cancelled =
                EditorUtility.DisplayCancelableProgressBar(
                    "프로젝트 전체 역참조 분석",
                    $"[{i + 1}/{projectAssetPaths.Length}] {projectAssetPath}",
                    projectAssetPaths.Length == 0
                        ? 1f
                        : (float)i / projectAssetPaths.Length);

            if (cancelled)
                throw new OperationCanceledException();

            string[] dependencies =
                SafeGetDependencies(
                    projectAssetPath,
                    recursive: false);

            foreach (string dependency
                     in dependencies)
            {
                if (!byPath.TryGetValue(
                        dependency,
                        out DataAssetInfo target))
                {
                    continue;
                }

                if (string.Equals(
                        projectAssetPath,
                        dependency,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                target.ProjectIncomingReferences.Add(
                    projectAssetPath);
            }
        }

        foreach (DataAssetInfo asset
                 in context.Assets)
        {
            asset.InternalIncomingReferences.Sort(
                StringComparer.Ordinal);

            asset.ProjectIncomingReferences.Sort(
                StringComparer.Ordinal);
        }
    }

    private static string[] SafeGetDependencies(
        string assetPath,
        bool recursive)
    {
        try
        {
            return AssetDatabase.GetDependencies(
                    assetPath,
                    recursive)
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(path))
                .Distinct(
                    StringComparer.Ordinal)
                .OrderBy(
                    path => path,
                    StringComparer.Ordinal)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void WriteDocumentHeader(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "PROJECT_ABYSS_DATA_ASSET_EXPORT");

        writer.WriteLine(
            $"FORMAT_VERSION: {FormatVersion}");

        writer.WriteLine(
            $"EXPORTED_AT_UTC: {DateTime.UtcNow:O}");

        writer.WriteLine(
            $"UNITY_VERSION: {Application.unityVersion}");

        writer.WriteLine(
            $"PROJECT_ROOT: {NormalizePath(context.ProjectRoot)}");

        writer.WriteLine(
            $"SOURCE_FOLDER: {SourceFolder}");

        writer.WriteLine(
            $"OUTPUT_FOLDER: {context.Profile.RelativeOutputFolder}");

        writer.WriteLine(
            $"EXPORT_MODE: {context.Profile.Mode}");

        writer.WriteLine(
            $"ASSET_COUNT: {context.Assets.Count}");

        writer.WriteLine(
            $"DETAIL_ASSET_COUNT: {context.DetailAssets.Count}");

        writer.WriteLine(
            $"WHOLE_PROJECT_REVERSE_REFERENCE_SCAN: true");

        writer.WriteLine(
            "EMBEDDED_RAW_TEXT_SIZE_LIMIT: NONE");

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteAiReadingGuide(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "AI_READING_GUIDE_BEGIN");

        writer.WriteLine(
            $"- Export Mode: {context.Profile.Mode}");

        writer.WriteLine(
            "- 이 Manifest는 Assets/2. Data의 전체 인덱스와 참조 그래프를 유지한다.");

        writer.WriteLine(
            "- 상세 Part에는 현재 Export Mode가 선택한 에셋만 포함한다.");

        if (context.Profile.Mode == DataExportMode.Core)
        {
            writer.WriteLine(
                "- Core 모드는 모델 원본의 무거운 Mesh/Bone/Avatar 상세를 제외한다.");

            writer.WriteLine(
                "- Core Manifest의 MODEL_REFERENCE_SUMMARY에는 모델 식별자, 연결 관계, Sub-Asset 목록, Importer .meta 원문이 유지된다.");
        }
        else if (context.Profile.Mode == DataExportMode.Models)
        {
            writer.WriteLine(
                "- Models 모드는 FBX/OBJ/DAE/Blend 모델 상세만 모델별 Part로 출력한다.");
        }
        else
        {
            writer.WriteLine(
                "- All 모드는 Core와 Models의 상세를 모두 포함한다.");
        }

        writer.WriteLine(
            "- ASSET_INDEX에서 경로와 타입을 먼저 찾고 ASSET_DETAILS의 같은 ASSET_INDEX로 이동한다.");

        writer.WriteLine(
            "- SERIALIZED_PROPERTIES는 Unity SerializedObject가 노출하는 숨김 프로퍼티까지 포함한다.");

        writer.WriteLine(
            "- OBJECT_REFERENCE 값에는 참조 대상의 경로, GUID, Local File ID, 타입이 포함된다.");

        writer.WriteLine(
            "- DIRECT_DEPENDENCIES는 에셋이 직접 참조하는 대상이다.");

        writer.WriteLine(
            "- RECURSIVE_DEPENDENCIES는 모든 하위 의존성을 포함한다.");

        writer.WriteLine(
            "- PROJECT_INCOMING_REFERENCES는 Assets 전체에서 이 데이터 에셋을 직접 참조하는 에셋이다.");

        writer.WriteLine(
            "- SUB_ASSET에는 FBX 내부 Mesh, AnimationClip, Avatar, Material 등의 Local File ID가 포함된다.");

        writer.WriteLine(
            "- META_RAW_TEXT에는 GUID와 Importer 직렬화 원문이 들어간다.");

        writer.WriteLine(
            "- 텍스트 직렬화 에셋은 SOURCE_RAW_TEXT로 원문을 함께 기록한다.");

        writer.WriteLine(
            "- FBX, 이미지, 오디오 같은 바이너리 원본 바이트는 문서에 직접 삽입하지 않고 구조·Importer·Sub-Asset 정보와 SHA-256으로 표현한다.");

        writer.WriteLine(
            "AI_READING_GUIDE_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteExportScopeSummary(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "EXPORT_SCOPE_BEGIN");

        writer.WriteLine(
            $"EXPORT_MODE: {context.Profile.Mode}");

        writer.WriteLine(
            $"SCANNED_ASSET_COUNT: {context.Assets.Count}");

        writer.WriteLine(
            $"DETAIL_ASSET_COUNT: {context.DetailAssets.Count}");

        writer.WriteLine(
            $"MODEL_ASSET_COUNT: {context.Assets.Count(IsModelAsset)}");

        writer.WriteLine(
            $"MODEL_DETAIL_INCLUDED: " +
            $"{context.Profile.Mode != DataExportMode.Core}");

        writer.WriteLine(
            $"SPLIT_EACH_DETAIL_ASSET: {context.Profile.SplitEachDetailAsset}");

        foreach (DataAssetInfo asset
                 in context.Assets)
        {
            writer.WriteLine(
                $"EXPORT_SCOPE_ASSET: " +
                $"index={asset.Index}; " +
                $"detail_included={context.DetailAssetPaths.Contains(asset.AssetPath)}; " +
                $"model={IsModelAsset(asset)}; " +
                $"path={Escape(asset.AssetPath)}");
        }

        writer.WriteLine(
            "EXPORT_SCOPE_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteLightweightModelSummary(
        StreamWriter writer,
        ExportContext context)
    {
        DataAssetInfo[] models =
            context.Assets
                .Where(
                    IsModelAsset)
                .OrderBy(
                    asset => asset.AssetPath,
                    StringComparer.Ordinal)
                .ToArray();

        writer.WriteLine(
            "MODEL_REFERENCE_SUMMARY_BEGIN");

        writer.WriteLine(
            $"MODEL_COUNT: {models.Length}");

        for (int i = 0;
             i < models.Length;
             i++)
        {
            DataAssetInfo model =
                models[i];

            writer.WriteLine(
                "MODEL_REFERENCE_BEGIN");

            writer.WriteLine(
                $"MODEL_INDEX: {i}");

            writer.WriteLine(
                $"ASSET_INDEX: {model.Index}");

            writer.WriteLine(
                $"ASSET_PATH: {model.AssetPath}");

            writer.WriteLine(
                $"ASSET_GUID: {model.Guid}");

            writer.WriteLine(
                $"MAIN_OBJECT_LOCAL_FILE_ID: {model.MainObjectLocalId}");

            writer.WriteLine(
                $"FILE_SIZE_BYTES: {model.FileSizeBytes}");

            writer.WriteLine(
                $"FILE_SHA256: {model.Sha256}");

            writer.WriteLine(
                $"IMPORTER_TYPE: {model.ImporterTypeName}");

            writer.WriteLine(
                $"ASSET_LABELS: {JoinEscaped(model.Labels)}");

            WriteStringList(
                writer,
                "MODEL_DIRECT_DEPENDENCY",
                model.DirectDependencies);

            WriteStringList(
                writer,
                "MODEL_RECURSIVE_DEPENDENCY",
                model.RecursiveDependencies);

            WriteStringList(
                writer,
                "MODEL_PROJECT_INCOMING_REFERENCE",
                model.ProjectIncomingReferences);

            string modelMetaPath =
                model.AbsolutePath +
                ".meta";

            writer.WriteLine(
                "MODEL_IMPORTER_META_RAW_TEXT_BEGIN");

            if (File.Exists(modelMetaPath))
            {
                writer.Write(
                    NormalizeNewLines(
                        File.ReadAllText(
                            modelMetaPath,
                            Encoding.UTF8)));

                writer.WriteLine();
            }
            else
            {
                writer.WriteLine(
                    "MODEL_IMPORTER_META_STATUS: FILE_NOT_FOUND");
            }

            writer.WriteLine(
                "MODEL_IMPORTER_META_RAW_TEXT_END");

            UnityEngine.Object[] subAssets =
                AssetDatabase.LoadAllAssetsAtPath(
                    model.AssetPath)
                ?? Array.Empty<UnityEngine.Object>();

            writer.WriteLine(
                $"MODEL_SUB_ASSET_COUNT: {subAssets.Length}");

            IEnumerable<IGrouping<string, UnityEngine.Object>>
                typeGroups =
                    subAssets
                        .Where(
                            item => item != null)
                        .GroupBy(
                            item => GetTypeName(item.GetType()))
                        .OrderBy(
                            group => group.Key,
                            StringComparer.Ordinal);

            foreach (IGrouping<string, UnityEngine.Object> group
                     in typeGroups)
            {
                writer.WriteLine(
                    $"MODEL_SUB_ASSET_TYPE_COUNT: " +
                    $"type={Escape(group.Key)}; " +
                    $"count={group.Count()}");
            }

            for (int subIndex = 0;
                 subIndex < subAssets.Length;
                 subIndex++)
            {
                UnityEngine.Object subAsset =
                    subAssets[subIndex];

                if (subAsset == null)
                    continue;

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    subAsset,
                    out string guid,
                    out long localId);

                writer.WriteLine(
                    $"MODEL_SUB_ASSET: " +
                    $"index={subIndex}; " +
                    $"name={Escape(subAsset.name)}; " +
                    $"type={Escape(GetTypeName(subAsset.GetType()))}; " +
                    $"guid={Escape(guid)}; " +
                    $"local_file_id={localId}");
            }

            writer.WriteLine(
                "MODEL_REFERENCE_END");

            writer.WriteLine();
        }

        writer.WriteLine(
            "MODEL_REFERENCE_SUMMARY_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteStatistics(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "STATISTICS_BEGIN");

        writer.WriteLine(
            $"SCANNED_ASSET_COUNT: {context.Assets.Count}");

        writer.WriteLine(
            $"DETAIL_ASSET_COUNT: {context.DetailAssets.Count}");

        writer.WriteLine(
            $"MODEL_ASSET_COUNT: {context.Assets.Count(IsModelAsset)}");

        IEnumerable<IGrouping<string, DataAssetInfo>>
            categoryGroups =
                context.Assets
                    .GroupBy(
                        asset => asset.Category)
                    .OrderBy(
                        group => group.Key,
                        StringComparer.Ordinal);

        foreach (IGrouping<string, DataAssetInfo> group
                 in categoryGroups)
        {
            writer.WriteLine(
                $"CATEGORY_COUNT: category={Escape(group.Key)}; count={group.Count()}");
        }

        IEnumerable<IGrouping<string, DataAssetInfo>>
            extensionGroups =
                context.Assets
                    .GroupBy(
                        asset =>
                            string.IsNullOrEmpty(asset.Extension)
                                ? "<NO_EXTENSION>"
                                : asset.Extension)
                    .OrderBy(
                        group => group.Key,
                        StringComparer.OrdinalIgnoreCase);

        foreach (IGrouping<string, DataAssetInfo> group
                 in extensionGroups)
        {
            writer.WriteLine(
                $"EXTENSION_COUNT: extension={Escape(group.Key)}; count={group.Count()}");
        }

        writer.WriteLine(
            $"TOTAL_FILE_BYTES: {context.Assets.Sum(asset => asset.FileSizeBytes)}");

        writer.WriteLine(
            $"TOTAL_DIRECT_DEPENDENCY_EDGES: " +
            $"{context.Assets.Sum(asset => asset.DirectDependencies.Length)}");

        writer.WriteLine(
            $"TOTAL_PROJECT_INCOMING_REFERENCE_EDGES: " +
            $"{context.Assets.Sum(asset => asset.ProjectIncomingReferences.Count)}");

        writer.WriteLine(
            "STATISTICS_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteFolderTree(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "FOLDER_TREE_BEGIN");

        FolderNode root =
            new FolderNode(
                SourceFolder);

        foreach (DataAssetInfo asset
                 in context.Assets)
        {
            string relative =
                asset.AssetPath.Substring(
                    SourceFolder.Length)
                .TrimStart('/');

            string[] segments =
                relative.Split(
                    new[]
                    {
                        '/'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            root.Add(
                segments,
                asset);
        }

        root.Write(
            writer,
            indent: 0);

        writer.WriteLine(
            "FOLDER_TREE_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteAssetIndex(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "ASSET_INDEX_BEGIN");

        foreach (DataAssetInfo asset
                 in context.Assets)
        {
            writer.WriteLine(
                $"ASSET_INDEX_ENTRY: " +
                $"index={asset.Index}; " +
                $"category={Escape(asset.Category)}; " +
                $"type={Escape(asset.MainTypeName)}; " +
                $"guid={Escape(asset.Guid)}; " +
                $"detail_included={context.DetailAssetPaths.Contains(asset.AssetPath)}; " +
                $"path={Escape(asset.AssetPath)}");
        }

        writer.WriteLine(
            "ASSET_INDEX_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteReferenceGraph(
        StreamWriter writer,
        ExportContext context)
    {
        writer.WriteLine(
            "REFERENCE_GRAPH_BEGIN");

        foreach (DataAssetInfo asset
                 in context.Assets)
        {
            foreach (string dependency
                     in asset.DirectDependencies)
            {
                if (string.Equals(
                        dependency,
                        asset.AssetPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                writer.WriteLine(
                    $"REFERENCE_EDGE: " +
                    $"from={Escape(asset.AssetPath)}; " +
                    $"to={Escape(dependency)}; " +
                    $"target_in_data_folder={IsInSourceFolder(dependency)}");
            }
        }

        writer.WriteLine(
            "REFERENCE_GRAPH_END");

        writer.WriteLine();

        writer.WriteLine(
            Separator);

        writer.WriteLine();
    }

    private static void WriteAssetDetail(
        StreamWriter writer,
        ExportContext context,
        DataAssetInfo asset)
    {
        writer.WriteLine(
            Separator);

        writer.WriteLine(
            "ASSET_BEGIN");

        writer.WriteLine(
            $"ASSET_INDEX: {asset.Index}/{context.Assets.Count}");

        writer.WriteLine(
            $"ASSET_PATH: {asset.AssetPath}");

        writer.WriteLine(
            $"ASSET_NAME: {asset.FileName}");

        writer.WriteLine(
            $"ASSET_CATEGORY: {asset.Category}");

        writer.WriteLine(
            $"ASSET_EXTENSION: {asset.Extension}");

        writer.WriteLine(
            $"ASSET_GUID: {asset.Guid}");

        writer.WriteLine(
            $"MAIN_OBJECT_GUID: {asset.MainObjectGuid}");

        writer.WriteLine(
            $"MAIN_OBJECT_LOCAL_FILE_ID: {asset.MainObjectLocalId}");

        writer.WriteLine(
            $"MAIN_OBJECT_NAME: {asset.MainObjectName}");

        writer.WriteLine(
            $"MAIN_OBJECT_TYPE: {asset.MainTypeName}");

        writer.WriteLine(
            $"IMPORTER_TYPE: {asset.ImporterTypeName}");

        writer.WriteLine(
            $"FILE_SIZE_BYTES: {asset.FileSizeBytes}");

        writer.WriteLine(
            $"FILE_LAST_WRITE_UTC: {asset.LastWriteUtc:O}");

        writer.WriteLine(
            $"FILE_SHA256: {asset.Sha256}");

        writer.WriteLine(
            $"ASSET_LABELS: {JoinEscaped(asset.Labels)}");

        writer.WriteLine();

        WriteDependencies(
            writer,
            asset);

        AssetImporter importer =
            AssetImporter.GetAtPath(
                asset.AssetPath);

        WriteImporter(
            writer,
            importer);

        UnityEngine.Object[] allObjects =
            AssetDatabase.LoadAllAssetsAtPath(
                asset.AssetPath)
            ?? Array.Empty<UnityEngine.Object>();

        WriteSubAssetIndex(
            writer,
            allObjects);

        HashSet<int> writtenInstanceIds =
            new HashSet<int>();

        for (int i = 0;
             i < allObjects.Length;
             i++)
        {
            UnityEngine.Object item =
                allObjects[i];

            if (item == null)
                continue;

            if (!writtenInstanceIds.Add(
                    item.GetInstanceID()))
            {
                continue;
            }

            WriteObjectDetail(
                writer,
                asset,
                item,
                i);
        }

        if (allObjects.Length == 0)
        {
            UnityEngine.Object mainObject =
                AssetDatabase.LoadMainAssetAtPath(
                    asset.AssetPath);

            if (mainObject != null)
            {
                WriteObjectDetail(
                    writer,
                    asset,
                    mainObject,
                    objectIndex: 0);
            }
        }

        WriteMetaRawText(
            writer,
            asset);

        WriteSourceRawText(
            writer,
            asset);

        writer.WriteLine(
            "ASSET_END");

        writer.WriteLine();
    }

    private static void WriteDependencies(
        StreamWriter writer,
        DataAssetInfo asset)
    {
        writer.WriteLine(
            "DEPENDENCIES_BEGIN");

        WriteStringList(
            writer,
            "DIRECT_DEPENDENCY",
            asset.DirectDependencies);

        WriteStringList(
            writer,
            "RECURSIVE_DEPENDENCY",
            asset.RecursiveDependencies);

        WriteStringList(
            writer,
            "INTERNAL_INCOMING_REFERENCE",
            asset.InternalIncomingReferences);

        WriteStringList(
            writer,
            "PROJECT_INCOMING_REFERENCE",
            asset.ProjectIncomingReferences);

        writer.WriteLine(
            "DEPENDENCIES_END");

        writer.WriteLine();
    }

    private static void WriteImporter(
        StreamWriter writer,
        AssetImporter importer)
    {
        writer.WriteLine(
            "IMPORTER_BEGIN");

        if (importer == null)
        {
            writer.WriteLine(
                "IMPORTER_STATUS: NONE");

            writer.WriteLine(
                "IMPORTER_END");

            writer.WriteLine();

            return;
        }

        writer.WriteLine(
            $"IMPORTER_TYPE: {GetTypeName(importer.GetType())}");

        writer.WriteLine(
            $"ASSET_BUNDLE_NAME: {Escape(importer.assetBundleName)}");

        writer.WriteLine(
            $"ASSET_BUNDLE_VARIANT: {Escape(importer.assetBundleVariant)}");

        writer.WriteLine(
            $"USER_DATA: {Escape(importer.userData)}");

        WriteSerializedObject(
            writer,
            importer,
            "IMPORTER_SERIALIZED_PROPERTIES");

        writer.WriteLine(
            "IMPORTER_END");

        writer.WriteLine();
    }

    private static void WriteSubAssetIndex(
        StreamWriter writer,
        IReadOnlyList<UnityEngine.Object> objects)
    {
        writer.WriteLine(
            "SUB_ASSET_INDEX_BEGIN");

        writer.WriteLine(
            $"SUB_ASSET_COUNT: {objects.Count}");

        for (int i = 0;
             i < objects.Count;
             i++)
        {
            UnityEngine.Object item =
                objects[i];

            if (item == null)
            {
                writer.WriteLine(
                    $"SUB_ASSET: index={i}; status=NULL");

                continue;
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                item,
                out string guid,
                out long localId);

            writer.WriteLine(
                $"SUB_ASSET: " +
                $"index={i}; " +
                $"name={Escape(item.name)}; " +
                $"type={Escape(GetTypeName(item.GetType()))}; " +
                $"guid={Escape(guid)}; " +
                $"local_file_id={localId}; " +
                $"hide_flags={item.hideFlags}; " +
                $"instance_id={item.GetInstanceID()}");
        }

        writer.WriteLine(
            "SUB_ASSET_INDEX_END");

        writer.WriteLine();
    }

    private static void WriteObjectDetail(
        StreamWriter writer,
        DataAssetInfo asset,
        UnityEngine.Object item,
        int objectIndex)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
            item,
            out string guid,
            out long localId);

        writer.WriteLine(
            "OBJECT_DETAIL_BEGIN");

        writer.WriteLine(
            $"OBJECT_INDEX: {objectIndex}");

        writer.WriteLine(
            $"OBJECT_NAME: {Escape(item.name)}");

        writer.WriteLine(
            $"OBJECT_TYPE: {GetTypeName(item.GetType())}");

        writer.WriteLine(
            $"OBJECT_ASSEMBLY_QUALIFIED_TYPE: " +
            $"{Escape(item.GetType().AssemblyQualifiedName)}");

        writer.WriteLine(
            $"OBJECT_GUID: {guid}");

        writer.WriteLine(
            $"OBJECT_LOCAL_FILE_ID: {localId}");

        writer.WriteLine(
            $"OBJECT_INSTANCE_ID: {item.GetInstanceID()}");

        writer.WriteLine(
            $"OBJECT_HIDE_FLAGS: {item.hideFlags}");

        writer.WriteLine(
            $"OBJECT_ASSET_PATH: {AssetDatabase.GetAssetPath(item)}");

        WriteTypeHierarchy(
            writer,
            item.GetType());

        WriteSpecializedObjectData(
            writer,
            asset,
            item);

        WriteSerializedObject(
            writer,
            item,
            "OBJECT_SERIALIZED_PROPERTIES");

        writer.WriteLine(
            "OBJECT_DETAIL_END");

        writer.WriteLine();
    }

    private static void WriteTypeHierarchy(
        StreamWriter writer,
        Type type)
    {
        writer.WriteLine(
            "TYPE_HIERARCHY_BEGIN");

        Type current =
            type;

        int depth =
            0;

        while (current != null)
        {
            writer.WriteLine(
                $"TYPE_LEVEL: " +
                $"depth={depth}; " +
                $"type={Escape(GetTypeName(current))}; " +
                $"assembly={Escape(current.Assembly.GetName().Name)}");

            current =
                current.BaseType;

            depth++;
        }

        writer.WriteLine(
            "TYPE_HIERARCHY_END");

        writer.WriteLine();
    }

    private static void WriteSpecializedObjectData(
        StreamWriter writer,
        DataAssetInfo asset,
        UnityEngine.Object item)
    {
        switch (item)
        {
            case AnimatorController animatorController:
                WriteAnimatorController(
                    writer,
                    animatorController);
                break;

            case AnimatorOverrideController overrideController:
                WriteAnimatorOverrideController(
                    writer,
                    overrideController);
                break;

            case AnimationClip clip:
                WriteAnimationClip(
                    writer,
                    clip);
                break;

            case GameObject gameObject:
                WriteGameObjectAssetHierarchy(
                    writer,
                    gameObject);
                break;

            case Mesh mesh:
                WriteMesh(
                    writer,
                    mesh);
                break;

            case Material material:
                WriteMaterial(
                    writer,
                    material);
                break;

            case Sprite sprite:
                WriteSprite(
                    writer,
                    sprite);
                break;

            case Texture2D texture:
                WriteTexture(
                    writer,
                    texture);
                break;

            case AudioClip audioClip:
                WriteAudioClip(
                    writer,
                    audioClip);
                break;

            case Avatar avatar:
                WriteAvatar(
                    writer,
                    avatar);
                break;

            case ScriptableObject scriptableObject:
                WriteScriptableObject(
                    writer,
                    scriptableObject);
                break;

            case TextAsset textAsset:
                WriteTextAsset(
                    writer,
                    textAsset);
                break;

            case Shader shader:
                WriteShader(
                    writer,
                    shader);
                break;
        }
    }

    private static void WriteScriptableObject(
        StreamWriter writer,
        ScriptableObject scriptableObject)
    {
        writer.WriteLine(
            "SCRIPTABLE_OBJECT_BEGIN");

        MonoScript monoScript =
            MonoScript.FromScriptableObject(
                scriptableObject);

        if (monoScript != null)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                monoScript,
                out string scriptGuid,
                out long scriptLocalId);

            Type scriptClass =
                monoScript.GetClass();

            writer.WriteLine(
                $"SCRIPT_PATH: {AssetDatabase.GetAssetPath(monoScript)}");

            writer.WriteLine(
                $"SCRIPT_GUID: {scriptGuid}");

            writer.WriteLine(
                $"SCRIPT_LOCAL_FILE_ID: {scriptLocalId}");

            writer.WriteLine(
                $"SCRIPT_CLASS: {GetTypeName(scriptClass)}");

            writer.WriteLine(
                $"SCRIPT_ASSEMBLY: " +
                $"{Escape(scriptClass?.Assembly.GetName().Name)}");

            writer.WriteLine(
                $"SCRIPT_NAMESPACE: {Escape(scriptClass?.Namespace)}");
        }
        else
        {
            writer.WriteLine(
                "SCRIPT_STATUS: MONO_SCRIPT_NOT_FOUND");
        }

        writer.WriteLine(
            "SCRIPTABLE_OBJECT_END");

        writer.WriteLine();
    }

    private static void WriteAnimatorController(
        StreamWriter writer,
        AnimatorController controller)
    {
        writer.WriteLine(
            "ANIMATOR_CONTROLLER_BEGIN");

        writer.WriteLine(
            $"PARAMETER_COUNT: {controller.parameters.Length}");

        for (int i = 0;
             i < controller.parameters.Length;
             i++)
        {
            AnimatorControllerParameter parameter =
                controller.parameters[i];

            writer.WriteLine(
                $"PARAMETER: " +
                $"index={i}; " +
                $"name={Escape(parameter.name)}; " +
                $"type={parameter.type}; " +
                $"default_bool={parameter.defaultBool}; " +
                $"default_int={parameter.defaultInt}; " +
                $"default_float={FormatFloat(parameter.defaultFloat)}");
        }

        writer.WriteLine(
            $"LAYER_COUNT: {controller.layers.Length}");

        HashSet<int> visitedStateMachines =
            new HashSet<int>();

        for (int i = 0;
             i < controller.layers.Length;
             i++)
        {
            AnimatorControllerLayer layer =
                controller.layers[i];

            writer.WriteLine(
                "ANIMATOR_LAYER_BEGIN");

            writer.WriteLine(
                $"LAYER_INDEX: {i}");

            writer.WriteLine(
                $"LAYER_NAME: {Escape(layer.name)}");

            writer.WriteLine(
                $"DEFAULT_WEIGHT: {FormatFloat(layer.defaultWeight)}");

            writer.WriteLine(
                $"BLENDING_MODE: {layer.blendingMode}");

            writer.WriteLine(
                $"IK_PASS: {layer.iKPass}");

            writer.WriteLine(
                $"SYNCED_LAYER_INDEX: {layer.syncedLayerIndex}");

            writer.WriteLine(
                $"SYNCED_LAYER_AFFECTS_TIMING: {layer.syncedLayerAffectsTiming}");

            writer.WriteLine(
                $"AVATAR_MASK: {DescribeObjectReference(layer.avatarMask)}");

            WriteAnimatorStateMachine(
                writer,
                layer.stateMachine,
                $"Layer[{i}]/{layer.name}",
                depth: 0,
                visitedStateMachines);

            writer.WriteLine(
                "ANIMATOR_LAYER_END");

            writer.WriteLine();
        }

        writer.WriteLine(
            "ANIMATOR_CONTROLLER_END");

        writer.WriteLine();
    }

    private static void WriteAnimatorStateMachine(
        StreamWriter writer,
        AnimatorStateMachine stateMachine,
        string logicalPath,
        int depth,
        HashSet<int> visited)
    {
        if (stateMachine == null)
            return;

        int instanceId =
            stateMachine.GetInstanceID();

        string indent =
            new string(
                ' ',
                depth * 2);

        if (!visited.Add(instanceId))
        {
            writer.WriteLine(
                $"{indent}STATE_MACHINE_REFERENCE: " +
                $"path={Escape(logicalPath)}; " +
                $"name={Escape(stateMachine.name)}; " +
                $"instance_id={instanceId}");

            return;
        }

        writer.WriteLine(
            $"{indent}STATE_MACHINE_BEGIN");

        writer.WriteLine(
            $"{indent}STATE_MACHINE_PATH: {Escape(logicalPath)}");

        writer.WriteLine(
            $"{indent}STATE_MACHINE_NAME: {Escape(stateMachine.name)}");

        writer.WriteLine(
            $"{indent}DEFAULT_STATE: " +
            $"{DescribeObjectReference(stateMachine.defaultState)}");

        writer.WriteLine(
            $"{indent}ENTRY_POSITION: {FormatVector3(stateMachine.entryPosition)}");

        writer.WriteLine(
            $"{indent}EXIT_POSITION: {FormatVector3(stateMachine.exitPosition)}");

        writer.WriteLine(
            $"{indent}ANY_STATE_POSITION: {FormatVector3(stateMachine.anyStatePosition)}");

        writer.WriteLine(
            $"{indent}PARENT_STATE_MACHINE_POSITION: " +
            $"{FormatVector3(stateMachine.parentStateMachinePosition)}");

        WriteStateMachineBehaviours(
            writer,
            stateMachine.behaviours,
            indent + "  ",
            "STATE_MACHINE_BEHAVIOUR");

        for (int i = 0;
             i < stateMachine.entryTransitions.Length;
             i++)
        {
            WriteAnimatorTransition(
                writer,
                stateMachine.entryTransitions[i],
                indent + "  ",
                $"ENTRY_TRANSITION[{i}]");
        }

        for (int i = 0;
             i < stateMachine.anyStateTransitions.Length;
             i++)
        {
            WriteAnimatorStateTransition(
                writer,
                stateMachine.anyStateTransitions[i],
                indent + "  ",
                $"ANY_STATE_TRANSITION[{i}]");
        }

        ChildAnimatorState[] states =
            stateMachine.states;

        for (int i = 0;
             i < states.Length;
             i++)
        {
            ChildAnimatorState child =
                states[i];

            AnimatorState state =
                child.state;

            if (state == null)
                continue;

            writer.WriteLine(
                $"{indent}  STATE_BEGIN");

            writer.WriteLine(
                $"{indent}  STATE_INDEX: {i}");

            writer.WriteLine(
                $"{indent}  STATE_NAME: {Escape(state.name)}");

            writer.WriteLine(
                $"{indent}  STATE_POSITION: {FormatVector3(child.position)}");

            writer.WriteLine(
                $"{indent}  MOTION: {DescribeObjectReference(state.motion)}");

            writer.WriteLine(
                $"{indent}  SPEED: {FormatFloat(state.speed)}");

            writer.WriteLine(
                $"{indent}  SPEED_PARAMETER_ACTIVE: {state.speedParameterActive}");

            writer.WriteLine(
                $"{indent}  SPEED_PARAMETER: {Escape(state.speedParameter)}");

            writer.WriteLine(
                $"{indent}  TIME_PARAMETER_ACTIVE: {state.timeParameterActive}");

            writer.WriteLine(
                $"{indent}  TIME_PARAMETER: {Escape(state.timeParameter)}");

            writer.WriteLine(
                $"{indent}  MIRROR: {state.mirror}");

            writer.WriteLine(
                $"{indent}  MIRROR_PARAMETER_ACTIVE: {state.mirrorParameterActive}");

            writer.WriteLine(
                $"{indent}  MIRROR_PARAMETER: {Escape(state.mirrorParameter)}");

            writer.WriteLine(
                $"{indent}  CYCLE_OFFSET: {FormatFloat(state.cycleOffset)}");

            writer.WriteLine(
                $"{indent}  CYCLE_OFFSET_PARAMETER_ACTIVE: {state.cycleOffsetParameterActive}");

            writer.WriteLine(
                $"{indent}  CYCLE_OFFSET_PARAMETER: {Escape(state.cycleOffsetParameter)}");

            writer.WriteLine(
                $"{indent}  IK_ON_FEET: {state.iKOnFeet}");

            writer.WriteLine(
                $"{indent}  WRITE_DEFAULT_VALUES: {state.writeDefaultValues}");

            writer.WriteLine(
                $"{indent}  TAG: {Escape(state.tag)}");

            WriteStateMachineBehaviours(
                writer,
                state.behaviours,
                indent + "    ",
                "STATE_BEHAVIOUR");

            if (state.motion is BlendTree blendTree)
            {
                WriteBlendTree(
                    writer,
                    blendTree,
                    indent + "    ",
                    new HashSet<int>());
            }

            for (int transitionIndex = 0;
                 transitionIndex < state.transitions.Length;
                 transitionIndex++)
            {
                WriteAnimatorStateTransition(
                    writer,
                    state.transitions[transitionIndex],
                    indent + "    ",
                    $"STATE_TRANSITION[{transitionIndex}]");
            }

            writer.WriteLine(
                $"{indent}  STATE_END");
        }

        ChildAnimatorStateMachine[] childMachines =
            stateMachine.stateMachines;

        for (int i = 0;
             i < childMachines.Length;
             i++)
        {
            ChildAnimatorStateMachine child =
                childMachines[i];

            writer.WriteLine(
                $"{indent}  CHILD_STATE_MACHINE_POSITION: " +
                $"index={i}; " +
                $"position={FormatVector3(child.position)}");

            WriteAnimatorStateMachine(
                writer,
                child.stateMachine,
                $"{logicalPath}/{child.stateMachine?.name}",
                depth + 1,
                visited);
        }

        writer.WriteLine(
            $"{indent}STATE_MACHINE_END");
    }

    private static void WriteAnimatorStateTransition(
        StreamWriter writer,
        AnimatorStateTransition transition,
        string indent,
        string label)
    {
        if (transition == null)
            return;

        writer.WriteLine(
            $"{indent}{label}_BEGIN");

        WriteAnimatorTransitionBase(
            writer,
            transition,
            indent + "  ");

        writer.WriteLine(
            $"{indent}  DESTINATION_STATE: " +
            $"{DescribeObjectReference(transition.destinationState)}");

        writer.WriteLine(
            $"{indent}  DESTINATION_STATE_MACHINE: " +
            $"{DescribeObjectReference(transition.destinationStateMachine)}");

        writer.WriteLine(
            $"{indent}  IS_EXIT: {transition.isExit}");

        writer.WriteLine(
            $"{indent}  HAS_EXIT_TIME: {transition.hasExitTime}");

        writer.WriteLine(
            $"{indent}  EXIT_TIME: {FormatFloat(transition.exitTime)}");

        writer.WriteLine(
            $"{indent}  HAS_FIXED_DURATION: {transition.hasFixedDuration}");

        writer.WriteLine(
            $"{indent}  DURATION: {FormatFloat(transition.duration)}");

        writer.WriteLine(
            $"{indent}  OFFSET: {FormatFloat(transition.offset)}");

        writer.WriteLine(
            $"{indent}  INTERRUPTION_SOURCE: {transition.interruptionSource}");

        writer.WriteLine(
            $"{indent}  ORDERED_INTERRUPTION: {transition.orderedInterruption}");

        writer.WriteLine(
            $"{indent}  CAN_TRANSITION_TO_SELF: {transition.canTransitionToSelf}");

        writer.WriteLine(
            $"{indent}{label}_END");
    }

    private static void WriteAnimatorTransition(
        StreamWriter writer,
        AnimatorTransition transition,
        string indent,
        string label)
    {
        if (transition == null)
            return;

        writer.WriteLine(
            $"{indent}{label}_BEGIN");

        WriteAnimatorTransitionBase(
            writer,
            transition,
            indent + "  ");

        writer.WriteLine(
            $"{indent}  DESTINATION_STATE: " +
            $"{DescribeObjectReference(transition.destinationState)}");

        writer.WriteLine(
            $"{indent}  DESTINATION_STATE_MACHINE: " +
            $"{DescribeObjectReference(transition.destinationStateMachine)}");

        writer.WriteLine(
            $"{indent}  IS_EXIT: {transition.isExit}");

        writer.WriteLine(
            $"{indent}{label}_END");
    }

    private static void WriteAnimatorTransitionBase(
        StreamWriter writer,
        AnimatorTransitionBase transition,
        string indent)
    {
        writer.WriteLine(
            $"{indent}NAME: {Escape(transition.name)}");

        writer.WriteLine(
            $"{indent}SOLO: {transition.solo}");

        writer.WriteLine(
            $"{indent}MUTE: {transition.mute}");

        AnimatorCondition[] conditions =
            transition.conditions;

        writer.WriteLine(
            $"{indent}CONDITION_COUNT: {conditions.Length}");

        for (int i = 0;
             i < conditions.Length;
             i++)
        {
            AnimatorCondition condition =
                conditions[i];

            writer.WriteLine(
                $"{indent}CONDITION: " +
                $"index={i}; " +
                $"mode={condition.mode}; " +
                $"parameter={Escape(condition.parameter)}; " +
                $"threshold={FormatFloat(condition.threshold)}");
        }
    }

    private static void WriteStateMachineBehaviours(
        StreamWriter writer,
        StateMachineBehaviour[] behaviours,
        string indent,
        string label)
    {
        writer.WriteLine(
            $"{indent}{label}_COUNT: {behaviours.Length}");

        for (int i = 0;
             i < behaviours.Length;
             i++)
        {
            StateMachineBehaviour behaviour =
                behaviours[i];

            writer.WriteLine(
                $"{indent}{label}: " +
                $"index={i}; " +
                $"{DescribeObjectReference(behaviour)}");
        }
    }

    private static void WriteBlendTree(
        StreamWriter writer,
        BlendTree tree,
        string indent,
        HashSet<int> visited)
    {
        if (tree == null)
            return;

        if (!visited.Add(tree.GetInstanceID()))
        {
            writer.WriteLine(
                $"{indent}BLEND_TREE_REFERENCE: " +
                $"{DescribeObjectReference(tree)}");

            return;
        }

        writer.WriteLine(
            $"{indent}BLEND_TREE_BEGIN");

        writer.WriteLine(
            $"{indent}NAME: {Escape(tree.name)}");

        writer.WriteLine(
            $"{indent}BLEND_TYPE: {tree.blendType}");

        writer.WriteLine(
            $"{indent}BLEND_PARAMETER: {Escape(tree.blendParameter)}");

        writer.WriteLine(
            $"{indent}BLEND_PARAMETER_Y: {Escape(tree.blendParameterY)}");

        writer.WriteLine(
            $"{indent}MIN_THRESHOLD: {FormatFloat(tree.minThreshold)}");

        writer.WriteLine(
            $"{indent}MAX_THRESHOLD: {FormatFloat(tree.maxThreshold)}");

        writer.WriteLine(
            $"{indent}USE_AUTOMATIC_THRESHOLDS: {tree.useAutomaticThresholds}");

        ChildMotion[] children =
            tree.children;

        writer.WriteLine(
            $"{indent}CHILD_COUNT: {children.Length}");

        for (int i = 0;
             i < children.Length;
             i++)
        {
            ChildMotion child =
                children[i];

            writer.WriteLine(
                $"{indent}CHILD_MOTION: " +
                $"index={i}; " +
                $"motion={DescribeObjectReference(child.motion)}; " +
                $"threshold={FormatFloat(child.threshold)}; " +
                $"position={FormatVector2(child.position)}; " +
                $"time_scale={FormatFloat(child.timeScale)}; " +
                $"cycle_offset={FormatFloat(child.cycleOffset)}; " +
                $"mirror={child.mirror}; " +
                $"direct_blend_parameter={Escape(child.directBlendParameter)}");

            if (child.motion is BlendTree childTree)
            {
                WriteBlendTree(
                    writer,
                    childTree,
                    indent + "  ",
                    visited);
            }
        }

        writer.WriteLine(
            $"{indent}BLEND_TREE_END");
    }

    private static void WriteAnimatorOverrideController(
        StreamWriter writer,
        AnimatorOverrideController controller)
    {
        writer.WriteLine(
            "ANIMATOR_OVERRIDE_CONTROLLER_BEGIN");

        writer.WriteLine(
            $"BASE_CONTROLLER: " +
            $"{DescribeObjectReference(controller.runtimeAnimatorController)}");

        writer.WriteLine(
            $"OVERRIDE_COUNT: {controller.overridesCount}");

        List<KeyValuePair<AnimationClip, AnimationClip>>
            overrides =
                new List<KeyValuePair<AnimationClip, AnimationClip>>();

        controller.GetOverrides(
            overrides);

        for (int i = 0;
             i < overrides.Count;
             i++)
        {
            KeyValuePair<AnimationClip, AnimationClip> pair =
                overrides[i];

            writer.WriteLine(
                $"CLIP_OVERRIDE: " +
                $"index={i}; " +
                $"original={DescribeObjectReference(pair.Key)}; " +
                $"override={DescribeObjectReference(pair.Value)}");
        }

        writer.WriteLine(
            "ANIMATOR_OVERRIDE_CONTROLLER_END");

        writer.WriteLine();
    }

    private static void WriteAnimationClip(
        StreamWriter writer,
        AnimationClip clip)
    {
        writer.WriteLine(
            "ANIMATION_CLIP_BEGIN");

        writer.WriteLine(
            $"LENGTH_SECONDS: {FormatFloat(clip.length)}");

        writer.WriteLine(
            $"FRAME_RATE: {FormatFloat(clip.frameRate)}");

        writer.WriteLine(
            $"LEGACY: {clip.legacy}");

        writer.WriteLine(
            $"WRAP_MODE: {clip.wrapMode}");

        writer.WriteLine(
            $"EMPTY: {clip.empty}");

        writer.WriteLine(
            $"HUMAN_MOTION: {clip.humanMotion}");

        writer.WriteLine(
            $"LOCAL_BOUNDS: {FormatBounds(clip.localBounds)}");

        try
        {
            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(
                    clip);

            writer.WriteLine(
                $"CLIP_SETTINGS: " +
                $"start_time={FormatFloat(settings.startTime)}; " +
                $"stop_time={FormatFloat(settings.stopTime)}; " +
                $"loop_time={settings.loopTime}; " +
                $"loop_blend={settings.loopBlend}; " +
                $"loop_blend_orientation={settings.loopBlendOrientation}; " +
                $"loop_blend_position_y={settings.loopBlendPositionY}; " +
                $"loop_blend_position_xz={settings.loopBlendPositionXZ}; " +
                $"keep_original_orientation={settings.keepOriginalOrientation}; " +
                $"keep_original_position_y={settings.keepOriginalPositionY}; " +
                $"keep_original_position_xz={settings.keepOriginalPositionXZ}; " +
                $"height_from_feet={settings.heightFromFeet}; " +
                $"mirror={settings.mirror}; " +
                $"cycle_offset={FormatFloat(settings.cycleOffset)}");
        }
        catch (Exception exception)
        {
            writer.WriteLine(
                $"CLIP_SETTINGS_READ_ERROR: {Escape(exception.Message)}");
        }

        EditorCurveBinding[] floatBindings =
            AnimationUtility.GetCurveBindings(
                clip);

        writer.WriteLine(
            $"FLOAT_CURVE_BINDING_COUNT: {floatBindings.Length}");

        for (int i = 0;
             i < floatBindings.Length;
             i++)
        {
            EditorCurveBinding binding =
                floatBindings[i];

            AnimationCurve curve =
                AnimationUtility.GetEditorCurve(
                    clip,
                    binding);

            writer.WriteLine(
                "FLOAT_CURVE_BEGIN");

            writer.WriteLine(
                $"CURVE_INDEX: {i}");

            writer.WriteLine(
                $"CURVE_PATH: {Escape(binding.path)}");

            writer.WriteLine(
                $"CURVE_TYPE: {Escape(GetTypeName(binding.type))}");

            writer.WriteLine(
                $"CURVE_PROPERTY: {Escape(binding.propertyName)}");

            WriteAnimationCurve(
                writer,
                curve);

            writer.WriteLine(
                "FLOAT_CURVE_END");
        }

        EditorCurveBinding[] objectBindings =
            AnimationUtility.GetObjectReferenceCurveBindings(
                clip);

        writer.WriteLine(
            $"OBJECT_REFERENCE_CURVE_BINDING_COUNT: {objectBindings.Length}");

        for (int i = 0;
             i < objectBindings.Length;
             i++)
        {
            EditorCurveBinding binding =
                objectBindings[i];

            ObjectReferenceKeyframe[] keyframes =
                AnimationUtility.GetObjectReferenceCurve(
                    clip,
                    binding);

            writer.WriteLine(
                "OBJECT_REFERENCE_CURVE_BEGIN");

            writer.WriteLine(
                $"CURVE_INDEX: {i}");

            writer.WriteLine(
                $"CURVE_PATH: {Escape(binding.path)}");

            writer.WriteLine(
                $"CURVE_TYPE: {Escape(GetTypeName(binding.type))}");

            writer.WriteLine(
                $"CURVE_PROPERTY: {Escape(binding.propertyName)}");

            writer.WriteLine(
                $"KEY_COUNT: {keyframes.Length}");

            for (int keyIndex = 0;
                 keyIndex < keyframes.Length;
                 keyIndex++)
            {
                ObjectReferenceKeyframe key =
                    keyframes[keyIndex];

                writer.WriteLine(
                    $"KEY: " +
                    $"index={keyIndex}; " +
                    $"time={FormatFloat(key.time)}; " +
                    $"value={DescribeObjectReference(key.value)}");
            }

            writer.WriteLine(
                "OBJECT_REFERENCE_CURVE_END");
        }

        AnimationEvent[] events =
            AnimationUtility.GetAnimationEvents(
                clip);

        writer.WriteLine(
            $"ANIMATION_EVENT_COUNT: {events.Length}");

        for (int i = 0;
             i < events.Length;
             i++)
        {
            AnimationEvent animationEvent =
                events[i];

            writer.WriteLine(
                $"ANIMATION_EVENT: " +
                $"index={i}; " +
                $"time={FormatFloat(animationEvent.time)}; " +
                $"function={Escape(animationEvent.functionName)}; " +
                $"string_parameter={Escape(animationEvent.stringParameter)}; " +
                $"float_parameter={FormatFloat(animationEvent.floatParameter)}; " +
                $"int_parameter={animationEvent.intParameter}; " +
                $"object_parameter={DescribeObjectReference(animationEvent.objectReferenceParameter)}; " +
                $"message_options={animationEvent.messageOptions}");
        }

        writer.WriteLine(
            "ANIMATION_CLIP_END");

        writer.WriteLine();
    }

    private static void WriteAnimationCurve(
        StreamWriter writer,
        AnimationCurve curve)
    {
        if (curve == null)
        {
            writer.WriteLine(
                "CURVE_STATUS: NULL");

            return;
        }

        writer.WriteLine(
            $"PRE_WRAP_MODE: {curve.preWrapMode}");

        writer.WriteLine(
            $"POST_WRAP_MODE: {curve.postWrapMode}");

        Keyframe[] keys =
            curve.keys;

        writer.WriteLine(
            $"KEY_COUNT: {keys.Length}");

        for (int i = 0;
             i < keys.Length;
             i++)
        {
            Keyframe key =
                keys[i];

            writer.WriteLine(
                $"KEY: " +
                $"index={i}; " +
                $"time={FormatFloat(key.time)}; " +
                $"value={FormatFloat(key.value)}; " +
                $"in_tangent={FormatFloat(key.inTangent)}; " +
                $"out_tangent={FormatFloat(key.outTangent)}; " +
                $"in_weight={FormatFloat(key.inWeight)}; " +
                $"out_weight={FormatFloat(key.outWeight)}; " +
                $"weighted_mode={key.weightedMode}");
        }
    }

    private static void WriteGameObjectAssetHierarchy(
        StreamWriter writer,
        GameObject root)
    {
        writer.WriteLine(
            "GAME_OBJECT_ASSET_HIERARCHY_BEGIN");

        writer.WriteLine(
            $"PREFAB_ASSET_TYPE: {PrefabUtility.GetPrefabAssetType(root)}");

        writer.WriteLine(
            $"PREFAB_INSTANCE_STATUS: {PrefabUtility.GetPrefabInstanceStatus(root)}");

        WriteGameObjectNode(
            writer,
            root.transform,
            root.name,
            depth: 0);

        writer.WriteLine(
            "GAME_OBJECT_ASSET_HIERARCHY_END");

        writer.WriteLine();
    }

    private static void WriteGameObjectNode(
        StreamWriter writer,
        Transform transform,
        string hierarchyPath,
        int depth)
    {
        if (transform == null)
            return;

        GameObject gameObject =
            transform.gameObject;

        string indent =
            new string(
                ' ',
                depth * 2);

        writer.WriteLine(
            $"{indent}GAME_OBJECT_BEGIN");

        writer.WriteLine(
            $"{indent}HIERARCHY_PATH: {Escape(hierarchyPath)}");

        writer.WriteLine(
            $"{indent}NAME: {Escape(gameObject.name)}");

        writer.WriteLine(
            $"{indent}ACTIVE_SELF: {gameObject.activeSelf}");

        writer.WriteLine(
            $"{indent}ACTIVE_IN_HIERARCHY: {gameObject.activeInHierarchy}");

        writer.WriteLine(
            $"{indent}LAYER: {gameObject.layer}");

        writer.WriteLine(
            $"{indent}TAG: {Escape(gameObject.tag)}");

        writer.WriteLine(
            $"{indent}STATIC_FLAGS: {GameObjectUtility.GetStaticEditorFlags(gameObject)}");

        writer.WriteLine(
            $"{indent}LOCAL_POSITION: {FormatVector3(transform.localPosition)}");

        writer.WriteLine(
            $"{indent}LOCAL_ROTATION: {FormatQuaternion(transform.localRotation)}");

        writer.WriteLine(
            $"{indent}LOCAL_EULER_ANGLES: {FormatVector3(transform.localEulerAngles)}");

        writer.WriteLine(
            $"{indent}LOCAL_SCALE: {FormatVector3(transform.localScale)}");

        Component[] components =
            gameObject.GetComponents<Component>();

        writer.WriteLine(
            $"{indent}COMPONENT_COUNT: {components.Length}");

        for (int i = 0;
             i < components.Length;
             i++)
        {
            Component component =
                components[i];

            writer.WriteLine(
                $"{indent}COMPONENT_BEGIN");

            writer.WriteLine(
                $"{indent}COMPONENT_INDEX: {i}");

            if (component == null)
            {
                writer.WriteLine(
                    $"{indent}COMPONENT_STATUS: MISSING_SCRIPT");

                writer.WriteLine(
                    $"{indent}COMPONENT_END");

                continue;
            }

            writer.WriteLine(
                $"{indent}COMPONENT_TYPE: {GetTypeName(component.GetType())}");

            writer.WriteLine(
                $"{indent}COMPONENT_ENABLED_STATE: {DescribeEnabledState(component)}");

            WriteSerializedObject(
                writer,
                component,
                $"{indent}COMPONENT_SERIALIZED_PROPERTIES");

            writer.WriteLine(
                $"{indent}COMPONENT_END");
        }

        for (int i = 0;
             i < transform.childCount;
             i++)
        {
            Transform child =
                transform.GetChild(i);

            WriteGameObjectNode(
                writer,
                child,
                $"{hierarchyPath}/{child.name}",
                depth + 1);
        }

        writer.WriteLine(
            $"{indent}GAME_OBJECT_END");
    }

    private static string DescribeEnabledState(
        Component component)
    {
        switch (component)
        {
            case Behaviour behaviour:
                return
                    $"behaviour_enabled={behaviour.enabled}; " +
                    $"active_and_enabled={behaviour.isActiveAndEnabled}";

            case Renderer renderer:
                return
                    $"renderer_enabled={renderer.enabled}";

            case Collider collider:
                return
                    $"collider_enabled={collider.enabled}";

            default:
                return "NOT_APPLICABLE";
        }
    }

    private static void WriteMesh(
        StreamWriter writer,
        Mesh mesh)
    {
        writer.WriteLine(
            "MESH_BEGIN");

        writer.WriteLine(
            $"VERTEX_COUNT: {mesh.vertexCount}");

        writer.WriteLine(
            $"SUB_MESH_COUNT: {mesh.subMeshCount}");

        writer.WriteLine(
            $"BLEND_SHAPE_COUNT: {mesh.blendShapeCount}");

        writer.WriteLine(
            $"BIND_POSE_COUNT: {mesh.bindposes.Length}");

        writer.WriteLine(
            $"BONE_WEIGHT_COUNT: {mesh.GetAllBoneWeights().Length}");

        writer.WriteLine(
            $"BOUNDS: {FormatBounds(mesh.bounds)}");

        writer.WriteLine(
            $"INDEX_FORMAT: {mesh.indexFormat}");

        VertexAttributeDescriptor[] attributes =
            mesh.GetVertexAttributes();

        writer.WriteLine(
            $"VERTEX_ATTRIBUTE_COUNT: {attributes.Length}");

        for (int i = 0;
             i < attributes.Length;
             i++)
        {
            VertexAttributeDescriptor attribute =
                attributes[i];

            writer.WriteLine(
                $"VERTEX_ATTRIBUTE: " +
                $"index={i}; " +
                $"attribute={attribute.attribute}; " +
                $"format={attribute.format}; " +
                $"dimension={attribute.dimension}; " +
                $"stream={attribute.stream}");
        }

        ulong totalIndexCount =
            0;

        for (int i = 0;
             i < mesh.subMeshCount;
             i++)
        {
            SubMeshDescriptor subMesh =
                mesh.GetSubMesh(i);

            totalIndexCount +=
                checked(
                    (ulong)subMesh.indexCount);

            writer.WriteLine(
                $"SUB_MESH: " +
                $"index={i}; " +
                $"topology={subMesh.topology}; " +
                $"index_start={subMesh.indexStart}; " +
                $"index_count={subMesh.indexCount}; " +
                $"base_vertex={subMesh.baseVertex}; " +
                $"first_vertex={subMesh.firstVertex}; " +
                $"vertex_count={subMesh.vertexCount}; " +
                $"bounds={FormatBounds(subMesh.bounds)}");
        }

        writer.WriteLine(
            $"TOTAL_INDEX_COUNT: {totalIndexCount}");

        for (int i = 0;
             i < mesh.blendShapeCount;
             i++)
        {
            string shapeName =
                mesh.GetBlendShapeName(i);

            int frameCount =
                mesh.GetBlendShapeFrameCount(i);

            writer.WriteLine(
                $"BLEND_SHAPE: " +
                $"index={i}; " +
                $"name={Escape(shapeName)}; " +
                $"frame_count={frameCount}");

            for (int frameIndex = 0;
                 frameIndex < frameCount;
                 frameIndex++)
            {
                writer.WriteLine(
                    $"BLEND_SHAPE_FRAME: " +
                    $"shape_index={i}; " +
                    $"frame_index={frameIndex}; " +
                    $"weight={FormatFloat(mesh.GetBlendShapeFrameWeight(i, frameIndex))}");
            }
        }

        writer.WriteLine(
            "MESH_END");

        writer.WriteLine();
    }

    private static void WriteMaterial(
        StreamWriter writer,
        Material material)
    {
        writer.WriteLine(
            "MATERIAL_BEGIN");

        Shader shader =
            material.shader;

        writer.WriteLine(
            $"SHADER: {DescribeObjectReference(shader)}");

        writer.WriteLine(
            $"RENDER_QUEUE: {material.renderQueue}");

        writer.WriteLine(
            $"ENABLE_INSTANCING: {material.enableInstancing}");

        writer.WriteLine(
            $"DOUBLE_SIDED_GI: {material.doubleSidedGI}");

        writer.WriteLine(
            $"GLOBAL_ILLUMINATION_FLAGS: {material.globalIlluminationFlags}");

        string[] enabledKeywords =
            material.enabledKeywords
                .Select(
                    keyword => keyword.name)
                .OrderBy(
                    keyword => keyword,
                    StringComparer.Ordinal)
                .ToArray();

        writer.WriteLine(
            $"ENABLED_KEYWORDS: {JoinEscaped(enabledKeywords)}");

        writer.WriteLine(
            $"PASS_COUNT: {material.passCount}");

        for (int i = 0;
             i < material.passCount;
             i++)
        {
            writer.WriteLine(
                $"PASS: " +
                $"index={i}; " +
                $"name={Escape(material.GetPassName(i))}");
        }

        if (shader != null)
        {
            WriteShaderProperties(
                writer,
                shader,
                material);
        }

        writer.WriteLine(
            "MATERIAL_END");

        writer.WriteLine();
    }

    private static void WriteShader(
        StreamWriter writer,
        Shader shader)
    {
        writer.WriteLine(
            "SHADER_BEGIN");

        writer.WriteLine(
            $"SHADER_NAME: {Escape(shader.name)}");

        writer.WriteLine(
            $"IS_SUPPORTED: {shader.isSupported}");

        writer.WriteLine(
            $"RENDER_QUEUE: {shader.renderQueue}");

        WriteShaderProperties(
            writer,
            shader,
            material: null);

        writer.WriteLine(
            "SHADER_END");

        writer.WriteLine();
    }

    private static void WriteShaderProperties(
        StreamWriter writer,
        Shader shader,
        Material material)
    {
        int propertyCount =
            shader.GetPropertyCount();

        writer.WriteLine(
            $"SHADER_PROPERTY_COUNT: {propertyCount}");

        for (int i = 0;
             i < propertyCount;
             i++)
        {
            string propertyName =
                shader.GetPropertyName(i);

            ShaderPropertyType propertyType =
                shader.GetPropertyType(i);

            string value =
                material == null ||
                !material.HasProperty(propertyName)
                    ? "<NO_MATERIAL_VALUE>"
                    : GetMaterialPropertyValue(
                        material,
                        propertyName,
                        propertyType);

            writer.WriteLine(
                $"SHADER_PROPERTY: " +
                $"index={i}; " +
                $"name={Escape(propertyName)}; " +
                $"description={Escape(shader.GetPropertyDescription(i))}; " +
                $"type={propertyType}; " +
                $"flags={shader.GetPropertyFlags(i)}; " +
                $"attributes={JoinEscaped(shader.GetPropertyAttributes(i))}; " +
                $"value={Escape(value)}");
        }
    }

    private static string GetMaterialPropertyValue(
        Material material,
        string propertyName,
        ShaderPropertyType propertyType)
    {
        switch (propertyType)
        {
            case ShaderPropertyType.Color:
                return FormatColor(
                    material.GetColor(
                        propertyName));

            case ShaderPropertyType.Vector:
                return FormatVector4(
                    material.GetVector(
                        propertyName));

            case ShaderPropertyType.Float:
            case ShaderPropertyType.Range:
                return FormatFloat(
                    material.GetFloat(
                        propertyName));

            case ShaderPropertyType.Int:
                return material.GetInteger(
                        propertyName)
                    .ToString(
                        Invariant);

            case ShaderPropertyType.Texture:
                Texture texture =
                    material.GetTexture(
                        propertyName);

                return
                    $"{DescribeObjectReference(texture)}; " +
                    $"scale={FormatVector2(material.GetTextureScale(propertyName))}; " +
                    $"offset={FormatVector2(material.GetTextureOffset(propertyName))}";

            default:
                return "<UNSUPPORTED_SHADER_PROPERTY_TYPE>";
        }
    }

    private static void WriteTexture(
        StreamWriter writer,
        Texture2D texture)
    {
        writer.WriteLine(
            "TEXTURE_2D_BEGIN");

        writer.WriteLine(
            $"WIDTH: {texture.width}");

        writer.WriteLine(
            $"HEIGHT: {texture.height}");

        writer.WriteLine(
            $"FORMAT: {texture.format}");

        writer.WriteLine(
            $"MIPMAP_COUNT: {texture.mipmapCount}");

        writer.WriteLine(
            $"IS_READABLE: {texture.isReadable}");

        writer.WriteLine(
            $"FILTER_MODE: {texture.filterMode}");

        writer.WriteLine(
            $"WRAP_MODE: {texture.wrapMode}");

        writer.WriteLine(
            $"ANISO_LEVEL: {texture.anisoLevel}");

        writer.WriteLine(
            $"DIMENSION: {texture.dimension}");

        writer.WriteLine(
            "TEXTURE_2D_END");

        writer.WriteLine();
    }

    private static void WriteSprite(
        StreamWriter writer,
        Sprite sprite)
    {
        writer.WriteLine(
            "SPRITE_BEGIN");

        writer.WriteLine(
            $"TEXTURE: {DescribeObjectReference(sprite.texture)}");

        writer.WriteLine(
            $"ASSOCIATED_ALPHA_SPLIT_TEXTURE: " +
            $"{DescribeObjectReference(sprite.associatedAlphaSplitTexture)}");

        writer.WriteLine(
            $"RECT: {FormatRect(sprite.rect)}");

        writer.WriteLine(
            $"TEXTURE_RECT: {FormatRect(sprite.textureRect)}");

        writer.WriteLine(
            $"PIVOT: {FormatVector2(sprite.pivot)}");

        writer.WriteLine(
            $"BORDER: {FormatVector4(sprite.border)}");

        writer.WriteLine(
            $"PIXELS_PER_UNIT: {FormatFloat(sprite.pixelsPerUnit)}");

        writer.WriteLine(
            $"PACKED: {sprite.packed}");

        writer.WriteLine(
            $"PACKING_MODE: {sprite.packingMode}");

        writer.WriteLine(
            $"PACKING_ROTATION: {sprite.packingRotation}");

        Vector2[] vertices =
            sprite.vertices;

        ushort[] triangles =
            sprite.triangles;

        Vector2[] uv =
            sprite.uv;

        writer.WriteLine(
            $"VERTEX_COUNT: {vertices.Length}");

        for (int i = 0;
             i < vertices.Length;
             i++)
        {
            writer.WriteLine(
                $"VERTEX: index={i}; value={FormatVector2(vertices[i])}");
        }

        writer.WriteLine(
            $"TRIANGLE_INDEX_COUNT: {triangles.Length}");

        for (int i = 0;
             i < triangles.Length;
             i++)
        {
            writer.WriteLine(
                $"TRIANGLE_INDEX: index={i}; value={triangles[i]}");
        }

        writer.WriteLine(
            $"UV_COUNT: {uv.Length}");

        for (int i = 0;
             i < uv.Length;
             i++)
        {
            writer.WriteLine(
                $"UV: index={i}; value={FormatVector2(uv[i])}");
        }

        int physicsShapeCount =
            sprite.GetPhysicsShapeCount();

        writer.WriteLine(
            $"PHYSICS_SHAPE_COUNT: {physicsShapeCount}");

        List<Vector2> physicsShape =
            new List<Vector2>();

        for (int i = 0;
             i < physicsShapeCount;
             i++)
        {
            physicsShape.Clear();

            sprite.GetPhysicsShape(
                i,
                physicsShape);

            writer.WriteLine(
                $"PHYSICS_SHAPE_BEGIN: index={i}; point_count={physicsShape.Count}");

            for (int pointIndex = 0;
                 pointIndex < physicsShape.Count;
                 pointIndex++)
            {
                writer.WriteLine(
                    $"PHYSICS_POINT: " +
                    $"shape_index={i}; " +
                    $"point_index={pointIndex}; " +
                    $"value={FormatVector2(physicsShape[pointIndex])}");
            }

            writer.WriteLine(
                $"PHYSICS_SHAPE_END: index={i}");
        }

        writer.WriteLine(
            "SPRITE_END");

        writer.WriteLine();
    }

    private static void WriteAudioClip(
        StreamWriter writer,
        AudioClip clip)
    {
        writer.WriteLine(
            "AUDIO_CLIP_BEGIN");

        writer.WriteLine(
            $"LENGTH_SECONDS: {FormatFloat(clip.length)}");

        writer.WriteLine(
            $"SAMPLES: {clip.samples}");

        writer.WriteLine(
            $"CHANNELS: {clip.channels}");

        writer.WriteLine(
            $"FREQUENCY: {clip.frequency}");

        writer.WriteLine(
            $"LOAD_TYPE: {clip.loadType}");

        writer.WriteLine(
            $"PRELOAD_AUDIO_DATA: {clip.preloadAudioData}");

        writer.WriteLine(
            $"LOAD_IN_BACKGROUND: {clip.loadInBackground}");

        writer.WriteLine(
            $"AMBISONIC: {clip.ambisonic}");

        writer.WriteLine(
            $"LOAD_STATE: {clip.loadState}");

        writer.WriteLine(
            "AUDIO_CLIP_END");

        writer.WriteLine();
    }

    private static void WriteAvatar(
        StreamWriter writer,
        Avatar avatar)
    {
        writer.WriteLine(
            "AVATAR_BEGIN");

        writer.WriteLine(
            $"IS_VALID: {avatar.isValid}");

        writer.WriteLine(
            $"IS_HUMAN: {avatar.isHuman}");

        if (avatar.isHuman)
        {
            HumanDescription description =
                avatar.humanDescription;

            writer.WriteLine(
                $"UPPER_ARM_TWIST: {FormatFloat(description.upperArmTwist)}");

            writer.WriteLine(
                $"LOWER_ARM_TWIST: {FormatFloat(description.lowerArmTwist)}");

            writer.WriteLine(
                $"UPPER_LEG_TWIST: {FormatFloat(description.upperLegTwist)}");

            writer.WriteLine(
                $"LOWER_LEG_TWIST: {FormatFloat(description.lowerLegTwist)}");

            writer.WriteLine(
                $"ARM_STRETCH: {FormatFloat(description.armStretch)}");

            writer.WriteLine(
                $"LEG_STRETCH: {FormatFloat(description.legStretch)}");

            writer.WriteLine(
                $"FEET_SPACING: {FormatFloat(description.feetSpacing)}");

            writer.WriteLine(
                $"HAS_TRANSLATION_DOF: {description.hasTranslationDoF}");

            writer.WriteLine(
                $"HUMAN_BONE_COUNT: {description.human.Length}");

            for (int i = 0;
                 i < description.human.Length;
                 i++)
            {
                HumanBone bone =
                    description.human[i];

                HumanLimit limit =
                    bone.limit;

                writer.WriteLine(
                    $"HUMAN_BONE: " +
                    $"index={i}; " +
                    $"human_name={Escape(bone.humanName)}; " +
                    $"bone_name={Escape(bone.boneName)}; " +
                    $"axis_length={FormatFloat(limit.axisLength)}; " +
                    $"use_default_values={limit.useDefaultValues}; " +
                    $"min={FormatVector3(limit.min)}; " +
                    $"max={FormatVector3(limit.max)}; " +
                    $"center={FormatVector3(limit.center)}");
            }

            writer.WriteLine(
                $"SKELETON_BONE_COUNT: {description.skeleton.Length}");

            for (int i = 0;
                 i < description.skeleton.Length;
                 i++)
            {
                SkeletonBone bone =
                    description.skeleton[i];

                writer.WriteLine(
                    $"SKELETON_BONE: " +
                    $"index={i}; " +
                    $"name={Escape(bone.name)}; " +
                    "parent_name=<NOT_EXPOSED_BY_UNITY_API>; " +
                    $"position={FormatVector3(bone.position)}; " +
                    $"rotation={FormatQuaternion(bone.rotation)}; " +
                    $"scale={FormatVector3(bone.scale)}");
            }
        }

        writer.WriteLine(
            "AVATAR_END");

        writer.WriteLine();
    }

    private static void WriteTextAsset(
        StreamWriter writer,
        TextAsset textAsset)
    {
        writer.WriteLine(
            "TEXT_ASSET_BEGIN");

        string text =
            textAsset.text
            ?? string.Empty;

        writer.WriteLine(
            $"TEXT_LENGTH: {text.Length}");

        writer.WriteLine(
            $"BYTE_LENGTH: {textAsset.bytes.Length}");

        writer.WriteLine(
            "TEXT_CONTENT_BEGIN");

        writer.Write(
            NormalizeNewLines(
                text));

        if (text.Length > 0 &&
            !text.EndsWith(
                "\n",
                StringComparison.Ordinal))
        {
            writer.WriteLine();
        }

        writer.WriteLine(
            "TEXT_CONTENT_END");

        writer.WriteLine(
            "TEXT_ASSET_END");

        writer.WriteLine();
    }

    private static void WriteSerializedObject(
        StreamWriter writer,
        UnityEngine.Object target,
        string sectionName)
    {
        writer.WriteLine(
            $"{sectionName}_BEGIN");

        if (target == null)
        {
            writer.WriteLine(
                "SERIALIZED_OBJECT_STATUS: NULL");

            writer.WriteLine(
                $"{sectionName}_END");

            writer.WriteLine();

            return;
        }

        try
        {
            SerializedObject serializedObject =
                new SerializedObject(
                    target);

            serializedObject.UpdateIfRequiredOrScript();

            SerializedProperty property =
                serializedObject.GetIterator();

            bool enterChildren =
                true;

            int propertyIndex =
                0;

            while (property.Next(
                       enterChildren))
            {
                enterChildren =
                    true;

                WriteSerializedProperty(
                    writer,
                    property,
                    propertyIndex);

                propertyIndex++;
            }

            writer.WriteLine(
                $"SERIALIZED_PROPERTY_COUNT: {propertyIndex}");
        }
        catch (Exception exception)
        {
            writer.WriteLine(
                $"SERIALIZED_OBJECT_READ_ERROR: " +
                $"{Escape(exception.GetType().Name)}: " +
                $"{Escape(exception.Message)}");
        }

        writer.WriteLine(
            $"{sectionName}_END");

        writer.WriteLine();
    }

    private static void WriteSerializedProperty(
        StreamWriter writer,
        SerializedProperty property,
        int propertyIndex)
    {
        string value;

        try
        {
            value =
                GetSerializedPropertyValue(
                    property);
        }
        catch (Exception exception)
        {
            value =
                $"<VALUE_READ_ERROR " +
                $"{exception.GetType().Name}: " +
                $"{exception.Message}>";
        }

        string arrayInfo;

        try
        {
            arrayInfo =
                property.isArray
                    ? $"true; array_size={property.arraySize}"
                    : "false";
        }
        catch
        {
            arrayInfo =
                "UNKNOWN";
        }

        writer.WriteLine(
            $"SERIALIZED_PROPERTY: " +
            $"index={propertyIndex}; " +
            $"depth={property.depth}; " +
            $"path={Escape(property.propertyPath)}; " +
            $"name={Escape(property.name)}; " +
            $"display_name={Escape(property.displayName)}; " +
            $"type={property.propertyType}; " +
            $"type_name={Escape(property.type)}; " +
            $"array={arrayInfo}; " +
            $"editable={property.editable}; " +
            $"animated={property.isAnimated}; " +
            $"prefab_override={property.prefabOverride}; " +
            $"value={Escape(value)}");
    }

    private static string GetSerializedPropertyValue(
        SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Generic:
                return
                    property.hasVisibleChildren
                        ? "<GENERIC_WITH_VISIBLE_CHILDREN>"
                        : "<GENERIC>";

            case SerializedPropertyType.Integer:
                return property.longValue.ToString(
                    Invariant);

            case SerializedPropertyType.Boolean:
                return property.boolValue.ToString();

            case SerializedPropertyType.Float:
                return property.doubleValue.ToString(
                    "R",
                    Invariant);

            case SerializedPropertyType.String:
                return property.stringValue;

            case SerializedPropertyType.Color:
                return FormatColor(
                    property.colorValue);

            case SerializedPropertyType.ObjectReference:
                return DescribeObjectReference(
                    property.objectReferenceValue);

            case SerializedPropertyType.LayerMask:
                return property.intValue.ToString(
                    Invariant);

            case SerializedPropertyType.Enum:
                string enumName =
                    property.enumValueIndex >= 0 &&
                    property.enumValueIndex < property.enumDisplayNames.Length
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : "<OUT_OF_RANGE>";

                return
                    $"index={property.enumValueIndex}; " +
                    $"name={enumName}; " +
                    $"options=[{string.Join(", ", property.enumDisplayNames.Select(Escape))}]";

            case SerializedPropertyType.Vector2:
                return FormatVector2(
                    property.vector2Value);

            case SerializedPropertyType.Vector3:
                return FormatVector3(
                    property.vector3Value);

            case SerializedPropertyType.Vector4:
                return FormatVector4(
                    property.vector4Value);

            case SerializedPropertyType.Rect:
                return FormatRect(
                    property.rectValue);

            case SerializedPropertyType.ArraySize:
                return property.intValue.ToString(
                    Invariant);

            case SerializedPropertyType.Character:
                return
                    $"code={property.intValue}; " +
                    $"character={(char)property.intValue}";

            case SerializedPropertyType.AnimationCurve:
                return DescribeAnimationCurve(
                    property.animationCurveValue);

            case SerializedPropertyType.Bounds:
                return FormatBounds(
                    property.boundsValue);

            case SerializedPropertyType.Quaternion:
                return FormatQuaternion(
                    property.quaternionValue);

            case SerializedPropertyType.ExposedReference:
                return DescribeObjectReference(
                    property.exposedReferenceValue);

            case SerializedPropertyType.FixedBufferSize:
                return property.fixedBufferSize.ToString(
                    Invariant);

            case SerializedPropertyType.Vector2Int:
                return property.vector2IntValue.ToString();

            case SerializedPropertyType.Vector3Int:
                return property.vector3IntValue.ToString();

            case SerializedPropertyType.RectInt:
                return property.rectIntValue.ToString();

            case SerializedPropertyType.BoundsInt:
                return property.boundsIntValue.ToString();

            case SerializedPropertyType.ManagedReference:
                return
                    $"id={property.managedReferenceId}; " +
                    $"full_type={property.managedReferenceFullTypename}; " +
                    $"field_type={property.managedReferenceFieldTypename}";

            case SerializedPropertyType.Hash128:
                return property.hash128Value.ToString();

            case SerializedPropertyType.Gradient:
                return "<GRADIENT_NATIVE_SERIALIZED_VALUE>";

            default:
                return
                    $"<UNSUPPORTED_SERIALIZED_PROPERTY_TYPE " +
                    $"{property.propertyType}>";
        }
    }

    private static string DescribeAnimationCurve(
        AnimationCurve curve)
    {
        if (curve == null)
            return "NULL";

        return
            $"key_count={curve.length}; " +
            $"pre_wrap={curve.preWrapMode}; " +
            $"post_wrap={curve.postWrapMode}; " +
            $"keys=[" +
            string.Join(
                ", ",
                curve.keys.Select(
                    key =>
                        $"(t={FormatFloat(key.time)}, " +
                        $"v={FormatFloat(key.value)}, " +
                        $"in={FormatFloat(key.inTangent)}, " +
                        $"out={FormatFloat(key.outTangent)}, " +
                        $"iw={FormatFloat(key.inWeight)}, " +
                        $"ow={FormatFloat(key.outWeight)}, " +
                        $"mode={key.weightedMode})")) +
            "]";
    }

    private static void WriteMetaRawText(
        StreamWriter writer,
        DataAssetInfo asset)
    {
        string metaPath =
            asset.AbsolutePath + ".meta";

        writer.WriteLine(
            "META_RAW_TEXT_BEGIN");

        if (!File.Exists(metaPath))
        {
            writer.WriteLine(
                "META_STATUS: FILE_NOT_FOUND");
        }
        else
        {
            writer.Write(
                NormalizeNewLines(
                    File.ReadAllText(
                        metaPath,
                        Encoding.UTF8)));

            writer.WriteLine();
        }

        writer.WriteLine(
            "META_RAW_TEXT_END");

        writer.WriteLine();
    }

    private static void WriteSourceRawText(
        StreamWriter writer,
        DataAssetInfo asset)
    {
        writer.WriteLine(
            "SOURCE_RAW_TEXT_BEGIN");

        if (!File.Exists(asset.AbsolutePath))
        {
            writer.WriteLine(
                "SOURCE_RAW_TEXT_STATUS: FILE_NOT_FOUND");

            writer.WriteLine(
                "SOURCE_RAW_TEXT_END");

            writer.WriteLine();

            return;
        }

        if (!RawTextExtensions.Contains(
                asset.Extension))
        {
            writer.WriteLine(
                "SOURCE_RAW_TEXT_STATUS: BINARY_OR_UNSUPPORTED_EXTENSION");

            writer.WriteLine(
                "SOURCE_RAW_TEXT_END");

            writer.WriteLine();

            return;
        }

        if (!LooksLikeTextFile(
                asset.AbsolutePath))
        {
            writer.WriteLine(
                "SOURCE_RAW_TEXT_STATUS: BINARY_CONTENT_DETECTED");

            writer.WriteLine(
                "SOURCE_RAW_TEXT_END");

            writer.WriteLine();

            return;
        }

        string text =
            File.ReadAllText(
                asset.AbsolutePath,
                Encoding.UTF8);

        writer.WriteLine(
            $"SOURCE_RAW_TEXT_LENGTH: {text.Length}");

        writer.Write(
            NormalizeNewLines(
                text));

        if (text.Length > 0 &&
            !text.EndsWith(
                "\n",
                StringComparison.Ordinal))
        {
            writer.WriteLine();
        }

        writer.WriteLine(
            "SOURCE_RAW_TEXT_END");

        writer.WriteLine();
    }

    private static bool LooksLikeTextFile(
        string absolutePath)
    {
        const int sampleLength =
            4096;

        byte[] buffer =
            new byte[sampleLength];

        using (FileStream stream =
               new FileStream(
                   absolutePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite))
        {
            int read =
                stream.Read(
                    buffer,
                    0,
                    buffer.Length);

            for (int i = 0;
                 i < read;
                 i++)
            {
                if (buffer[i] == 0)
                    return false;
            }
        }

        return true;
    }

    private static void WriteAssetError(
        StreamWriter writer,
        DataAssetInfo asset,
        Exception exception)
    {
        writer.WriteLine(
            Separator);

        writer.WriteLine(
            "ASSET_BEGIN");

        writer.WriteLine(
            $"ASSET_INDEX: {asset.Index}");

        writer.WriteLine(
            $"ASSET_PATH: {asset.AssetPath}");

        writer.WriteLine(
            "READ_STATUS: ERROR");

        writer.WriteLine(
            $"ERROR_TYPE: {Escape(exception.GetType().FullName)}");

        writer.WriteLine(
            $"ERROR_MESSAGE: {Escape(exception.Message)}");

        writer.WriteLine(
            $"ERROR_STACK_TRACE: {Escape(exception.StackTrace)}");

        writer.WriteLine(
            "ASSET_END");

        writer.WriteLine();
    }

    private static void WriteErrorSummary(
        StreamWriter writer,
        IReadOnlyList<ExportError> errors)
    {
        writer.WriteLine(
            Separator);

        writer.WriteLine(
            "EXPORT_ERRORS_BEGIN");

        writer.WriteLine(
            $"EXPORT_ERROR_COUNT: {errors.Count}");

        for (int i = 0;
             i < errors.Count;
             i++)
        {
            ExportError error =
                errors[i];

            writer.WriteLine(
                $"EXPORT_ERROR: " +
                $"index={i}; " +
                $"asset_path={Escape(error.AssetPath)}; " +
                $"exception_type={Escape(error.ExceptionType)}; " +
                $"message={Escape(error.Message)}");
        }

        writer.WriteLine(
            "EXPORT_ERRORS_END");

        writer.WriteLine();
    }

    private static void WriteDocumentFooter(
        StreamWriter writer,
        ExportContext context,
        IReadOnlyList<ExportError> errors)
    {
        writer.WriteLine(
            Separator);

        writer.WriteLine(
            "EXPORT_FOOTER_BEGIN");

        writer.WriteLine(
            $"SCANNED_ASSET_COUNT: {context.Assets.Count}");

        writer.WriteLine(
            $"DETAIL_ASSET_COUNT: {context.DetailAssets.Count}");

        writer.WriteLine(
            $"SUCCESS_COUNT: {context.DetailAssets.Count - errors.Count}");

        writer.WriteLine(
            $"ERROR_COUNT: {errors.Count}");

        writer.WriteLine(
            $"COMPLETED_AT_UTC: {DateTime.UtcNow:O}");

        writer.WriteLine(
            "EXPORT_FOOTER_END");
    }

    private static void WriteStringList(
        StreamWriter writer,
        string label,
        IEnumerable<string> values)
    {
        string[] array =
            values
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(value))
                .Distinct(
                    StringComparer.Ordinal)
                .OrderBy(
                    value => value,
                    StringComparer.Ordinal)
                .ToArray();

        writer.WriteLine(
            $"{label}_COUNT: {array.Length}");

        for (int i = 0;
             i < array.Length;
             i++)
        {
            writer.WriteLine(
                $"{label}: " +
                $"index={i}; " +
                $"path={Escape(array[i])}; " +
                $"in_data_folder={IsInSourceFolder(array[i])}");
        }
    }

    private static string DescribeObjectReference(
        UnityEngine.Object value)
    {
        if (value == null)
            return "NULL";

        string assetPath =
            AssetDatabase.GetAssetPath(
                value);

        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
            value,
            out string guid,
            out long localId);

        string globalId;

        try
        {
            globalId =
                GlobalObjectId.GetGlobalObjectIdSlow(
                    value)
                .ToString();
        }
        catch
        {
            globalId =
                string.Empty;
        }

        return
            $"name={Escape(value.name)}; " +
            $"type={Escape(GetTypeName(value.GetType()))}; " +
            $"asset_path={Escape(assetPath)}; " +
            $"guid={Escape(guid)}; " +
            $"local_file_id={localId}; " +
            $"global_id={Escape(globalId)}; " +
            $"instance_id={value.GetInstanceID()}";
    }

    private static bool IsModelAsset(
        DataAssetInfo asset)
    {
        if (asset == null)
            return false;

        return IsModelAsset(
            asset.AssetPath,
            asset.MainType);
    }

    private static bool IsModelAsset(
        string assetPath,
        Type mainType)
    {
        if (mainType != typeof(GameObject))
            return false;

        string extension =
            Path.GetExtension(
                assetPath)
            .ToLowerInvariant();

        return
            extension == ".fbx" ||
            extension == ".obj" ||
            extension == ".dae" ||
            extension == ".blend" ||
            extension == ".3ds" ||
            extension == ".dxf";
    }

    private static string SanitizeFileNameToken(
        string value)
    {
        string safe =
            string.IsNullOrWhiteSpace(value)
                ? "UnnamedModel"
                : value.Trim();

        foreach (char invalid
                 in Path.GetInvalidFileNameChars())
        {
            safe =
                safe.Replace(
                    invalid,
                    '_');
        }

        StringBuilder builder =
            new StringBuilder(
                safe.Length);

        foreach (char character
                 in safe)
        {
            if (char.IsLetterOrDigit(character) ||
                character == '_' ||
                character == '-')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        string result =
            builder
                .ToString()
                .Trim('_');

        return
            string.IsNullOrWhiteSpace(result)
                ? "UnnamedModel"
                : result;
    }

    private static string ClassifyAsset(
        string assetPath,
        Type mainType)
    {
        if (mainType == null)
            return "UNKNOWN";

        string extension =
            Path.GetExtension(
                assetPath)
            .ToLowerInvariant();

        if (typeof(ScriptableObject).IsAssignableFrom(mainType))
            return "SCRIPTABLE_OBJECT";

        if (mainType == typeof(AnimatorController))
            return "ANIMATOR_CONTROLLER";

        if (mainType == typeof(AnimatorOverrideController))
            return "ANIMATOR_OVERRIDE_CONTROLLER";

        if (mainType == typeof(AnimationClip))
            return "ANIMATION_CLIP";

        if (mainType == typeof(Material))
            return "MATERIAL";

        if (mainType == typeof(Texture2D))
            return "TEXTURE";

        if (mainType == typeof(AudioClip))
            return "AUDIO";

        if (mainType == typeof(GameObject))
        {
            if (IsModelAsset(
                    assetPath,
                    mainType))
            {
                return "MODEL";
            }

            return "PREFAB_OR_MODEL";
        }

        if (mainType == typeof(Shader))
            return "SHADER";

        if (mainType == typeof(TextAsset))
            return "TEXT_ASSET";

        if (extension == ".vfx")
            return "VFX_GRAPH";

        if (extension == ".shadergraph" ||
            extension == ".shadersubgraph")
        {
            return "SHADER_GRAPH";
        }

        if (extension == ".playable")
            return "TIMELINE";

        return
            string.IsNullOrWhiteSpace(mainType?.Name)
                ? "UNKNOWN"
                : mainType.Name.ToUpperInvariant();
    }

    private static bool IsInSourceFolder(
        string path)
    {
        return
            !string.IsNullOrWhiteSpace(path) &&
            (string.Equals(
                 path,
                 SourceFolder,
                 StringComparison.Ordinal) ||
             path.StartsWith(
                 SourceFolder + "/",
                 StringComparison.Ordinal));
    }

    private static string GetTypeName(
        Type type)
    {
        if (type == null)
            return "NULL";

        return
            string.IsNullOrWhiteSpace(type.FullName)
                ? type.Name
                : type.FullName;
    }

    private static string ComputeSha256(
        string absolutePath)
    {
        using (SHA256 sha256 =
               SHA256.Create())
        using (FileStream stream =
               new FileStream(
                   absolutePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.ReadWrite))
        {
            byte[] hash =
                sha256.ComputeHash(
                    stream);

            return string.Concat(
                hash.Select(
                    value =>
                        value.ToString(
                            "x2",
                            Invariant)));
        }
    }

    private static void ReplaceFileAtomically(
        string temporaryPath,
        string outputPath)
    {
        string outputDirectory =
            Path.GetDirectoryName(
                outputPath);

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(
                outputDirectory);
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
        if (!string.IsNullOrWhiteSpace(path) &&
            File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string NormalizePath(
        string path)
    {
        return path?.Replace(
            '\\',
            '/');
    }

    private static string NormalizeNewLines(
        string text)
    {
        return (text ?? string.Empty)
            .Replace(
                "\r\n",
                "\n")
            .Replace(
                "\r",
                "\n");
    }

    private static string Escape(
        object value)
    {
        if (value == null)
            return string.Empty;

        return value
            .ToString()
            .Replace(
                "\\",
                "\\\\")
            .Replace(
                "\r",
                "\\r")
            .Replace(
                "\n",
                "\\n")
            .Replace(
                "\t",
                "\\t");
    }

    private static string JoinEscaped(
        IEnumerable<string> values)
    {
        if (values == null)
            return "[]";

        return
            "[" +
            string.Join(
                ", ",
                values.Select(
                    value =>
                        $"\"{Escape(value)}\"")) +
            "]";
    }

    private static string FormatFloat(
        float value)
    {
        return value.ToString(
            "R",
            Invariant);
    }

    private static string FormatVector2(
        Vector2 value)
    {
        return
            $"({FormatFloat(value.x)}, " +
            $"{FormatFloat(value.y)})";
    }

    private static string FormatVector3(
        Vector3 value)
    {
        return
            $"({FormatFloat(value.x)}, " +
            $"{FormatFloat(value.y)}, " +
            $"{FormatFloat(value.z)})";
    }

    private static string FormatVector4(
        Vector4 value)
    {
        return
            $"({FormatFloat(value.x)}, " +
            $"{FormatFloat(value.y)}, " +
            $"{FormatFloat(value.z)}, " +
            $"{FormatFloat(value.w)})";
    }

    private static string FormatQuaternion(
        Quaternion value)
    {
        return
            $"({FormatFloat(value.x)}, " +
            $"{FormatFloat(value.y)}, " +
            $"{FormatFloat(value.z)}, " +
            $"{FormatFloat(value.w)})";
    }

    private static string FormatColor(
        Color value)
    {
        return
            $"rgba(" +
            $"{FormatFloat(value.r)}, " +
            $"{FormatFloat(value.g)}, " +
            $"{FormatFloat(value.b)}, " +
            $"{FormatFloat(value.a)})";
    }

    private static string FormatRect(
        Rect value)
    {
        return
            $"x={FormatFloat(value.x)}; " +
            $"y={FormatFloat(value.y)}; " +
            $"width={FormatFloat(value.width)}; " +
            $"height={FormatFloat(value.height)}";
    }

    private static string FormatBounds(
        Bounds value)
    {
        return
            $"center={FormatVector3(value.center)}; " +
            $"size={FormatVector3(value.size)}; " +
            $"min={FormatVector3(value.min)}; " +
            $"max={FormatVector3(value.max)}";
    }

    private static void RecreateDirectory(
        string path)
    {
        DeleteDirectoryIfExists(
            path);

        Directory.CreateDirectory(
            path);
    }

    private static void DeleteDirectoryIfExists(
        string path)
    {
        if (!string.IsNullOrWhiteSpace(path) &&
            Directory.Exists(path))
        {
            Directory.Delete(
                path,
                recursive: true);
        }
    }

    private static string FormatBytes(
        long bytes)
    {
        const double kib =
            1024d;

        const double mib =
            kib * 1024d;

        const double gib =
            mib * 1024d;

        if (bytes >= gib)
        {
            return
                $"{bytes / gib:0.00} GiB";
        }

        if (bytes >= mib)
        {
            return
                $"{bytes / mib:0.00} MiB";
        }

        if (bytes >= kib)
        {
            return
                $"{bytes / kib:0.00} KiB";
        }

        return
            $"{bytes} B";
    }

    private enum DataExportMode
    {
        Core,
        Models,
        All
    }

    private sealed class ExportProfile
    {
        private ExportProfile(
            DataExportMode mode,
            string displayName,
            string outputSubfolder,
            string manifestFileName,
            string filePrefix,
            bool splitEachDetailAsset)
        {
            Mode =
                mode;

            DisplayName =
                displayName;

            RelativeOutputFolder =
                $"{OutputFolder}/{outputSubfolder}";

            ManifestFileName =
                manifestFileName;

            ManifestZipFileName =
                Path.GetFileNameWithoutExtension(
                    manifestFileName) +
                ".zip";

            FilePrefix =
                filePrefix;

            SplitEachDetailAsset =
                splitEachDetailAsset;
        }

        public DataExportMode Mode
        {
            get;
        }

        public string DisplayName
        {
            get;
        }

        public string RelativeOutputFolder
        {
            get;
        }

        public string ManifestFileName
        {
            get;
        }

        public string ManifestZipFileName
        {
            get;
        }

        public string FilePrefix
        {
            get;
        }

        public bool SplitEachDetailAsset
        {
            get;
        }

        public bool ShouldExportDetail(
            DataAssetInfo asset)
        {
            switch (Mode)
            {
                case DataExportMode.Core:
                    return !IsModelAsset(asset);

                case DataExportMode.Models:
                    return IsModelAsset(asset);

                case DataExportMode.All:
                    return true;

                default:
                    return false;
            }
        }

        public string GetPartFileName(
            int partIndex,
            DataAssetInfo currentAsset)
        {
            if (Mode == DataExportMode.Models &&
                currentAsset != null)
            {
                string modelName =
                    SanitizeFileNameToken(
                        Path.GetFileNameWithoutExtension(
                            currentAsset.FileName));

                return
                    $"Project_Abyss_Model_{modelName}_Part_" +
                    $"{partIndex:D3}_For_AI.txt";
            }

            return
                $"{FilePrefix}_Part_{partIndex:D3}_For_AI.txt";
        }

        public static ExportProfile CreateCore()
        {
            return
                new ExportProfile(
                    DataExportMode.Core,
                    "Core Data",
                    "Core",
                    "Project_Abyss_Core_Data_Manifest_For_AI.txt",
                    "Project_Abyss_Core_Data",
                    splitEachDetailAsset: false);
        }

        public static ExportProfile CreateModels()
        {
            return
                new ExportProfile(
                    DataExportMode.Models,
                    "Model Data",
                    "Models",
                    "Project_Abyss_Model_Data_Manifest_For_AI.txt",
                    "Project_Abyss_Model_Data",
                    splitEachDetailAsset: true);
        }

        public static ExportProfile CreateAll()
        {
            return
                new ExportProfile(
                    DataExportMode.All,
                    "All Data",
                    "All",
                    "Project_Abyss_All_Data_Manifest_For_AI.txt",
                    "Project_Abyss_All_Data",
                    splitEachDetailAsset: false);
        }
    }

    private sealed class ExportContext
    {
        public ExportContext(
            string projectRoot,
            List<DataAssetInfo> assets,
            List<DataAssetInfo> detailAssets,
            ExportProfile profile)
        {
            ProjectRoot =
                projectRoot;

            Assets =
                assets;

            DetailAssets =
                detailAssets;

            Profile =
                profile;

            AssetsByPath =
                assets.ToDictionary(
                    asset => asset.AssetPath,
                    asset => asset,
                    StringComparer.Ordinal);

            DetailAssetPaths =
                new HashSet<string>(
                    detailAssets.Select(
                        asset => asset.AssetPath),
                    StringComparer.Ordinal);
        }

        public string ProjectRoot
        {
            get;
        }

        public List<DataAssetInfo> Assets
        {
            get;
        }

        public List<DataAssetInfo> DetailAssets
        {
            get;
        }

        public ExportProfile Profile
        {
            get;
        }

        public Dictionary<string, DataAssetInfo> AssetsByPath
        {
            get;
        }

        public HashSet<string> DetailAssetPaths
        {
            get;
        }
    }

    private sealed class DataAssetInfo
    {
        public int Index;
        public string AssetPath;
        public string AbsolutePath;
        public string Guid;
        public string Extension;
        public string FileName;
        public Type MainType;
        public string MainTypeName;
        public string MainObjectName;
        public string MainObjectGuid;
        public long MainObjectLocalId;
        public string ImporterTypeName;
        public string Category;
        public long FileSizeBytes;
        public DateTime LastWriteUtc;
        public string Sha256;
        public string[] Labels =
            Array.Empty<string>();

        public string[] DirectDependencies =
            Array.Empty<string>();

        public string[] RecursiveDependencies =
            Array.Empty<string>();

        public readonly List<string>
            InternalIncomingReferences =
                new List<string>();

        public readonly List<string>
            ProjectIncomingReferences =
                new List<string>();
    }

    private sealed class ExportError
    {
        public ExportError(
            string assetPath,
            Exception exception)
        {
            AssetPath =
                assetPath;

            ExceptionType =
                exception.GetType().FullName;

            Message =
                exception.Message;
        }

        public string AssetPath
        {
            get;
        }

        public string ExceptionType
        {
            get;
        }

        public string Message
        {
            get;
        }
    }

    private sealed class ExportSummary
    {
        public ExportSummary(
            int detailAssetCount,
            int partCount,
            long totalTextBytes,
            long totalZipBytes)
        {
            DetailAssetCount =
                detailAssetCount;

            PartCount =
                partCount;

            TotalTextBytes =
                totalTextBytes;

            TotalZipBytes =
                totalZipBytes;
        }

        public int DetailAssetCount
        {
            get;
        }

        public int PartCount
        {
            get;
        }

        public long TotalTextBytes
        {
            get;
        }

        public long TotalZipBytes
        {
            get;
        }
    }

    private sealed class AssetPlacementInfo
    {
        public AssetPlacementInfo(
            DataAssetInfo asset)
        {
            Asset =
                asset;
        }

        public DataAssetInfo Asset
        {
            get;
        }

        public List<int> PartIndexes
        {
            get;
        } =
            new List<int>();
    }

    private sealed class PartInfo
    {
        public int PartIndex;
        public string TextFileName;
        public string TextAbsolutePath;
        public long TextBytes;
        public string TextSha256;
        public string ZipFileName;
        public string ZipAbsolutePath;
        public long ZipBytes;
        public string ZipSha256;
        public int FirstAssetIndex;
        public int LastAssetIndex;

        public readonly List<string>
            AssetPaths =
                new List<string>();
    }

    private sealed class PartOutput :
        IDisposable
    {
        private readonly FileStream stream;
        private readonly StreamWriter writer;
        private readonly HashSet<string> assetPathSet =
            new HashSet<string>(
                StringComparer.Ordinal);

        private bool completed;

        public PartOutput(
            int partIndex,
            string fileName,
            string absolutePath,
            ExportContext context)
        {
            PartIndex =
                partIndex;

            FileName =
                fileName;

            AbsolutePath =
                absolutePath;

            stream =
                new FileStream(
                    absolutePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 64 * 1024,
                    options: FileOptions.SequentialScan);

            writer =
                new StreamWriter(
                    stream,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 64 * 1024,
                    leaveOpen: true);

            writer.NewLine =
                "\n";

            writer.WriteLine(
                "PROJECT_ABYSS_DATA_ASSET_PART");

            writer.WriteLine(
                $"FORMAT_VERSION: {FormatVersion}");

            writer.WriteLine(
                $"PART_INDEX: {PartIndex}");

            writer.WriteLine(
                $"EXPORTED_AT_UTC: {DateTime.UtcNow:O}");

            writer.WriteLine(
                $"UNITY_VERSION: {Application.unityVersion}");

            writer.WriteLine(
                $"SOURCE_FOLDER: {SourceFolder}");

            writer.WriteLine(
                $"EXPORT_MODE: {context.Profile.Mode}");

            writer.WriteLine(
                $"OUTPUT_FOLDER: {context.Profile.RelativeOutputFolder}");

            writer.WriteLine(
                $"TOTAL_PROJECT_ASSET_COUNT: {context.Assets.Count}");

            writer.WriteLine(
                $"DETAIL_ASSET_COUNT: {context.DetailAssets.Count}");

            writer.WriteLine(
                $"MAXIMUM_PART_BYTES: {MaximumPartBytes}");

            writer.WriteLine(
                $"MAXIMUM_PART_SIZE: {FormatBytes(MaximumPartBytes)}");

            writer.WriteLine(
                Separator);

            writer.WriteLine();

            writer.WriteLine(
                "ASSET_DETAILS_BEGIN");

            writer.WriteLine();
        }

        public int PartIndex
        {
            get;
        }

        public string FileName
        {
            get;
        }

        public string AbsolutePath
        {
            get;
        }

        public int AssetCount =>
            assetPathSet.Count;

        public int FirstAssetIndex
        {
            get;
            private set;
        }

        public int LastAssetIndex
        {
            get;
            private set;
        }

        public long LengthBytes
        {
            get
            {
                FlushText();
                return stream.Position;
            }
        }

        public long RemainingPayloadBytes =>
            Math.Max(
                0L,
                MaximumPartBytes -
                PartFooterReserveBytes -
                LengthBytes);

        public bool CanFit(
            long additionalBytes)
        {
            return
                additionalBytes >= 0 &&
                LengthBytes +
                additionalBytes +
                PartFooterReserveBytes <=
                MaximumPartBytes;
        }

        public void AddAsset(
            DataAssetInfo asset,
            AssetPlacementInfo placement)
        {
            if (asset == null ||
                placement == null)
            {
                return;
            }

            if (assetPathSet.Add(
                    asset.AssetPath))
            {
                if (FirstAssetIndex == 0)
                {
                    FirstAssetIndex =
                        asset.Index;
                }

                LastAssetIndex =
                    asset.Index;
            }

            if (!placement.PartIndexes.Contains(
                    PartIndex))
            {
                placement.PartIndexes.Add(
                    PartIndex);
            }
        }

        public void AppendFile(
            string path)
        {
            FlushText();

            using (FileStream source =
                   new FileStream(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: CopyBufferBytes,
                       options: FileOptions.SequentialScan))
            {
                source.CopyTo(
                    stream,
                    CopyBufferBytes);
            }
        }

        public void AppendRawBytes(
            byte[] buffer,
            int count)
        {
            stream.Write(
                buffer,
                0,
                count);
        }

        public void WriteLine()
        {
            writer.WriteLine();
        }

        public void WriteLine(
            string value)
        {
            writer.WriteLine(
                value);
        }

        public void FlushText()
        {
            writer.Flush();
            stream.Flush();
        }

        public PartInfo Complete(
            ExportContext context)
        {
            if (completed)
            {
                throw new InvalidOperationException(
                    $"Part가 이미 완료되었습니다. Part={PartIndex}");
            }

            writer.WriteLine();

            writer.WriteLine(
                "ASSET_DETAILS_END");

            writer.WriteLine();

            writer.WriteLine(
                Separator);

            writer.WriteLine(
                "PART_FOOTER_BEGIN");

            writer.WriteLine(
                $"PART_INDEX: {PartIndex}");

            writer.WriteLine(
                $"PART_ASSET_COUNT: {AssetCount}");

            writer.WriteLine(
                $"FIRST_ASSET_INDEX: {FirstAssetIndex}");

            writer.WriteLine(
                $"LAST_ASSET_INDEX: {LastAssetIndex}");

            writer.WriteLine(
                $"TOTAL_PROJECT_ASSET_COUNT: {context.Assets.Count}");

            writer.WriteLine(
                $"EXPORT_MODE: {context.Profile.Mode}");

            writer.WriteLine(
                $"COMPLETED_AT_UTC: {DateTime.UtcNow:O}");

            writer.WriteLine(
                "PART_FOOTER_END");

            FlushText();

            completed =
                true;

            writer.Dispose();
            stream.Dispose();

            FileInfo fileInfo =
                new FileInfo(
                    AbsolutePath);

            PartInfo info =
                new PartInfo
                {
                    PartIndex =
                        PartIndex,
                    TextFileName =
                        FileName,
                    TextAbsolutePath =
                        AbsolutePath,
                    TextBytes =
                        fileInfo.Length,
                    TextSha256 =
                        ComputeSha256(
                            AbsolutePath),
                    FirstAssetIndex =
                        FirstAssetIndex,
                    LastAssetIndex =
                        LastAssetIndex
                };

            info.AssetPaths.AddRange(
                assetPathSet.OrderBy(
                    path => path,
                    StringComparer.Ordinal));

            return info;
        }

        public void Dispose()
        {
            if (completed)
                return;

            completed =
                true;

            writer.Dispose();
            stream.Dispose();
        }
    }

    private sealed class FolderNode
    {
        private readonly SortedDictionary<string, FolderNode>
            folders =
                new SortedDictionary<string, FolderNode>(
                    StringComparer.Ordinal);

        private readonly List<DataAssetInfo>
            assets =
                new List<DataAssetInfo>();

        public FolderNode(
            string name)
        {
            Name =
                name;
        }

        public string Name
        {
            get;
        }

        public void Add(
            IReadOnlyList<string> segments,
            DataAssetInfo asset)
        {
            if (segments.Count <= 1)
            {
                assets.Add(
                    asset);

                return;
            }

            string folderName =
                segments[0];

            if (!folders.TryGetValue(
                    folderName,
                    out FolderNode child))
            {
                child =
                    new FolderNode(
                        folderName);

                folders.Add(
                    folderName,
                    child);
            }

            child.Add(
                segments
                    .Skip(1)
                    .ToArray(),
                asset);
        }

        public void Write(
            StreamWriter writer,
            int indent)
        {
            string prefix =
                new string(
                    ' ',
                    indent * 2);

            writer.WriteLine(
                $"{prefix}- FOLDER name=\"{Escape(Name)}\"");

            foreach (FolderNode child
                     in folders.Values)
            {
                child.Write(
                    writer,
                    indent + 1);
            }

            foreach (DataAssetInfo asset
                     in assets.OrderBy(
                         item => item.FileName,
                         StringComparer.Ordinal))
            {
                writer.WriteLine(
                    $"{prefix}  - ASSET " +
                    $"index={asset.Index}; " +
                    $"category={Escape(asset.Category)}; " +
                    $"type={Escape(asset.MainTypeName)}; " +
                    $"name={Escape(asset.FileName)}");
            }
        }
    }
}

#endif
