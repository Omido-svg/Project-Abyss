using UnityEngine;

public class EliteEnemyMechanic : CombatMechanic
{
    public override string MechanicName => "엘리트 본능";

    private const int ClashWinPrestigeGain = 10;
    private const int SelfPartWeakenedPrestigeGain = 15;
    private const int SelfPartDestroyedPrestigeGain = 25;

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnClashWin += OnClashWin,
            () => battleEvent.OnClashWin -= OnClashWin,
            "OnClashWin");

        SubscribeToBattleEvent(
            () => battleEvent.OnBodyPartWeakenResolved += OnBodyPartWeakenResolved,
            () => battleEvent.OnBodyPartWeakenResolved -= OnBodyPartWeakenResolved,
            "OnBodyPartWeakenResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnBodyPartBreakResolved += OnBodyPartBreakResolved,
            () => battleEvent.OnBodyPartBreakResolved -= OnBodyPartBreakResolved,
            "OnBodyPartBreakResolved");
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

        resolver.AddPrestige(
            EffectRequest.Prestige(
                owner,
                owner,
                ClashWinPrestigeGain));

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
            $"합 승리 위세 +{ClashWinPrestigeGain}, 혈상 1, " +
            $"Target={GetTargetName(winnerAction)}");
    }

    private void OnBodyPartWeakenResolved(
        BodyPartWeakenEventContext context)
    {
        if (context?.Target != owner ||
            context.Part == null)
        {
            return;
        }

        AddPrestige(
            SelfPartWeakenedPrestigeGain);

        Debug.Log(
            $"{GetOwnerName()} 메커닉 발동 : {MechanicName} / " +
            $"{context.Part.Type} 약화로 위세 " +
            $"+{SelfPartWeakenedPrestigeGain}");
    }

    private void OnBodyPartBreakResolved(
        BodyPartBreakEventContext context)
    {
        if (context?.Target != owner ||
            context.Part == null)
        {
            return;
        }

        AddPrestige(
            SelfPartDestroyedPrestigeGain);

        Debug.Log(
            $"{GetOwnerName()} 메커닉 발동 : {MechanicName} / " +
            $"{context.Part.Type} 파괴로 위세 " +
            $"+{SelfPartDestroyedPrestigeGain}");
    }

    private void AddPrestige(
        int amount)
    {
        if (amount <= 0)
            return;

        owner?.BattleContext?.EffectResolver?
            .AddPrestige(
                EffectRequest.Prestige(
                    owner,
                    owner,
                    amount));
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
