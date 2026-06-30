using UnityEngine;

public class BodyPartButton : MonoBehaviour
{
    private Character owner;
    private BodyPart bodyPart;

    private BattleUIManager uiManager;
    private BattleManager battleManager;

    private void Awake()
    {
        uiManager = FindFirstObjectByType<BattleUIManager>();
        battleManager = FindFirstObjectByType<BattleManager>();
    }

    public void Bind(Character owner, BodyPart part)
    {
        this.owner = owner;
        this.bodyPart = part;
    }

    public void OnClick()
    {
        if (uiManager == null || battleManager == null)
            return;

        if (owner == null || bodyPart == null)
        {
            Debug.LogError("[BodyPartButton] Not bound properly");
            return;
        }

        Character player = battleManager.BattleContext.Player;

        if (owner == player)
            uiManager.SelectOwnerPart(owner, bodyPart);
        else
            uiManager.SelectTargetPart(owner, bodyPart);
    }
}