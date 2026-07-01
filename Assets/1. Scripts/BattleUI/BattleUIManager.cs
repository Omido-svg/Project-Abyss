using System.Linq;
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

    private Character selectedTarget;
    private BodyPart selectedTargetPart;

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
        SubscribeTurnStart();

        if (skillSelectPanel != null)
            skillSelectPanel.Hide();
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

    private void OnDestroy()
    {
        UnsubscribeTurnStart();
    }

    //---------------------------------------

    private void SubscribeTurnStart()
    {
        if (battleManager == null)
            return;

        if (battleManager.BattleContext == null)
            return;

        if (battleManager.BattleContext._battleEvent == null)
            return;

        battleManager.BattleContext._battleEvent.OnTurnStart -= HandleTurnStart;
        battleManager.BattleContext._battleEvent.OnTurnStart += HandleTurnStart;
    }

    private void UnsubscribeTurnStart()
    {
        if (battleManager == null)
            return;

        if (battleManager.BattleContext == null)
            return;

        if (battleManager.BattleContext._battleEvent == null)
            return;

        battleManager.BattleContext._battleEvent.OnTurnStart -= HandleTurnStart;
    }

    private void HandleTurnStart(int turn)
    {
        ResetTurn();
    }

    //---------------------------------------
    // BodyPartButton 왼쪽 클릭
    //---------------------------------------

    public void OnBodyPartClicked(Character owner, BodyPart part)
    {
        if (!IsManagerReady())
            return;

        if (owner == null || part == null)
            return;

        if (inputMode == BattleInputMode.SelectOwner)
        {
            if (!IsPlayer(owner))
            {
                Debug.Log("먼저 플레이어의 행동 부위를 선택하세요.");
                return;
            }

            bool success =
                TrySelectOwnerSlot(owner, part);

            if (success)
                inputMode = BattleInputMode.SelectTarget;

            return;
        }

        if (inputMode == BattleInputMode.SelectTarget)
        {
            if (IsPlayer(owner))
            {
                Debug.Log("대상으로는 적 부위를 선택하세요.");
                return;
            }

            bool success =
                TrySelectTargetSlot(owner, part);

            if (success)
                inputMode = BattleInputMode.SelectSkill;

            return;
        }

        if (inputMode == BattleInputMode.SelectSkill)
        {
            Debug.Log("먼저 왼쪽 스킬 패널에서 사용할 스킬을 선택하세요.");
            return;
        }
    }

    //---------------------------------------
    // BodyPartButton 오른쪽 클릭
    //---------------------------------------

    public void OnBodyPartRightClicked(Character owner, BodyPart part)
    {
        if (!IsManagerReady())
            return;

        if (owner == null || part == null)
        {
            CancelCurrentSelection();
            return;
        }

        // 플레이어 부위를 우클릭하면 해당 부위의 지정된 행동 슬롯 삭제
        if (IsPlayer(owner))
        {
            ActionSlot oldSlot =
                battleManager.ActionManager.FindSlot(owner, part);

            if (oldSlot != null)
            {
                battleManager.ActionManager.RemoveSlot(owner, part);

                Debug.Log(
                    "[ActionSlot Canceled]\n" +
                    $"{GetCharacterName(owner)} {part.Type}");

                RefreshAllBodyPartButtons();
            }
        }

        CancelCurrentSelection();
    }

    //---------------------------------------
    // 행동 부위 선택
    //---------------------------------------

    public void SelectOwnerSlot(Character owner, BodyPart part)
    {
        TrySelectOwnerSlot(owner, part);
    }

    private bool TrySelectOwnerSlot(Character owner, BodyPart part)
    {
        if (!IsManagerReady())
            return false;

        if (owner == null || part == null)
            return false;

        if (!IsPlayer(owner))
        {
            Debug.Log("플레이어의 부위만 행동 슬롯으로 선택할 수 있습니다.");
            return false;
        }

        if (owner.IsDead)
        {
            Debug.Log($"{GetCharacterName(owner)}는 사망 상태입니다.");
            return false;
        }

        if (part.IsBroken)
        {
            Debug.Log($"[{part.Type}] 파괴된 부위는 행동할 수 없습니다.");
            return false;
        }

        if (!part.IsUsable)
        {
            Debug.Log($"[{part.Type}] 사용할 수 없는 부위입니다.");
            return false;
        }

        selectedOwner = owner;
        selectedOwnerPart = part;
        
        RefreshAllBodyPartButtons();

        ActionSlot oldSlot =
            battleManager.ActionManager.FindSlot(owner, part);

        if (oldSlot != null)
        {
            Debug.Log(
                "[기존 슬롯 선택됨 - 타겟/스킬 변경 가능]\n" +
                FormatSlot(oldSlot));
        }
        else
        {
            Debug.Log(
                $"[Owner Slot Selected] " +
                $"{GetCharacterName(owner)} / {part.Type}");
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

    private bool TrySelectTargetSlot(Character target, BodyPart part)
    {
        if (!IsManagerReady())
            return false;

        if (target == null || part == null)
            return false;

        if (selectedOwner == null || selectedOwnerPart == null)
        {
            Debug.Log("먼저 공격 부위를 선택하세요.");
            return false;
        }

        if (target == selectedOwner)
        {
            Debug.Log("자기 자신은 공격할 수 없습니다.");
            return false;
        }

        if (target.IsDead)
        {
            Debug.Log($"{GetCharacterName(target)}는 이미 사망했습니다.");
            return false;
        }

        if (part.IsBroken)
        {
            Debug.Log($"[{part.Type}] 파괴된 부위는 대상으로 지정할 수 없습니다.");
            return false;
        }

        selectedTarget = target;
        selectedTargetPart = part;
        
        RefreshAllBodyPartButtons();

        Debug.Log(
            $"[Target Selected] " +
            $"{GetCharacterName(selectedOwner)} {selectedOwnerPart.Type} " +
            $"-> {GetCharacterName(target)} {part.Type}");

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
            selectedOwnerPart);
    }

    public void OnSkillSelectedFromPanel(Skill skill)
    {
        if (skill == null)
            return;

        if (!IsSkillSelectable(selectedOwnerPart, skill))
        {
            Debug.Log($"[{skill.SkillName}] 사용할 수 없는 스킬입니다.");
            return;
        }

        bool created =
            CreateSlot(skill);

        if (!created)
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

        if (!part.AvailableSkills.Contains(skill))
            return false;

        if (skill.ActionType == ActionType.Prestige)
        {
            // 1. 위세 게이지가 다 차야 함
            if (!IsPrestigeReady(selectedOwner))
                return false;

            // 2. 이번 턴에 이미 다른 부위가 위세를 선택했으면 불가능
            if (HasPrestigeSlotSelected(selectedOwner, part))
                return false;
        }

        return true;
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

    private bool CreateSlot(Skill skill)
    {
        if (!IsManagerReady())
            return false;

        if (selectedOwner == null ||
            selectedOwnerPart == null ||
            selectedTarget == null ||
            selectedTargetPart == null)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 선택 정보가 부족합니다.");
            return false;
        }

        if (selectedOwner.IsDead || selectedTarget.IsDead)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 사망한 캐릭터가 포함되어 있습니다.");
            return false;
        }

        if (selectedOwnerPart.IsBroken)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 행동 부위가 파괴되어 있습니다.");
            return false;
        }

        if (selectedTargetPart.IsBroken)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 대상 부위가 파괴되어 있습니다.");
            return false;
        }

        if (skill == null)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : Skill이 없습니다.");
            return false;
        }
        
        if (skill.ActionType == ActionType.Prestige)
        {
            if (!IsPrestigeReady(selectedOwner))
            {
                Debug.LogWarning("ActionSlot 생성 실패 : 위세 게이지가 부족합니다.");
                return false;
            }

            if (HasPrestigeSlotSelected(selectedOwner, selectedOwnerPart))
            {
                Debug.LogWarning("ActionSlot 생성 실패 : 이번 턴에 이미 위세 스킬을 선택했습니다.");
                return false;
            }
        }

        ActionSlot oldSlot =
            battleManager.ActionManager.FindSlot(
                selectedOwner,
                selectedOwnerPart);

        ActionSlot newSlot = new ActionSlot
        {
            Owner = selectedOwner,
            Part = selectedOwnerPart,

            Skill = skill,

            TargetCharacter = selectedTarget,
            TargetPart = selectedTargetPart,

            Speed = battleManager.SpeedManager.GetSpeed(selectedOwnerPart),

            Phase = CalculatePhase(skill.ActionType),

            TargetSlot = null
        };

        battleManager.ActionManager.AddOrReplaceSlot(newSlot);

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
            Debug.Log(
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

        Debug.Log("[UI] Current selection canceled.");
    }

    //---------------------------------------
    // 턴 UI 초기화
    //---------------------------------------

    public void ResetTurn()
    {
        if (skillSelectPanel != null)
            skillSelectPanel.Hide();

        ClearSelection();

        ClearLines();

        RefreshAllBodyPartButtons();
    }

    //---------------------------------------
    // Phase 계산
    //---------------------------------------

    private ActionPhase CalculatePhase(ActionType type)
    {
        return type switch
        {
            ActionType.Prestige => ActionPhase.PRETURN,
            ActionType.Preparation => ActionPhase.FORESIGHT,

            ActionType.NormalAttack => ActionPhase.COMBAT,
            ActionType.Duel => ActionPhase.COMBAT,

            _ => ActionPhase.COMBAT
        };
    }

    //---------------------------------------
    // 선택 초기화
    //---------------------------------------

    private void ClearSelection()
    {
        selectedOwner = null;
        selectedOwnerPart = null;

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
            FindObjectsByType<BodyPartButton>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (BodyPartButton button in buttons)
        {
            if (button == null)
                continue;

            button.Refresh();

            if (!button.gameObject.activeInHierarchy)
                continue;

            button.SetNormalColor();
        }

        ApplyButtonHighlights(buttons);
    }
    
    private void ApplyButtonHighlights(BodyPartButton[] buttons)
    {
        if (!IsManagerReady())
            return;

        Character player =
            battleManager.BattleContext.Player;

        if (player == null)
            return;

        foreach (ActionSlot slot in battleManager.ActionManager.Slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != player)
                continue;

            BodyPartButton ownerButton =
                FindBodyPartButton(
                    buttons,
                    slot.Owner,
                    slot.Part);

            BodyPartButton targetButton =
                FindBodyPartButton(
                    buttons,
                    slot.TargetCharacter,
                    slot.TargetPart);

            if (ownerButton != null &&
                ownerButton.gameObject.activeInHierarchy)
            {
                ownerButton.SetOwnerSelectedColor();
            }

            if (targetButton != null &&
                targetButton.gameObject.activeInHierarchy)
            {
                targetButton.SetTargetSelectedColor();
            }
        }

        if (selectedOwner != null && selectedOwnerPart != null)
        {
            BodyPartButton selectedOwnerButton =
                FindBodyPartButton(
                    buttons,
                    selectedOwner,
                    selectedOwnerPart);

            if (selectedOwnerButton != null &&
                selectedOwnerButton.gameObject.activeInHierarchy)
            {
                selectedOwnerButton.SetOwnerSelectedColor();
            }
        }

        if (selectedTarget != null && selectedTargetPart != null)
        {
            BodyPartButton selectedTargetButton =
                FindBodyPartButton(
                    buttons,
                    selectedTarget,
                    selectedTargetPart);

            if (selectedTargetButton != null &&
                selectedTargetButton.gameObject.activeInHierarchy)
            {
                selectedTargetButton.SetTargetSelectedColor();
            }
        }
    }
    
    private BodyPartButton FindBodyPartButton(
        BodyPartButton[] buttons,
        Character owner,
        BodyPart part)
    {
        if (buttons == null)
            return null;

        if (owner == null || part == null)
            return null;

        foreach (BodyPartButton button in buttons)
        {
            if (button == null)
                continue;

            if (button.Owner != owner)
                continue;

            if (button.BodyPart == part)
                return button;

            if (button.BodyPart != null &&
                button.BodyPart.Type == part.Type)
            {
                return button;
            }
        }

        return null;
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
    // 이름 호환용
    //---------------------------------------

    public void SelectOwnerPart(Character owner, BodyPart part)
    {
        SelectOwnerSlot(owner, part);
    }

    public void SelectTargetPart(Character target, BodyPart part)
    {
        SelectTargetSlot(target, part);
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
                : $"{GetCharacterName(slot.TargetSlot.Owner)} {slot.TargetSlot.Part.Type}";

        return
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
        BodyPart ignorePart = null)
    {
        if (owner == null)
            return false;

        if (battleManager == null ||
            battleManager.ActionManager == null)
            return false;

        foreach (ActionSlot slot in battleManager.ActionManager.Slots)
        {
            if (slot == null)
                continue;

            if (slot.Owner != owner)
                continue;

            if (slot.Skill == null)
                continue;

            if (ignorePart != null &&
                IsSamePart(slot.Part, ignorePart))
            {
                continue;
            }

            if (slot.Skill.ActionType == ActionType.Prestige)
                return true;
        }

        return false;
    }
    
    private bool IsSamePart(
        BodyPart a,
        BodyPart b)
    {
        if (a == null || b == null)
            return false;

        if (a == b)
            return true;

        return a.Type == b.Type;
    }

}

public enum BattleInputMode
{
    SelectOwner,
    SelectTarget,
    SelectSkill
}