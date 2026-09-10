using System;
using System.Collections;
using UnityEngine;

public enum BattlePlaybackSpeedMode
{
    Normal = 1,
    Fast = 2,
    VeryFast = 3
}

/// <summary>
/// 전투 Resolution 구간에만 1x/2x/3x 배속을 적용한다.
/// Planning/상세/감정 증강 선택 UI 등 전투 외 상호작용 구간은 항상 원래 TimeScale로 복원한다.
///
/// Timeline의 SetTimeScale/RestoreTimeScale과 공존하기 위해 Resolution 시작 시 기준 TimeScale을
/// 캡처하고, Timeline이 요청한 로컬 배율 위에 사용자가 선택한 전투 배율을 곱한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BattlePlaybackSpeedController : MonoBehaviour
{
    private static BattlePlaybackSpeedMode sessionMode =
        BattlePlaybackSpeedMode.Normal;

    public static BattlePlaybackSpeedController Instance
    {
        get;
        private set;
    }

    public event Action<BattlePlaybackSpeedMode, float> SpeedChanged;

    [SerializeField]
    private BattlePlaybackSpeedMode defaultSpeed =
        BattlePlaybackSpeedMode.Normal;

    [SerializeField]
    private bool logChanges;

    private BattleManager battleManager;
    private BattlePlaybackSpeedMode currentMode;
    private bool resolutionActive;
    private bool capturedBaseTimeScale;
    private float baseTimeScale = 1f;
    private bool missingSceneUiLogged;

    public BattlePlaybackSpeedMode CurrentMode => currentMode;
    public int CurrentLevel => Mathf.Clamp((int)currentMode, 1, 3);
    public float SelectedMultiplier => CurrentLevel;
    public bool IsResolutionActive => resolutionActive;

    /// <summary>
    /// 전투 Resolution 중일 때만 실제 배속을 노출한다.
    /// unscaled 기반 전투 UI가 이 값을 사용하면 Planning UI는 영향을 받지 않는다.
    /// </summary>
    public float EffectiveMultiplier =>
        resolutionActive
            ? SelectedMultiplier
            : 1f;

    public float BaseTimeScale =>
        capturedBaseTimeScale
            ? baseTimeScale
            : Time.timeScale;

    public static float BattleUnscaledDeltaTime =>
        Time.unscaledDeltaTime *
        (Instance != null
            ? Instance.EffectiveMultiplier
            : 1f);

    public static IEnumerator WaitForBattleUnscaledSeconds(
        float duration)
    {
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += BattleUnscaledDeltaTime;
            yield return null;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
        sessionMode = BattlePlaybackSpeedMode.Normal;
    }

    public static BattlePlaybackSpeedController EnsureInstalled(
        BattleManager manager)
    {
        if (manager == null)
            return null;

        BattlePlaybackSpeedController controller =
            manager.GetComponent<BattlePlaybackSpeedController>();

        if (controller == null)
        {
            controller =
                manager.gameObject.AddComponent<
                    BattlePlaybackSpeedController>();
        }

        controller.Bind(manager);
        controller.EnsureUi();
        return controller;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        battleManager ??= GetComponent<BattleManager>();

        currentMode = IsValidMode(sessionMode)
            ? sessionMode
            : SanitizeMode(defaultSpeed);

        sessionMode = currentMode;
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    private void Update()
    {
        // 명시적 BattleManager hook가 누락되거나 외부 테스트가 Resolve를 직접 시작하는 경우의 안전망.
        bool shouldBeActive =
            battleManager?.TurnManager?.IsResolving == true;

        if (shouldBeActive != resolutionActive)
            SetResolutionActive(shouldBeActive);
    }

    private void OnDisable()
    {
        RestoreBaseTimeScale();

        if (Instance == this)
            Instance = null;
    }

    private void OnDestroy()
    {
        RestoreBaseTimeScale();

        if (Instance == this)
            Instance = null;
    }

    public void Bind(BattleManager manager)
    {
        if (manager != null)
            battleManager = manager;
    }

    public void SetSpeed(BattlePlaybackSpeedMode mode)
    {
        mode = SanitizeMode(mode);

        if (mode == currentMode)
            return;

        float oldMultiplier =
            Mathf.Max(0.01f, SelectedMultiplier);

        float localTimelineScale = 1f;

        if (resolutionActive &&
            capturedBaseTimeScale &&
            Mathf.Abs(baseTimeScale) > 0.0001f)
        {
            // Timeline hit-stop 등 현재 로컬 TimeScale을 보존한 채 사용자 배율만 교체한다.
            localTimelineScale =
                Time.timeScale /
                (baseTimeScale * oldMultiplier);
        }

        currentMode = mode;
        sessionMode = mode;

        if (resolutionActive && capturedBaseTimeScale)
        {
            Time.timeScale =
                baseTimeScale *
                SelectedMultiplier *
                localTimelineScale;
        }

        SpeedChanged?.Invoke(
            currentMode,
            SelectedMultiplier);

        if (logChanges)
        {
            Debug.Log(
                $"[BattleSpeed] Mode={CurrentLevel}x, " +
                $"Resolution={resolutionActive}, " +
                $"TimeScale={Time.timeScale:0.###}",
                this);
        }
    }

    public void SetSpeed(int level)
    {
        SetSpeed(
            (BattlePlaybackSpeedMode)
            Mathf.Clamp(level, 1, 3));
    }

    public void CycleSpeed()
    {
        int next = CurrentLevel >= 3
            ? 1
            : CurrentLevel + 1;

        SetSpeed(next);
    }

    public void SetResolutionActive(bool active)
    {
        if (active == resolutionActive)
            return;

        if (active)
        {
            BeginResolutionSpeed();
            return;
        }

        EndResolutionSpeed();
    }

    /// <summary>
    /// Skill Timeline의 SetTimeScale 이벤트가 작성한 값에 현재 전투 배율을 합성한다.
    /// 예: authored 0.1 hit-stop + 2x battle speed => 기준 TimeScale * 0.2.
    /// </summary>
    public void ApplyAuthoredTimeScale(float authoredScale)
    {
        float safeAuthoredScale =
            Mathf.Max(0.01f, authoredScale);

        if (!resolutionActive || !capturedBaseTimeScale)
        {
            Time.timeScale = safeAuthoredScale;
            return;
        }

        Time.timeScale =
            baseTimeScale *
            SelectedMultiplier *
            safeAuthoredScale;
    }

    public void RestoreNominalBattleTimeScale()
    {
        if (!resolutionActive || !capturedBaseTimeScale)
            return;

        Time.timeScale =
            baseTimeScale *
            SelectedMultiplier;
    }

    private void BeginResolutionSpeed()
    {
        if (resolutionActive)
            return;

        baseTimeScale = Time.timeScale;
        capturedBaseTimeScale = true;
        resolutionActive = true;

        RestoreNominalBattleTimeScale();

        if (logChanges)
        {
            Debug.Log(
                $"[BattleSpeed] Resolution Begin / " +
                $"Base={baseTimeScale:0.###}, " +
                $"Mode={CurrentLevel}x, " +
                $"Applied={Time.timeScale:0.###}",
                this);
        }
    }

    private void EndResolutionSpeed()
    {
        if (!resolutionActive)
            return;

        resolutionActive = false;
        RestoreBaseTimeScale();

        if (logChanges)
        {
            Debug.Log(
                $"[BattleSpeed] Resolution End / " +
                $"Restored={Time.timeScale:0.###}",
                this);
        }
    }

    private void RestoreBaseTimeScale()
    {
        if (!capturedBaseTimeScale)
            return;

        Time.timeScale = baseTimeScale;
        capturedBaseTimeScale = false;
        baseTimeScale = 1f;
    }

    private void EnsureUi()
    {
        BattlePlaybackSpeedUI ui = null;

        BattleSceneHudRegistry registry =
            BattleSceneHudRegistry.Find();

        if (registry != null)
            ui = registry.PlaybackSpeedUi;

        if (ui == null)
        {
            if (!missingSceneUiLogged)
            {
                missingSceneUiLogged = true;
                Debug.LogError(
                    "[BattlePlaybackSpeedController] Registry의 Scene-authored BattlePlaybackSpeedUI 참조가 없습니다. " +
                    "전역 Find fallback으로 숨기지 않습니다.",
                    this);
            }
            return;
        }

        missingSceneUiLogged = false;
        ui.Bind(this);
    }

    private static BattlePlaybackSpeedMode SanitizeMode(
        BattlePlaybackSpeedMode mode)
    {
        int level = Mathf.Clamp((int)mode, 1, 3);
        return (BattlePlaybackSpeedMode)level;
    }

    private static bool IsValidMode(
        BattlePlaybackSpeedMode mode)
    {
        int value = (int)mode;
        return value >= 1 && value <= 3;
    }
}