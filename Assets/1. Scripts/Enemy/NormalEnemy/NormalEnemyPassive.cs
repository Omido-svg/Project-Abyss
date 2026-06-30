using UnityEngine;

public class NormalEnemyPassive : Passive
{
    public NormalEnemyPassive()
    {
        PassiveName = "피 냄새";
    }

    protected override void OnRegister()
    {
        battleEvent.OnClashWin += OnClashWin;
    }

    protected override void OnUnregister()
    {
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
        // 일반몹 패시브: 합 승리 시 출혈 1 부여
        //--------------------------------

        winnerAction.Target.AddStatus(
            new Bleeding(1),
            owner);

        Debug.Log(
            $"{owner.Data.CharacterName} 패시브 발동 : 피 냄새");
    }
}