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

    private void Start()
    {
        if (battleManager != null &&
            battleManager.BattleContext != null)
        {
            battleManager.BattleContext._battleEvent.OnTurnStart += HandleTurnStart;
        }
    }

    private void OnDestroy()
    {
        if (battleManager != null &&
            battleManager.BattleContext != null)
        {
            battleManager.BattleContext._battleEvent.OnTurnStart -= HandleTurnStart;
        }
    }

    //---------------------------------------

    private void HandleTurnStart(int turn)
    {
        ResetTurn();
    }

    //---------------------------------------
    // 공격 부위 선택
    //---------------------------------------

    public void SelectOwnerSlot(Character owner, BodyPart part)
    {
        if (!IsManagerReady())
            return;

        if (owner == null || part == null)
            return;

        //---------------------------------------
        // 현재는 플레이어만 직접 선택 가능
        //---------------------------------------

        if (owner != battleManager.BattleContext.Player)
        {
            Debug.Log("플레이어의 부위만 행동 슬롯으로 선택할 수 있습니다.");
            return;
        }

        if (owner.IsDead)
        {
            Debug.Log($"{owner.Data.CharacterName}는 사망 상태입니다.");
            return;
        }

        if (part.IsBroken)
        {
            Debug.Log($"[{part.Type}] 파괴된 부위는 행동할 수 없습니다.");
            return;
        }

        if (part.CurrentSkill == null)
        {
            Debug.Log($"[{part.Type}] 선택된 스킬이 없습니다.");
            return;
        }

        selectedOwner = owner;
        selectedOwnerPart = part;

        //---------------------------------------
        // 기존 슬롯 확인
        // 여기서 삭제하지 않는다.
        // 새 타겟을 고르면 AddOrReplaceSlot에서 교체된다.
        //---------------------------------------

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
                $"{owner.Data.CharacterName} / {part.Type} / " +
                $"Skill : {part.CurrentSkill.SkillName}");
        }
    }

    //---------------------------------------
    // 공격 대상 선택
    //---------------------------------------
    
    public void OnBodyPartClicked(Character owner, BodyPart part)
    {
        if (inputMode == BattleInputMode.SelectOwner)
        {
            if (!IsPlayer(owner))
            {
                Debug.Log("먼저 플레이어의 행동 부위를 선택하세요.");
                return;
            }

            SelectOwnerSlot(owner, part);

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

            SelectTargetSlot(owner, part);

            inputMode = BattleInputMode.SelectOwner;
        }
    }

    public void SelectTargetSlot(Character target, BodyPart part)
    {
        if (!IsManagerReady())
            return;

        if (target == null || part == null)
            return;

        if (selectedOwner == null || selectedOwnerPart == null)
        {
            Debug.Log("먼저 공격 부위를 선택하세요.");
            return;
        }

        if (target == selectedOwner)
        {
            Debug.Log("자기 자신은 공격할 수 없습니다.");
            return;
        }

        if (target.IsDead)
        {
            Debug.Log($"{target.Data.CharacterName}는 이미 사망했습니다.");
            return;
        }

        if (part.IsBroken)
        {
            Debug.Log($"[{part.Type}] 파괴된 부위는 대상으로 지정할 수 없습니다.");
            return;
        }

        selectedTarget = target;
        selectedTargetPart = part;

        //---------------------------------------
        // 타겟 선택 디버그 출력
        //---------------------------------------

        Debug.Log(
            $"[Target Selected] " +
            $"{selectedOwner.Data.CharacterName} {selectedOwnerPart.Type} " +
            $"-> {target.Data.CharacterName} {part.Type}");

        CreateSlot();
    }

    //---------------------------------------

    private void CreateSlot()
    {
        if (!IsManagerReady())
            return;

        if (selectedOwner == null ||
            selectedOwnerPart == null ||
            selectedTarget == null ||
            selectedTargetPart == null)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 선택 정보가 부족합니다.");
            return;
        }

        if (selectedOwner.IsDead || selectedTarget.IsDead)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 사망한 캐릭터가 포함되어 있습니다.");
            return;
        }

        if (selectedOwnerPart.IsBroken)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 행동 부위가 파괴되어 있습니다.");
            return;
        }

        if (selectedTargetPart.IsBroken)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : 대상 부위가 파괴되어 있습니다.");
            return;
        }

        Skill skill = selectedOwnerPart.CurrentSkill;

        if (skill == null)
        {
            Debug.LogWarning("ActionSlot 생성 실패 : Skill이 없습니다.");
            return;
        }

        //---------------------------------------
        // 기존 슬롯 확인
        //---------------------------------------

        ActionSlot oldSlot =
            battleManager.ActionManager.FindSlot(
                selectedOwner,
                selectedOwnerPart);

        //---------------------------------------
        // 새 슬롯 생성
        //---------------------------------------

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

        //---------------------------------------
        // 한 부위당 하나의 슬롯만 유지
        // 기존 슬롯이 있으면 교체됨
        //---------------------------------------

        battleManager.ActionManager.AddOrReplaceSlot(newSlot);

        //---------------------------------------
        // 디버그 출력
        //---------------------------------------

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

        ClearSelection();
    }

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

    //---------------------------------------

    private string FormatSlot(ActionSlot slot)
    {
        if (slot == null)
            return "NULL SLOT";

        string ownerName =
            slot.Owner == null
                ? "NULL"
                : slot.Owner.Data.CharacterName;

        string partName =
            slot.Part == null
                ? "NULL"
                : slot.Part.Type.ToString();

        string skillName =
            slot.Skill == null
                ? "NULL"
                : slot.Skill.SkillName;

        string targetName =
            slot.TargetCharacter == null
                ? "NULL"
                : slot.TargetCharacter.Data.CharacterName;

        string targetPartName =
            slot.TargetPart == null
                ? "NULL"
                : slot.TargetPart.Type.ToString();

        string targetSlotName =
            slot.TargetSlot == null
                ? "NULL"
                : $"{slot.TargetSlot.Owner.Data.CharacterName} {slot.TargetSlot.Part.Type}";

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

    private void ClearLines()
    {
        // TODO
        // 나중에 UI 선 연결을 쓰면 여기서 삭제
    }

    //---------------------------------------

    public void ResetTurn()
    {
        ClearSelection();

        ClearLines();
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
    
    public bool IsPlayer(Character character)
    {
        if (battleManager == null)
            return false;

        if (battleManager.BattleContext == null)
            return false;

        return character == battleManager.BattleContext.Player;
    }
}

public enum BattleInputMode
{
    SelectOwner,
    SelectTarget
}