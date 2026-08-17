using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleAutoPlanButtonPanel :
    MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private BattleUIManager battleUiManager;
    [SerializeField] private Button winRateButton;
    [SerializeField] private Button damageButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Selected Mode Visual")]
    [SerializeField, Range(0f, 1f)]
    private float selectedBrighten = 0.20f;

    [SerializeField, Range(0f, 1f)]
    private float inactiveBrightness = 0.30f;

    [Header("Runtime")]
    [SerializeField, Min(0.05f)]
    private float stateRefreshInterval = 0.15f;

    private readonly PlayerAutoPlanService service =
        new PlayerAutoPlanService();

    private Image winRateBackground;
    private Image damageBackground;

    private Color winRateBaseColor =
        Color.white;

    private Color damageBaseColor =
        Color.white;

    private bool capturedBaseColors;
    private float nextStateRefresh;

    private PlayerAutoPlanMode? activeMode;

    public PlayerAutoPlanMode? ActiveMode =>
        activeMode;

    /// <summary>
    /// Character Verification 자동 전투에서 승률 버튼과 동일한 계획을
    /// 매 턴 반복 적용한다. 일반 버튼의 "같은 모드 재클릭 시 취소" 동작은
    /// 자동 분석에 부적합하므로 이 경로에서는 계획을 취소하지 않는다.
    /// </summary>
    public PlayerAutoPlanResult ApplyWinRatePlanForAutomation()
    {
        return ApplyPlanForAutomation(
            PlayerAutoPlanMode.WinRate);
    }

    public PlayerAutoPlanResult ApplyPlanForAutomation(
        PlayerAutoPlanMode mode)
    {
        ResolveReferences();

        PlayerAutoPlanResult blocked =
            new PlayerAutoPlanResult
            {
                Mode = mode
            };

        if (!CanApplyPlan())
        {
            blocked.Message =
                "현재 자동 지정 불가";

            SetStatus(
                "검증 자동 진행 · 현재 자동 지정 불가");

            return blocked;
        }

        battleUiManager?
            .CancelCurrentSelection();

        PlayerAutoPlanResult result =
            service.BuildAndApply(
                battleManager,
                mode);

        if (result?.Success == true)
            activeMode = mode;

        SetStatus(
            "검증 자동 진행 · " +
            (result?.Message ??
             "자동 지정 결과 없음"));

        battleUiManager?
            .RefreshAllBodyPartButtons();

        ApplyModeVisuals();

        RefreshInteractableState(
            force: true);

        return result ?? blocked;
    }

    public void Configure(
        BattleManager manager,
        BattleUIManager uiManager,
        Button winButton,
        Button damageModeButton,
        TMP_Text statusLabel)
    {
        battleManager = manager;
        battleUiManager = uiManager;
        winRateButton = winButton;
        damageButton = damageModeButton;
        statusText = statusLabel;

        ResolveReferences();
        CaptureBaseColors();
        BindButtons();
        ApplyModeVisuals();
        RefreshInteractableState(force: true);
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseColors();
        BindButtons();

        if (statusText != null &&
            string.IsNullOrWhiteSpace(
                statusText.text))
        {
            statusText.text =
                "적 행동에 맞춰 자동 지정";
        }

        ApplyModeVisuals();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureBaseColors();
        BindButtons();
        SynchronizeModeWithCurrentSlots();
        ApplyModeVisuals();
        RefreshInteractableState(force: true);
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    private void Update()
    {
        SynchronizeModeWithCurrentSlots();
        RefreshInteractableState(force: false);
    }

    private void ResolveReferences()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>(
                    FindObjectsInactive.Include);
        }

        if (battleUiManager == null)
        {
            battleUiManager =
                FindFirstObjectByType<BattleUIManager>(
                    FindObjectsInactive.Include);
        }

        if (winRateButton == null)
        {
            winRateButton =
                transform.Find("WinRateButton")
                    ?.GetComponent<Button>();
        }

        if (damageButton == null)
        {
            damageButton =
                transform.Find("DamageButton")
                    ?.GetComponent<Button>();
        }

        if (statusText == null)
        {
            statusText =
                transform.Find("AutoPlanStatus")
                    ?.GetComponent<TMP_Text>();
        }

        winRateBackground ??=
            ResolveButtonImage(
                winRateButton);

        damageBackground ??=
            ResolveButtonImage(
                damageButton);
    }

    private static Image ResolveButtonImage(
        Button button)
    {
        if (button == null)
            return null;

        if (button.targetGraphic is Image image)
            return image;

        return button.GetComponent<Image>();
    }

    private void CaptureBaseColors()
    {
        if (capturedBaseColors)
            return;

        ResolveReferences();

        if (winRateBackground != null)
        {
            winRateBaseColor =
                winRateBackground.color;
        }

        if (damageBackground != null)
        {
            damageBaseColor =
                damageBackground.color;
        }

        capturedBaseColors =
            winRateBackground != null ||
            damageBackground != null;
    }

    private void BindButtons()
    {
        UnbindButtons();

        winRateButton?.onClick.AddListener(
            ApplyWinRatePlan);

        damageButton?.onClick.AddListener(
            ApplyDamagePlan);
    }

    private void UnbindButtons()
    {
        winRateButton?.onClick.RemoveListener(
            ApplyWinRatePlan);

        damageButton?.onClick.RemoveListener(
            ApplyDamagePlan);
    }

    private void ApplyWinRatePlan()
    {
        TogglePlan(
            PlayerAutoPlanMode.WinRate);
    }

    private void ApplyDamagePlan()
    {
        TogglePlan(
            PlayerAutoPlanMode.Damage);
    }

    private void TogglePlan(
        PlayerAutoPlanMode mode)
    {
        // Button 클릭과 같은 프레임의 마우스 입력이 월드 캐릭터 선택으로
        // 재사용되지 않도록 먼저 차단한다.
        BattleCharacterPointerRouter
            .BlockWorldInputForFrames(2);

        ResolveReferences();

        // 현재 선택된 모드를 한 번 더 누르면
        // 해당 자동 계획뿐 아니라 플레이어 행동 전체를 취소한다.
        if (activeMode.HasValue &&
            activeMode.Value == mode)
        {
            CancelAllPlayerActions();
            return;
        }

        if (!CanApplyPlan())
        {
            SetStatus(
                "현재 자동 지정 불가");
            return;
        }

        battleUiManager?
            .CancelCurrentSelection();

        PlayerAutoPlanResult result =
            service.BuildAndApply(
                battleManager,
                mode);

        if (result?.Success == true)
        {
            activeMode = mode;
        }

        SetStatus(
            result?.Message ??
            "자동 지정 결과 없음");

        battleUiManager?
            .RefreshAllBodyPartButtons();

        ApplyModeVisuals();

        RefreshInteractableState(
            force: true);
    }

    private void CancelAllPlayerActions()
    {
        BattleCharacterPointerRouter
            .BlockWorldInputForFrames(2);

        battleUiManager?
            .CancelCurrentSelection();

        battleManager?
            .ResetPlayerActions();

        activeMode = null;

        SetStatus(
            "자동 지정 해제 · 아군 행동 전체 취소");

        battleUiManager?
            .RefreshAllBodyPartButtons();

        ApplyModeVisuals();

        RefreshInteractableState(
            force: true);

        Debug.Log(
            "[PlayerAutoPlan][CANCELLED] " +
            "활성 자동 계획을 해제하고 플레이어 행동을 전부 취소했습니다.",
            this);
    }

    private bool CanApplyPlan()
    {
        if (battleManager == null ||
            !battleManager.IsInitialized ||
            battleManager.IsEndingOrEnded ||
            battleManager.BattleContext?.Player == null ||
            battleManager.BattleContext.Player.IsDead ||
            battleManager.TurnManager == null ||
            !battleManager.TurnManager.IsBattleRunning ||
            battleManager.TurnManager.IsResolving)
        {
            return false;
        }

        ActionManager actionManager =
            battleManager.ActionManager;

        if (actionManager?.IsDisposed != false)
            return false;

        foreach (ActionSlot slot
                 in actionManager.Slots)
        {
            Character owner =
                slot?.Owner;

            if (owner == null ||
                slot.Skill == null)
            {
                continue;
            }

            if (battleManager.BattleContext
                    .Enemies.Contains(owner))
            {
                return true;
            }
        }

        return false;
    }

    private void SynchronizeModeWithCurrentSlots()
    {
        if (!activeMode.HasValue)
            return;

        Character player =
            battleManager?.BattleContext?.Player;

        ActionManager actionManager =
            battleManager?.ActionManager;

        if (player == null ||
            actionManager == null ||
            actionManager.IsDisposed)
        {
            ClearModeVisualOnly();
            return;
        }

        int playerSlotCount =
            actionManager.CountSlots(
                player);

        if (playerSlotCount > 0)
            return;

        ClearModeVisualOnly();

        if (statusText != null &&
            !battleManager.TurnManager.IsResolving)
        {
            SetStatus(
                "적 행동에 맞춰 자동 지정");
        }
    }

    private void ClearModeVisualOnly()
    {
        activeMode = null;
        ApplyModeVisuals();
    }

    private void ApplyModeVisuals()
    {
        CaptureBaseColors();

        if (!activeMode.HasValue)
        {
            SetImageColor(
                winRateBackground,
                winRateBaseColor);

            SetImageColor(
                damageBackground,
                damageBaseColor);

            return;
        }

        bool winRateSelected =
            activeMode.Value ==
            PlayerAutoPlanMode.WinRate;

        SetImageColor(
            winRateBackground,
            winRateSelected
                ? Brighten(
                    winRateBaseColor)
                : Dim(
                    winRateBaseColor));

        SetImageColor(
            damageBackground,
            winRateSelected
                ? Dim(
                    damageBaseColor)
                : Brighten(
                    damageBaseColor));
    }

    private Color Brighten(
        Color source)
    {
        Color bright =
            Color.Lerp(
                source,
                Color.white,
                Mathf.Clamp01(
                    selectedBrighten));

        bright.a = source.a;
        return bright;
    }

    private Color Dim(
        Color source)
    {
        float factor =
            Mathf.Clamp01(
                inactiveBrightness);

        return new Color(
            source.r * factor,
            source.g * factor,
            source.b * factor,
            source.a);
    }

    private static void SetImageColor(
        Image image,
        Color color)
    {
        if (image != null)
            image.color = color;
    }

    private void RefreshInteractableState(
        bool force)
    {
        if (!force &&
            Time.unscaledTime <
            nextStateRefresh)
        {
            return;
        }

        nextStateRefresh =
            Time.unscaledTime +
            Mathf.Max(
                0.05f,
                stateRefreshInterval);

        bool interactable =
            CanApplyPlan();

        if (winRateButton != null)
        {
            winRateButton.interactable =
                interactable;
        }

        if (damageButton != null)
        {
            damageButton.interactable =
                interactable;
        }
    }

    private void SetStatus(
        string value)
    {
        if (statusText == null)
            return;

        statusText.text =
            string.IsNullOrWhiteSpace(value)
                ? "적 행동에 맞춰 자동 지정"
                : value;
    }
}