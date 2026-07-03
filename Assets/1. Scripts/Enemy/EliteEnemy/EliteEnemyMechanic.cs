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

        if (winnerAction.TargetPart == null)
            return;

        BattleEffectResolver resolver =
            owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.AddPrestige(
            EffectRequest.Prestige(
                owner,
                owner,
                ClashWinPrestigeGain));

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                owner,
                winnerAction.Target,
                winnerAction.TargetPart,
                new Bleeding(1)));

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

        BattleEffectResolver resolver =
            owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.AddPrestige(
            EffectRequest.Prestige(
                owner,
                owner,
                SelfPartWeakenedPrestigeGain));

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

        BattleEffectResolver resolver =
            owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.AddPrestige(
            EffectRequest.Prestige(
                owner,
                owner,
                SelfPartDestroyedPrestigeGain));

        Debug.Log(
            $"{owner.Data.CharacterName} 메커닉 발동 : {MechanicName} / " +
            $"{part.Type} 파괴로 위세 +{SelfPartDestroyedPrestigeGain}");
    }
}