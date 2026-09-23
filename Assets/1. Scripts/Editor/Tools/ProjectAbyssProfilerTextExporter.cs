#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Project Abyss - Unity Profiler -> lossless compact TXT/ZIP exporter.
///
/// 목표:
/// - Profiler가 캡처한 Hierarchy / Raw Sample / Metadata 정보를 버리지 않는다.
/// - 반복되는 긴 문자열은 String Table로 한 번만 기록한다.
/// - 기본 출력은 ZIP 안의 compact TXT로 직접 스트리밍하여 거대한 임시 TXT를 만들지 않는다.
/// - AI/사람이 다시 원본 트리/샘플 순서를 복원할 수 있도록 frame/thread/sample index,
///   depth, child count, timing, marker/category, metadata를 모두 보존한다.
///
/// Compact TXT record:
///   F = frame
///   T = thread
///   H = hierarchy node
///   R = raw sample
///   S = string-table entry
/// </summary>
public sealed class ProjectAbyssProfilerTextExporter : EditorWindow
{
    private enum RangeMode
    {
        SelectedFrame = 0,
        RecentFrames = 1,
        ExplicitRange = 2
    }

    private enum OutputMode
    {
        LosslessCompactZip = 0,
        PlainCompactTxt = 1
    }

    private sealed class StringTable
    {
        private readonly Dictionary<string, int> idByValue =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly List<string> values =
            new List<string>();

        public StringTable()
        {
            // 0은 empty/null 예약.
            values.Add(string.Empty);
            idByValue[string.Empty] = 0;
        }

        public int Count => values.Count;

        public int Intern(string value)
        {
            value ??= string.Empty;

            if (idByValue.TryGetValue(value, out int id))
                return id;

            id = values.Count;
            values.Add(value);
            idByValue.Add(value, id);
            return id;
        }

        public void WriteTo(StreamWriter writer)
        {
            writer.WriteLine();
            writer.WriteLine("[STRING_TABLE]");
            writer.WriteLine("# S<TAB>Id<TAB>Value");

            for (int i = 0; i < values.Count; i++)
            {
                writer.Write("S\t");
                writer.Write(i.ToString(CultureInfo.InvariantCulture));
                writer.Write('\t');
                writer.WriteLine(EscapeField(values[i]));
            }

            writer.WriteLine($"STRING_TABLE_COUNT={values.Count}");
        }
    }

    private const string MenuPath =
        "Tools/Project Abyss/Exports/Profiler Text Exporter";

    private const string DefaultDirectory =
        "Logs/ProfilerExports";

    [SerializeField]
    private RangeMode rangeMode = RangeMode.SelectedFrame;

    [SerializeField, Min(1)]
    private int recentFrameCount = 5;

    [SerializeField]
    private long explicitStartFrame;

    [SerializeField]
    private long explicitEndFrame;

    [SerializeField]
    private OutputMode outputMode = OutputMode.LosslessCompactZip;

    [SerializeField]
    private bool losslessFullCapture = true;

    [SerializeField]
    private bool includeHierarchy = true;

    [SerializeField]
    private bool includeRawSamples = true;

    [SerializeField]
    private bool includeSampleMetadata = true;

    [SerializeField]
    private bool includeAllThreads = true;

    [SerializeField]
    private string threadNameFilter = "";

    [SerializeField]
    private string sampleNameFilter = "";

    [SerializeField, Min(0f)]
    private float minimumSampleTimeMs = 0f;

    [SerializeField, Min(0)]
    private int maxRawSamplesPerThread = 0;

    [SerializeField]
    private bool revealAfterExport = true;

    private Vector2 scroll;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        ProjectAbyssProfilerTextExporter window =
            GetWindow<ProjectAbyssProfilerTextExporter>();

        window.titleContent =
            new GUIContent("Profiler Export");

        window.minSize =
            new Vector2(560f, 600f);

        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(6);

