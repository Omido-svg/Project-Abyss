using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;

    private Character selectedOwner;
    private BodyPart selectedOwnerPart;

    private Character selectedTarget;
    private BodyPart selectedTargetPart;

    //------------------------------------------------

    public void SelectOwnerPart(Character owner, BodyPart part)
    {
        if (owner == null || part == null)
        {
            Debug.LogError("SelectOwnerPart: null reference");
            return;
        }

        if (owner.Data == null)
        {
            Debug.LogError("owner.Data is NULL");
            return;
        }

        selectedOwner = owner;
        selectedOwnerPart = part;

        Debug.Log($"Owner : {owner.Data.CharacterName} / {part.Type}");
    }

    //------------------------------------------------

    public void SelectTargetPart(Character target, BodyPart part)
    {
        if (selectedOwner == null)
        {
            Debug.Log("먼저 공격 부위를 선택하세요.");
            return;
        }

        selectedTarget = target;
        selectedTargetPart = part;

        CreateAction();
    }

    //------------------------------------------------

    private void CreateAction()
    {
        if (selectedOwnerPart.CurrentSkill == null)
        {
            Debug.LogError("UI딴에서 Skill 이 NULL임");
            return; // ← 이거 중요 (이 상태로 Action 만들면 터짐)
        }

        BattleAction action = new BattleAction
        {
            Owner = selectedOwner,
            Target = selectedTarget,
            OwnerPart = selectedOwnerPart,
            TargetPart = selectedTargetPart,
            Skill = selectedOwnerPart.CurrentSkill,

            // 🔥 핵심 수정
            Phase = CalculatePhase(selectedOwnerPart.CurrentSkill.ActionType)
        };

        battleManager.ActionManager.AddAction(action);

        Debug.Log(
            $"{action.Owner.Data.CharacterName} [{action.OwnerPart.Type}] -> " +
            $"{action.Target.Data.CharacterName} [{action.TargetPart.Type}]");

        ClearSelection();
    }

    //------------------------------------------------

    private void ClearSelection()
    {
        selectedOwner = null;
        selectedOwnerPart = null;

        selectedTarget = null;
        selectedTargetPart = null;
    }
    
    private ActionPhase CalculatePhase(ActionType type)
    {
        switch (type)
        {
            case ActionType.Duel:
            case ActionType.NormalAttack:
                return ActionPhase.COMBAT;

            case ActionType.Preparation:
                return ActionPhase.FORESIGHT;

            case ActionType.Prestige:
                return ActionPhase.PRETURN;

            default:
                Debug.LogWarning($"Unknown ActionType: {type}");
                return ActionPhase.COMBAT; // 안전 fallback
        }
    }
}