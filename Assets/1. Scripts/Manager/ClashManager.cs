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

    //--------------------------------------------------

    public void Resolve(Queue<ClashPair> clashQueue)
    {
        while (clashQueue.Count > 0)
        {
            ClashPair pair = clashQueue.Dequeue();

            if (pair == null)
                continue;

            if (!IsValidSlot(pair.First))
                continue;

            BattleAction firstAction = CreateBattleAction(pair.First);

            //------------------------------------
            // First Action Start
            //------------------------------------

            battleContext._battleEvent.RaiseActionStart(firstAction);

            //------------------------------------
            // 합
            //------------------------------------

            if (pair.IsClash && IsValidSlot(pair.Second))
            {
                BattleAction secondAction =
                    CreateBattleAction(pair.Second);

                battleContext._battleEvent.RaiseActionStart(secondAction);

                ResolveClash(firstAction, secondAction);

                battleContext._battleEvent.RaiseActionEnd(secondAction);
            }
            //------------------------------------
            // 일방 공격
            //------------------------------------
            else
            {
                ResolveOneSide(firstAction);
            }

            //------------------------------------
            // First Action End
            //------------------------------------

            battleContext._battleEvent.RaiseActionEnd(firstAction);
        }
    }

    //--------------------------------------------------
    // ActionSlot -> BattleAction
    //--------------------------------------------------

    private BattleAction CreateBattleAction(ActionSlot slot)
    {
        return new BattleAction
        {
            Slot = slot
        };
    }

    //--------------------------------------------------
    // Slot 검증
    //--------------------------------------------------

    private bool IsValidSlot(ActionSlot slot)
    {
        if (slot == null)
            return false;

        if (slot.Owner == null)
            return false;

        if (slot.Part == null)
            return false;

        if (slot.Skill == null)
            return false;

        if (slot.TargetCharacter == null)
            return false;

        if (slot.TargetPart == null)
            return false;

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter.IsDead)
            return false;

        if (slot.Part.IsBroken)
            return false;

        if (slot.TargetPart.IsBroken)
            return false;

        return true;
    }

    // =====================================================
    // Clash
    // =====================================================

    private void ResolveClash(
        BattleAction first,
        BattleAction second)
    {
        battleContext._battleEvent.RaiseClashStart(
            first.Owner,
            second.Owner);

        //------------------------------------
        // 최초 굴림
        //------------------------------------

        RollClashPower(first);
        RollClashPower(second);

        int firstClash =
            CalculateClashPower(first, second);

        int secondClash =
            CalculateClashPower(second, first);

        //------------------------------------
        // 동점이면 재굴림
        //------------------------------------

        while (firstClash == secondClash)
        {
            RollClashPower(first);
            RollClashPower(second);

            firstClash =
                CalculateClashPower(first, second);

            secondClash =
                CalculateClashPower(second, first);
        }

        //------------------------------------
        // 승패 결정
        //------------------------------------

        bool firstWin = firstClash > secondClash;

        BattleAction winner = firstWin ? first : second;
        BattleAction loser = firstWin ? second : first;

        int winnerClash = firstWin ? firstClash : secondClash;
        int loserClash = firstWin ? secondClash : firstClash;
        
        ClashResultContext context = new ClashResultContext
        {
            WinnerAction = winner,
            LoserAction = loser,
            WinnerClashPower = winnerClash,
            LoserClashPower = loserClash,
            Gap = Mathf.Abs(winnerClash - loserClash)
        };

        battleContext._battleEvent.RaiseClashResolved(context);

        //------------------------------------
        // 합 승리 / 패배 이벤트
        //------------------------------------

        battleContext._battleEvent.RaiseClashWin(
            winner,
            loser);

        battleContext._battleEvent.RaiseClashLose(
            loser,
            winner);

        //------------------------------------
        // 기세 / 위세
        //------------------------------------

        int rawPowerGap =
            Mathf.Abs(first.RolledPower - second.RolledPower);

        bool wasOverwhelm =
            momentumManager.IsOverwhelm(winner.Owner);

        int momentumBonus = 0;

        if (winner.Skill != null)
        {
            momentumBonus =
                winner.Skill.GetMomentumPushBonus(
                    winner);
        }

        momentumManager.ApplyClashResult(
            winner.Owner,
            rawPowerGap,
            momentumBonus);

        int prestigeGain = 0;

        if (winner.Skill != null && winner.Skill.GainPrestige)
        {
            prestigeGain =
                momentumManager.CalculatePrestigeGain(
                    rawPowerGap);

            prestigeGain +=
                winner.Skill.GetPrestigeGainBonus(
                    winner);

            if (prestigeGain > 0)
            {
                winner.Owner.AddPrestige(
                    prestigeGain);
            }
        }

        if (!wasOverwhelm &&
            momentumManager.IsOverwhelm(winner.Owner))
        {
            winner.Owner.RuntimeStatus.currentPrestige =
                winner.Owner.CurrentStatus.maxPrestige;
        }

        //------------------------------------
        // 승자 스킬 실행
        //------------------------------------

        ExecuteSkill(winner);

        //------------------------------------
        // 피해 적용
        //------------------------------------

        int beforeHP =
            Mathf.RoundToInt(winner.TargetPart.PartHP);

        int damage =
            damageManager.ApplyDamage(winner);

        int afterHP =
            Mathf.RoundToInt(winner.TargetPart.PartHP);

        //------------------------------------
        // 로그
        //------------------------------------

        battleContext.battleManager.BattleLogger.LogClashResult(
            winner,
            true,
            winnerClash,
            loserClash,
            damage,
            prestigeGain,
            beforeHP,
            afterHP);

        battleContext.battleManager.BattleLogger.LogClashResult(
            loser,
            false,
            loserClash,
            winnerClash);
    }

    //--------------------------------------------------

    private int CalculateClashPower(
        BattleAction self,
        BattleAction opponent)
    {
        return self.RolledPower +
               (self.Speed - opponent.Speed) * SpeedWeight;
    }

    // =====================================================
    // OneSide
    // =====================================================

    private void ResolveOneSide(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Skill == null)
            return;

        //------------------------------------
        // 위력 굴림
        //------------------------------------

        if (!action.HasRolled)
        {
            action.RolledPower = action.RollPower();
            action.finalPower = action.RolledPower;
            action.HasRolled = true;
        }

        //------------------------------------
        // 피해 전 HP
        //------------------------------------

        int beforeHP =
            Mathf.RoundToInt(action.TargetPart.PartHP);

        //------------------------------------
        // 스킬 효과
        //------------------------------------

        ExecuteSkill(action);

        //------------------------------------
        // 피해 적용
        //------------------------------------

        int damage =
            damageManager.ApplyDamage(action);

        //------------------------------------
        // 피해 후 HP
        //------------------------------------

        int afterHP =
            Mathf.RoundToInt(action.TargetPart.PartHP);

        //------------------------------------
        // 로그
        //------------------------------------

        battleContext.battleManager.BattleLogger.LogOneSide(
            action,
            damage,
            beforeHP,
            afterHP);
    }
    
    private void ExecuteSkill(BattleAction action)
    {
        if (action == null)
            return;

        if (action.Skill == null)
            return;

        action.Skill.Execute(action);

        action.Skill.ConsumeResource(
            action.Owner);
    }
    
    private void RollClashPower(BattleAction action)
    {
        if (action == null)
            return;

        action.RolledPower =
            momentumManager.ApplyLastStand(
                action.Owner,
                action.RollPower());

        action.finalPower = action.RolledPower;
        action.HasRolled = true;
    }
}