using UnityEngine;

public class NormalEnemyBloodScentMechanic : CombatMechanic
{
    public override string MechanicName => "피 냄새";

    public override void OnRegister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnClashWin += OnClashWin;
    }

    public override void OnUnregister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnClashWin -= OnClashWin;
    }

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction == null)
            return;

        if (loserAction == null)
            return;

        if (winnerAction.Owner != owner)
            return;

        if (winnerAction.Target == null)
            return;

        //--------------------------------
        // 일반몹 메커닉:
        // 합 승리 시 대상에게 출혈 1 부여
        //--------------------------------

        winnerAction.Target.AddStatus(
            new Bleeding(1),
            owner);

        Debug.Log(
            $"{owner.Data.CharacterName} 메커닉 발동 : {MechanicName}");
    }
}