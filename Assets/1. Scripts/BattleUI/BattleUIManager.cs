using System.Collections.Generic;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [SerializeField] private BattleManager battleManager;
    private ActionBuffer Buffer => battleManager.ActionBuffer;

    private Character selectedOwner;
    private BodyPart selectedOwnerPart;

    private Character selectedTarget;
    private BodyPart selectedTargetPart;

    // 🔥 핵심: 부위별 이미 선택된 Target 저장
    private Dictionary<BodyPart, Character> lockedTargets = new();
    private Dictionary<BodyPart, BattleAction> lockedActions = new();

    //------------------------------------------------
    
    private void Start()
    {
        if (battleManager != null)
        {
            battleManager.BattleContext._battleEvent.OnTurnStart += HandleTurnStart;
        }
    }
    
    private void OnDestroy()
    {
        if (battleManager != null)
        {
            battleManager.BattleContext._battleEvent.OnTurnStart -= HandleTurnStart;
        }
    }
    
    private void HandleTurnStart(int turn)
    {
        ResetTurn();
    }

    public void SelectOwnerPart(Character owner, BodyPart part)
    {
        if (owner == null || part == null)
            return;

        selectedOwner = owner;
        selectedOwnerPart = part;

        // 이미 선택했던 부위라면
        if (lockedActions.TryGetValue(part, out BattleAction oldAction))
        {
            Buffer.Remove(oldAction);

            lockedActions.Remove(part);
            lockedTargets.Remove(part);

            Debug.Log($"[{part.Type}] 기존 행동 취소");
        }

        Debug.Log($"[SELECT OWNER] {owner.Data.CharacterName} / {part.Type}");
    }

    //------------------------------------------------

    public void SelectTargetPart(Character target, BodyPart part)
    {
        if (selectedOwner == null || selectedOwnerPart == null)
        {
            Debug.LogWarning("먼저 공격 부위를 선택하세요.");
            return;
        }

        if (target == null || part == null)
        {
            Debug.LogWarning("Target NULL");
            return;
        }

        if (target == selectedOwner)
        {
            Debug.LogWarning("자기 자신 타겟 불가");
            return;
        }

        selectedTarget = target;
        selectedTargetPart = part;

        CreateAction();
    }

    //------------------------------------------------

    private void CreateAction()
    {
        if (selectedOwnerPart == null || selectedTargetPart == null)
            return;

        if (selectedOwnerPart.CurrentSkill == null)
        {
            Debug.LogError("Skill NULL");
            return;
        }

        // 🔥 핵심: 이미 이 부위가 다른 타겟 공격 중인지 검사
        if (lockedTargets.TryGetValue(selectedOwnerPart, out Character existingTarget))
        {
            if (existingTarget != selectedTarget)
            {
                Debug.LogWarning(
                    $"⚠ {selectedOwnerPart.Type}는 이미 {existingTarget.Data.CharacterName}을 공격 중");

                // ❗ 여기서 선택 차단 (림버스식)
                return;
            }
        }

        lockedTargets[selectedOwnerPart] = selectedTarget;

        BattleAction action = new BattleAction
        {
            Owner = selectedOwner,
            Target = selectedTarget,
            OwnerPart = selectedOwnerPart,
            TargetPart = selectedTargetPart,
            Skill = selectedOwnerPart.CurrentSkill,
            Phase = CalculatePhase(selectedOwnerPart.CurrentSkill.ActionType)
        };

        Buffer.Add(action);
        
        lockedTargets[selectedOwnerPart] = selectedTarget;
        lockedActions[selectedOwnerPart] = action;

        Debug.Log($"[BUFFER LOCKED] {action.Owner.Data.CharacterName} {selectedOwnerPart.Type} → {action.Target.Data.CharacterName}");

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

    //------------------------------------------------

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
                return ActionPhase.COMBAT;
        }
    }
    
    // 선택한 선들 초기화 (차후 구현)
    private void ClearLines()
    {
        // TODO
    }
    
    public void ResetTurn()
    {
        Buffer.Clear();

        lockedTargets.Clear();
        lockedActions.Clear();

        ClearSelection();
        ClearLines();
    }
}