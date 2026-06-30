using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;

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
    }

    private void Start()
    {
        SubscribeTurnStart();
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

    //---------------------------------------

    private void HandleTurnStart(int turn)
    {
        ResetTurn();
    }

    //---------------------------------------
    // BodyPartButton에서 호출되는 함수
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
                inputMode = BattleInputMode.SelectOwner;

            return;
        }
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

        if (owner != battleManager.BattleContext.Player)
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

        if (part.CurrentSkill == null)
        {
            Debug.Log($"[{part.Type}] 선택된 스킬이 없습니다.");
            return false;
        }

        selectedOwner = owner;
        selectedOwnerPart = part;

        ActionSlot oldSlot =
            battleManager.ActionManager.FindSlot(owner, part);

        if (oldSlot != null)
        {
            Debug.Log(
                "[기존 슬롯 선택됨 - 타겟 변경 가능]\n" +
                FormatSlot(oldSlot));
        }
        else
        {
            Debug.Log(
                $"[Owner Slot Selected] " +
                $"{GetCharacterName(owner)} / {part.Type} / " +
                $"Skill : {part.CurrentSkill.SkillName}");
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

        Debug.Log(
            $"[Target Selected] " +
            $"{GetCharacterName(selectedOwner)} {selectedOwnerPart.Type} " +
            $"-> {GetCharacterName(target)} {part.Type}");

        bool created =
            CreateSlot();

        if (!created)
            return false;

        ClearSelection();
        RefreshAllBodyPartButtons();

        return true;
    }

    //---------------------------------------
    // ActionSlot 생성
    //---------------------------------------

    private bool CreateSlot()
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

        Skill skill = selectedOwnerPart.CurrentSkill;

        if (skill == null)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : Skill이 없습니다.");
            return false;
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
                "[ActionSlot Target Changed]\n" +
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

        ClearSelection();

        ClearLines();

        RefreshAllBodyPartButtons();

        Debug.Log("[UI] Player selection reset.");
    }

    //---------------------------------------
    // 턴 UI 초기화
    //---------------------------------------

    public void ResetTurn()
    {
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
        // TargetArrowUI는 LateUpdate에서 ActionManager 슬롯을 보고 자동 갱신하므로
        // 여기서는 별도로 삭제할 필요 없음.
    }

    //---------------------------------------
    // 버튼 갱신
    //---------------------------------------

    private void RefreshAllBodyPartButtons()
    {
        BodyPartButton[] buttons =
            FindObjectsByType<BodyPartButton>(
                FindObjectsSortMode.None);

        foreach (BodyPartButton button in buttons)
        {
            if (button == null)
                continue;

            button.Refresh();
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
}

public enum BattleInputMode
{
    SelectOwner,
    SelectTarget
}