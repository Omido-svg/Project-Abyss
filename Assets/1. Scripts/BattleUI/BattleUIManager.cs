using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private SkillSelectPanelUI skillSelectPanel;
    
    private BattleInputMode inputMode = BattleInputMode.SelectOwner;

    //---------------------------------------
    // 현재 선택 정보
    //---------------------------------------

    private Character selectedOwner;
    private BodyPart selectedOwnerPart;

    private int selectedActionIndex;
    private int selectedMaxActionSlots = 1;

    private readonly Dictionary<BodyPart, int>
        actionIndexCursorByPart = new();

    private Character selectedTarget;
    private BodyPart selectedTargetPart;

    public int SelectedActionIndex => selectedActionIndex;
    public int SelectedMaxActionSlots => selectedMaxActionSlots;

    //---------------------------------------

    private void Awake()
    {
        if (battleManager == null)
            battleManager = FindFirstObjectByType<BattleManager>();

        if (skillSelectPanel == null)
            skillSelectPanel = FindFirstObjectByType<SkillSelectPanelUI>();
    }

    private void Start()
    {
        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

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
                Interactable = false
            };
        }

        bool characterTarget =
            part == null &&
            owner.IsSingleHpTarget;

        return new BodyPartButtonViewModel
        {
            PartText = characterTarget
                ? $"<color=#86EFAC><b>{GetCharacterName(owner)} [SINGLE HP]</b></color>"
                : GetPartText(part),

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
                part.IsWeakened
        };
    }
    
    private string GetSelectedSkillText(
        Character owner,
        BodyPart part)
    {
        List<ActionSlot> slots =
            GetSlotsByOwnerPart(owner, part);

        if (slots.Count == 0)
            return "";

        StringBuilder builder = new();

        foreach (ActionSlot slot in slots)
        {
            if (slot?.Skill == null)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(
                $"<color=#FFFFFF><b>[#{slot.ActionIndex + 1}] " +
                $"{slot.Skill.SkillName}</b></color>");
        }

        return builder.ToString();
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
        if (owner == null ||
            owner.IsDead)
        {
            return false;
        }

        if (part == null)
        {
            return
                !IsPlayer(owner) &&
                owner.IsSingleHpTarget;
        }

        if (IsPlayer(owner))
        {
            return
                !part.IsBroken &&
                part.IsUsable;
        }

        // 파괴된 대상 부위는 재공격 가능하다.
        return true;
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

    private string GetPartText(BodyPart part)
    {
        if (part == null)
            return "NULL";

        string stateColor =
            part.State switch
            {
                BodyPartState.Normal => "#86EFAC",
                BodyPartState.Weakened => "#FACC15",
                BodyPartState.Broken => "#F87171",
                _ => "#FFFFFF"
            };

        return
            $"<color={stateColor}><b>{part.Type} [{part.State}]</b></color>";
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

        BodyPartButton[] buttons =
            Object.FindObjectsByType<BodyPartButton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BodyPartButton button in buttons)
        {
            if (button == null ||
                button.Owner != character)
            {
                continue;
            }

            if (button.BodyPart == part)
                return button;

            if (part != null &&
                button.BodyPart != null &&
                button.BodyPart.Type ==
                part.Type)
            {
                return button;
            }
        }

        return null;
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
                    BattleInputMode.SelectTarget;
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

            if (success)
            {
                inputMode =
                    BattleInputMode.SelectSkill;
            }

            return;
        }

        BattleDebugLog.UIInput(
            "먼저 왼쪽 스킬 패널에서 사용할 스킬을 선택하세요.");
    }
    
    public void RefreshAllUI()
    {
        RefreshAllBodyPartButtons();
    }

    public void RefreshCharacterUI(Character character)
    {
        if (character == null)
            return;

        RefreshAllBodyPartButtons();
    }

    public void RefreshBodyPartUI(
        BodyPart part)
    {
        RefreshAllBodyPartButtons();
    }

    //---------------------------------------
    // BodyPartButton 오른쪽 클릭
    //---------------------------------------

    public void OnBodyPartRightClicked(
        Character owner,
        BodyPart part)
    {
        if (!IsManagerReady())
            return;

        if (owner == null || part == null)
        {
            CancelCurrentSelection();
            return;
        }

        if (IsPlayer(owner))
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
                battleManager.ActionManager.RemoveSlot(
                    owner,
                    part,
                    actionIndex);

                actionIndexCursorByPart[part] =
                    Mathf.Max(0, actionIndex - 1);

                Debug.Log(
                    "[ActionSlot Canceled]\n" +
                    $"{GetCharacterName(owner)} {part.Type} / " +
                    $"ActionIndex={actionIndex}, " +
                    $"ActionId={oldSlot.ActionId}");

                RefreshAllBodyPartButtons();
            }
        }

        CancelCurrentSelection();
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
                TargetSelectionRule.StandardAttack))
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

        RefreshAllBodyPartButtons();

        BattleDebugLog.UIInput(
            $"[Target Selected] " +
            $"{selectedOwner.Data.CharacterName} " +
            $"{selectedOwnerPart.Type} -> " +
            $"{GetCharacterName(target)} " +
            $"{(part == null ? "SINGLE_HP" : part.Type.ToString())}");

        ShowSkillPanel();

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

        if (!IsSkillSelectable(
                selectedOwnerPart,
                skill))
        {
            Debug.Log(
                $"[{skill.SkillName}] 사용할 수 없는 스킬입니다.");
            return;
        }

        if (!CreateSlot(skill))
            return;

        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        ClearSelection();
        RefreshAllBodyPartButtons();
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

        if (part.AvailableSkills == null)
            return null;

        foreach (Skill skill in part.AvailableSkills)
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

        if (part.AvailableSkills == null)
            return false;

        if (!ContainsSkill(part.AvailableSkills, skill))
            return false;

        if (skill.ActionType == ActionType.Prestige)
        {
            if (!IsPrestigeReady(selectedOwner))
                return false;

            if (skill.PrestigeUsePolicy ==
                PrestigeUsePolicy.OncePerTurn)
            {
                if (HasPrestigeSlotSelected(selectedOwner))
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

    //---------------------------------------
    // ActionSlot 생성
    //---------------------------------------

    private bool CreateSlot(
        Skill skill)
    {
        if (!IsManagerReady())
            return false;

        if (selectedOwner == null ||
            selectedOwnerPart == null ||
            selectedTarget == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 선택 정보가 부족합니다.");
            return false;
        }

        if (!BattleTargetValidator.IsValid(
                selectedTarget,
                selectedTargetPart,
                TargetSelectionRule.StandardAttack))
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 대상 계약이 올바르지 않습니다.");
            return false;
        }

        if (selectedOwner.IsDead ||
            selectedTarget.IsDead)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 사망한 캐릭터가 포함되어 있습니다.");
            return false;
        }

        if (selectedOwnerPart.IsBroken)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 행동 부위가 파괴되어 있습니다.");
            return false;
        }

        if (skill == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : Skill이 없습니다.");
            return false;
        }

        if (skill.ActionType ==
            ActionType.Prestige)
        {
            if (!IsPrestigeReady(
                    selectedOwner))
            {
                Debug.LogWarning(
                    "ActionSlot 생성 실패 : 위세 게이지가 부족합니다.");
                return false;
            }

            if (HasPrestigeSlotSelected(
                    selectedOwner,
                    selectedOwnerPart,
                    selectedActionIndex))
            {
                Debug.LogWarning(
                    "ActionSlot 생성 실패 : 이번 턴에 이미 위세 스킬을 선택했습니다.");
                return false;
            }
        }

        ActionSlot oldSlot =
            battleManager.ActionManager.FindSlot(
                selectedOwner,
                selectedOwnerPart,
                selectedActionIndex);

        ActionSlot newSlot =
            new ActionSlot
            {
                Owner = selectedOwner,
                Part = selectedOwnerPart,
                Skill = skill,
                TargetCharacter = selectedTarget,
                TargetPart = selectedTargetPart,
                Speed = battleManager.SpeedManager.GetSpeed(
                    selectedOwner,
                    selectedOwnerPart),
                ActionIndex = selectedActionIndex,
                Phase = skill.DefaultPhase,
                TargetSlot = null
            };

        battleManager.ActionManager
            .AddOrReplaceSlot(
                newSlot);

        if (oldSlot != null)
        {
            Debug.Log(
                "[ActionSlot Changed]\n" +
                "Before :\n" +
                FormatSlot(oldSlot) +
                "\nAfter :\n" +
                FormatSlot(newSlot));
        }
        else
        {
            BattleDebugLog.ActionSlot(
                "[ActionSlot Created]\n" +
                FormatSlot(newSlot));
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

        battleManager.ResetPlayerActions();

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

    private void CancelCurrentSelection()
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
        BodyPartButton[] buttons =
            Object.FindObjectsByType<BodyPartButton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BodyPartButton button in buttons)
        {
            if (button == null)
                continue;

            BodyPartButtonViewModel viewModel =
                CreateBodyPartButtonViewModel(button);

            button.ApplyViewModel(viewModel);
        }
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
        if (owner == null ||
            battleManager?.ActionManager == null)
        {
            return false;
        }

        foreach (ActionSlot slot
                 in battleManager.ActionManager.Slots)
        {
            if (slot == null ||
                slot.Owner != owner ||
                slot.Skill == null)
            {
                continue;
            }

            bool isIgnoredExactSlot =
                ignoreActionIndex >= 0 &&
                IsSamePart(slot.Part, ignorePart) &&
                slot.ActionIndex == ignoreActionIndex;

            if (isIgnoredExactSlot)
                continue;

            if (slot.Skill.ActionType ==
                ActionType.Prestige)
            {
                return true;
            }
        }

        return false;
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
