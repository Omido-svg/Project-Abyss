using System.Collections.Generic;
using UnityEngine;

public class ClashManager
{
    private readonly DamageManager damageManager;
    private readonly MomentumManager momentumManager;
    private readonly BattleContext battleContext;

    //--------------------------------

    // 디버깅용
    private const int SpeedWeight = 1;

    //--------------------------------

    public ClashManager(
        BattleContext battleContext,
        DamageManager damageManager,
        MomentumManager momentumManager)
    {
        this.battleContext = battleContext;
        this.damageManager = damageManager;
        this.momentumManager = momentumManager;
    }

    //--------------------------------

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

    //--------------------------------

    private void ResolveClash(
        BattleAction first,
        BattleAction second)
    {
        battleContext._battleEvent.RaiseClashStart(
            first.Owner,
            second.Owner);

        // 위력 굴림 (한 번만)
        first.RolledPower =
            momentumManager.ApplyLastStand(
                first.Owner,
                first.RollPower());

        second.RolledPower =
            momentumManager.ApplyLastStand(
                second.Owner,
                second.RollPower());

        // 합 판정값
        int firstClash =
            first.RolledPower +
            (first.Speed - second.Speed) * SpeedWeight;

        int secondClash =
            second.RolledPower +
            (second.Speed - first.Speed) * SpeedWeight;

        // 동점이면 속도 제외 후 재굴림
        while (firstClash == secondClash)
        {
            first.RolledPower =
                momentumManager.ApplyLastStand(
                    first.Owner,
                    first.RollPower());

            second.RolledPower =
                momentumManager.ApplyLastStand(
                    second.Owner,
                    second.RollPower());

            firstClash = first.RolledPower;
            secondClash = second.RolledPower;
        }

        // 승자 / 패자 결정
        BattleAction winner;
        BattleAction loser;

        int winnerClash;
        int loserClash;

        if (firstClash > secondClash)
        {
            winner = first;
            loser = second;

            winnerClash = firstClash;
            loserClash = secondClash;
        }
        else
        {
            winner = second;
            loser = first;

            winnerClash = secondClash;
            loserClash = firstClash;
        }

        // 이벤트
        battleContext._battleEvent.RaiseClashWin(
            winner.Owner,
            loser.Owner);

        battleContext._battleEvent.RaiseClashLose(
            loser.Owner,
            winner.Owner);

        int gap = winnerClash - loserClash;

        // 이전 상태
        bool wasOverwhelm = momentumManager.IsOverwhelm(winner.Owner);

        // 기세 이동
        momentumManager.ApplyClashResult(
            winner.Owner,
            loser.Owner,
            gap);
        
        int prestigeGain = momentumManager.CalculatePrestigeGain(gap);
        winner.Owner.AddPrestige(prestigeGain);

        // Overwhelm 최초 진입
        bool enteredOverwhelm =
            !wasOverwhelm &&
            momentumManager.IsOverwhelm(winner.Owner);
            
        if (enteredOverwhelm)
        {
            winner.Owner.RuntimeStatus.currentPrestige =
                winner.Owner.CurrentStatus.maxPrestige;
        }

        // 데미지
        damageManager.ApplyDamage(winner);
    }

    //--------------------------------

    private void ResolveOneSide(BattleAction action)
    {
        action.RolledPower = action.RollPower();

        action.Skill.Execute(action);
        
        damageManager.ApplyDamage(action);
    }

}