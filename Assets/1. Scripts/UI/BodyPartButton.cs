// using UnityEngine;

// public class BodyPartButton : MonoBehaviour
// {
//     public Character owner;
//     public BodyPart bodyPart;
//     private BattleUIManager uiManager;
//     private BattleManager battleManager;

//     private void Awake()
//     {
//         uiManager = FindFirstObjectByType<BattleUIManager>();
//         battleManager = FindFirstObjectByType<BattleManager>();
//     }

//     public void OnClick()
//     {
//         if (owner == battleManager.BattleContext.Player)
//         {
//             uiManager.SelectOwnerPart(owner, bodyPart);
//         }
//         else
//         {
//             uiManager.SelectTargetPart(owner, bodyPart);
//         }
//     }
// }