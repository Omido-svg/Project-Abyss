using UnityEngine;

public sealed class DamageEventDispatcher
{
    private readonly BattleContext battleContext;

    public DamageEventDispatcher(
        BattleContext battleContext)
    {
        this.battleContext = battleContext;
    }

    public DamageEventResult DispatchResolved(
        DamageContext context)
    {
        if (context == null)
            return null;

        if (context.WasDispatched)
            return context.EventResult;

        context.WasDispatched = true;
        context.CurrentStage = DamageStage.Resolved;

        context.RecordStage(
            DamageStage.Resolved,
            context.AppliedDamage);

        DamageEventResult result =
            DamageEventResult.FromContext(context);

        context.EventResult = result;

        context.Action?.SetDamageContext(
            context);

        BattleEvent battleEvent =
            battleContext?._battleEvent;

        if (battleEvent == null)
            return result;

        // 공통 피해 결과를 먼저 알린다.
        battleEvent.RaiseDamageResolved(context);
        battleEvent.RaiseDamageEventResolved(result);

        int appliedDamage =
            context.GetDisplayDamage();

        if (appliedDamage > 0)
        {
            battleEvent.RaiseDamageTaken(
                context.Target,
                appliedDamage);

            if (context.Attacker != null)
            {
                battleEvent.RaiseDamageDealt(
                    context.Attacker,
                    appliedDamage);
            }
        }

        // 한 번의 피해로 여러 변화가 생기면
        // 약화 → 파괴 → 사망 → 처치 순서로 발행한다.
        if (result?.HasWeaken == true)
        {
            Debug.Log(
                $"[DamageEvent] Weaken / " +
                $"Source={context.Attacker?.Data?.CharacterName}, " +
                $"Target={context.Target?.Data?.CharacterName}, " +
                $"Part={context.TargetPart?.Type}, " +
                $"ActionId={context.Action?.ActionId ?? 0}");

            battleEvent.RaiseBodyPartWeakened(
                result.Weaken);
        }

        if (result?.HasBreak == true)
        {
            Debug.Log(
                $"[DamageEvent] Break / " +
                $"Source={context.Attacker?.Data?.CharacterName}, " +
                $"Target={context.Target?.Data?.CharacterName}, " +
                $"Part={context.TargetPart?.Type}, " +
                $"ActionId={context.Action?.ActionId ?? 0}");

            battleEvent.RaiseBodyPartDestroyed(
                result.Break);
        }

        if (result?.HasKill == true)
        {
            Debug.Log(
                $"[DamageEvent] Kill / " +
                $"Killer={result.Kill.Killer?.Data?.CharacterName}, " +
                $"Victim={result.Kill.Victim?.Data?.CharacterName}, " +
                $"DamageType={result.Kill.DamageType}, " +
                $"ActionId={result.Kill.SourceAction?.ActionId ?? 0}");

            battleEvent.RaiseCharacterDeath(
                result.Kill);

            battleEvent.RaiseKill(
                result.Kill);
        }

        return result;
    }
}
