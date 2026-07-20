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

    [Header("Dynamic Participant Buttons")]
    [SerializeField]
    private BattleParticipantButtonFactory participantButtonFactory;

    private readonly TargetSelectionViewModel selection = new();
    private readonly List<BodyPartButton> buttonScratch = new();

    public TargetSelectionViewModel Selection => selection;
    
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
        BattleContext context,
        IReadOnlyList<BodyPartButton> legacyPlayerButtons,
        IReadOnlyList<BodyPartButton> legacyEnemyButtons)
    {
        EnsureReferences();

        participantButtonFactory?.ConfigureFromLegacy(
            legacyPlayerButtons,
            legacyEnemyButtons);

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
        selection.SelectSkill(skill);

        if (!IsSkillSelectable(
                selectedOwnerPart,
                skill))
        {
            Debug.Log(
                $"[{skill.SkillName}] 사용할 수 없는 스킬입니다.");
            return;
        }

        if (skill.ActionType == ActionType.Preparation)
        {
            if (!CreateSlot(skill))
                return;

            if (skillSelectPanel != null)
                skillSelectPanel.Hide();

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

        if (!selectedOwner.CanUseSkill(part, skill))
            return false;

        if (!battleManager.ActionManager.CanReserveEnergy(
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

    //---------------------------------------
    // ActionSlot 생성
    //---------------------------------------

    private bool CreateSlot(
        Skill skill)
    {
        if (!IsManagerReady())
            return false;

        if (selectedOwner == null ||
            selectedOwnerPart == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 행동 주체 정보가 부족합니다.");
            return false;
        }

        if (skill == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : Skill이 없습니다.");
            return false;
        }

        // 도사림은 공격 대상 선택 결과를 사용하지 않는다.
        // 항상 행동 주체 자신의 해당 부위를 대상으로 정규화한다.
        bool isPreparation =
            skill.ActionType == ActionType.Preparation;

        selection.ResolveTargetForSkill(
            skill,
            out Character resolvedTarget,
            out BodyPart resolvedTargetPart);

        if (resolvedTarget == null)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 대상 정보가 부족합니다.");
            return false;
        }

        if (isPreparation)
        {
            if (resolvedTargetPart == null ||
                resolvedTargetPart.Owner != resolvedTarget ||
                resolvedTargetPart.IsBroken)
            {
                Debug.LogWarning(
                    "ActionSlot 생성 실패 : 도사림 자기 대상이 올바르지 않습니다.");
                return false;
            }
        }
        else if (!BattleTargetValidator.IsValid(
                     resolvedTarget,
                     resolvedTargetPart,
                     selection.TargetRule))
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 대상 계약이 올바르지 않습니다.");
            return false;
        }

        if (selectedOwner.IsDead ||
            resolvedTarget.IsDead)
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
                TargetCharacter = resolvedTarget,
                TargetPart = resolvedTargetPart,
                Speed = battleManager.SpeedManager.GetSpeed(
                    selectedOwner,
                    selectedOwnerPart),
                ActionIndex = selectedActionIndex,
                Phase = skill.DefaultPhase,
                TargetSlot = null
            };

        bool registered =
            battleManager.ActionManager
                .TryAddOrReplaceSlot(newSlot);

        if (!registered)
        {
            Debug.LogWarning(
                "ActionSlot 생성 실패 : 에너지 예산 또는 슬롯 계약을 만족하지 못했습니다.");
            return false;
        }

        if (isPreparation)
        {
            Debug.Log(
                $"[Preparation Target Normalized] " +
                $"Owner={GetCharacterName(selectedOwner)}, " +
                $"Part={selectedOwnerPart.Type}, " +
                $"Target={GetCharacterName(resolvedTarget)}, " +
                $"TargetPart={resolvedTargetPart.Type}");
        }

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

        if (part.AvailableSkills == null ||
            !ContainsSkill(part.AvailableSkills, skill))
        {
            return "부위에 없는 스킬";
        }

        if (skill.ActionType == ActionType.Prestige &&
            !IsPrestigeReady(selectedOwner))
        {
            return "위세 부족";
        }

        if (!selectedOwner.CanUseSkill(part, skill))
        {
            if (!selectedOwner.CanAffordEnergy(skill.EnergyCost))
            {
                return
                    $"에너지 부족 " +
                    $"({selectedOwner.CurrentEnergy}/{skill.EnergyCost})";
            }

            return "조건 또는 자원 부족";
        }

        if (!battleManager.ActionManager.CanReserveEnergy(
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

        battleManager.ActionManager.RemoveSlot(
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