        EditorGUILayout.LabelField(
            "Project Abyss · Profiler Export",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "기본값은 Lossless Compact ZIP입니다. Raw sample을 삭제하지 않고, " +
            "반복 문자열을 String Table ID로 치환한 뒤 ZIP으로 무손실 압축합니다. " +
            "따라서 기존 1GB급 TXT보다 훨씬 작게 만들면서 분석 정보는 유지합니다.",
            MessageType.Info);

        DrawProfilerStatus();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(
            "Frame Range",
            EditorStyles.boldLabel);

        rangeMode =
            (RangeMode)EditorGUILayout.EnumPopup(
                "Range",
                rangeMode);

        switch (rangeMode)
        {
            case RangeMode.RecentFrames:
                recentFrameCount =
                    Mathf.Max(
                        1,
                        EditorGUILayout.IntField(
                            "Recent Frames",
                            recentFrameCount));
                break;

            case RangeMode.ExplicitRange:
                explicitStartFrame =
                    EditorGUILayout.LongField(
                        "Start Frame",
                        explicitStartFrame);

                explicitEndFrame =
                    EditorGUILayout.LongField(
                        "End Frame",
                        explicitEndFrame);
                break;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(
            "Output",
            EditorStyles.boldLabel);

        outputMode =
            (OutputMode)EditorGUILayout.EnumPopup(
                "Mode",
                outputMode);

        if (outputMode == OutputMode.LosslessCompactZip)
        {
            EditorGUILayout.HelpBox(
                "추천. TXT를 임시 파일로 만들지 않고 ZIP entry에 직접 기록합니다. " +
                "ZIP 내부에는 일반 UTF-8 .txt 파일이 들어 있습니다.",
                MessageType.None);
        }

        losslessFullCapture =
            EditorGUILayout.ToggleLeft(
                "Lossless full capture (recommended)",
                losslessFullCapture);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(
            "Export Contents",
            EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(losslessFullCapture))
        {
            includeHierarchy =
                EditorGUILayout.ToggleLeft(
                    "CPU Hierarchy",
                    includeHierarchy);

            includeRawSamples =
                EditorGUILayout.ToggleLeft(
                    "Raw Profiler Samples",
                    includeRawSamples);

            using (new EditorGUI.DisabledScope(!includeRawSamples))
            {
                includeSampleMetadata =
                    EditorGUILayout.ToggleLeft(
                        "Raw sample metadata",
                        includeSampleMetadata);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(
                "Optional lossy filters",
                EditorStyles.miniBoldLabel);

            includeAllThreads =
                EditorGUILayout.ToggleLeft(
                    "All threads",
                    includeAllThreads);

            using (new EditorGUI.DisabledScope(includeAllThreads))
            {
                threadNameFilter =
                    EditorGUILayout.TextField(
                        "Thread contains",
                        threadNameFilter ?? string.Empty);
            }

            sampleNameFilter =
                EditorGUILayout.TextField(
                    "Sample contains",
                    sampleNameFilter ?? string.Empty);

            minimumSampleTimeMs =
                Mathf.Max(
                    0f,
                    EditorGUILayout.FloatField(
                        "Min sample time (ms)",
                        minimumSampleTimeMs));

            maxRawSamplesPerThread =
                Mathf.Max(
                    0,
                    EditorGUILayout.IntField(
                        "Max raw samples / thread",
                        maxRawSamplesPerThread));
        }

        if (losslessFullCapture)
        {
            EditorGUILayout.HelpBox(
                "Lossless full capture에서는 Hierarchy + Raw + Metadata + 모든 Thread를 " +
                "강제로 포함하고 sample/time/count filter를 적용하지 않습니다.",
                MessageType.None);
        }

        revealAfterExport =
            EditorGUILayout.ToggleLeft(
                "Reveal export after completion",
                revealAfterExport);

        EditorGUILayout.Space(14);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(
                    outputMode == OutputMode.LosslessCompactZip
                        ? "Export Compact ZIP"
                        : "Export Compact TXT",
                    GUILayout.Height(34)))
            {
                Export();
            }

            if (GUILayout.Button(
                    "Open Export Folder",
                    GUILayout.Height(34)))
            {
                string directory =
                    GetAbsoluteExportDirectory();

                Directory.CreateDirectory(directory);
                EditorUtility.RevealInFinder(directory);
            }
        }

        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "AutoPlan 분석 추천: Profiler Record → 승률/피해량 버튼 → Pause → spike frame 선택 → " +
            "Selected Frame + Lossless Compact ZIP. 여러 프레임이 필요할 때만 Recent Frames를 사용하세요.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private static ProfilerWindow FindProfilerWindow()
    {
        ProfilerWindow[] windows =
            Resources.FindObjectsOfTypeAll<ProfilerWindow>();

        if (windows == null ||
            windows.Length == 0)
        {
            return null;
        }

        return windows[0];
    }

