using UnityEngine;

public class EliteEnemyPassive : Passive
{
    private int pressureStack;

    private const int MaxPressureStack = 3;

    public EliteEnemyPassive()
    {
        PassiveName = "정예의 압박";
    }

    protected override void OnRegister()
    {
        battleEvent.OnTurnStart += OnTurnStart;
        battleEvent.OnClashWin += OnClashWin;
        battleEvent.OnDamageDealt += OnDamageDealt;
    }

    protected override void OnUnregister()
    {
        battleEvent.OnTurnStart -= OnTurnStart;
        battleEvent.OnClashWin -= OnClashWin;
        battleEvent.OnDamageDealt -= OnDamageDealt;
    }

    private void OnTurnStart(int turn)
    {
        if (owner == null)
            return;

        pressureStack = 0;
    }

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction == null)
            return;

        if (winnerAction.Owner != owner)
            return;

        if (winnerAction.Target == null)
            return;

        //--------------------------------
        // 정예몹 패시브 1:
        // 합 승리 시 압박 스택 증가
        //--------------------------------

        pressureStack++;

        if (pressureStack > MaxPressureStack)
            pressureStack = MaxPressureStack;

        //--------------------------------
        // 정예몹 패시브 2:
        // 합 승리 시 대상에게 출혈 부여
        //--------------------------------

        winnerAction.Target.AddStatus(
            new Bleeding(1),
            owner);

        //--------------------------------
        // 정예몹 패시브 3:
        // 한 턴에 합을 2번 이상 이기면 화상 추가
        //--------------------------------

        if (pressureStack >= 2)
        {
            winnerAction.Target.AddStatus(
                new Burn(1),
                owner);
        }

        Debug.Log(
            $"{owner.Data.CharacterName} 패시브 발동 : 정예의 압박 " +
            $"({pressureStack}/{MaxPressureStack})");
    }

    private void OnDamageDealt(
        Character attacker,
        int damage)
    {
        if (attacker != owner)
            return;

        if (owner == null)
            return;

        //--------------------------------
        // 피해를 줄 때마다 압박이 쌓여 있으면 유지 보상
        // 너무 강하지 않게 로그만 남기고,
        // 실제 효과는 합 승리 쪽에 집중
        //--------------------------------

        if (pressureStack <= 0)
            return;

        Debug.Log(
            $"{owner.Data.CharacterName}가 압박 상태로 피해를 입힘");
    }
}