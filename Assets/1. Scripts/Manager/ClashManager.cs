using System.Collections.Generic;

public class ClashManager
{
    private readonly DamageManager damageManager;
    private readonly MomentumManager momentumManager;
    private readonly BattleContext battleContext;

    //------------------------------------------------

    public ClashManager(
        BattleContext battleContext,
        DamageManager damageManager,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.damageManager = damageManager;
        this.momentumManager = momentumManager;
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

        //--------------------------------
        // 합 판정값
        //--------------------------------

        int firstPower =
            first.Skill.RollPower() +
            (first.Speed - second.Speed);

        int secondPower =
            second.Skill.RollPower() +
            (second.Speed - first.Speed);

        //--------------------------------
        // 동률이면 속도 제외하고 다시 굴림
        //--------------------------------

        while (firstPower == secondPower)
        {
            firstPower = first.Skill.RollPower();
            secondPower = second.Skill.RollPower();
        }

        //--------------------------------
        // 승패
        //--------------------------------

        if (firstPower > secondPower)
        {
            battleContext._battleEvent.RaiseClashWin(
                first.Owner,
                second.Owner);

            battleContext._battleEvent.RaiseClashLose(
                second.Owner,
                first.Owner);

            int gap = firstPower - secondPower;

            // 기세 이동
            momentumManager.ApplyClashResult(
                first.Owner,
                second.Owner,
                gap);

            // 데미지는 순수 위력
            damageManager.ApplyDamage(first);
        }
        else
        {
            battleContext._battleEvent.RaiseClashWin(
                second.Owner,
                first.Owner);

            battleContext._battleEvent.RaiseClashLose(
                first.Owner,
                second.Owner);

            int gap = secondPower - firstPower;

            momentumManager.ApplyClashResult(
                second.Owner,
                first.Owner,
                gap);       

            damageManager.ApplyDamage(second);
        }
    }

    //------------------------------------------------

    private void ResolveOneSide(BattleAction action)
    {
        // 도사림 등 합이 없는 행동
        damageManager.ApplyDamage(action);
    }
}