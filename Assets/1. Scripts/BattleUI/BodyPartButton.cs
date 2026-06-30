using UnityEngine;

public class BodyPartButton : MonoBehaviour
{
    private Character owner;
    private BodyPart bodyPart;

    private BattleUIManager uiManager;
    private BattleManager battleManager;

    //------------------------------------

    private void Awake()
    {
        uiManager = FindFirstObjectByType<BattleUIManager>();
        battleManager = FindFirstObjectByType<BattleManager>();
    }

    //------------------------------------

    public void Bind(Character owner, BodyPart part)
    {
        this.owner = owner;
        this.bodyPart = part;
    }

    //------------------------------------

    public void OnClick()
    {
        if (uiManager == null || battleManager == null)
            return;

        if (owner == null || bodyPart == null)
            return;

        if (owner == battleManager.BattleContext.Player)
        {
            uiManager.SelectOwnerSlot(owner, bodyPart);
        }
        else
        {
            uiManager.SelectTargetSlot(owner, bodyPart);
        }
    }
}