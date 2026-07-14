using UnityEngine;

public class NormalEnemyBloodScentMechanic : CombatMechanic
{
    public override string MechanicName => "피 냄새";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnClashWin += OnClashWin,
            () => battleEvent.OnClashWin -= OnClashWin,
            "OnClashWin");
    }

    public override void OnUnregister()
    {
    }

    private void OnClashWin(
        BattleAction winnerAction,
        BattleAction loserAction)
    {
        if (winnerAction?.Owner != owner ||
            winnerAction.Target == null ||
            winnerAction.Target.IsDead)
        {
            return;
        }

        BattleEffectResolver resolver =
            owner?.BattleContext?.EffectResolver;

        if (resolver == null)
            return;

        Bleeding bleeding =
            new Bleeding(1);

        if (winnerAction.TargetPart != null)
        {
            resolver.ApplyBodyPartStatus(
                EffectRequest.BodyPartStatus(
                    owner,
                    winnerAction.Target,
                    winnerAction.TargetPart,
                    bleeding));
        }
        else
        {
            resolver.ApplyCharacterStatus(
                EffectRequest.CharacterStatus(
                    owner,
                    winnerAction.Target,
                    bleeding));
        }

        Debug.Log(
            $"{GetOwnerName()} 메커닉 발동 : {MechanicName} / " +
            $"Target={GetTargetName(winnerAction)}");
    }

    private string GetOwnerName()
    {
        return owner?.Data?.CharacterName ??
               owner?.name ??
               "NULL";
    }

    private string GetTargetName(
        BattleAction action)
    {
        if (action?.Target == null)
            return "NULL";

        string characterName =
            action.Target.Data?.CharacterName ??
            action.Target.name;

        return action.TargetPart == null
            ? $"{characterName}/SINGLE_HP"
            : $"{characterName}/{action.TargetPart.Type}";
    }
}
