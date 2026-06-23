// using UnityEngine;

// public class BattleUIManager : MonoBehaviour
// {
//     [SerializeField] private BattleManager battleManager;

//     private Character selectedOwner;
//     private BodyPart selectedOwnerPart;

//     private Character selectedTarget;
//     private BodyPart selectedTargetPart;

//     //------------------------------------------------

//     public void SelectOwnerPart(Character owner, BodyPart part)
//     {
//         selectedOwner = owner;
//         selectedOwnerPart = part;

//         Debug.Log($"Owner : {owner.CharacterName} / {part.type}");
//     }

//     //------------------------------------------------

//     public void SelectTargetPart(Character target, BodyPart part)
//     {
//         if (selectedOwner == null)
//         {
//             Debug.Log("먼저 공격 부위를 선택하세요.");
//             return;
//         }

//         selectedTarget = target;
//         selectedTargetPart = part;

//         CreateAction();
//     }

//     //------------------------------------------------

//     private void CreateAction()
//     {
//         BattleAction action = new BattleAction();

//         action.Owner = selectedOwner;
//         action.Target = selectedTarget;

//         action.OwnerPart = selectedOwnerPart;
//         action.TargetPart = selectedTargetPart;

//         action.Skill = selectedOwnerPart.SelectedSkill;

//         action.Speed = selectedOwnerPart.CurrentSpeed;

//         battleManager.ActionManager.AddAction(action);

//         Debug.Log(
//             $"{action.Owner.CharacterName} [{action.OwnerPart.type}] -> " +
//             $"{action.Target.CharacterName} [{action.TargetPart.type}]");

//         ClearSelection();
//     }

//     //------------------------------------------------

//     private void ClearSelection()
//     {
//         selectedOwner = null;
//         selectedOwnerPart = null;

//         selectedTarget = null;
//         selectedTargetPart = null;
//     }
// }