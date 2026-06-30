using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;

    private ActionBuffer Buffer => battleManager.ActionBuffer;

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
        if (battleManager != null)
            battleManager.BattleContext._battleEvent.OnTurnStart += HandleTurnStart;
    }

    private void OnDestroy()
    {
        if (battleManager != null)
            battleManager.BattleContext._battleEvent.OnTurnStart -= HandleTurnStart;
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
        if (owner == null || part == null)
            return;

        selectedOwner = owner;
        selectedOwnerPart = part;

        // 이미 선택했던 슬롯이면 제거 (수정 모드)
        ActionSlot oldSlot = Buffer.FindSlot(owner, part);

        if (oldSlot != null)
        {
            Buffer.Remove(oldSlot);

            Debug.Log($"[{part.Type}] 기존 행동 삭제");
        }

        Debug.Log($"Owner : {owner.Data.CharacterName} / {part.Type}");
    }

    //---------------------------------------
    // 공격 대상 선택
    //---------------------------------------

    public void SelectTargetSlot(Character target, BodyPart part)
    {
        if (selectedOwnerPart == null)
        {
            Debug.Log("먼저 공격 부위를 선택하세요.");
            return;
        }

        if (target == selectedOwner)
        {
            Debug.Log("자기 자신은 공격할 수 없습니다.");
            return;
        }

        selectedTarget = target;
        selectedTargetPart = part;

        CreateSlot();
    }

    //---------------------------------------

    private void CreateSlot()
    {
        ActionSlot slot = new ActionSlot
        {
            Owner = selectedOwner,
            Part = selectedOwnerPart,
            TargetCharacter = selectedTarget,
            TargetPart = selectedTargetPart,
            Skill = selectedOwnerPart.CurrentSkill,
            Phase = CalculatePhase(selectedOwnerPart.CurrentSkill.ActionType)
        };

        Buffer.Add(slot);

        Debug.Log(
            $"{slot.Owner.Data.CharacterName} " +
            $"{slot.Part.Type} -> " +
            $"{slot.TargetCharacter.Data.CharacterName}");

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

    private void ClearSelection()
    {
        selectedOwner = null;
        selectedOwnerPart = null;

        selectedTarget = null;
        selectedTargetPart = null;
    }

    //---------------------------------------

    private void ClearLines()
    {
        // TODO
    }

    //---------------------------------------

    public void ResetTurn()
    {
        Buffer.Clear();

        ClearSelection();

        ClearLines();
    }
}