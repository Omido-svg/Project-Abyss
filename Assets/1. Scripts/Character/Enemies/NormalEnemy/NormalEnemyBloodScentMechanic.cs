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

        if (winnerAction.TargetPart == null)
            return;

        BattleEffectResolver resolver =
            owner.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.ApplyBodyPartStatus(
            EffectRequest.BodyPartStatus(
                owner,
                winnerAction.Target,
                winnerAction.TargetPart,
                new Bleeding(1)));

        Debug.Log(
            $"{owner.Data.CharacterName} 메커닉 발동 : {MechanicName}");
    }
}