using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleAutoPlanButtonPanel :
    MonoBehaviour
{
    // Full click snapshots enumerate all targets/slots and build a large log string.
    // Keep them available for deep debugging, but do not pay that cost on every
    // normal WinRate/Damage button click.
    private static readonly bool VerboseClickDiagnostics = false;

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
    private bool? lastDiagnosticInteractableState;

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

        // [0918_AUTOPLAN_DIAG:POINTER_PROBE]
        // Unity Button은 interactable=false일 때 onClick 자체를 호출하지 않는다.
        // 진단 중에는 별도 EventSystem probe로 PointerDown/PointerClick을 받아
        // 비활성 버튼을 눌러도 CanApplyPlan 탈락 원인을 Console에 남긴다.
        EnsureDiagnosticProbe(
            winRateButton,
            PlayerAutoPlanMode.WinRate);

        EnsureDiagnosticProbe(
            damageButton,
            PlayerAutoPlanMode.Damage);
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
        if (VerboseClickDiagnostics)
        {
            Debug.Log(
                BuildDiagnosticSnapshot(
                    PlayerAutoPlanMode.WinRate,
                    "ONCLICK"),
                this);
        }

        TogglePlan(
            PlayerAutoPlanMode.WinRate);
    }

    private void ApplyDamagePlan()
    {
        if (VerboseClickDiagnostics)
        {
            Debug.Log(
                BuildDiagnosticSnapshot(
                    PlayerAutoPlanMode.Damage,
                    "ONCLICK"),
                this);
        }

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

        // 자동계획은 현재 수동/자동 Planning을 명시적으로 취소한 뒤 새로 계산한다.
        // CancelOwner가 각 슬롯의 즉시 도사림 효과와 실제 지불 Energy를 함께 되돌리므로,
        // WinRate <-> Damage 전환도 같은 transaction 경로를 사용한다.
        if (!RollbackAutoPlanSession())
        {
            SetStatus(
                "현재 Planning 행동을 취소하지 못해 자동 계획을 다시 짤 수 없습니다.");
            return;
        }

        activeMode = null;

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
            // failure is rolled back through the same explicit Planning cancellation path.
            if (RollbackAutoPlanSession())
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

        if (!RollbackAutoPlanSession())
        {
            SetStatus(
                "현재 Planning 행동을 취소하지 못했습니다.");

            Debug.LogWarning(
                "[PlayerAutoPlan][CANCEL BLOCKED] " +
                "Planning 취소 중 일부 상태를 되돌리지 못했습니다.",
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
        return EvaluateCanApplyPlan(
            out _);
    }

    private bool EvaluateCanApplyPlan(
        out string reason)
    {
        if (battleManager == null)
        {
            reason = "BATTLE_MANAGER_NULL";
            return false;
        }

        if (!battleManager.IsInitialized)
        {
            reason = "BATTLE_MANAGER_NOT_INITIALIZED";
            return false;
        }

        if (battleManager.IsEndingOrEnded)
        {
            reason = "BATTLE_ENDING_OR_ENDED";
            return false;
        }

        BattleContext context =
            battleManager.BattleContext;

        if (context?.Player == null)
        {
            reason = "PLAYER_NULL";
            return false;
        }

        if (context.Player.IsDead)
        {
            reason = "PLAYER_DEAD";
            return false;
        }

        if (battleManager.TurnManager == null)
        {
            reason = "TURN_MANAGER_NULL";
            return false;
        }

        if (!battleManager.TurnManager.IsBattleRunning)
        {
            reason = "BATTLE_NOT_RUNNING";
            return false;
        }

        if (battleManager.TurnManager.IsResolving)
        {
            reason = "TURN_IS_RESOLVING";
            return false;
        }

        ActionManager actionManager =
            battleManager.ActionManager;

        if (actionManager == null)
        {
            reason = "ACTION_MANAGER_NULL";
            return false;
        }

        if (actionManager.IsDisposed)
        {
            reason = "ACTION_MANAGER_DISPOSED";
            return false;
        }

        // [0918_NORMAL_AUTOPLAN_HOTFIX:LIVING_ENEMY_GATE]
        // [0918_NORMAL_AUTOPLAN_HOTFIX_V2:CANONICAL_TARGET_GATE]
        IReadOnlyList<Character> enemies =
            context.Enemies;

        if (enemies == null)
        {
            reason = "ENEMY_LIST_NULL";
            return false;
        }

        bool hasLivingEnemy = false;

        foreach (Character enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                hasLivingEnemy = true;
                break;
            }
        }

        if (!hasLivingEnemy)
        {
            reason = "NO_LIVING_ENEMY";
            return false;
        }

        if (!PlayerAutoPlanService.HasAnyAutoPlanTarget(
                context))
        {
            reason = "NO_CANONICAL_AUTOPLAN_TARGET";
            return false;
        }

        reason = "PASS";
        return true;
    }

    internal void LogAutoPlanDiagnosticPointer(
        PlayerAutoPlanMode mode,
        string pointerPhase,
        PointerEventData eventData)
    {
        ResolveReferences();

        string pointer =
            eventData == null
                ? pointerPhase
                : $"{pointerPhase} button={eventData.button} pos={eventData.position}";

        Debug.Log(
            BuildDiagnosticSnapshot(
                mode,
                pointer),
            this);
    }

    private void EnsureDiagnosticProbe(
        Button button,
        PlayerAutoPlanMode mode)
    {
        if (button == null)
            return;

        BattleAutoPlanDiagnosticClickProbe probe =
            button.GetComponent<BattleAutoPlanDiagnosticClickProbe>();

        if (probe == null)
        {
            probe = button.gameObject.AddComponent<
                BattleAutoPlanDiagnosticClickProbe>();
        }

        probe.Configure(
            this,
            mode);
    }

    private string BuildDiagnosticSnapshot(
        PlayerAutoPlanMode mode,
        string origin)
    {
        ResolveReferences();

        bool canApply =
            EvaluateCanApplyPlan(
                out string gateReason);

        StringBuilder sb =
            new StringBuilder(2048);

        sb.AppendLine(
            $"[AUTO_PLAN_DIAG][{origin}][{mode}] CanApply={canApply} Gate={gateReason}");

        sb.AppendLine(
            $"Panel activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}");

        AppendButtonDiagnostic(
            sb,
            "WinRateButton",
            winRateButton);

        AppendButtonDiagnostic(
            sb,
            "DamageButton",
            damageButton);

        if (EventSystem.current == null)
        {
            sb.AppendLine(
                "EventSystem=NULL");
        }
        else
        {
            sb.AppendLine(
                $"EventSystem={EventSystem.current.name} active={EventSystem.current.isActiveAndEnabled}");
        }

        if (battleManager == null)
        {
            sb.AppendLine(
                "BattleManager=NULL");
            return sb.ToString();
        }

        sb.AppendLine(
            $"BattleManager init={battleManager.IsInitialized} ending={battleManager.IsEndingOrEnded}");

        sb.AppendLine(
            $"TurnManager null={battleManager.TurnManager == null} running={battleManager.TurnManager?.IsBattleRunning ?? false} resolving={battleManager.TurnManager?.IsResolving ?? false}");

        sb.AppendLine(
            $"SpeedManager null={battleManager.SpeedManager == null}");

        ActionManager actionManager =
            battleManager.ActionManager;

        sb.AppendLine(
            $"ActionManager null={actionManager == null} disposed={actionManager?.IsDisposed ?? true} slotCount={actionManager?.Slots?.Count ?? -1}");

        BattleContext context =
            battleManager.BattleContext;

        Character player =
            context?.Player;

        sb.AppendLine(
            $"Context null={context == null} Player={(player == null ? "NULL" : player.name)} dead={player?.IsDead ?? true} energy={(player == null ? "-" : $"{player.CurrentEnergy}/{player.MaxEnergy}")}");

        IReadOnlyList<Character> enemies =
            context?.Enemies;

        sb.AppendLine(
            $"Enemies null={enemies == null} count={enemies?.Count ?? -1} HasAnyAutoPlanTarget={PlayerAutoPlanService.HasAnyAutoPlanTarget(context)}");

        if (enemies != null)
        {
            TargetSelectionRule rule =
                TargetSelectionRule.StandardAttack;

            for (int i = 0; i < enemies.Count; i++)
            {
                Character enemy = enemies[i];

                if (enemy == null)
                {
                    sb.AppendLine(
                        $"Enemy[{i}]=NULL");
                    continue;
                }

                IReadOnlyList<TargetPoint> points =
                    BattleTargetValidator.GetTargetPoints(
                        enemy,
                        rule);

                sb.AppendLine(
                    $"Enemy[{i}] name={enemy.name} dead={enemy.IsDead} usesBodyParts={enemy.UsesBodyParts} targetPoints={points?.Count ?? -1}");

                if (points != null)
                {
                    for (int p = 0; p < points.Count; p++)
                    {
                        TargetPoint point = points[p];
                        bool validByRule =
                            point.Character != null &&
                            BattleTargetValidator.IsValid(
                                point.Character,
                                point.Part,
                                rule);

                        sb.AppendLine(
                            $"  Target[{p}] isValid={point.IsValid} char={(point.Character == null ? "NULL" : point.Character.name)} part={(point.Part == null ? "NULL" : point.Part.Type.ToString())} ruleValid={validByRule}");
                    }
                }
            }
        }

        if (actionManager?.Slots != null)
        {
            for (int i = 0; i < actionManager.Slots.Count; i++)
            {
                ActionSlot slot =
                    actionManager.Slots[i];

                sb.AppendLine(
                    slot == null
                        ? $"Slot[{i}]=NULL"
                        : $"Slot[{i}] owner={(slot.Owner == null ? "NULL" : slot.Owner.name)} skill={(slot.Skill == null ? "NULL" : slot.Skill.SkillName)} phase={slot.Phase} targetChar={(slot.TargetCharacter == null ? "NULL" : slot.TargetCharacter.name)} targetPart={(slot.TargetPart == null ? "NULL" : slot.TargetPart.Type.ToString())} targetSlotNull={slot.TargetSlot == null}");
            }
        }

        return sb.ToString();
    }

    private static void AppendButtonDiagnostic(
        StringBuilder sb,
        string label,
        Button button)
    {
        if (button == null)
        {
            sb.AppendLine(
                $"{label}=NULL");
            return;
        }

        Graphic graphic =
            button.targetGraphic;

        sb.AppendLine(
            $"{label} name={button.name} activeSelf={button.gameObject.activeSelf} activeInHierarchy={button.gameObject.activeInHierarchy} enabled={button.enabled} interactable={button.interactable} targetGraphic={(graphic == null ? "NULL" : graphic.name)} raycastTarget={graphic?.raycastTarget ?? false}");
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
        
        ClearModeVisualOnly();

        if (statusText != null &&
            !battleManager.TurnManager.IsResolving)
        {
            SetStatus(
                "적 행동에 맞춰 자동 지정");
        }
    }

    private bool RollbackAutoPlanSession()
    {
        Character player =
            battleManager?.BattleContext?.Player;

        if (player == null)
            return false;

        battleManager?
            .ResetPlayerActions();

        bool cleared =
            battleManager?.ActionManager?
                .CountSlots(player) == 0;

        if (cleared)
        {
            Debug.Log(
                $"[PlayerAutoPlan][ROLLBACK] " +
                $"Planning cleared / Energy={player.CurrentEnergy}/{player.MaxEnergy}",
                this);
        }

        return cleared;
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
            EvaluateCanApplyPlan(
                out string gateReason);

        if (!lastDiagnosticInteractableState.HasValue ||
            lastDiagnosticInteractableState.Value != interactable)
        {
            lastDiagnosticInteractableState = interactable;

            Debug.Log(
                $"[AUTO_PLAN_DIAG][INTERACTABLE_CHANGED] value={interactable} gate={gateReason}",
                this);
        }

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