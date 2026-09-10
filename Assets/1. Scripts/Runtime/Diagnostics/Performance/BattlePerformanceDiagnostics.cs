using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattlePerformanceDiagnostics : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleCameraDirector cameraDirector;
    [SerializeField] private Camera targetCamera;

    [Header("Sampling")]
    [SerializeField, Min(0.1f)] private float sampleInterval = 0.25f;
    [SerializeField, Min(30)] private int maxCapturedSamples = 1200;
    [SerializeField, Min(1f)] private float lowFpsThreshold = 25f;
    [SerializeField, Min(0.1f)] private float lowFpsWarningDelay = 0.75f;
    [SerializeField, Min(1)] private int lowFpsWarningWarmupFrames = 30;
    [SerializeField] private bool ignoreWarningsDuringBatchSimulation = true;
    [SerializeField] private bool ignoreWarningsWhenApplicationUnfocused = true;
    [SerializeField] private bool ignoreWarningsWhileTimeScaleIsZero = true;

    [Tooltip("이 값보다 긴 단일 프레임은 실제 게임 병목이 아니라 Scene Load, Editor 정지, 창 전환으로 간주합니다.")]
    [SerializeField, Min(0.1f)] private float maximumTrustedFrameDeltaSeconds = 0.25f;

    [Header("Hotkeys")]
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F4;
    [SerializeField] private KeyCode toggleCaptureKey = KeyCode.F9;
    [SerializeField] private KeyCode exportCsvKey = KeyCode.F10;
    [SerializeField] private KeyCode toggleDebugUiKey = KeyCode.F6;
    [SerializeField] private KeyCode toggleWorldCanvasKey = KeyCode.F7;
    [SerializeField] private KeyCode toggleOutlineKey = KeyCode.F5;

    // v1: 성능 Overlay 토글을 F8에서 F4로 이동.
    // v2: Performance Diagnostics Overlay의 Play Mode 시작 상태를 기본 숨김으로 변경.
    // v3: Scene Load, Editor 정지, 포커스 이탈 프레임을 성능 경고에서 제외.
    private const int CurrentHotkeyLayoutVersion = 3;
    [SerializeField, HideInInspector] private int hotkeyLayoutVersion;

    [Header("Overlay")]
    [Tooltip("Play Mode에 진입할 때 Performance Diagnostics Overlay를 표시할지 결정합니다.")]
    [SerializeField] private bool startOverlayVisible;
    [SerializeField, HideInInspector] private bool showOverlay;
    [SerializeField] private Vector2 overlayPosition = new(12f, 12f);
    [SerializeField] private Vector2 overlaySize = new(520f, 330f);
    [SerializeField] private bool showPersistentToggleButton;
    [SerializeField] private Vector2 toggleButtonSize = new(210f, 34f);
    [SerializeField] private Vector2 toggleButtonMargin = new(12f, 12f);

    private readonly List<Sample> samples = new();
    private readonly StringBuilder overlayBuilder = new(1024);
    private readonly Dictionary<DebugBattleUI, bool> debugUiStates = new();
    private readonly Dictionary<Canvas, bool> worldCanvasStates = new();
    private readonly Dictionary<OutlineController, bool> outlineStates = new();

    private Counter cpuMainThread;
    private Counter cpuRenderThread;
    private Counter gpuFrame;
    private Counter gcAllocated;
    private Counter batches;
    private Counter drawCalls;
    private Counter setPassCalls;
    private Counter triangles;

    private GUIStyle overlayStyle;
    private GUIStyle toggleButtonStyle;
    private string cachedOverlay = string.Empty;

    private bool toggleButtonPositionInitialized;
    private Vector2 toggleButtonPosition;
    private bool draggingToggleButton;
    private Vector2 toggleButtonDragOffset;
    private Vector2 toggleButtonDragStartMouse;
    private bool toggleButtonWasDragged;
    private float nextSampleTime;
    private float smoothedFrameMs;
    private float lowFpsDuration;
    private bool warnedForCurrentDrop;
    private int lowFpsWarningsEnabledFromFrame;
    private bool resetSmoothedFrameTimeAfterWarmup;
    private bool captureEnabled = true;
    private bool debugUiSuppressed;
    private bool worldCanvasesSuppressed;
    private bool outlinesSuppressed;
    private Vector3 previousCameraPosition;
    private Quaternion previousCameraRotation;
    private bool hasPreviousCameraPose;

    private void Awake()
    {
        MigrateSettings();

        // Scene에 과거 showOverlay=true 값이 남아 있어도
        // Play Mode 진입 시에는 명시적인 시작 옵션만 사용한다.
        showOverlay = startOverlayVisible;

        ResolveReferences();
        StartRecorders();
        CacheIsolationTargets();

        samples.Capacity = Mathf.Max(samples.Capacity, maxCapturedSamples);
    }

    private void OnValidate()
    {
        MigrateSettings();

        // Edit Mode에서도 Inspector에 설정된 시작 상태와
        // 런타임 표시 상태가 서로 어긋나지 않게 유지한다.
        if (!Application.isPlaying)
            showOverlay = startOverlayVisible;
    }

    private void MigrateSettings()
    {
        if (hotkeyLayoutVersion < 1)
        {
            // 이전 패치에서 F8이 Scene/Prefab에 저장되어 있어도
            // 동적 분석 도구와 겹치지 않도록 자동 변경한다.
            if (toggleOverlayKey == KeyCode.F8)
                toggleOverlayKey = KeyCode.F4;
        }

        if (hotkeyLayoutVersion < 2)
        {
            // 기존 Scene에는 showOverlay=true가 직렬화되어 있었다.
            // v2부터는 Play Mode 시작 시 Performance Overlay를 기본 숨김으로 한다.
            startOverlayVisible = false;
            showOverlay = false;
        }

        if (hotkeyLayoutVersion < 3)
        {
            ignoreWarningsWhenApplicationUnfocused = true;
            ignoreWarningsWhileTimeScaleIsZero = true;
            maximumTrustedFrameDeltaSeconds = 0.25f;
        }

        hotkeyLayoutVersion = CurrentHotkeyLayoutVersion;
    }

    private void OnEnable()
    {
        ResetAfterUntrustedFrame();
    }

    private void Update()
    {
        if (!IsCurrentFrameTrusted())
        {
            ResetAfterUntrustedFrame();
            return;
        }

        UpdateSmoothedFrameTime();
        UpdateLowFpsWarning();

        if (Time.unscaledTime < nextSampleTime)
            return;

        nextSampleTime =
            Time.unscaledTime + Mathf.Max(0.1f, sampleInterval);

        CaptureSample();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // 탐색기 자동 열기, 창 전환, Editor 포커스 복귀 직후의 긴 Delta가
        // 실제 게임 프레임 평균에 섞이지 않도록 재워밍업한다.
        ResetAfterUntrustedFrame();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        ResetAfterUntrustedFrame();
    }

    private void OnGUI()
    {
        // Diagnostics는 이제 Game View에 UI를 그리지 않는다.
        // Performance/격리/CSV 제어는 Scene의 Battle Live Debug Inspector에서 수행한다.
        // 기존 F9/F10/F5/F6/F7 단축키는 빠른 진단용으로만 유지한다.
        HandleGuiHotkeys(Event.current);
    }

    private void OnDisable()
    {
        RestoreIsolationTargets();
    }

    private void OnDestroy()
    {
        DisposeRecorders();
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (cameraDirector == null)
            cameraDirector = FindFirstObjectByType<BattleCameraDirector>();

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void StartRecorders()
    {
        cpuMainThread = Counter.Start(ProfilerCategory.Render, "CPU Main Thread Frame Time");
        cpuRenderThread = Counter.Start(ProfilerCategory.Render, "CPU Render Thread Frame Time");
        gpuFrame = Counter.Start(ProfilerCategory.Render, "GPU Frame Time");
        gcAllocated = Counter.Start(ProfilerCategory.Memory, "GC Allocated In Frame");
        batches = Counter.Start(ProfilerCategory.Render, "Batches Count");
        drawCalls = Counter.Start(ProfilerCategory.Render, "Draw Calls Count");
        setPassCalls = Counter.Start(ProfilerCategory.Render, "SetPass Calls Count");
        triangles = Counter.Start(ProfilerCategory.Render, "Triangles Count");
    }

    private void DisposeRecorders()
    {
        cpuMainThread?.Dispose();
        cpuRenderThread?.Dispose();
        gpuFrame?.Dispose();
        gcAllocated?.Dispose();
        batches?.Dispose();
        drawCalls?.Dispose();
        setPassCalls?.Dispose();
        triangles?.Dispose();
    }

    private void HandleGuiHotkeys(Event guiEvent)
    {
        if (guiEvent == null || guiEvent.type != EventType.KeyDown)
            return;

        bool handled = true;

        if (guiEvent.keyCode == toggleCaptureKey)
            ToggleCapture();
        else if (guiEvent.keyCode == exportCsvKey)
            ExportCsv();
        else if (guiEvent.keyCode == toggleDebugUiKey)
            ToggleDebugUiIsolation();
        else if (guiEvent.keyCode == toggleWorldCanvasKey)
            ToggleWorldCanvasIsolation();
        else if (guiEvent.keyCode == toggleOutlineKey)
            ToggleOutlineIsolation();
        else
            handled = false;

        if (handled)
            guiEvent.Use();
    }

    private void DrawPersistentToggleButton()
    {
        if (!showPersistentToggleButton)
            return;

        toggleButtonStyle ??= new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };

        float width = Mathf.Max(120f, toggleButtonSize.x);
        float height = Mathf.Max(28f, toggleButtonSize.y);

        if (!toggleButtonPositionInitialized)
        {
            toggleButtonPosition = new Vector2(
                Mathf.Max(
                    0f,
                    Screen.width -
                    width -
                    Mathf.Max(0f, toggleButtonMargin.x)),
                Mathf.Max(0f, toggleButtonMargin.y));
            toggleButtonPositionInitialized = true;
        }

        toggleButtonPosition = ClampGuiPosition(
            toggleButtonPosition,
            width,
            height);

        Rect buttonRect = new Rect(
            toggleButtonPosition.x,
            toggleButtonPosition.y,
            width,
            height);

        Event guiEvent = Event.current;

        if (guiEvent != null &&
            guiEvent.button == 0)
        {
            if (guiEvent.type == EventType.MouseDown &&
                buttonRect.Contains(guiEvent.mousePosition))
            {
                draggingToggleButton = true;
                toggleButtonWasDragged = false;
                toggleButtonDragStartMouse =
                    guiEvent.mousePosition;
                toggleButtonDragOffset =
                    guiEvent.mousePosition -
                    toggleButtonPosition;
                guiEvent.Use();
            }
            else if (guiEvent.type == EventType.MouseDrag &&
                     draggingToggleButton)
            {
                toggleButtonPosition =
                    ClampGuiPosition(
                        guiEvent.mousePosition -
                        toggleButtonDragOffset,
                        width,
                        height);

                if (!toggleButtonWasDragged &&
                    Vector2.Distance(
                        toggleButtonDragStartMouse,
                        guiEvent.mousePosition) >= 4f)
                {
                    toggleButtonWasDragged = true;
                }

                guiEvent.Use();
            }
            else if (guiEvent.type == EventType.MouseUp &&
                     draggingToggleButton)
            {
                bool shouldToggle =
                    !toggleButtonWasDragged;

                draggingToggleButton = false;
                toggleButtonWasDragged = false;

                if (shouldToggle)
                    ToggleOverlayVisibility();

                guiEvent.Use();
            }
        }

        string label = showOverlay
            ? $"Hide Performance [{toggleOverlayKey}]"
            : $"Show Performance [{toggleOverlayKey}]";

        // 클릭/드래그 입력은 위에서 직접 처리한다.
        GUI.Box(
            buttonRect,
            label,
            toggleButtonStyle);
    }

    private static Vector2 ClampGuiPosition(
        Vector2 position,
        float width,
        float height)
    {
        return new Vector2(
            Mathf.Clamp(
                position.x,
                0f,
                Mathf.Max(0f, Screen.width - width)),
            Mathf.Clamp(
                position.y,
                0f,
                Mathf.Max(0f, Screen.height - height)));
    }

    public bool IsOverlayVisible => showOverlay;
    public bool CaptureEnabled => captureEnabled;
    public int CapturedSampleCount => samples.Count;
    public string LiveSummary => cachedOverlay;
    public bool DebugUiSuppressed => debugUiSuppressed;
    public bool WorldCanvasesSuppressed => worldCanvasesSuppressed;
    public bool OutlinesSuppressed => outlinesSuppressed;

    public void ToggleOverlayVisibility()
    {
        showOverlay = !showOverlay;
    }

    public void SetOverlayVisible(bool visible)
    {
        showOverlay = visible;
    }

    [ContextMenu("Show Performance Overlay")]
    private void ShowPerformanceOverlay()
    {
        SetOverlayVisible(true);
    }

    [ContextMenu("Hide Performance Overlay")]
    private void HidePerformanceOverlay()
    {
        SetOverlayVisible(false);
    }

    [ContextMenu("Reset Performance Overlay To Startup State")]
    private void ResetPerformanceOverlayToStartupState()
    {
        SetOverlayVisible(startOverlayVisible);
    }

    public void ToggleCapture()
    {
        captureEnabled = !captureEnabled;
        Debug.Log($"[BattlePerformanceDiagnostics] CSV capture: {captureEnabled}", this);
    }

    private bool IsCurrentFrameTrusted()
    {
        float delta = Time.unscaledDeltaTime;

        if (float.IsNaN(delta) ||
            float.IsInfinity(delta) ||
            delta <= 0f ||
            delta > Mathf.Max(0.1f, maximumTrustedFrameDeltaSeconds))
        {
            return false;
        }

        if (ignoreWarningsDuringBatchSimulation &&
            BattleSimulationRuntime.IsBatchSimulation)
        {
            return false;
        }

        if (ignoreWarningsWhenApplicationUnfocused &&
            !Application.isFocused)
        {
            return false;
        }

        if (ignoreWarningsWhileTimeScaleIsZero &&
            Mathf.Approximately(Time.timeScale, 0f))
        {
            return false;
        }

        if (battleManager != null &&
            !battleManager.isActiveAndEnabled)
        {
            return false;
        }

        return true;
    }

    private void ResetAfterUntrustedFrame()
    {
        smoothedFrameMs = 0f;
        resetSmoothedFrameTimeAfterWarmup = true;

        lowFpsWarningsEnabledFromFrame =
            Time.frameCount + Mathf.Max(1, lowFpsWarningWarmupFrames);

        nextSampleTime =
            Time.unscaledTime + Mathf.Max(0.1f, sampleInterval);

        ResetLowFpsWarningState();
    }

    private void UpdateSmoothedFrameTime()
    {
        float currentFrameMs = Time.unscaledDeltaTime * 1000f;

        if (smoothedFrameMs <= 0f)
        {
            smoothedFrameMs = currentFrameMs;
            return;
        }

        float blend = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
        smoothedFrameMs = Mathf.Lerp(smoothedFrameMs, currentFrameMs, blend);
    }

    private void UpdateLowFpsWarning()
    {
        if (Time.frameCount < lowFpsWarningsEnabledFromFrame ||
            (ignoreWarningsDuringBatchSimulation &&
             BattleSimulationRuntime.IsBatchSimulation))
        {
            ResetLowFpsWarningState();
            return;
        }

        if (resetSmoothedFrameTimeAfterWarmup)
        {
            resetSmoothedFrameTimeAfterWarmup = false;
            smoothedFrameMs = Time.unscaledDeltaTime * 1000f;
            ResetLowFpsWarningState();
            return;
        }

        float fps = smoothedFrameMs > 0.001f ? 1000f / smoothedFrameMs : 0f;

        if (fps > 0f && fps < lowFpsThreshold)
        {
            // 한 번의 긴 Editor 정지 프레임이 경고 지연 시간을 단숨에 채우지 않게 한다.
            lowFpsDuration += Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (!warnedForCurrentDrop && lowFpsDuration >= lowFpsWarningDelay)
            {
                warnedForCurrentDrop = true;
                Debug.LogWarning(BuildDropWarning(fps), this);
            }

            return;
        }

        lowFpsDuration = 0f;
        warnedForCurrentDrop = false;
    }

    private void ResetLowFpsWarningState()
    {
        lowFpsDuration = 0f;
        warnedForCurrentDrop = false;
    }

    private string BuildDropWarning(float fps)
    {
        return
            $"[BattlePerformanceDiagnostics] Sustained frame drop: {fps:0.0} FPS, " +
            $"Frame {smoothedFrameMs:0.0} ms, " +
            $"CPU Main {FormatMilliseconds(cpuMainThread)}, " +
            $"Render {FormatMilliseconds(cpuRenderThread)}, " +
            $"GPU {FormatMilliseconds(gpuFrame)}, " +
            $"GC {FormatBytes(gcAllocated)}, " +
            $"Batches {FormatCount(batches)}, Draws {FormatCount(drawCalls)}, " +
            $"CameraMoving {IsCameraMoving()}, Selected {GetSelectedName()}.";
    }

    private void CaptureSample()
    {
        bool cameraMoving = IsCameraMoving();
        float fps = smoothedFrameMs > 0.001f ? 1000f / smoothedFrameMs : 0f;

        Sample sample = new()
        {
            Realtime = Time.realtimeSinceStartupAsDouble,
            Frame = Time.frameCount,
            Fps = fps,
            FrameMs = smoothedFrameMs,
            CpuMainMs = cpuMainThread?.ReadTimeMilliseconds() ?? -1d,
            CpuRenderMs = cpuRenderThread?.ReadTimeMilliseconds() ?? -1d,
            GpuMs = gpuFrame?.ReadTimeMilliseconds() ?? -1d,
            GcBytes = gcAllocated?.ReadValue() ?? -1L,
            Batches = batches?.ReadValue() ?? -1L,
            DrawCalls = drawCalls?.ReadValue() ?? -1L,
            SetPassCalls = setPassCalls?.ReadValue() ?? -1L,
            Triangles = triangles?.ReadValue() ?? -1L,
            CameraMoving = cameraMoving,
            SelectedCharacter = GetSelectedName(),
            DebugUiSuppressed = debugUiSuppressed,
            WorldCanvasesSuppressed = worldCanvasesSuppressed,
            OutlinesSuppressed = outlinesSuppressed
        };

        if (captureEnabled)
        {
            if (samples.Count >= Mathf.Max(30, maxCapturedSamples))
                samples.RemoveAt(0);

            samples.Add(sample);
        }

        RebuildOverlay(sample);
    }

    private void RebuildOverlay(Sample sample)
    {
        overlayBuilder.Clear();
        overlayBuilder.AppendLine("PROJECT ABYSS PERFORMANCE DIAGNOSTICS / INSPECTOR LIVE");
        overlayBuilder.Append("FPS / frame       : ").Append(sample.Fps.ToString("0.0")).Append(" / ")
            .Append(sample.FrameMs.ToString("0.00")).AppendLine(" ms");
        overlayBuilder.Append("CPU main / render : ").Append(FormatMs(sample.CpuMainMs)).Append(" / ")
            .AppendLine(FormatMs(sample.CpuRenderMs));
        overlayBuilder.Append("GPU               : ").AppendLine(FormatMs(sample.GpuMs));
        overlayBuilder.Append("GC alloc / frame  : ").AppendLine(FormatBytes(sample.GcBytes));
        overlayBuilder.Append("Batches / Draws   : ").Append(FormatCount(sample.Batches)).Append(" / ")
            .AppendLine(FormatCount(sample.DrawCalls));
        overlayBuilder.Append("SetPass / Tris    : ").Append(FormatCount(sample.SetPassCalls)).Append(" / ")
            .AppendLine(FormatCount(sample.Triangles));
        overlayBuilder.Append("Camera moving     : ").AppendLine(sample.CameraMoving.ToString());
        overlayBuilder.Append("Selected          : ").AppendLine(sample.SelectedCharacter);
        overlayBuilder.AppendLine();
        overlayBuilder.Append("F5 Outline A/B    : ").AppendLine(outlinesSuppressed ? "OFF" : "ON");
        overlayBuilder.Append("F6 Debug UI A/B   : ").AppendLine(debugUiSuppressed ? "OFF" : "ON");
        overlayBuilder.Append("F7 World Canvas   : ").AppendLine(worldCanvasesSuppressed ? "OFF" : "ON");
        overlayBuilder.Append("Capture           : ").AppendLine(captureEnabled ? "ON" : "OFF");
        overlayBuilder.Append("Captured samples  : ").AppendLine(samples.Count.ToString());
        overlayBuilder.AppendLine();
        overlayBuilder.AppendLine("Interpretation: CPU+GC spike = UI/TMP/script; GPU spike = render/shader.");

        cachedOverlay = overlayBuilder.ToString();
    }

    private bool IsCameraMoving()
    {
        if (cameraDirector != null)
            return cameraDirector.IsMoving;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return false;

        Transform cameraTransform = targetCamera.transform;

        if (!hasPreviousCameraPose)
        {
            previousCameraPosition = cameraTransform.position;
            previousCameraRotation = cameraTransform.rotation;
            hasPreviousCameraPose = true;
            return false;
        }

        bool moved =
            Vector3.SqrMagnitude(cameraTransform.position - previousCameraPosition) > 0.000001f ||
            Quaternion.Angle(cameraTransform.rotation, previousCameraRotation) > 0.01f;

        previousCameraPosition = cameraTransform.position;
        previousCameraRotation = cameraTransform.rotation;
        return moved;
    }

    private string GetSelectedName()
    {
        Character selected = battleManager != null ? battleManager.SelectedCharacter : null;
        return selected != null ? selected.name : "None";
    }

    private void CacheIsolationTargets()
    {
        debugUiStates.Clear();
        foreach (DebugBattleUI debugUi in FindObjectsByType<DebugBattleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (debugUi != null)
                debugUiStates[debugUi] = debugUi.gameObject.activeSelf;
        }

        worldCanvasStates.Clear();
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                worldCanvasStates[canvas] = canvas.enabled;
        }

        outlineStates.Clear();
        foreach (OutlineController outline in FindObjectsByType<OutlineController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (outline != null)
                outlineStates[outline] = outline.enabled;
        }
    }

    public void ToggleDebugUiIsolation()
    {
        debugUiSuppressed = !debugUiSuppressed;

        foreach (KeyValuePair<DebugBattleUI, bool> pair in debugUiStates)
        {
            if (pair.Key != null)
                pair.Key.gameObject.SetActive(debugUiSuppressed ? false : pair.Value);
        }

        Debug.Log($"[BattlePerformanceDiagnostics] DebugBattleUI A/B: {(debugUiSuppressed ? "OFF" : "RESTORED")}", this);
    }

    public void ToggleWorldCanvasIsolation()
    {
        worldCanvasesSuppressed = !worldCanvasesSuppressed;

        foreach (KeyValuePair<Canvas, bool> pair in worldCanvasStates)
        {
            if (pair.Key != null)
                pair.Key.enabled = worldCanvasesSuppressed ? false : pair.Value;
        }

        Debug.Log($"[BattlePerformanceDiagnostics] World-space Canvas A/B: {(worldCanvasesSuppressed ? "OFF" : "RESTORED")}", this);
    }

    public void ToggleOutlineIsolation()
    {
        outlinesSuppressed = !outlinesSuppressed;

        foreach (KeyValuePair<OutlineController, bool> pair in outlineStates)
        {
            if (pair.Key == null)
                continue;

            if (outlinesSuppressed)
            {
                pair.Key.DisableOutline();
                pair.Key.enabled = false;
            }
            else
            {
                pair.Key.enabled = pair.Value;
            }
        }

        Debug.Log($"[BattlePerformanceDiagnostics] Outline A/B: {(outlinesSuppressed ? "OFF" : "RESTORED")}", this);
    }

    private void RestoreIsolationTargets()
    {
        if (debugUiSuppressed)
        {
            foreach (KeyValuePair<DebugBattleUI, bool> pair in debugUiStates)
            {
                if (pair.Key != null)
                    pair.Key.gameObject.SetActive(pair.Value);
            }
        }

        if (worldCanvasesSuppressed)
        {
            foreach (KeyValuePair<Canvas, bool> pair in worldCanvasStates)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }
        }

        if (outlinesSuppressed)
        {
            foreach (KeyValuePair<OutlineController, bool> pair in outlineStates)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }
        }

        debugUiSuppressed = false;
        worldCanvasesSuppressed = false;
        outlinesSuppressed = false;
    }

    [ContextMenu("Export Performance CSV")]
    public void ExportCsv()
    {
        if (samples.Count == 0)
        {
            Debug.LogWarning("[BattlePerformanceDiagnostics] Export할 샘플이 없습니다.", this);
            return;
        }

        string fileName = $"ProjectAbyss_Performance_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        using StreamWriter writer = new(path, false, new UTF8Encoding(true));
        writer.WriteLine(
            "realtime_s,frame,fps,frame_ms,cpu_main_ms,cpu_render_ms,gpu_ms,gc_alloc_bytes," +
            "batches,draw_calls,setpass_calls,triangles,camera_moving,selected_character," +
            "debug_ui_off,world_canvas_off,outlines_off");

        foreach (Sample sample in samples)
        {
            writer.Write(sample.Realtime.ToString("0.000", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(sample.Frame);
            writer.Write(',');
            writer.Write(sample.Fps.ToString("0.00", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(sample.FrameMs.ToString("0.000", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(sample.CpuMainMs.ToString("0.000", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(sample.CpuRenderMs.ToString("0.000", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(sample.GpuMs.ToString("0.000", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(sample.GcBytes);
            writer.Write(',');
            writer.Write(sample.Batches);
            writer.Write(',');
            writer.Write(sample.DrawCalls);
            writer.Write(',');
            writer.Write(sample.SetPassCalls);
            writer.Write(',');
            writer.Write(sample.Triangles);
            writer.Write(',');
            writer.Write(sample.CameraMoving ? 1 : 0);
            writer.Write(',');
            writer.Write(EscapeCsv(sample.SelectedCharacter));
            writer.Write(',');
            writer.Write(sample.DebugUiSuppressed ? 1 : 0);
            writer.Write(',');
            writer.Write(sample.WorldCanvasesSuppressed ? 1 : 0);
            writer.Write(',');
            writer.WriteLine(sample.OutlinesSuppressed ? 1 : 0);
        }

        Debug.Log($"[BattlePerformanceDiagnostics] CSV 저장 완료: {path}", this);
    }

    private static string EscapeCsv(string value)
    {
        value ??= string.Empty;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private static string FormatMilliseconds(Counter counter)
    {
        if (counter == null || !counter.IsValid)
            return "N/A";

        return FormatMs(counter.ReadTimeMilliseconds());
    }

    private static string FormatMs(double value)
    {
        return value < 0d ? "N/A" : $"{value:0.00} ms";
    }

    private static string FormatBytes(Counter counter)
    {
        return counter == null || !counter.IsValid ? "N/A" : FormatBytes(counter.ReadValue());
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0L)
            return "N/A";

        if (bytes < 1024L)
            return $"{bytes} B";

        if (bytes < 1024L * 1024L)
            return $"{bytes / 1024d:0.0} KB";

        return $"{bytes / (1024d * 1024d):0.00} MB";
    }

    private static string FormatCount(Counter counter)
    {
        return counter == null || !counter.IsValid ? "N/A" : FormatCount(counter.ReadValue());
    }

    private static string FormatCount(long value)
    {
        return value < 0L ? "N/A" : value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private sealed class Counter : IDisposable
    {
        private ProfilerRecorder recorder;
        private Counter(ProfilerRecorder recorder)
        {
            this.recorder = recorder;
        }

        public bool IsValid => recorder.Valid;

        public static Counter Start(ProfilerCategory category, string statName)
        {
            try
            {
                return new Counter(
                    ProfilerRecorder.StartNew(category, statName, 1));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[BattlePerformanceDiagnostics] Profiler counter를 열 수 없습니다: {statName}\n" +
                    exception.Message);
                return null;
            }
        }

        public long ReadValue()
        {
            return recorder.Valid && recorder.Count > 0 ? recorder.LastValue : -1L;
        }

        public double ReadTimeMilliseconds()
        {
            long value = ReadValue();

            if (value < 0L)
                return -1d;

            return recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds
                ? value * 0.000001d
                : value;
        }

        public void Dispose()
        {
            if (recorder.Valid)
                recorder.Dispose();
        }
    }

    [Serializable]
    private struct Sample
    {
        public double Realtime;
        public int Frame;
        public float Fps;
        public float FrameMs;
        public double CpuMainMs;
        public double CpuRenderMs;
        public double GpuMs;
        public long GcBytes;
        public long Batches;
        public long DrawCalls;
        public long SetPassCalls;
        public long Triangles;
        public bool CameraMoving;
        public string SelectedCharacter;
        public bool DebugUiSuppressed;
        public bool WorldCanvasesSuppressed;
        public bool OutlinesSuppressed;
    }
}