    private void DrawProfilerStatus()
    {
        ProfilerWindow profiler =
            FindProfilerWindow();

        EditorGUILayout.Space(6);

        if (profiler == null)
        {
            EditorGUILayout.HelpBox(
                "Profiler Window가 열려 있지 않습니다. " +
                "Window > Analysis > Profiler를 열고 캡처하세요.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField(
            "Profiler available frames",
            $"{profiler.firstAvailableFrameIndex} ~ " +
            $"{profiler.lastAvailableFrameIndex}");

        EditorGUILayout.LabelField(
            "Selected frame",
            profiler.selectedFrameIndex.ToString());
    }

    private void Export()
    {
        ProfilerWindow profiler =
            FindProfilerWindow();

        if (profiler == null)
        {
            EditorUtility.DisplayDialog(
                "Profiler Export",
                "Profiler Window가 없습니다. 먼저 Profiler를 열고 데이터를 캡처하세요.",
                "OK");
            return;
        }

        if (!TryResolveRange(
                profiler,
                out int startFrame,
                out int endFrame,
                out string error))
        {
            EditorUtility.DisplayDialog(
                "Profiler Export",
                error,
                "OK");
            return;
        }

        bool effectiveHierarchy =
            losslessFullCapture || includeHierarchy;

        bool effectiveRaw =
            losslessFullCapture || includeRawSamples;

        bool effectiveMetadata =
            losslessFullCapture || includeSampleMetadata;

        if (!effectiveHierarchy &&
            !effectiveRaw)
        {
            EditorUtility.DisplayDialog(
                "Profiler Export",
                "Hierarchy 또는 Raw Samples 중 최소 하나를 선택하세요.",
                "OK");
            return;
        }

        string directory =
            GetAbsoluteExportDirectory();

        Directory.CreateDirectory(directory);

        string stem =
            $"Profiler_{DateTime.Now:yyyyMMdd_HHmmss}_" +
            $"F{startFrame}-{endFrame}_compact";

        string exportPath =
            outputMode == OutputMode.LosslessCompactZip
                ? Path.Combine(directory, stem + ".txt.zip")
                : Path.Combine(directory, stem + ".txt");

        try
        {
            StringTable strings =
                new StringTable();

            if (outputMode == OutputMode.LosslessCompactZip)
            {
                WriteZipExport(
                    exportPath,
                    stem + ".txt",
                    profiler,
                    startFrame,
                    endFrame,
                    strings,
                    effectiveHierarchy,
                    effectiveRaw,
                    effectiveMetadata);
            }
            else
            {
                using FileStream stream =
                    new FileStream(
                        exportPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        1024 * 1024);

                using StreamWriter writer =
                    CreateWriter(stream);

                WriteExport(
                    writer,
                    profiler,
                    startFrame,
                    endFrame,
                    strings,
                    effectiveHierarchy,
                    effectiveRaw,
                    effectiveMetadata);
            }

            Debug.Log(
                "[Profiler Export] Complete: " +
                exportPath);

            if (revealAfterExport)
            {
                EditorUtility.RevealInFinder(exportPath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);

            EditorUtility.DisplayDialog(
                "Profiler Export",
                "Export 실패:\n" + ex,
                "OK");
        }
    }

    private static StreamWriter CreateWriter(Stream stream)
    {
        return new StreamWriter(
            stream,
            new System.Text.UTF8Encoding(false),
            1024 * 1024,
            leaveOpen: false);
    }

    private void WriteZipExport(
        string zipPath,
        string entryName,
        ProfilerWindow profiler,
        int startFrame,
        int endFrame,
        StringTable strings,
        bool effectiveHierarchy,
        bool effectiveRaw,
        bool effectiveMetadata)
    {
        using FileStream fileStream =
            new FileStream(
                zipPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                1024 * 1024);

        using ZipArchive archive =
            new ZipArchive(
                fileStream,
                ZipArchiveMode.Create,
                leaveOpen: false);

        ZipArchiveEntry entry =
            archive.CreateEntry(
                entryName,
                System.IO.Compression.CompressionLevel.Optimal);

        using Stream entryStream =
            entry.Open();

        using StreamWriter writer =
            CreateWriter(entryStream);

        WriteExport(
            writer,
            profiler,
            startFrame,
            endFrame,
            strings,
            effectiveHierarchy,
            effectiveRaw,
            effectiveMetadata);
    }

    private void WriteExport(
        StreamWriter writer,
        ProfilerWindow profiler,
        int startFrame,
        int endFrame,
        StringTable strings,
        bool effectiveHierarchy,
        bool effectiveRaw,
        bool effectiveMetadata)
    {
        WriteHeader(
            writer,
            profiler,
            startFrame,
            endFrame,
            effectiveHierarchy,
            effectiveRaw,
            effectiveMetadata);

        for (int frame =
             startFrame;
             frame <= endFrame;
             frame++)
        {
            WriteFrame(
                writer,
                frame,
                strings,
                effectiveHierarchy,
                effectiveRaw,
                effectiveMetadata);
        }

        strings.WriteTo(writer);

        writer.WriteLine();
        writer.WriteLine("EXPORT_END=TRUE");
        writer.Flush();
    }

    private bool TryResolveRange(
        ProfilerWindow profiler,
        out int startFrame,
        out int endFrame,
        out string error)
    {
        startFrame = -1;
        endFrame = -1;
        error = null;

        long first =
            profiler.firstAvailableFrameIndex;

        long last =
            profiler.lastAvailableFrameIndex;

        if (first < 0 ||
            last < first)
        {
            error =
                "Profiler에 사용 가능한 프레임이 없습니다.";
            return false;
        }

        long start;
        long end;

        switch (rangeMode)
        {
            case RangeMode.SelectedFrame:
            {
                long selected =
                    profiler.selectedFrameIndex;

                if (selected < first ||
                    selected > last)
                {
                    error =
                        "Profiler에서 유효한 프레임을 선택하세요.";
                    return false;
                }

                start = selected;
                end = selected;
                break;
            }

            case RangeMode.RecentFrames:
            {
                end = last;
                start =
                    Math.Max(
                        first,
                        last - Math.Max(1, recentFrameCount) + 1);
                break;
            }

            case RangeMode.ExplicitRange:
            {
                start =
                    Math.Max(first, explicitStartFrame);

                end =
                    Math.Min(last, explicitEndFrame);

                if (end < start)
                {
                    error =
                        $"잘못된 범위입니다. Available={first}~{last}";
                    return false;
                }

                break;
            }

            default:
                error = "지원하지 않는 frame range mode입니다.";
                return false;
        }

        if (start > int.MaxValue ||
            end > int.MaxValue)
        {
            error =
                "Frame index가 int 범위를 초과합니다.";
            return false;
        }

        startFrame = (int)start;
        endFrame = (int)end;
        return true;
    }

    private void WriteHeader(
        StreamWriter writer,
        ProfilerWindow profiler,
        int startFrame,
        int endFrame,
        bool effectiveHierarchy,
        bool effectiveRaw,
        bool effectiveMetadata)
    {
        writer.WriteLine(
            "PROJECT_ABYSS_PROFILER_COMPACT_TEXT_EXPORT_V2");

        writer.WriteLine(
            "FORMAT=LOSSLESS_STRING_TABLE");

        writer.WriteLine(
            "====================================");

        writer.WriteLine(
            $"GeneratedAt={DateTime.Now:O}");

        writer.WriteLine(
            $"UnityVersion={Application.unityVersion}");

        writer.WriteLine(
            $"Project={EscapeField(Application.productName)}");

        writer.WriteLine(
            $"Platform={Application.platform}");

        writer.WriteLine(
            $"FrameRange={startFrame}..{endFrame}");

        writer.WriteLine(
            $"ProfilerAvailable=" +
            $"{profiler.firstAvailableFrameIndex}.." +
            $"{profiler.lastAvailableFrameIndex}");

        writer.WriteLine(
            $"ProfilerSelected=" +
            $"{profiler.selectedFrameIndex}");

        writer.WriteLine(
            $"LosslessFullCapture={losslessFullCapture}");

        writer.WriteLine(
            $"IncludeHierarchy={effectiveHierarchy}");

        writer.WriteLine(
            $"IncludeRawSamples={effectiveRaw}");

        writer.WriteLine(
            $"IncludeSampleMetadata={effectiveMetadata}");

        if (!losslessFullCapture)
        {
            writer.WriteLine(
                $"AllThreads={includeAllThreads}");

            writer.WriteLine(
                $"ThreadFilter={EscapeField(threadNameFilter)}");

            writer.WriteLine(
                $"SampleFilter={EscapeField(sampleNameFilter)}");

            writer.WriteLine(
                $"MinimumSampleTimeMs=" +
                minimumSampleTimeMs.ToString(
                    "0.######",
                    CultureInfo.InvariantCulture));

            writer.WriteLine(
                $"MaxRawSamplesPerThread=" +
                maxRawSamplesPerThread);
        }

        writer.WriteLine();
        writer.WriteLine("[SCHEMA]");
        writer.WriteLine(
            "# F<TAB>FrameIndex<TAB>CPUms<TAB>GPUms<TAB>FPS<TAB>FrameStartMs");

        writer.WriteLine(
            "# T<TAB>FrameIndex<TAB>ThreadIndex<TAB>ThreadId<TAB>ThreadNameSid<TAB>ThreadGroupSid<TAB>SampleCount<TAB>MaxDepth");

        writer.WriteLine(
            "# H<TAB>FrameIndex<TAB>ThreadIndex<TAB>NodeSeq<TAB>ParentSeq<TAB>Depth<TAB>TotalMs<TAB>SelfMs<TAB>Calls<TAB>GCAlloc<TAB>TotalPct<TAB>SelfPct<TAB>NameSid<TAB>ObjectSid<TAB>PathSid");

        writer.WriteLine(
            "# R<TAB>FrameIndex<TAB>ThreadIndex<TAB>SampleIndex<TAB>Depth<TAB>StartMs<TAB>DurationMs<TAB>Children<TAB>RecursiveChildren<TAB>MarkerId<TAB>Category<TAB>NameSid<TAB>Metadata");

        writer.WriteLine(
            "# S<TAB>StringId<TAB>EscapedUtf8Value");
    }

    private void WriteFrame(
        StreamWriter writer,
        int frameIndex,
        StringTable strings,
        bool effectiveHierarchy,
        bool effectiveRaw,
        bool effectiveMetadata)
    {
        bool wroteFrameHeader = false;
        int threadCount = 0;

        for (int threadIndex = 0;
             ;
             threadIndex++)
        {
            using RawFrameDataView raw =
                ProfilerDriver.GetRawFrameDataView(
                    frameIndex,
                    threadIndex);

            if (!raw.valid)
                break;

            if (!ShouldIncludeThread(
                    raw.threadName,
                    raw.threadGroupName))
            {
                continue;
            }

            threadCount++;

            if (!wroteFrameHeader)
            {
                writer.Write("F\t");
                writer.Write(frameIndex.ToString(CultureInfo.InvariantCulture));
                writer.Write('\t');
                writer.Write(FormatFloat(raw.frameTimeMs));
                writer.Write('\t');
                writer.Write(FormatFloat(raw.frameGpuTimeMs));
                writer.Write('\t');
                writer.Write(FormatFloat(raw.frameFps));
                writer.Write('\t');
                writer.WriteLine(FormatDouble(raw.frameStartTimeMs));

                wroteFrameHeader = true;
            }

            int threadNameSid =
                strings.Intern(raw.threadName);

            int threadGroupSid =
                strings.Intern(raw.threadGroupName);

            writer.Write("T\t");
            writer.Write(frameIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(threadIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(raw.threadId.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(threadNameSid.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(threadGroupSid.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(raw.sampleCount.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.WriteLine(raw.maxDepth.ToString(CultureInfo.InvariantCulture));

            if (effectiveHierarchy)
            {
                WriteHierarchy(
                    writer,
                    frameIndex,
                    threadIndex,
                    strings);
            }

            if (effectiveRaw)
            {
                WriteRawSamples(
                    writer,
                    frameIndex,
                    threadIndex,
                    raw,
                    strings,
                    effectiveMetadata);
            }
        }

        writer.WriteLine(
            $"FRAME_THREAD_COUNT\t{frameIndex}\t{threadCount}");

        if (!wroteFrameHeader)
        {
            writer.WriteLine(
                $"FRAME_WARNING\t{frameIndex}\tNO_MATCHING_VALID_THREAD");
        }
    }

    private void WriteHierarchy(
        StreamWriter writer,
        int frameIndex,
        int threadIndex,
        StringTable strings)
    {
        using HierarchyFrameDataView hierarchy =
            ProfilerDriver.GetHierarchyFrameDataView(
                frameIndex,
                threadIndex,
                HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnTotalTime,
                false);

        if (!hierarchy.valid)
        {
            writer.WriteLine(
                $"HIERARCHY_INVALID\t{frameIndex}\t{threadIndex}");
            return;
        }

        int root =
            hierarchy.GetRootItemID();

        if (root ==
            HierarchyFrameDataView.invalidSampleId)
        {
            writer.WriteLine(
                $"HIERARCHY_NO_ROOT\t{frameIndex}\t{threadIndex}");
            return;
        }

        int nodeSequence = 0;

        WriteHierarchyItemRecursive(
            writer,
            hierarchy,
            frameIndex,
            threadIndex,
            root,
            parentSequence: -1,
            depth: 0,
            strings,
            ref nodeSequence);
    }

    private void WriteHierarchyItemRecursive(
        StreamWriter writer,
        HierarchyFrameDataView hierarchy,
        int frameIndex,
        int threadIndex,
        int itemId,
        int parentSequence,
        int depth,
        StringTable strings,
        ref int nodeSequence)
    {
        string name =
            hierarchy.GetItemName(itemId) ??
            "<unnamed>";

        float totalMs =
            SafeFloat(
                hierarchy,
                itemId,
                HierarchyFrameDataView.columnTotalTime);

        int thisSequence =
            nodeSequence++;

        if (PassesSampleFilter(
                name,
                totalMs))
        {
            float selfMs =
                SafeFloat(
                    hierarchy,
                    itemId,
                    HierarchyFrameDataView.columnSelfTime);

            string calls =
                SafeColumn(
                    hierarchy,
                    itemId,
                    HierarchyFrameDataView.columnCalls);

            string gc =
                SafeColumn(
                    hierarchy,
                    itemId,
                    HierarchyFrameDataView.columnGcMemory);

            string totalPercent =
                SafeColumn(
                    hierarchy,
                    itemId,
                    HierarchyFrameDataView.columnTotalPercent);

            string selfPercent =
                SafeColumn(
                    hierarchy,
                    itemId,
                    HierarchyFrameDataView.columnSelfPercent);

            int nameSid =
                strings.Intern(name);

            int objectSid =
                strings.Intern(
                    SafeColumn(
                        hierarchy,
                        itemId,
                        HierarchyFrameDataView.columnObjectName));

            int pathSid =
                strings.Intern(
                    SafePath(
                        hierarchy,
                        itemId));

            writer.Write("H\t");
            writer.Write(frameIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(threadIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(thisSequence.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(parentSequence.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(depth.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(FormatFloat(totalMs));
            writer.Write('\t');
            writer.Write(FormatFloat(selfMs));
            writer.Write('\t');
            writer.Write(EscapeField(calls));
            writer.Write('\t');
            writer.Write(EscapeField(gc));
            writer.Write('\t');
            writer.Write(EscapeField(totalPercent));
            writer.Write('\t');
            writer.Write(EscapeField(selfPercent));
            writer.Write('\t');
            writer.Write(nameSid.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(objectSid.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.WriteLine(pathSid.ToString(CultureInfo.InvariantCulture));
        }

        List<int> children =
            new List<int>();

        hierarchy.GetItemChildren(
            itemId,
            children);

        foreach (int child in children)
        {
            WriteHierarchyItemRecursive(
                writer,
                hierarchy,
                frameIndex,
                threadIndex,
                child,
                thisSequence,
                depth + 1,
                strings,
                ref nodeSequence);
        }
    }

    private void WriteRawSamples(
        StreamWriter writer,
        int frameIndex,
        int threadIndex,
        RawFrameDataView raw,
        StringTable strings,
        bool effectiveMetadata)
    {
        int index = 0;
        int written = 0;

        while (index < raw.sampleCount)
        {
            WriteRawSampleRecursive(
                writer,
                frameIndex,
                threadIndex,
                raw,
                strings,
                effectiveMetadata,
                ref index,
                depth: 0,
                ref written);

            if (!losslessFullCapture &&
                maxRawSamplesPerThread > 0 &&
                written >= maxRawSamplesPerThread)
            {
                break;
            }
        }

        writer.WriteLine(
            $"RAW_SAMPLES_WRITTEN\t{frameIndex}\t{threadIndex}\t{written}");

        if (!losslessFullCapture &&
            maxRawSamplesPerThread > 0 &&
            index < raw.sampleCount)
        {
            writer.WriteLine(
                $"RAW_SAMPLES_TRUNCATED\t{frameIndex}\t{threadIndex}\t{raw.sampleCount - index}");
        }
    }

    private void WriteRawSampleRecursive(
        StreamWriter writer,
        int frameIndex,
        int threadIndex,
        RawFrameDataView raw,
        StringTable strings,
        bool effectiveMetadata,
        ref int sampleIndex,
        int depth,
        ref int written)
    {
        if (sampleIndex < 0 ||
            sampleIndex >= raw.sampleCount)
        {
            return;
        }

        int current =
            sampleIndex;

        int childCount =
            raw.GetSampleChildrenCount(current);

        int recursiveChildren =
            raw.GetSampleChildrenCountRecursive(current);

        string name =
            raw.GetSampleName(current) ??
            "<unnamed>";

        float duration =
            raw.GetSampleTimeMs(current);

        bool include =
            PassesSampleFilter(
                name,
                duration) &&
            (losslessFullCapture ||
             maxRawSamplesPerThread <= 0 ||
             written < maxRawSamplesPerThread);

        if (include)
        {
            int nameSid =
                strings.Intern(name);

            string metadata =
                effectiveMetadata
                    ? BuildMetadata(
                        raw,
                        current)
                    : string.Empty;

            writer.Write("R\t");
            writer.Write(frameIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(threadIndex.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(current.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(depth.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(FormatDouble(raw.GetSampleStartTimeMs(current)));
            writer.Write('\t');
            writer.Write(FormatFloat(duration));
            writer.Write('\t');
            writer.Write(childCount.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(recursiveChildren.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(raw.GetSampleMarkerId(current).ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(raw.GetSampleCategoryIndex(current).ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.Write(nameSid.ToString(CultureInfo.InvariantCulture));
            writer.Write('\t');
            writer.WriteLine(EscapeField(metadata));

            written++;
        }

        sampleIndex++;

        for (int child = 0;
             child < childCount &&
             sampleIndex < raw.sampleCount;
             child++)
        {
            WriteRawSampleRecursive(
                writer,
                frameIndex,
                threadIndex,
                raw,
                strings,
                effectiveMetadata,
                ref sampleIndex,
                depth + 1,
                ref written);
        }
    }

    private static string BuildMetadata(
        RawFrameDataView raw,
        int sampleIndex)
    {
        int count =
            raw.GetSampleMetadataCount(sampleIndex);

        if (count <= 0)
            return string.Empty;

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder(
                Math.Max(32, count * 12));

        for (int i = 0;
             i < count;
             i++)
        {
            if (i > 0)
                builder.Append(';');

            builder.Append('[');
            builder.Append(i);
            builder.Append("]=");

            string value = null;

            try
            {
                value =
                    raw.GetSampleMetadataAsString(
                        sampleIndex,
                        i);
            }
            catch
            {
                // 일부 metadata type은 string 변환을 지원하지 않을 수 있다.
            }

            if (string.IsNullOrEmpty(value))
            {
                try
                {
                    value =
                        raw.GetSampleMetadataAsLong(
                            sampleIndex,
                            i)
                        .ToString(
                            CultureInfo.InvariantCulture);
                }
                catch
                {
                    value = "<unavailable>";
                }
            }

            builder.Append(value);
        }

        return builder.ToString();
    }

    private bool ShouldIncludeThread(
        string threadName,
        string threadGroup)
    {
        if (losslessFullCapture ||
            includeAllThreads)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(
                threadNameFilter))
        {
            return string.Equals(
                threadName,
                "Main Thread",
                StringComparison.OrdinalIgnoreCase);
        }

        string filter =
            threadNameFilter.Trim();

        return ContainsIgnoreCase(
                   threadName,
                   filter) ||
               ContainsIgnoreCase(
                   threadGroup,
                   filter);
    }

    private bool PassesSampleFilter(
        string sampleName,
        float durationMs)
    {
        if (losslessFullCapture)
            return true;

        if (durationMs <
            minimumSampleTimeMs)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                sampleNameFilter))
        {
            return true;
        }

        return ContainsIgnoreCase(
            sampleName,
            sampleNameFilter.Trim());
    }

    private static bool ContainsIgnoreCase(
        string source,
        string value)
    {
        return !string.IsNullOrEmpty(source) &&
               !string.IsNullOrEmpty(value) &&
               source.IndexOf(
                   value,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float SafeFloat(
        HierarchyFrameDataView data,
        int itemId,
        int column)
    {
        try
        {
            return data.GetItemColumnDataAsFloat(
                itemId,
                column);
        }
        catch
        {
            return 0f;
        }
    }

    private static string SafeColumn(
        HierarchyFrameDataView data,
        int itemId,
        int column)
    {
        try
        {
            return data.GetItemColumnData(
                       itemId,
                       column) ??
                   string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafePath(
        HierarchyFrameDataView data,
        int itemId)
    {
        try
        {
            return data.GetItemPath(itemId) ??
                   string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string EscapeField(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\t", "\\t")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private static string FormatFloat(
        float value)
    {
        return value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
    }

    private static string FormatDouble(
        double value)
    {
        return value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);
    }

    private static string GetAbsoluteExportDirectory()
    {
        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."));

        return Path.Combine(
            projectRoot,
            DefaultDirectory);
    }
}
#endif
