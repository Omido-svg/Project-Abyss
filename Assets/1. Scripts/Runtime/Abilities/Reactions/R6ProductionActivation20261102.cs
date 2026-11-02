using System;
using UnityEngine;
using ProjectAbyss.Abilities;

/// <summary>
/// Production composition boundary for the already-tested R6 stepwise
/// exchange coordinator. Activate AFTER characters register their mechanics,
/// never from a BattleEvent callback or from a SkillEffectEntry.
/// </summary>
public static class R6ProductionActivation20261102
{
    public static bool Activate(BattleContext battle)
    {
        var services=battle?.Services;
        if(services?.ActionResolver==null ||
           services.ReactionScheduler==null ||
           services.ReactionActionRegistry==null ||
           services.SpecialistReactionRegistry==null ||
           services.ClashManager==null)
            throw new InvalidOperationException(
                "R6 production startup requires battle-local resolver, " +
                "reaction scheduler, registries and ClashManager.");

        // The runtime factory already attached queue/registries. This switches
        // the ACTUAL ActionResolver.Resolve coroutine into exchange-by-exchange
        // commit/presentation/checkpoint handling.
        services.ActionResolver.EnableR6StepwiseClashes();

        // Hifumi's legacy synchronous OnClashResolved callback MUST NOT run
        // in parallel with a deferred reaction. Only switch after its
        // character-owned mechanic has completed initialization/registration.
        bool EnsureHifumi(Character character)
        {
            if(!(character is Hifumi hifumi) ||
               hifumi.HifumiMechanic == null ||
               !hifumi.HifumiMechanic.IsRegistered)
                return true;
            return hifumi.HifumiMechanic.TryEnableR6DeferredCounters();
        }

        if(!EnsureHifumi(battle.Player))
        {
            services.ActionResolver.DisableR6StepwiseClashes();
            throw new InvalidOperationException("R6 Hifumi deferred counter registration failed.");
        }
        if(battle.Enemies!=null)
            foreach(var enemy in battle.Enemies)
                if(!EnsureHifumi(enemy))
                {
                    Deactivate(battle);
                    throw new InvalidOperationException("R6 enemy Hifumi deferred counter registration failed.");
                }
        return services.ActionResolver.IsR6StepwiseActive;
    }

    /// <summary>
    /// Idempotent end-of-battle cleanup. This is invoked only after TurnManager
    /// stops its coroutine and after registered character mechanics end.
    /// </summary>
    public static void Deactivate(BattleContext battle)
    {
        var services=battle?.Services;
        services?.ActionResolver?.DisableR6StepwiseClashes();
        services?.ReactionScheduler?.ClearForBattleEnd();
        services?.ReactionActionRegistry?.ClearForBattleEnd();

        services?.ReactionLifetimeRegistry?.ClearForBattleEnd();
        services?.SpecialistReactionRegistry?.ClearForBattleEnd();
        if(battle?.Player is Hifumi player)
            player.HifumiMechanic?.DisableR6DeferredCounters();
        if(battle?.Enemies!=null)
            foreach(var enemy in battle.Enemies)
                if(enemy is Hifumi hifumi)
                    hifumi.HifumiMechanic?.DisableR6DeferredCounters();
    }
}
