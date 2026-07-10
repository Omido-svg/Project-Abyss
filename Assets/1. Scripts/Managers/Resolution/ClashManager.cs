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

    public List<ClashResultContext> Resolve(
        Queue<ClashPair> clashQueue)
    {
        List<ClashResultContext> results =
            new List<ClashResultContext>();

        if (clashQueue == null)
            return results;

        while (clashQueue.Count > 0)
        {
            ClashPair pair =
                clashQueue.Dequeue();

            ClashResultContext context =
                ResolvePair(pair);

            if (context != null)
                results.Add(context);
        }

        return results;
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

        //--------------------------------
        // 공격자의 사용 부위가 파괴되면 행동 불가
        //--------------------------------
        if (slot.Part.IsBroken)
            return false;

        //--------------------------------
        // 중요:
        // TargetPart.IsBroken은 여기서 막으면 안 됨
        // 대상 부위가 파괴되어 있어도 공격은 가능해야 함
        // 피해 처리에서 직접 피해로 넘긴다
        //--------------------------------

        return true;
    }

    // =====================================================
    // Clash
    // =====================================================

    private ClashResultContext ResolveClash(
        BattleAction first,
        BattleAction second)
    {
        bool canA =
            CanExecuteAction(first);

        bool canB =
            CanExecuteAction(second);

        if (!canA && !canB)
        {
            Debug.Log("[CLASH SKIP] 양쪽 행동 모두 실행 불가");
            return null;
        }

        if (canA && !canB)
        {
            Debug.Log(
                $"[CLASH -> ONESIDE] " +
                $"{GetActionName(first)} 행동만 실행 / " +
                $"{GetActionName(second)} 행동 불가");

            return ResolveOneSide(first);
        }

        if (!canA && canB)
        {
            Debug.Log(
                $"[CLASH -> ONESIDE] " +
                $"{GetActionName(second)} 행동만 실행 / " +
                $"{GetActionName(first)} 행동 불가");

            return ResolveOneSide(second);
        }

        battleContext._battleEvent.RaiseClashStart(
            first.Owner,
            second.Owner);

        //------------------------------------
        // 합 굴림 기록
        //------------------------------------

        List<ClashRollVisualStep> firstSecondSteps =
            new List<ClashRollVisualStep>();

        int firstClash;
        int secondClash;

        do
        {
            RollClashPower(first);
            RollClashPower(second);

            firstClash =
                CalculateClashPower(first, second);

            secondClash =
                CalculateClashPower(second, first);

            int firstSpeedModifier =
                CalculateSpeedModifier(
                    first,
                    second);

            int secondSpeedModifier =
                CalculateSpeedModifier(
                    second,
                    first);
                    
            Debug.Log(
                $"[ClashManager] Clash Step / " +
                $"First={first.Owner?.Data.CharacterName}, " +
                $"FirstRoll={first.LastRollResult?.GetShortDisplayText()}, " +
                $"FirstClash={firstClash}, " +
                $"Second={second.Owner?.Data.CharacterName}, " +
                $"SecondRoll={second.LastRollResult?.GetShortDisplayText()}, " +
                $"SecondClash={secondClash}");

            firstSecondSteps.Add(
                new ClashRollVisualStep(
                    firstClash,
                    secondClash,
                    first.LastRollResult,
                    second.LastRollResult,
                    firstSpeedModifier,
                    secondSpeedModifier));

        } while (firstClash == secondClash);

        //------------------------------------
        // 승패 결정
        //------------------------------------

        bool firstWin =
            firstClash > secondClash;

        BattleAction winner =
            firstWin ? first : second;

        BattleAction loser =
            firstWin ? second : first;

        int winnerClash =
            firstWin ? firstClash : secondClash;

        int loserClash =
            firstWin ? secondClash : firstClash;

        ClashResultContext context =
            new ClashResultContext
            {
                IsClash = true,
                WinnerAction = winner,
                LoserAction = loser,
                WinnerClashPower = winnerClash,
                LoserClashPower = loserClash,
                Gap = Mathf.Abs(winnerClash - loserClash)
            };

        //------------------------------------
        // 연출용 합 기록을 Winner / Loser 기준으로 변환
        //------------------------------------
        foreach (ClashRollVisualStep step in firstSecondSteps)
        {
            if (firstWin)
            {
                context.ClashSteps.Add(
                    new ClashRollVisualStep(
                        step.AttackerValue,
                        step.TargetValue,
                        step.AttackerRollResult,
                        step.TargetRollResult,
                        step.AttackerSpeedModifier,
                        step.TargetSpeedModifier));
            }
            else
            {
                context.ClashSteps.Add(
                    new ClashRollVisualStep(
                        step.TargetValue,
                        step.AttackerValue,
                        step.TargetRollResult,
                        step.AttackerRollResult,
                        step.TargetSpeedModifier,
                        step.AttackerSpeedModifier));
            }
        }

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

        if (winner.Skill != null &&
            winner.Skill.GainPrestige)
        {
            prestigeGain =
                momentumManager.CalculatePrestigeGain(
                    rawPowerGap);

            prestigeGain +=
                winner.Skill.GetPrestigeGainBonus(
                    winner);

            if (prestigeGain > 0)
            {
                AddPrestigeThroughResolver(
                    winner.Owner,
                    winner.Owner,
                    prestigeGain);
            }
        }

        context.PrestigeGain = prestigeGain;

        if (!wasOverwhelm &&
            momentumManager.IsOverwhelm(winner.Owner))
        {
            SetPrestigeToMaxThroughResolver(
                winner.Owner,
                winner.Owner);
        }

        //------------------------------------
        // 승자 스킬 실행
        //------------------------------------

        ExecuteSkill(winner);

        //------------------------------------
        // 피해 적용
        //------------------------------------

        bool targetPartWasBrokenBeforeDamage =
            winner.TargetPart != null &&
            winner.TargetPart.IsBroken;

        int beforeHP = 0;

        if (winner.TargetPart != null)
        {
            beforeHP =
                Mathf.RoundToInt(
                    winner.TargetPart.PartHP);
        }

        int damage =
            damageManager.ApplyDamage(winner);

        int afterHP = 0;

        if (winner.TargetPart != null)
        {
            afterHP =
                Mathf.RoundToInt(
                    winner.TargetPart.PartHP);
        }

        context.HitDamages.Add(damage);

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
            afterHP,
            targetPartWasBrokenBeforeDamage);

        battleContext.battleManager.BattleLogger.LogClashResult(
            loser,
            false,
            loserClash,
            winnerClash);

        return context;
    }
        
    private string GetActionName(BattleAction action)
    {
        if (action == null)
            return "NULL";

        string ownerName =
            action.Owner != null
                ? action.Owner.Data.CharacterName
                : "NULL_OWNER";

        string partName =
            action.OwnerPart != null
                ? action.OwnerPart.Type.ToString()
                : "NULL_PART";

        string skillName =
            action.Skill != null
                ? action.Skill.SkillName
                : "NULL_SKILL";

        return $"{ownerName} {partName} / {skillName}";
    }

    //--------------------------------------------------

    private int CalculateClashPower(
        BattleAction self,
        BattleAction opponent)
    {
        if (self == null)
            return 0;

        return
            self.RolledPower +
            CalculateSpeedModifier(
                self,
                opponent);
    }
    
    private int CalculateSpeedModifier(
        BattleAction self,
        BattleAction opponent)
    {
        if (self == null || opponent == null)
            return 0;

        return
            (self.Speed - opponent.Speed) *
            SpeedWeight;
    }

    // =====================================================
    // OneSide
    // =====================================================

    private ClashResultContext ResolveOneSide(BattleAction action)
    {
        if (!CanExecuteAction(action))
        {
            Debug.Log(
                $"[ONESIDE SKIP] {GetActionName(action)} 실행 불가");

            return null;
        }

        if (action == null)
            return null;

        if (action.Skill == null)
            return null;

        //------------------------------------
        // 위력 굴림
        //------------------------------------

        if (!action.HasRolled)
        {
            action.RolledPower =
                action.RollPower();

            action.finalPower =
                action.RolledPower;

            action.HasRolled =
                true;
        }

        //------------------------------------
        // 스킬 효과
        //------------------------------------

        ExecuteSkill(action);

        //------------------------------------
        // 피해 적용 직전 상태 저장
        //------------------------------------

        bool targetPartWasBrokenBeforeDamage =
            action.TargetPart != null &&
            action.TargetPart.IsBroken;

        int beforeHP = 0;

        if (action.TargetPart != null)
        {
            beforeHP =
                Mathf.RoundToInt(
                    action.TargetPart.PartHP);
        }

        //------------------------------------
        // 피해 적용
        //------------------------------------

        int damage =
            damageManager.ApplyDamage(action);

        int afterHP = 0;

        if (action.TargetPart != null)
        {
            afterHP =
                Mathf.RoundToInt(
                    action.TargetPart.PartHP);
        }

        //------------------------------------
        // 로그
        //------------------------------------

        battleContext.battleManager.BattleLogger.LogOneSideResult(
            action,
            damage,
            beforeHP,
            afterHP,
            targetPartWasBrokenBeforeDamage);

        //------------------------------------
        // 연출용 결과 반환
        //------------------------------------

        ClashResultContext context =
            new ClashResultContext
            {
                IsClash = false,
                WinnerAction = action,
                WinnerClashPower = action.finalPower,
                LoserClashPower = 0,
                Gap = 0
            };

        context.HitDamages.Add(damage);

        return context;
    }
    
    private void ExecuteSkill(BattleAction action)
    {
        if (!CanExecuteAction(action))
            return;
        
        if (action == null)
            return;

        if (action.Skill == null)
            return;

        action.Skill.Execute(action);

        action.Skill.ConsumeResource(action.Owner);
    }
    
    private bool CanExecuteAction(BattleAction action)
    {
        if (action == null)
            return false;

        if (action.Owner == null)
            return false;

        if (action.Owner.IsDead)
            return false;

        if (action.OwnerPart == null)
            return false;

        if (action.Skill == null)
            return false;

        if (action.Target == null)
            return false;

        if (action.TargetPart == null)
            return false;

        if (action.OwnerPart.IsBroken)
        {
            Debug.Log(
                $"[ACTION INVALID - BROKEN OWNER PART] " +
                $"{action.Owner.Data.CharacterName} / " +
                $"{action.OwnerPart.Type} / " +
                $"{action.Skill?.SkillName}");

            return false;
        }

        return true;
    }
    
    private void RollClashPower(BattleAction action)
    {
        if (action == null)
            return;

        int rolledPowerBeforeLastStand =
            action.RollPower();

        int rolledPowerAfterLastStand =
            momentumManager.ApplyLastStand(
                action.Owner,
                rolledPowerBeforeLastStand);

        if (action.LastRollResult != null &&
            rolledPowerAfterLastStand != action.LastRollResult.FinalPower)
        {
            action.LastRollResult.ApplyExternalFinalPower(
                rolledPowerAfterLastStand);
        }

        action.RolledPower =
            rolledPowerAfterLastStand;

        action.finalPower =
            action.RolledPower;

        action.HasRolled =
            true;
    }
    
    private void AddPrestigeThroughResolver(
        Character source,
        Character target,
        int amount)
    {
        if (source == null)
            return;

        if (target == null)
            return;

        if (amount <= 0)
            return;

        BattleEffectResolver resolver =
            battleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.AddPrestige(
            EffectRequest.Prestige(
                source,
                target,
                amount));
    }

    private void SetPrestigeToMaxThroughResolver(
        Character source,
        Character target)
    {
        if (source == null)
            return;

        if (target == null)
            return;

        BattleEffectResolver resolver =
            battleContext?.EffectResolver;

        if (resolver == null)
            return;

        resolver.SetPrestigeToMax(
            EffectRequest.PrestigeToMax(
                source,
                target));
    }
    
    public ClashResultContext ResolvePair(
        ClashPair pair)
    {
        if (pair == null)
            return null;

        ActionSlot firstSlot =
            pair.First;

        ActionSlot secondSlot =
            pair.Second;

        BattleAction firstAction =
            IsValidSlot(firstSlot)
                ? CreateBattleAction(firstSlot)
                : null;

        BattleAction secondAction =
            IsValidSlot(secondSlot)
                ? CreateBattleAction(secondSlot)
                : null;

        if (pair.IsClash)
        {
            return ResolveClash(
                firstAction,
                secondAction);
        }

        return ResolveOneSide(
            firstAction);
    }
}