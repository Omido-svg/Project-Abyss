using System.Collections.Generic;
using UnityEngine;

public class ClashManager
{
    private readonly DamageManager damageManager;
    private readonly MomentumManager momentumManager;
    private readonly BattleContext battleContext;

    private const int SpeedWeight = 1;

    public ClashManager(
        BattleContext battleContext,
        DamageManager damageManager,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.damageManager = damageManager;
        this.momentumManager = momentumManager;
    }

    public void Resolve(Queue<ClashPair> clashQueue)
    {
        while (clashQueue.Count > 0)
        {
            ClashPair pair = clashQueue.Dequeue();

            battleContext._battleEvent.RaiseActionStart(pair.First);

            if (pair.IsClash)
            {
                battleContext._battleEvent.RaiseActionStart(pair.Second);

                ResolveClash(pair.First, pair.Second);

                battleContext._battleEvent.RaiseActionEnd(pair.Second);
            }
            else
            {
                ResolveOneSide(pair.First);
            }

            battleContext._battleEvent.RaiseActionEnd(pair.First);
        }
    }

    // =====================================================
    // Clash
    // =====================================================
    private void ResolveClash(BattleAction first, BattleAction second)
    {
        battleContext._battleEvent.RaiseClashStart(first.Owner, second.Owner);

        first.RolledPower = momentumManager.ApplyLastStand(
            first.Owner,
            first.RollPower());

        second.RolledPower = momentumManager.ApplyLastStand(
            second.Owner,
            second.RollPower());

        int firstClash = first.RolledPower + (first.Speed - second.Speed) * SpeedWeight;
        int secondClash = second.RolledPower + (second.Speed - first.Speed) * SpeedWeight;

        while (firstClash == secondClash)
        {
            first.RolledPower = momentumManager.ApplyLastStand(first.Owner, first.RollPower());
            second.RolledPower = momentumManager.ApplyLastStand(second.Owner, second.RollPower());

            firstClash = first.RolledPower;
            secondClash = second.RolledPower;
        }

        bool firstWin = firstClash > secondClash;

        BattleAction winner = firstWin ? first : second;
        BattleAction loser  = firstWin ? second : first;

        int winnerClash = firstWin ? firstClash : secondClash;
        int loserClash  = firstWin ? secondClash : firstClash;

        battleContext._battleEvent.RaiseClashWin(winner.Owner, loser.Owner);
        battleContext._battleEvent.RaiseClashLose(loser.Owner, winner.Owner);

        int gap = winnerClash - loserClash;

        bool wasOverwhelm = momentumManager.IsOverwhelm(winner.Owner);

        momentumManager.ApplyClashResult(winner.Owner, loser.Owner, gap);

        int prestigeGain = momentumManager.CalculatePrestigeGain(gap);
        winner.Owner.AddPrestige(prestigeGain);

        if (!wasOverwhelm && momentumManager.IsOverwhelm(winner.Owner))
        {
            winner.Owner.RuntimeStatus.currentPrestige =
                winner.Owner.CurrentStatus.maxPrestige;
        }

        int beforeHP = (int)winner.TargetPart.PartHP;

        int damage = damageManager.ApplyDamage(winner);

        int afterHP = (int)winner.TargetPart.PartHP;

        battleContext.battleManager.BattleLogger.LogClash(

            winner,
            loser,

            winnerClash,
            loserClash,

            damage,

            prestigeGain,

            beforeHP,
            afterHP

        );
    }

    // =====================================================
    // OneSide
    // =====================================================
        private void ResolveOneSide(BattleAction action)
        {
            action.RolledPower = action.RollPower();
            action.Skill.Execute(action);

            int beforeHP = (int)action.TargetPart.PartHP;

            int damage = damageManager.ApplyDamage(action);

            int afterHP = (int)action.TargetPart.PartHP;

            battleContext.battleManager.BattleLogger.LogDamage(
                action,
                damage,
                beforeHP,
                afterHP);
        }
}