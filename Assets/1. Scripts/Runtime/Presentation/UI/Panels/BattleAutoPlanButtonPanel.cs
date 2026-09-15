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

    // Auto-plan is a planning UI transaction. C-04 still keeps committed costs
    // non-refundable when a live action disappears during combat, but explicitly
    // cancelling/replacing the auto-plan before resolution must restore the
    // energy that existed before this auto-plan session started.
    private bool hasAutoPlanEnergySnapshot;
    private int autoPlanEnergySnapshot;

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

        // The first interactive auto-plan captures the energy baseline.
        // Switching WinRate <-> Damage is a re-plan of the same UI transaction,
        // so undo the previous auto-plan costs before calculating the new one.
        if (!activeMode.HasValue)
        {
            CaptureAutoPlanEnergySnapshot();
        }
        else if (activeMode.Value != mode)
        {
            if (!RollbackAutoPlanSession(
                    clearSnapshot: false))
            {
                SetStatus(
                    "도사림 즉시 효과가 이미 확정되어 자동 계획을 다시 짤 수 없습니다.");
                return;
            }

            activeMode = null;
        }

        PlayerAutoPlanResult result =
            service.BuildAndApply(
                battleManager,
                mode);

        if (result?.Success == true)
        {
            activeMode = mode;
        }
        else
        {
            // A failed auto-plan can already have committed one or more costs
            // before a later planning hook rejects the plan. Explicit UI planning
            // failure is rolled back to the pre-auto-plan energy baseline.
            if (RollbackAutoPlanSession(
                    clearSnapshot: true))
            {
                activeMode = null;
            }
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

        if (!RollbackAutoPlanSession(
                clearSnapshot: true))
        {
            SetStatus(
                "도사림 즉시 효과가 이미 확정되어 자동 계획을 취소할 수 없습니다.");

            Debug.LogWarning(
                "[PlayerAutoPlan][CANCEL BLOCKED] " +
                "계획 단계에서 이미 실행된 도사림이 있어 rollback을 거부했습니다.",
                this);
            return;
        }

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

        // Slots disappearing outside the explicit auto-plan cancel path means the
        // plan has left the editable planning transaction (resolution/turn reset,
        // external cleanup, etc.). Do not refund here: C-04's no-refund rule still
        // applies to runtime slot loss. Just discard the UI rollback snapshot.
        DiscardAutoPlanEnergySnapshot();
        ClearModeVisualOnly();

        if (statusText != null &&
            !battleManager.TurnManager.IsResolving)
        {
            SetStatus(
                "적 행동에 맞춰 자동 지정");
        }
    }

    private void CaptureAutoPlanEnergySnapshot()
    {
        Character player =
            battleManager?.BattleContext?.Player;

        if (player == null)
        {
            DiscardAutoPlanEnergySnapshot();
            return;
        }

        autoPlanEnergySnapshot =
            player.CurrentEnergy;
        hasAutoPlanEnergySnapshot = true;

        Debug.Log(
            $"[PlayerAutoPlan][SNAPSHOT] Energy={autoPlanEnergySnapshot}",
            this);
    }

    private bool RollbackAutoPlanSession(
        bool clearSnapshot)
    {
        Character player =
            battleManager?.BattleContext?.Player;

        // C-03: 도사림은 누르는 순간 효과가 확정된다. generic Skill.Execute의
        // 임의 효과를 완전히 되돌리는 역연산은 존재하지 않으므로, 이미 즉시 실행된
        // 도사림이 포함된 자동계획은 취소/모드교체 rollback 자체를 금지한다.
        if (HasCommittedImmediatePlayerAction(player))
        {
            Debug.LogWarning(
                "[PlayerAutoPlan][ROLLBACK BLOCKED] " +
                "Committed preparation cannot be reverted safely.",
                this);
            return false;
        }

        // Remove the auto-generated slots first so UI/validation immediately sees
        // an empty player plan. This is an explicit planning rollback, not combat
        // invalidation.
        battleManager?
            .ResetPlayerActions();

        if (player != null &&
            hasAutoPlanEnergySnapshot)
        {
            int before = player.CurrentEnergy;
            int delta =
                autoPlanEnergySnapshot - before;

            if (delta != 0)
            {
                player.AddEnergy(
                    delta,
                    CombatResourceChangeReason.Restore);
            }

            Debug.Log(
                $"[PlayerAutoPlan][ROLLBACK] Energy={before}->{player.CurrentEnergy}",
                this);
        }

        if (clearSnapshot)
            DiscardAutoPlanEnergySnapshot();

        return true;
    }

    private bool HasCommittedImmediatePlayerAction(
        Character player)
    {
        ActionManager actionManager =
            battleManager?.ActionManager;

        if (player == null ||
            actionManager == null ||
            actionManager.IsDisposed)
        {
            return false;
        }

        foreach (ActionSlot slot in actionManager.Slots)
        {
            if (slot?.Owner == player &&
                slot.PlanningEffectCommitted)
            {
                return true;
            }
        }

        return false;
    }

    private void DiscardAutoPlanEnergySnapshot()
    {
        hasAutoPlanEnergySnapshot = false;
        autoPlanEnergySnapshot = 0;
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
