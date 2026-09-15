using System;

/// <summary>
/// 0915 C-18/C-22 공통 전투 보상 서비스.
/// PartBroken 자체에는 보상을 연결하지 않는다.
/// </summary>
public sealed class BattleRewardService : IDisposable
{
    private readonly BattleContext context;
    private readonly PrestigeChargeService prestige;
    private bool bound;

    public BattleRewardService(BattleContext context, PrestigeChargeService prestige)
    {
        this.context = context;
        this.prestige = prestige;
        Bind();
    }

    private void Bind()
    {
        BattleEvent e = context?._battleEvent;
        if (e == null || bound)
            return;
        e.OnBodyPartWeakenResolved += OnPartWeakened;
        e.OnKillResolved += OnKill;
        bound = true;
    }

    public void ResetForBattle()
    {
        if (context?.AllCharacters != null)
        {
            foreach (Character character in context.AllCharacters)
            {
                if (character?.RuntimeStatus != null)
                    character.RuntimeStatus.currentPrestige = 0;
            }
        }
    }

    private void OnPartWeakened(BodyPartWeakenEventContext eventContext)
    {
        Character player = context?.Player;
        if (player == null || eventContext == null)
            return;

        if (eventContext.Source == player &&
            IsEnemy(eventContext.Target))
        {
            player.AddEnergy(1, CombatResourceChangeReason.SkillEffect, eventContext.SourceAction);
        }
    }

    private void OnKill(KillEventContext eventContext)
    {
        if (eventContext == null || !eventContext.HasKiller)
            return;

        Character killer = eventContext.Killer;
        Character victim = eventContext.Victim;

        if (killer == context?.Player && IsEnemy(victim))
            killer.AddEnergy(1, CombatResourceChangeReason.SkillEffect, eventContext.SourceAction);

        prestige?.ChargeKill(killer, victim, eventContext.SourceAction);
    }

    private bool IsEnemy(Character candidate)
    {
        if (candidate == null || context?.Enemies == null)
            return false;

        foreach (Character enemy in context.Enemies)
        {
            if (ReferenceEquals(enemy, candidate))
                return true;
        }

        return false;
    }

    public void Dispose()
    {
        BattleEvent e = context?._battleEvent;
        if (e != null && bound)
        {
            e.OnBodyPartWeakenResolved -= OnPartWeakened;
            e.OnKillResolved -= OnKill;
        }
        bound = false;
    }
}
