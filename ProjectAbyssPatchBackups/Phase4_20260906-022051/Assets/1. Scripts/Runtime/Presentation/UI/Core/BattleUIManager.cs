using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private SkillSelectPanelUI skillSelectPanel;
    [SerializeField] private BodyPartButtonRegistry bodyPartButtonRegistry;
    [SerializeField] private BattleTargetButtonRegistry targetButtonRegistry;
    [SerializeField] private BattleCharacterDetailPanelUI characterDetailPanel;

    [Header("Dynamic Participant Buttons")]
    [SerializeField]
    private BattleParticipantButtonFactory participantButtonFactory;

    private readonly TargetSelectionViewModel selection = new();
    private readonly List<BodyPartButton> buttonScratch = new();

    private readonly BattlePlanningSelectionState
        planningState = new();

    private BattlePlanningQueryService planningQuery;
    private BattleActionPlanCommandService actionPlanCommands;

    private BattleActionPlanCommandService PlanCommands
    {
        get
        {
            if (battleManager?.ActionManager == null ||
                battleManager.SpeedManager == null)
            {
                return null;
            }

            return actionPlanCommands ??=
                new BattleActionPlanCommandService(
                    battleManager.ActionManager,
                    battleManager.SpeedManager);
        }
    }

    public TargetSelectionViewModel Selection => selection;

    // 기존 내부 코드의 이름을 유지하되 실제 state storage는 별도 객체가 소유한다.
    private BattleInputMode inputMode
    {
        get => planningState.InputMode;
        set => planningState.InputMode = value;
    }

    //---------------------------------------
    // 현재 선택 정보
    //---------------------------------------

    private Character selectedOwner
    {
        get => planningState.SelectedOwner;
        set => planningState.SelectedOwner = value;
    }

    private BodyPart selectedOwnerPart
    {
        get => planningState.SelectedOwnerPart;
        set => planningState.SelectedOwnerPart = value;
    }

    private int selectedActionIndex
    {
        get => planningState.SelectedActionIndex;
        set => planningState.SelectedActionIndex = value;
    }

    private int selectedMaxActionSlots
    {
        get => planningState.SelectedMaxActionSlots;
        set => planningState.SelectedMaxActionSlots = value;
    }

    private Dictionary<BodyPart, int>
        actionIndexCursorByPart =>
            planningState.ActionIndexCursorByPart;

    private Character selectedTarget
    {
        get => planningState.SelectedTarget;
        set => planningState.SelectedTarget = value;
    }

    private BodyPart selectedTargetPart
    {
        get => planningState.SelectedTargetPart;
        set => planningState.SelectedTargetPart = value;
    }

    private BattlePlanningQueryService PlanningQuery =>
        planningQuery ??=
            new BattlePlanningQueryService(
                planningState,
                selection);

    public int SelectedActionIndex => selectedActionIndex;
    public int SelectedMaxActionSlots => selectedMaxActionSlots;
    public Character SelectedOwner => selectedOwner;
    public BodyPart SelectedOwnerPart => selectedOwnerPart;

    public BattleInputMode InputMode => inputMode;
    public bool IsSelectingTarget => inputMode == BattleInputMode.SelectTarget;
    public bool IsSelectingSkill => inputMode == BattleInputMode.SelectSkill;
    public BattleManager BattleManager => battleManager;

    /// <summary>
    /// 월드 플레이어 행동 슬롯이 스킬 지정 전에도 표시할 현재 턴 부위 속도.
    /// </summary>
    public int GetWorldPlanningSlotSpeed(
        Character owner,
        BodyPart part)
    {
        return PlanningQuery
            .GetWorldPlanningSlotSpeed(
                battleManager,
                owner,
                part);
    }

    /// <summary>
    /// 월드 머리 위 슬롯이 현재 편집 대상으로 선택되었는지 반환한다.
    /// Hover/Select 애니메이션과 Pending 화살표가 동일한 선택 상태를 공유한다.
    /// </summary>
    public bool IsWorldPlanningSlotSelected(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return PlanningQuery
            .IsWorldPlanningSlotSelected(
                owner,
                part,
                actionIndex);
    }

    /// <summary>
    /// 스킬 선택이 끝나 정확한 적 ActionSlot을 고르는 중인 플레이어 슬롯.
    /// 이 상태에서 해당 슬롯은 느리게 반짝이고 Mouse Arrow의 시작점이 된다.
    /// </summary>
    public bool IsWorldPlanningSlotPendingTarget(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return PlanningQuery
            .IsWorldPlanningSlotPendingTarget(
                owner,
                part,
                actionIndex);
    }

    /// <summary>
    /// 현재 플레이어 계획 중 하나가 해당 적 ActionSlot을 정확히 TargetSlot로 지정했는지 반환한다.
    /// </summary>
    public bool IsWorldTargetSlotAssigned(
        ActionSlot targetSlot)
    {
        return PlanningQuery
            .IsWorldTargetSlotAssigned(
                battleManager,
                targetSlot);
    }

    /// <summary>
    /// 스킬 선택/대상 선택 중에는 현재 논리 슬롯을 교체한다고 가정하여
    /// 계획 완료 뒤 실제로 남을 빛을 실시간 계산한다.
    /// 실제 Character.CurrentEnergy는 턴 해석 전까지 변경하지 않는다.
    /// </summary>
    public bool TryGetPlayerEnergyDisplay(
        Character player,
        out int available,
        out int maximum,
        out int plannedCost,
        out int pendingCost,
        out bool hasPendingPreview)
    {
        return PlanningQuery.TryGetPlayerEnergyDisplay(
            battleManager,
            player,
            out available,
            out maximum,
            out plannedCost,
            out pendingCost,
            out hasPendingPreview);
    }

    public IReadOnlyList<Skill> GetSelectableSkillsForCurrentSlot(
        BodyPart part)
    {
        return PlanningQuery
            .GetSelectableSkillsForCurrentSlot(
                part);
    }

    /// <summary>
    /// 스킬 서랍에는 현재 선택한 부위와 행동 슬롯의 구조 규칙을 통과한
    /// 장착 스킬만 표시한다. 카드의 자원·상태 조건은 기존 선택 검증이
    /// 별도로 처리한다.
    /// </summary>
    public IReadOnlyList<Skill> GetSkillsForCurrentSlotDrawer(
        BodyPart part)
    {
        return GetSelectableSkillsForCurrentSlot(part);
    }

    //---------------------------------------

    private void Awake()
    {
        EnsureReferences();
        SyncSelectionViewModel();
    }

    private void EnsureReferences()
    {
        if (battleManager == null)
        {
            battleManager =
                FindFirstObjectByType<BattleManager>();
        }

        if (skillSelectPanel == null)
        {
            skillSelectPanel =
                FindFirstObjectByType<SkillSelectPanelUI>();
        }

        if (bodyPartButtonRegistry == null)
        {
            bodyPartButtonRegistry =
                FindFirstObjectByType<BodyPartButtonRegistry>();
        }

        if (bodyPartButtonRegistry == null)
        {
            bodyPartButtonRegistry =
                gameObject.AddComponent<BodyPartButtonRegistry>();
        }

        if (targetButtonRegistry == null)
        {
            targetButtonRegistry =
                FindFirstObjectByType<BattleTargetButtonRegistry>();
        }

        if (targetButtonRegistry == null)
        {
            targetButtonRegistry =
                gameObject.AddComponent<BattleTargetButtonRegistry>();
        }

        if (characterDetailPanel == null)
        {
            characterDetailPanel =
                FindFirstObjectByType<BattleCharacterDetailPanelUI>(
                    FindObjectsInactive.Include);
        }

        if (participantButtonFactory == null)
        {
            participantButtonFactory =
                GetComponent<BattleParticipantButtonFactory>();
        }

        if (participantButtonFactory == null)
        {
            participantButtonFactory =
                gameObject.AddComponent<BattleParticipantButtonFactory>();
        }
    }

    public void AssignParticipantButtonFactory(
        BattleParticipantButtonFactory factory)
    {
        participantButtonFactory = factory;
        EnsureReferences();
    }

    public void BuildParticipantButtons(
        BattleContext context)
    {
        EnsureReferences();
        ClearSelection();

        participantButtonFactory?.Rebuild(
            context,
            this,
            bodyPartButtonRegistry);

        RefreshAllBodyPartButtons();
    }

    public void ClearParticipantButtons()
    {
        participantButtonFactory?.ClearGeneratedButtons();
        bodyPartButtonRegistry?.RebuildFromScene();
        ClearSelection();
    }

    public BodyPartButtonRegistry GetButtonRegistry()
    {
        EnsureReferences();
        return bodyPartButtonRegistry;
    }

    private void Start()
    {
        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        if (bodyPartButtonRegistry != null &&
            bodyPartButtonRegistry.Buttons.Count == 0)
        {
            bodyPartButtonRegistry.RebuildFromScene();
        }

        RefreshAllBodyPartButtons();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(1))
            return;

        // 스킬 선택창이 떠 있는 상태에서는
        // UI 위에서 우클릭해도 무조건 현재 선택 취소
        if (inputMode == BattleInputMode.SelectSkill)
        {
            CancelCurrentSelection();
            return;
        }

        // BodyPartButton 위에서 우클릭한 경우는
        // BodyPartButton.OnPointerClick이 처리하게 둠
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // 빈 공간 우클릭이면 현재 선택 취소
        CancelCurrentSelection();
    }
    
    private BodyPartButtonViewModel CreateBodyPartButtonViewModel(
        BodyPartButton button)
    {
        if (button == null)
        {
            return new BodyPartButtonViewModel
            {
                PartText = "NULL",
                HpText = "HP -",
                SpeedText = "SPD -",
                SlotText = "",
                SkillText = "",
                AssignedSkills = System.Array.Empty<Skill>(),
                Interactable = false
            };
        }

        Character owner =
            button.Owner;

        BodyPart part =
            button.BodyPart;

        if (owner == null)
        {
            return new BodyPartButtonViewModel
            {
                PartText = "NULL",
                HpText = "HP -",
                SpeedText = "SPD -",
                SlotText = "",
                SkillText = "",
                AssignedSkills = System.Array.Empty<Skill>(),
                Interactable = false
            };
        }

        bool characterTarget =
            part == null &&
            owner.IsSingleHpTarget;

        return new BodyPartButtonViewModel
        {
            PartText =
                GetPartText(
                    owner,
                    part),

            HpText =
                GetHpText(button),

            SpeedText =
                GetSpeedText(owner, part),

            SlotText =
                characterTarget
                    ? ""
                    : GetActionSlotStatusText(
                        owner,
                        part),

            SkillText =
                GetSelectedSkillText(
                    owner,
                    part),

            AssignedSkills =
                GetAssignedSkills(
                    owner,
                    part),

            Interactable =
                CanInteractWithPart(
                    owner,
                    part),

            IsOwnerSelected =
                IsOwnerHighlighted(
                    owner,
                    part),

            IsTargetSelected =
                IsTargetHighlighted(
                    owner,
                    part),

            IsWeakened =
                part != null &&
                part.IsWeakened,

            IsBroken =
                part != null &&
                part.IsBroken,

            IsCharacterTarget = characterTarget,
            HasHpOverride = button.HasHpOverride,
            ActionCount = GetSlotsByOwnerPart(owner, part).Count,
            MaxActionSlots =
                part == null
                    ? 0
                    : GetMaxActionSlots(owner, part)
        };
    }
    
    private string GetSelectedSkillText(
        Character owner,
        BodyPart part)
    {
        IReadOnlyList<Skill> skills =
            GetAssignedSkills(
                owner,
                part);

        if (skills == null ||
            skills.Count == 0)
        {
            return "";
        }

        StringBuilder builder =
            new();

        for (int actionIndex = 0;
             actionIndex < skills.Count;
             actionIndex++)
        {
            Skill skill =
                skills[actionIndex];

            if (skill == null)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(
                $"<color=#FFFFFF><b>[#{actionIndex + 1}] " +
                $"{skill.SkillName}</b></color>");
        }

        return builder.ToString();
    }


    private IReadOnlyList<Skill> GetAssignedSkills(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return System.Array.Empty<Skill>();

        List<ActionSlot> slots =
            GetSlotsByOwnerPart(
                owner,
                part);

        int highestActionIndex =
            -1;

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot == null)
                continue;

            highestActionIndex =
                Mathf.Max(
                    highestActionIndex,
                    slot.ActionIndex);
        }

        bool hasPendingSkill =
            inputMode == BattleInputMode.SelectTarget &&
            selectedOwner == owner &&
            IsSamePart(
                selectedOwnerPart,
                part) &&
            selection.Skill != null;

        if (hasPendingSkill)
        {
            highestActionIndex =
                Mathf.Max(
                    highestActionIndex,
                    selectedActionIndex);
        }

        if (highestActionIndex < 0)
            return System.Array.Empty<Skill>();

        List<Skill> assigned =
            new(highestActionIndex + 1);

        for (int index = 0;
             index <= highestActionIndex;
             index++)
        {
            assigned.Add(null);
        }

        foreach (ActionSlot slot
                 in slots)
        {
            if (slot?.Skill == null ||
                slot.ActionIndex < 0 ||
                slot.ActionIndex >= assigned.Count)
            {
                continue;
            }

            assigned[slot.ActionIndex] =
                slot.Skill;
        }

        // 대상 선택 전의 Pending 스킬도 즉시 공/수 패턴에 반영한다.
        if (hasPendingSkill &&
            selectedActionIndex >= 0 &&
            selectedActionIndex < assigned.Count)
        {
            assigned[selectedActionIndex] =
                selection.Skill;
        }

        return assigned;
    }


    private string GetActionSlotStatusText(
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            part == null ||
            !IsPlayer(owner))
        {
            return "";
        }

        int maxSlots =
            GetMaxActionSlots(owner, part);

        int currentCount =
            GetSlotsByOwnerPart(owner, part).Count;

        if (maxSlots <= 1)
        {
            return
                selectedOwner == owner &&
                IsSamePart(selectedOwnerPart, part)
                    ? "<color=#93C5FD>행동 슬롯 #1 선택</color>"
                    : "";
        }

        string selectionText =
            selectedOwner == owner &&
            IsSamePart(selectedOwnerPart, part)
                ? $" / 편집 #{selectedActionIndex + 1}"
                : "";

        return
            $"<color=#93C5FD>행동 {currentCount}/{maxSlots}" +
            $"{selectionText}</color>";
    }

    private List<ActionSlot> GetSlotsByOwnerPart(
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            battleManager?.ActionManager == null)
        {
            return new List<ActionSlot>();
        }

        return battleManager.ActionManager
            .FindSlots(owner, part);
    }

    private int GetMaxActionSlots(
        Character owner,
        BodyPart part)
    {
        if (owner == null ||
            part == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            owner.GetMaxActionSlotsForPart(part));
    }

    private bool CanInteractWithPart(
        Character owner,
        BodyPart part)
    {
        if (owner == null || owner.IsDead)
            return false;

        if (IsPlayer(owner))
        {
            if (part == null)
                return false;

            return !part.IsBroken && part.IsUsable;
        }

        if (inputMode != BattleInputMode.SelectTarget)
            return false;

        return BattleTargetValidator.IsValid(
            owner,
            part,
            selection.TargetRule);
    }

    private bool IsOwnerHighlighted(
        Character owner,
        BodyPart part)
    {
        if (owner == null || part == null)
            return false;

        if (selectedOwner == owner &&
            IsSamePart(selectedOwnerPart, part))
        {
            return true;
        }

        if (battleManager == null ||
            battleManager.ActionManager == null ||
            battleManager.BattleContext == null)
        {
            return false;
        }

        Character player =
            battleManager.BattleContext.Player;

        foreach (ActionSlot slot in battleManager.ActionManager.Slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != player)
                continue;

            if (slot.Owner == owner &&
                IsSamePart(slot.Part, part))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTargetHighlighted(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return false;

        if (selectedTarget == owner &&
            IsSameTargetPart(
                selectedTargetPart,
                part))
        {
            return true;
        }

        if (battleManager?.ActionManager == null ||
            battleManager.BattleContext == null)
        {
            return false;
        }

        Character player =
            battleManager.BattleContext.Player;

        foreach (ActionSlot slot
                 in battleManager.ActionManager.Slots)
        {
            if (slot == null ||
                slot.Owner != player)
            {
                continue;
            }

            if (slot.TargetCharacter == owner &&
                IsSameTargetPart(
                    slot.TargetPart,
                    part))
            {
                return true;
            }
        }

        return false;
    }

    private string GetPartText(
        Character owner,
        BodyPart part)
    {
        if (owner == null)
            return "NULL";

        string characterName =
            GetCharacterName(owner);

        if (part == null)
        {
            return
                $"<size=72%>{characterName}</size>\n" +
                "<color=#86EFAC><b>SINGLE HP</b></color>";
        }

        string stateColor =
            part.State switch
            {
                BodyPartState.Normal => "#86EFAC",
                BodyPartState.Weakened => "#FACC15",
                BodyPartState.Broken => "#F87171",
                _ => "#FFFFFF"
            };

        return
            $"<size=72%>{characterName}</size>\n" +
            $"<color={stateColor}><b>" +
            $"{part.Type} [{part.State}]" +
            "</b></color>";
    }

    private string GetHpText(
        BodyPartButton button)
    {
        if (button?.Owner == null)
            return "HP -";

        Character owner =
            button.Owner;

        BodyPart part =
            button.BodyPart;

        int currentHp =
            button.HasHpOverride
                ? button.HpOverrideValue
                : part != null
                    ? Mathf.RoundToInt(
                        part.PartHP)
                    : owner.CurrentHP;

        int maxHp =
            part != null
                ? Mathf.RoundToInt(
                    part.MaxPartHP)
                : Mathf.Max(
                    1,
                    owner.MaxCombatHP);

        string hpColor =
            GetHpColor(
                owner,
                part,
                currentHp,
                maxHp);

        return
            $"<color={hpColor}>HP {currentHp}/{maxHp}</color>";
    }

    private string GetHpColor(
        Character owner,
        BodyPart part,
        int displayHp,
        int maxHp)
    {
        if (owner == null)
            return "#FFFFFF";

        if (part != null)
        {
            if (part.IsBroken)
                return "#F87171";

            if (part.IsWeakened)
                return "#FACC15";
        }

        if (maxHp <= 0)
            return "#FFFFFF";

        float ratio =
            (float)displayHp /
            maxHp;

        if (ratio <= 0.3f)
            return "#FACC15";

        return "#86EFAC";
    }
    
    public void SetBodyPartHpOverride(
        Character character,
        BodyPart part,
        int hp)
    {
        SetTargetHpOverride(
            character,
            part,
            hp);
    }

    public void SetTargetHpOverride(
        Character character,
        BodyPart part,
        int hp)
    {
        BodyPartButton button =
            FindBodyPartButton(
                character,
                part);

        button?.SetHpOverride(hp);
    }

    public void ClearBodyPartHpOverride(
        Character character,
        BodyPart part)
    {
        ClearTargetHpOverride(
            character,
            part);
    }

    public void ClearTargetHpOverride(
        Character character,
        BodyPart part)
    {
        BodyPartButton button =
            FindBodyPartButton(
                character,
                part);

        button?.ClearHpOverride();
    }

    private BodyPartButton FindBodyPartButton(
        Character character,
        BodyPart part)
    {
        if (character == null)
            return null;

        return bodyPartButtonRegistry?.Find(
            character,
            part,
            requireActive: false);
    }

    private string GetSpeedText(
        Character owner,
        BodyPart part)
    {
        if (battleManager?.SpeedManager == null)
            return "SPD 0";

        int speed =
            battleManager.SpeedManager.GetSpeed(
                owner,
                part);

        return $"SPD {speed}";
    }

    public void ResetTurnInputState()
    {
        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        ClearSelection();

        ClearLines();
    }
    
    //---------------------------------------
    // BodyPartButton 왼쪽 클릭
    //---------------------------------------

    public void OnBodyPartClicked(
        Character owner,
        BodyPart part)
    {
        if (!IsManagerReady() ||
            owner == null)
        {
            return;
        }

        if (inputMode ==
            BattleInputMode.SelectOwner)
        {
            if (!IsPlayer(owner) ||
                part == null)
            {
                Debug.Log(
                    "먼저 플레이어의 행동 부위를 선택하세요.");
                return;
            }

            bool success =
                TrySelectOwnerSlot(
                    owner,
                    part);

            if (success)
            {
                inputMode =
                    BattleInputMode.SelectSkill;
                SyncSelectionViewModel();
                ShowSkillPanel();
            }

            return;
        }

        if (inputMode ==
            BattleInputMode.SelectTarget)
        {
            if (IsPlayer(owner))
            {
                BattleDebugLog.UIInput(
                    "대상으로는 적을 선택하세요.");
                return;
            }

            bool success =
                TrySelectTargetSlot(
                    owner,
                    part);

            if (!success)
                return;

            Skill selectedSkill = selection.Skill;

            if (selectedSkill == null)
            {
                Debug.LogWarning(
                    "타겟 선택 완료 후 실행할 스킬이 없습니다.");
                CancelCurrentSelection();
                return;
            }

            if (!CreateSlot(selectedSkill))
                return;

            if (skillSelectPanel != null)
                skillSelectPanel.Hide();

            ClearSelection();
            RefreshAllBodyPartButtons();
            return;
        }

        BattleDebugLog.UIInput(
            "스킬 패널에서 사용할 스킬을 선택하세요.");
    }
    
    public void RefreshAllUI()
    {
        RefreshAllBodyPartButtons();
        skillSelectPanel?.RefreshVisibleButtons();
    }

    public void RefreshCharacterUI(Character character)
    {
        if (character == null)
            return;

        bodyPartButtonRegistry?.CopyCharacterButtonsTo(
            character,
            buttonScratch,
            requireActive: false);

        RefreshButtons(buttonScratch);
        skillSelectPanel?.RefreshVisibleButtons();
    }

    public void RefreshBodyPartUI(BodyPart part)
    {
        if (part == null)
            return;

        RefreshTargetUI(part.Owner, part);
    }

    public void RefreshTargetUI(
        Character character,
        BodyPart part)
    {
        if (character == null)
            return;

        RefreshButton(
            FindBodyPartButton(character, part));

        skillSelectPanel?.RefreshVisibleButtons();
    }

    public void RefreshButton(BodyPartButton button)
    {
        if (button == null)
            return;

        button.ApplyViewModel(
            CreateBodyPartButtonViewModel(button));
    }

    private void RefreshButtons(
        IReadOnlyList<BodyPartButton> buttons)
    {
        if (buttons == null)
            return;

        foreach (BodyPartButton button in buttons)
            RefreshButton(button);
    }

    //---------------------------------------
    // BodyPartButton 오른쪽 클릭
    //---------------------------------------

    public void OnBodyPartRightClicked(
        Character owner,
        BodyPart part)
    {
        EnsureReferences();

        if (!IsManagerReady())
            return;

        // 우클릭은 상세 보기로 사용하지 않는다.
        // 아군 부위에서는 해당 부위의 가장 최근/현재 행동 슬롯을 취소하고,
        // 그 외에는 진행 중인 선택 상태만 취소한다.
        if (IsPlayer(owner) &&
            part != null)
        {
            int actionIndex =
                ResolveActionIndexForRemoval(
                    owner,
                    part);

            ActionSlot oldSlot =
                battleManager.ActionManager.FindSlot(
                    owner,
                    part,
                    actionIndex);

            if (oldSlot != null)
            {
                PlanCommands?.Remove(
                    owner,
                    part,
                    actionIndex);

                actionIndexCursorByPart[part] =
                    Mathf.Max(
                        0,
                        actionIndex - 1);

                BattleDebugLog.UIInput(
                    "[ActionSlot Canceled] " +
                    $"{GetCharacterName(owner)} / " +
                    $"{part.Type} / " +
                    $"ActionIndex={actionIndex}, " +
                    $"ActionId={oldSlot.ActionId}");

                RefreshAllBodyPartButtons();
            }
        }

        CancelCurrentSelection();
    }


    public bool ShowCharacterDetails(Character owner)
    {
        EnsureReferences();

        if (owner == null || characterDetailPanel == null)
            return false;

        characterDetailPanel.Show(owner);
        return true;
    }

    //---------------------------------------
    // 행동 부위 선택
    //---------------------------------------

    public void SelectOwnerSlot(
        Character owner,
        BodyPart part)
    {
        TrySelectOwnerSlot(
            owner,
            part,
            requestedActionIndex: null);
    }

    public bool SelectOwnerSlot(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        return TrySelectOwnerSlot(
            owner,
            part,
            actionIndex);
    }

    private bool TrySelectOwnerSlot(
        Character owner,
        BodyPart part)
    {
        return TrySelectOwnerSlot(
            owner,
            part,
            requestedActionIndex: null);
    }

    private bool TrySelectOwnerSlot(
        Character owner,
        BodyPart part,
        int? requestedActionIndex)
    {
        if (!IsManagerReady())
            return false;

        if (owner == null || part == null)
            return false;

        if (!IsPlayer(owner))
        {
            Debug.Log(
                "플레이어의 부위만 행동 슬롯으로 선택할 수 있습니다.");
            return false;
        }

        if (owner.IsDead)
        {
            Debug.Log(
                $"{GetCharacterName(owner)}는 사망 상태입니다.");
            return false;
        }

        if (part.IsBroken)
        {
            Debug.Log(
                $"[{part.Type}] 파괴된 부위는 행동할 수 없습니다.");
            return false;
        }

        if (!part.IsUsable)
        {
            Debug.Log(
                $"[{part.Type}] 사용할 수 없는 부위입니다.");
            return false;
        }

        selectedMaxActionSlots =
            GetMaxActionSlots(
                owner,
                part);

        if (selectedMaxActionSlots <= 0)
        {
            Debug.LogWarning(
                $"[{part.Type}] 선택 가능한 행동 슬롯이 없습니다.");
            return false;
        }

        selectedActionIndex =
            ResolveActionIndexForSelection(
                owner,
                part,
                selectedMaxActionSlots,
                requestedActionIndex);

        selectedOwner = owner;
        selectedOwnerPart = part;
        actionIndexCursorByPart[part] =
            selectedActionIndex;

        SyncSelectionViewModel();

        RefreshAllBodyPartButtons();

        ActionSlot oldSlot =
            battleManager.ActionManager.FindSlot(
                owner,
                part,
                selectedActionIndex);

        if (oldSlot != null)
        {
            Debug.Log(
                "[기존 슬롯 선택됨 - 타겟/스킬 변경 가능]\n" +
                FormatSlot(oldSlot));
        }
        else
        {
            BattleDebugLog.UIInput(
                $"[Owner Slot Selected] " +
                $"{owner.Data.CharacterName} / {part.Type} / " +
                $"ActionIndex={selectedActionIndex} / " +
                $"MaxSlots={selectedMaxActionSlots}");
        }

        return true;
    }

    //---------------------------------------
    // 공격 대상 선택
    //---------------------------------------

    public void SelectTargetSlot(Character target, BodyPart part)
    {
        TrySelectTargetSlot(target, part);
    }

    private bool TrySelectTargetSlot(
        Character target,
        BodyPart part)
    {
        if (!IsManagerReady() ||
            target == null)
        {
            return false;
        }

        if (selectedOwner == null ||
            selectedOwnerPart == null)
        {
            Debug.Log(
                "먼저 공격 부위를 선택하세요.");
            return false;
        }

        if (target == selectedOwner)
        {
            Debug.Log(
                "자기 자신은 공격할 수 없습니다.");
            return false;
        }

        if (!BattleTargetValidator.IsValid(
                target,
                part,
                selection.TargetRule))
        {
            Debug.LogWarning(
                $"선택할 수 없는 대상입니다. " +
                $"Target={GetCharacterName(target)}, " +
                $"Part={(part == null ? "SINGLE_HP" : part.Type.ToString())}");
            return false;
        }

        selectedTarget =
            target;

        selectedTargetPart =
            part;

        SyncSelectionViewModel();
        RefreshAllBodyPartButtons();

        BattleDebugLog.UIInput(
            $"[Target Selected] " +
            $"{selectedOwner.Data.CharacterName} " +
            $"{selectedOwnerPart.Type} -> " +
            $"{GetCharacterName(target)} " +
            $"{(part == null ? "SINGLE_HP" : part.Type.ToString())}");

        return true;
    }

    //---------------------------------------
    // 스킬 패널
    //---------------------------------------

    private void ShowSkillPanel()
    {
        if (skillSelectPanel == null)
        {
            Debug.LogWarning("SkillSelectPanelUI가 연결되어 있지 않습니다.");
            return;
        }

        if (selectedOwnerPart == null)
            return;

        skillSelectPanel.Show(
            this,
            selectedOwnerPart,
            selectedActionIndex,
            selectedMaxActionSlots);
    }

    public void OnSkillSelectedFromPanel(
        Skill skill)
    {
        OnSkillSelectedFromPanel(
            skill,
            selectedActionIndex);
    }

    public void OnSkillSelectedFromPanel(
        Skill skill,
        int actionIndex)
    {
        if (skill == null)
            return;

        if (actionIndex < 0 ||
            actionIndex >= selectedMaxActionSlots)
        {
            Debug.LogWarning(
                $"잘못된 ActionIndex입니다. " +
                $"Index={actionIndex}, Max={selectedMaxActionSlots}");
            return;
        }

        selectedActionIndex = actionIndex;
        selection.SetActionIndex(
            selectedActionIndex,
            selectedMaxActionSlots);

        if (!IsSkillSelectable(
                selectedOwnerPart,
                skill))
        {
            string reason =
                GetSkillSelectionReason(
                    selectedOwnerPart,
                    skill);

            PlaySkillSelectionRejectedFeedback(
                skill,
                reason);

            Debug.Log(
                $"[{skill.SkillName}] 사용할 수 없는 스킬입니다. " +
                reason);
            return;
        }

        // 이 시점부터 TopStatusBar/BattleEnergyUI가 선택한 스킬 비용을
        // 현재 슬롯 교체 기준으로 즉시 미리 보여준다.
        selection.SelectSkill(skill);

        if (skill.ActionType == ActionType.Preparation)
        {
            // 도사림도 START 전까지는 실제 ActionSlot 계획으로 유지한다.
            // 이 시점에는 효과/빛을 소비하지 않기 때문에 우클릭/Reset이
            // UI뿐 아니라 백엔드에서도 완전한 행동 취소가 된다.
            if (!CreateSlot(skill))
                return;

            if (skillSelectPanel != null)
                skillSelectPanel.Hide();

            BattleDebugLog.UIInput(
                $"[Preparation Planned] {skill.SkillName} / " +
                $"Part={selectedOwnerPart?.Type}, Index={selectedActionIndex}");

            ClearSelection();
            RefreshAllBodyPartButtons();
            return;
        }

        selectedTarget = null;
        selectedTargetPart = null;

        inputMode = BattleInputMode.SelectTarget;
        SyncSelectionViewModel();
        selection.SelectSkill(skill);

        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        RefreshAllBodyPartButtons();

        BattleDebugLog.UIInput(
            $"[Skill Selected] {skill.SkillName} / 공격 대상을 선택하세요.");
    }

    /// <summary>
    /// 머리 위 슬롯 UI에서 플레이어 행동 슬롯을 바로 선택하고 스킬 패널을 연다.
    /// </summary>
    public bool SelectOwnerSlotAndOpenSkills(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (!TrySelectOwnerSlot(owner, part, actionIndex))
            return false;

        inputMode = BattleInputMode.SelectSkill;
        ShowSkillPanel();
        return true;
    }

    /// <summary>
    /// 스킬 패널에서 카드 클릭을 끝낸 뒤 적 머리 위 ActionSlot을 클릭했을 때 사용한다.
    /// 정확한 TargetSlot을 "플레이어의 타깃 의도"로 저장한다.
    /// 속도에 따른 가로채기/합 성립 여부는 이 입력 단계가 아니라 ClashBuilder가 별도로 판정한다.
    /// </summary>
    public bool TryAssignSelectedSkillToActionSlot(ActionSlot targetSlot)
    {
        Skill skill = selection.Skill;

        if (skill == null ||
            inputMode != BattleInputMode.SelectTarget)
        {
            return false;
        }

        return TryAssignSkillToActionSlot(
            skill,
            selectedActionIndex,
            targetSlot);
    }

    /// <summary>
    /// 현재 선택한 플레이어 부위/행동 인덱스에 스킬을 배치하고
    /// 적의 정확한 ActionSlot을 TargetSlot 의도로 저장한다.
    /// TargetSlot 지정 성공은 합 성립을 의미하지 않는다.
    /// </summary>
    public bool TryAssignDraggedSkillToActionSlot(
        Skill skill,
        int actionIndex,
        ActionSlot targetSlot)
    {
        return TryAssignSkillToActionSlot(
            skill,
            actionIndex,
            targetSlot);
    }

    private bool TryAssignSkillToActionSlot(
        Skill skill,
        int actionIndex,
        ActionSlot targetSlot)
    {
        if (skill == null || targetSlot == null ||
            selectedOwner == null || selectedOwnerPart == null ||
            targetSlot.Owner == null || IsPlayer(targetSlot.Owner) ||
            targetSlot.Phase != ActionPhase.COMBAT)
        {
            return false;
        }

        // 도사림은 적 TargetSlot으로 드롭하는 스킬이 아니다.
        // 카드 클릭 시 자기 부위의 FORESIGHT 계획 슬롯으로 직접 등록된다.
        if (skill.ActionType == ActionType.Preparation)
            return false;

        if (actionIndex < 0 || actionIndex >= selectedMaxActionSlots)
            return false;

        selectedActionIndex = actionIndex;
        selection.SetActionIndex(selectedActionIndex, selectedMaxActionSlots);

        if (!IsSkillSelectable(selectedOwnerPart, skill))
            return false;

        selection.SelectSkill(skill);

        if (!TrySelectTargetSlot(targetSlot.Owner, targetSlot.Part))
            return false;

        if (!CreateSlot(skill, targetSlot))
            return false;

        ActionSlot created = battleManager.ActionManager.FindSlot(
            selectedOwner,
            selectedOwnerPart,
            selectedActionIndex);

        if (created == null)
            return false;

        BattleDebugLog.UIInput(
            $"[World Slot Target Assigned] {created.Owner?.Data?.CharacterName} " +
            $"{created.Skill?.SkillName} -> {targetSlot.Owner?.Data?.CharacterName} " +
            $"TargetSlot={targetSlot.ActionId} / Clash=ResolveLater");

        ClearSelection();
        RefreshAllBodyPartButtons();
        return true;
    }

    /// <summary>
    /// 이미 배치된 아군 머리 위 슬롯을 적 머리 위 슬롯으로 드래그했을 때
    /// 스킬은 유지하고 플레이어의 타깃 의도만 정확한 ActionSlot로 재지정한다.
    /// 실제 합/일방공격 판정은 재지정 이후 ClashBuilder가 계산한다.
    /// </summary>
    public bool TryRetargetPlannedActionSlot(
        ActionSlot sourceSlot,
        ActionSlot targetSlot)
    {
        if (!IsManagerReady() ||
            sourceSlot == null ||
            targetSlot == null ||
            sourceSlot.Owner == null ||
            targetSlot.Owner == null ||
            sourceSlot.Skill == null ||
            sourceSlot.Phase != ActionPhase.COMBAT ||
            targetSlot.Phase != ActionPhase.COMBAT ||
            !IsPlayer(sourceSlot.Owner) ||
            IsPlayer(targetSlot.Owner))
        {
            return false;
        }

        BattleActionPlanCommandService commands =
            PlanCommands;

        if (commands == null ||
            !commands.TryRetarget(
                sourceSlot,
                targetSlot,
                out ActionSlot liveSource))
        {
            return false;
        }

        BattleDebugLog.UIInput(
            $"[World Slot Retarget] {liveSource.Owner?.Data?.CharacterName} " +
            $"{liveSource.Skill?.SkillName} -> {targetSlot.Owner?.Data?.CharacterName} " +
            $"Slot={targetSlot.ActionId}");

        RefreshAllBodyPartButtons();
        return true;
    }

    private ActionPlanningSkillContext CreatePlanningSkillContext(
        Skill skill)
    {
        return new ActionPlanningSkillContext(
            selectedOwner,
            selectedOwnerPart,
            skill,
            selectedActionIndex,
            battleManager?.ActionManager?.Slots);
    }

    private string GetMechanicPlanningSelectionReason(
        Skill skill)
    {
        return ActionPlanningMechanicPolicy
            .GetSkillSelectionBlockReason(
                CreatePlanningSkillContext(skill));
    }

    private bool IsMechanicEnergyReservationExempt(
        Skill skill)
    {
        return ActionPlanningMechanicPolicy
            .IsEnergyReservationExempt(
                CreatePlanningSkillContext(skill));
    }

    private void PlaySkillSelectionRejectedFeedback(
        Skill skill,
        string reason)
    {
        if (!IsEnergySelectionFailure(reason))
            return;

        string message =
            string.IsNullOrWhiteSpace(reason)
                ? "빛이 부족합니다."
                : reason;

        BattleTopStatusBarUI[] topBars =
            FindObjectsByType<BattleTopStatusBarUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BattleTopStatusBarUI topBar in topBars)
            topBar?.PlayInsufficientEnergyFeedback(message);

        BattleEnergyUI[] energyPanels =
            FindObjectsByType<BattleEnergyUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BattleEnergyUI energyPanel in energyPanels)
            energyPanel?.PlayInsufficientEnergyFeedback(message);
    }

    private static bool IsEnergySelectionFailure(
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return false;

        return reason.Contains("에너지") ||
               reason.Contains("빛");
    }

    //---------------------------------------
    // 스킬 찾기 / 사용 가능 여부
    //---------------------------------------

    public Skill FindSkillByActionType(
        BodyPart part,
        ActionType actionType)
    {
        if (part == null)
            return null;

        if (selectedOwner.GetSelectableSkills(part, selectedActionIndex) == null)
            return null;

        foreach (Skill skill in selectedOwner.GetSelectableSkills(part, selectedActionIndex))
        {
            if (skill == null)
                continue;

            if (skill.ActionType == actionType)
                return skill;
        }

        return null;
    }

    public bool IsSkillSelectable(
        BodyPart part,
        Skill skill)
    {
        if (!IsManagerReady())
            return false;

        if (selectedOwner == null)
            return false;

        if (part == null || skill == null)
            return false;

        if (part.IsBroken)
            return false;

        if (!part.IsUsable)
            return false;

        if (selectedOwner.GetSelectableSkills(part, selectedActionIndex) == null)
            return false;

        if (!ContainsSkill(selectedOwner.GetSelectableSkills(part, selectedActionIndex), skill))
            return false;

        string mechanicReason =
            GetMechanicPlanningSelectionReason(
                skill);

        if (!string.IsNullOrWhiteSpace(
                mechanicReason))
        {
            return false;
        }

        // 캐릭터 고유 Planning 메커닉이 기존 예약 편집처럼
        // 추가 에너지 예약이 필요 없는 편집을 선언할 수 있다.
        bool energyReservationExempt =
            IsMechanicEnergyReservationExempt(
                skill);

        if (!energyReservationExempt &&
            !selectedOwner.CanUseSkill(part, skill))
        {
            return false;
        }

        if (!energyReservationExempt &&
            !battleManager.ActionManager.CanReserveEnergy(
                selectedOwner,
                skill,
                part,
                selectedActionIndex))
        {
            return false;
        }

        if (skill.ActionType == ActionType.Prestige)
        {
            if (!IsPrestigeReady(selectedOwner))
                return false;

            if (skill.PrestigeUsePolicy ==
                PrestigeUsePolicy.OncePerTurn)
            {
                if (HasPrestigeSlotSelected(
                        selectedOwner,
                        selectedOwnerPart,
                        selectedActionIndex))
                    return false;
            }
        }

        return true;
    }

    private bool ContainsSkill(
        IReadOnlyList<Skill> skills,
        Skill targetSkill)
    {
        if (skills == null || targetSkill == null)
            return false;

        for (int i = 0; i < skills.Count; i++)
        {
            if (skills[i] == targetSkill)
                return true;
        }

        return false;
    }

    private bool IsPrestigeReady(Character character)
    {
        if (character == null)
            return false;

        if (character.CurrentStatus.maxPrestige <= 0)
            return false;

        return
            character.RuntimeStatus.currentPrestige >=
            character.CurrentStatus.maxPrestige;
    }

    private static void RestoreCharacterPlanningState(
        Character owner,
        ActionSlot slot)
    {
        ActionPlanningMechanicPolicy.RestorePlanningState(
            owner,
            slot);
    }

    //---------------------------------------
    // ActionSlot 생성
    //---------------------------------------

    private bool CreateSlot(
        Skill skill,
        ActionSlot targetSlot = null)
    {
        if (!IsManagerReady())
            return false;

        if (selectedOwner == null ||
            selectedOwnerPart == null ||
            skill == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 행동 주체/부위/스킬 정보가 부족합니다.");
            return false;
        }

        selection.ResolveTargetForSkill(
            skill,
            out Character resolvedTarget,
            out BodyPart resolvedTargetPart);

        BattleActionPlanCommandService commands =
            PlanCommands;

        if (commands == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : Planning command service가 준비되지 않았습니다.");
            return false;
        }

        ActionPlanAssignmentResult result =
            commands.TryAssign(
                new ActionPlanAssignmentRequest
                {
                    Owner = selectedOwner,
                    OwnerPart = selectedOwnerPart,
                    Skill = skill,
                    ActionIndex = selectedActionIndex,
                    Target = resolvedTarget,
                    TargetPart = resolvedTargetPart,
                    TargetRule = selection.TargetRule,
                    TargetSlot = targetSlot
                });

        if (result?.Success != true ||
            result.Slot == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : " +
                (result?.FailureReason ?? "알 수 없는 Planning 오류"));
            return false;
        }

        if (skill.ActionType == ActionType.Preparation)
        {
            Debug.Log(
                $"[Preparation Target Normalized] " +
                $"Owner={GetCharacterName(selectedOwner)}, " +
                $"Part={selectedOwnerPart.Type}, " +
                $"Target={GetCharacterName(resolvedTarget)}, " +
                $"TargetPart={resolvedTargetPart?.Type}");
        }

        if (result.PreviousSlot != null)
        {
            Debug.Log(
                "[ActionSlot Changed]\n" +
                "Before :\n" +
                FormatSlot(result.PreviousSlot) +
                "\nAfter :\n" +
                FormatSlot(result.Slot));
        }
        else
        {
            BattleDebugLog.ActionSlot(
                "[ActionSlot Created]\n" +
                FormatSlot(result.Slot));
        }

        return true;
    }

    //---------------------------------------
    // Reset 버튼
    //---------------------------------------

    public void OnResetButtonClicked()
    {
        if (!IsManagerReady())
            return;

        PlanCommands?.ResetOwner(
            battleManager.BattleContext?.Player);

        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        ClearSelection();

        ClearLines();

        RefreshAllBodyPartButtons();

        Debug.Log("[UI] Player selection reset.");
    }

    //---------------------------------------
    // 우클릭 취소
    //---------------------------------------

    public void CancelCurrentSelection()
    {
        if (inputMode == BattleInputMode.SelectOwner)
            return;

        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        ClearSelection();

        RefreshAllBodyPartButtons();

        BattleDebugLog.UIInput("[UI] Current selection canceled.");
    }

    //---------------------------------------
    // 턴 UI 초기화
    //---------------------------------------

    public void ResetTurn()
    {
        ResetTurnInputState();

        RefreshAllBodyPartButtons();
    }

    //---------------------------------------
    // 선택 초기화
    //---------------------------------------

    private void ClearSelection()
    {
        selectedOwner = null;
        selectedOwnerPart = null;
        selectedActionIndex = 0;
        selectedMaxActionSlots = 1;

        selectedTarget = null;
        selectedTargetPart = null;

        inputMode = BattleInputMode.SelectOwner;
        selection.Reset();
    }

    private void SyncSelectionViewModel()
    {
        Skill preservedSkill = selection.Skill;

        if (selectedOwner != null &&
            selectedOwnerPart != null)
        {
            selection.SelectOwner(
                selectedOwner,
                selectedOwnerPart,
                selectedActionIndex,
                selectedMaxActionSlots);
        }
        else
        {
            selection.Reset();
        }

        if (selectedTarget != null)
        {
            selection.SelectTarget(
                selectedTarget,
                selectedTargetPart);
        }

        if (preservedSkill != null)
            selection.SelectSkill(preservedSkill);

        selection.SetMode(inputMode);
    }

    //---------------------------------------
    // 선 초기화
    //---------------------------------------

    private void ClearLines()
    {
        // TargetArrowUI는 ActionManager 슬롯을 보고 LateUpdate에서 자동 갱신함.
    }

    //---------------------------------------
    // 버튼 갱신
    //---------------------------------------

    public void RefreshAllBodyPartButtons()
    {
        if (bodyPartButtonRegistry == null)
            return;

        bodyPartButtonRegistry.CopyAllTo(
            buttonScratch,
            requireActive: false);

        RefreshButtons(buttonScratch);
    }

    //---------------------------------------
    // 상태 체크
    //---------------------------------------

    private bool IsManagerReady()
    {
        if (battleManager == null)
        {
            Debug.LogWarning("BattleUIManager : BattleManager가 없습니다.");
            return false;
        }

        if (battleManager.BattleContext == null)
        {
            Debug.LogWarning("BattleUIManager : BattleContext가 없습니다.");
            return false;
        }

        if (battleManager.ActionManager == null)
        {
            Debug.LogWarning("BattleUIManager : ActionManager가 없습니다.");
            return false;
        }

        if (battleManager.SpeedManager == null)
        {
            Debug.LogWarning("BattleUIManager : SpeedManager가 없습니다.");
            return false;
        }

        return true;
    }

    public bool IsPlayer(Character character)
    {
        if (battleManager == null)
            return false;

        if (battleManager.BattleContext == null)
            return false;

        return character == battleManager.BattleContext.Player;
    }

    public string GetSkillSelectionReason(
        BodyPart part,
        Skill skill)
    {
        if (selectedOwner == null)
            return "행동 부위 미선택";

        if (part == null || skill == null)
            return "스킬 없음";

        if (part.IsBroken || !part.IsUsable)
            return "부위 사용 불가";

        CharacterCombatRulesRuntime rules =
            selectedOwner.CombatRulesRuntime;

        CharacterSkillLoadoutRuntime loadout =
            rules?.Loadout;

        if (rules?.CurrentBossPhase == null &&
            loadout?.HasSource == true &&
            skill.Definition != null &&
            !loadout.IsEquipped(skill.Definition))
        {
            return "미장착 스킬";
        }

        IReadOnlyList<Skill> selectable =
            selectedOwner.GetSelectableSkills(
                part,
                selectedActionIndex);

        if (selectable == null ||
            !ContainsSkill(selectable, skill))
        {
            return "현재 부위·행동 슬롯에서 사용 불가";
        }

        string mechanicReason =
            GetMechanicPlanningSelectionReason(
                skill);

        if (!string.IsNullOrWhiteSpace(
                mechanicReason))
        {
            return mechanicReason;
        }

        bool energyReservationExempt =
            IsMechanicEnergyReservationExempt(
                skill);

        if (skill.ActionType == ActionType.Prestige &&
            !IsPrestigeReady(selectedOwner))
        {
            return "위세 부족";
        }

        if (!energyReservationExempt &&
            !selectedOwner.CanUseSkill(part, skill))
        {
            if (!selectedOwner.CanAffordEnergy(skill.EnergyCost))
            {
                return
                    $"에너지 부족 " +
                    $"({selectedOwner.CurrentEnergy}/{skill.EnergyCost})";
            }

            return "조건 또는 자원 부족";
        }

        if (!energyReservationExempt &&
            !battleManager.ActionManager.CanReserveEnergy(
                selectedOwner,
                skill,
                part,
                selectedActionIndex))
        {
            int remaining =
                battleManager.ActionManager
                    .GetRemainingEnergyAfterPlan(
                        selectedOwner,
                        part,
                        selectedActionIndex);

            return
                $"계획 에너지 부족 " +
                $"({remaining}/{skill.EnergyCost})";
        }

        if (skill.ActionType == ActionType.Prestige &&
            skill.PrestigeUsePolicy == PrestigeUsePolicy.OncePerTurn &&
            HasPrestigeSlotSelected(
                selectedOwner,
                selectedOwnerPart,
                selectedActionIndex))
        {
            return "이번 턴 위세 사용됨";
        }

        return "";
    }

    public ActionSlot FindActionSlot(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (battleManager?.ActionManager == null ||
            owner == null ||
            part == null ||
            actionIndex < 0)
        {
            return null;
        }

        return battleManager.ActionManager.FindSlot(
            owner,
            part,
            actionIndex);
    }

    /// <summary>
    /// 동적으로 생성된 부위별 ActionSlot 버튼에서 호출한다.
    /// 빈 슬롯과 이미 채워진 슬롯 모두 같은 경로로 편집할 수 있다.
    /// </summary>
    public bool OpenOwnerActionSlot(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (!TrySelectOwnerSlot(
                owner,
                part,
                actionIndex))
        {
            return false;
        }

        ActionSlot existing =
            FindActionSlot(
                owner,
                part,
                actionIndex);

        selectedTarget =
            existing?.TargetCharacter;

        selectedTargetPart =
            existing?.TargetPart;

        selection.ClearTarget();
        selection.SelectSkill(null);

        if (selectedTarget != null)
        {
            selection.SelectTarget(
                selectedTarget,
                selectedTargetPart);
        }

        if (existing?.Skill != null)
        {
            RestoreCharacterPlanningState(
                owner,
                existing);

            selection.SelectSkill(existing.Skill);
        }

        inputMode = BattleInputMode.SelectSkill;
        SyncSelectionViewModel();
        ShowSkillPanel();
        RefreshAllBodyPartButtons();

        return true;
    }

    /// <summary>
    /// 슬롯 바의 우클릭 삭제용. 삭제 뒤 같은 ActionIndex를 빈 슬롯으로
    /// 유지해 사용자가 즉시 새 행동을 다시 선택할 수 있게 한다.
    /// </summary>
    public bool RemoveActionSlotAndContinueEditing(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (!IsManagerReady() ||
            owner == null ||
            part == null)
        {
            return false;
        }

        ActionSlot slot =
            FindActionSlot(
                owner,
                part,
                actionIndex);

        if (slot == null)
            return false;

        PlanCommands?.Remove(
            owner,
            part,
            actionIndex);

        actionIndexCursorByPart[part] =
            Mathf.Max(0, actionIndex);

        bool reopened =
            OpenOwnerActionSlot(
                owner,
                part,
                actionIndex);

        if (!reopened)
            RefreshCharacterUI(owner);

        return true;
    }

    public bool SelectActionSlot(ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner == null ||
            slot.Part == null ||
            !IsPlayer(slot.Owner))
        {
            return false;
        }

        if (!TrySelectOwnerSlot(
                slot.Owner,
                slot.Part,
                slot.ActionIndex))
        {
            return false;
        }

        selectedTarget = slot.TargetCharacter;
        selectedTargetPart = slot.TargetPart;
        selectedActionIndex = slot.ActionIndex;
        selectedMaxActionSlots =
            GetMaxActionSlots(slot.Owner, slot.Part);

        inputMode = BattleInputMode.SelectSkill;
        SyncSelectionViewModel();

        RestoreCharacterPlanningState(
            slot.Owner,
            slot);

        selection.SelectSkill(slot.Skill);

        ShowSkillPanel();
        RefreshAllBodyPartButtons();

        return true;
    }

    public bool RemoveActionSlot(
        Character owner,
        BodyPart part,
        int actionIndex)
    {
        if (!IsManagerReady() ||
            owner == null ||
            part == null)
        {
            return false;
        }

        ActionSlot slot =
            battleManager.ActionManager.FindSlot(
                owner,
                part,
                actionIndex);

        if (slot == null)
            return false;

        PlanCommands?.Remove(
            owner,
            part,
            actionIndex);

        actionIndexCursorByPart[part] =
            Mathf.Max(0, actionIndex - 1);

        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        ClearSelection();
        RefreshCharacterUI(owner);

        return true;
    }

    //---------------------------------------
    // Debug
    //---------------------------------------

    private string FormatSlot(ActionSlot slot)
    {
        if (slot == null)
            return "NULL SLOT";

        string ownerName =
            GetCharacterName(slot.Owner);

        string partName =
            slot.Part == null
                ? "NULL"
                : slot.Part.Type.ToString();

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            GetCharacterName(slot.TargetCharacter);

        string targetPartName =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        string targetSlotName =
            slot.TargetSlot == null
                ? "NULL"
                : $"{GetCharacterName(slot.TargetSlot.Owner)} " +
                  $"{(slot.TargetSlot.Part == null ? "NONE" : slot.TargetSlot.Part.Type.ToString())} " +
                  $"[#{slot.TargetSlot.ActionIndex + 1}, Id={slot.TargetSlot.ActionId}]";

        return
            $"ActionId   : {slot.ActionId}\n" +
            $"ActionIndex: {slot.ActionIndex}\n" +
            $"Owner      : {ownerName}\n" +
            $"Part       : {partName}\n" +
            $"Skill      : {skillName}\n" +
            $"Target     : {targetName}\n" +
            $"TargetPart : {targetPartName}\n" +
            $"Speed      : {slot.Speed}\n" +
            $"Phase      : {slot.Phase}\n" +
            $"TargetSlot : {targetSlotName}";
    }

    private string GetCharacterName(Character character)
    {
        if (character == null)
            return "NULL";

        if (character.Data == null)
            return character.name;

        return character.Data.CharacterName;
    }
    
    private bool HasPrestigeSlotSelected(
        Character owner,
        BodyPart ignorePart = null,
        int ignoreActionIndex = -1)
    {
        return PlanCommands?.HasPrestigeSlotSelected(
                   owner,
                   ignorePart,
                   ignoreActionIndex) == true;
    }

    private int ResolveActionIndexForSelection(
        Character owner,
        BodyPart part,
        int maxSlots,
        int? requestedActionIndex)
    {
        if (requestedActionIndex.HasValue)
        {
            return Mathf.Clamp(
                requestedActionIndex.Value,
                0,
                Mathf.Max(0, maxSlots - 1));
        }

        // 빈 슬롯을 먼저 선택한다.
        for (int index = 0;
             index < maxSlots;
             index++)
        {
            if (battleManager.ActionManager.FindSlot(
                    owner,
                    part,
                    index) == null)
            {
                return index;
            }
        }

        // 모두 찼으면 클릭할 때마다 편집 대상을 순환한다.
        int cursor = -1;

        if (part != null &&
            actionIndexCursorByPart.TryGetValue(
                part,
                out int storedCursor))
        {
            cursor = storedCursor;
        }

        return
            (cursor + 1) %
            Mathf.Max(1, maxSlots);
    }

    private int ResolveActionIndexForRemoval(
        Character owner,
        BodyPart part)
    {
        List<ActionSlot> slots =
            GetSlotsByOwnerPart(owner, part);

        if (slots.Count == 0)
            return 0;

        if (selectedOwner == owner &&
            IsSamePart(selectedOwnerPart, part) &&
            battleManager.ActionManager.FindSlot(
                owner,
                part,
                selectedActionIndex) != null)
        {
            return selectedActionIndex;
        }

        if (part != null &&
            actionIndexCursorByPart.TryGetValue(
                part,
                out int cursor) &&
            battleManager.ActionManager.FindSlot(
                owner,
                part,
                cursor) != null)
        {
            return cursor;
        }

        return slots[slots.Count - 1].ActionIndex;
    }

    private bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null ||
            b == null)
        {
            return
                a == null &&
                b == null;
        }

        return
            a == b ||
            a.Type == b.Type;
    }

    private bool IsSameTargetPart(
        BodyPart a,
        BodyPart b)
    {
        return IsSamePart(a, b);
    }

}