using System.Collections.Generic;

public class ClashManager
{
    private readonly DamageManager damageManager;
    private readonly BattleContext battleContext;

    public ClashManager(
        BattleContext battleContext,
        DamageManager damageManager)
    {
        this.battleContext = battleContext;
        this.damageManager = damageManager;
    }

    //------------------------------------------------

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

    //------------------------------------------------

    private void ResolveClash(
        BattleAction first,
        BattleAction second)
    {
        battleContext._battleEvent.RaiseClashStart(
            first.Owner,
            second.Owner);

        int firstPower = first.Skill.Roll();
        int secondPower = second.Skill.Roll();

        if (firstPower > secondPower)
        {
            battleContext._battleEvent.RaiseClashWin(
                first.Owner,
                second.Owner);

            battleContext._battleEvent.RaiseClashLose(
                second.Owner,
                first.Owner);

            damageManager.ApplyDamage(first);
        }
        else if (firstPower < secondPower)
        {
            battleContext._battleEvent.RaiseClashWin(
                second.Owner,
                first.Owner);

            battleContext._battleEvent.RaiseClashLose(
                first.Owner,
                second.Owner);

            damageManager.ApplyDamage(second);
        }
        else
        {
            // TODO : 무승부 처리
        }
    }

    //------------------------------------------------

    private void ResolveOneSide(BattleAction action)
    {
        damageManager.ApplyDamage(action);
    }
}