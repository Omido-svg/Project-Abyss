using UnityEngine;

public class EliteEnemyMechanic : CombatMechanic
{
    public override string MechanicName => "엘리트 본능";

    private const int ClashWinPrestigeGain = 10;
    private const int SelfPartWeakenedPrestigeGain = 15;
    private const int SelfPartDestroyedPrestigeGain = 25;

    //------------------------------------------------
    // 등록 / 해제
    //------------------------------------------------

    public override void OnRegister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnClashWin += OnClashWin;
        battleEvent.OnBodyPartWeakened += OnBodyPartWeakened;
        battleEvent.OnBodyPartDestroyed += OnBodyPartDestroyed;
    }

    public override void OnUnregister()
    {
        if (battleEvent == null)
            return;

        battleEvent.OnClashWin -= OnClashWin;
        battleEvent.OnBodyPartWeakened -= OnBodyPartWeakened;
        battleEvent.OnBodyPartDestroyed -= OnBodyPartDestroyed;
    }

    //------------------------------------------------
    // 합 승리
    //------------------------------------------------

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
        // 엘리트 본능:
        // 합 승리 시 위세를 조금 더 빠르게 획득
        //--------------------------------

        owner.AddPrestige(
            ClashWinPrestigeGain);

        //--------------------------------
        // 추가 압박 효과
        //--------------------------------

        winnerAction.Target.AddStatus(
            new Bleeding(1),
            owner);

        Debug.Log(
            $"{owner.Data.CharacterName} 메커닉 발동 : {MechanicName} / " +
            $"합 승리 위세 +{ClashWinPrestigeGain}, 출혈 1");
    }

    //------------------------------------------------
    // 자신의 부위 약화
    //------------------------------------------------

    private void OnBodyPartWeakened(
        Character target,
        BodyPart part)
    {
        if (target == null || part == null)
            return;

        if (target != owner)
            return;

        owner.AddPrestige(
            SelfPartWeakenedPrestigeGain);

        Debug.Log(
            $"{owner.Data.CharacterName} 메커닉 발동 : {MechanicName} / " +
            $"{part.Type} 약화로 위세 +{SelfPartWeakenedPrestigeGain}");
    }

    //------------------------------------------------
    // 자신의 부위 파괴
    //------------------------------------------------

    private void OnBodyPartDestroyed(
        Character target,
        BodyPart part)
    {
        if (target == null || part == null)
            return;

        if (target != owner)
            return;

        owner.AddPrestige(
            SelfPartDestroyedPrestigeGain);

        Debug.Log(
            $"{owner.Data.CharacterName} 메커닉 발동 : {MechanicName} / " +
            $"{part.Type} 파괴로 위세 +{SelfPartDestroyedPrestigeGain}");
    }
}