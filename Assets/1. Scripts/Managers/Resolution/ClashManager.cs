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

    public List<ClashResultContext> Resolve(
        Queue<ClashPair> clashQueue)
    {
        List<ClashResultContext> results =
            new();

        if (clashQueue == null)
            return results;

        while (clashQueue.Count > 0)
        {
            ClashResultContext context =
                ResolvePair(
                    clashQueue.Dequeue());

            if (context != null)
                results.Add(context);
        }

        return results;
    }

    public ClashResultContext ResolvePair(
        ClashPair pair)
    {
        if (pair == null)
            return null;

        BattleAction firstAction =
            CreateValidBattleAction(
                pair.First);

        BattleAction secondAction =
            CreateValidBattleAction(
                pair.Second);

        bool firstStarted = false;
        bool secondStarted = false;

        try
        {
            if (CanExecuteAction(firstAction))
            {
                battleContext._battleEvent
                    .RaiseActionStart(firstAction);

                firstStarted = true;
            }

            if (CanExecuteAction(secondAction))
            {
                battleContext._battleEvent
                    .RaiseActionStart(secondAction);

                secondStarted = true;
            }

            if (pair.IsClash)
            {
                return ResolveClash(
                    firstAction,
                    secondAction);
            }

            return ResolveOneSide(
                firstAction);
        }
        finally
        {
            if (secondStarted)
            {
                battleContext._battleEvent
                    .RaiseActionEnd(secondAction);
            }

            if (firstStarted)
            {
                battleContext._battleEvent
                    .RaiseActionEnd(firstAction);
            }
        }
    }

    private BattleAction CreateValidBattleAction(
        ActionSlot slot)
    {
        if (!IsValidSlot(slot))
            return null;

        return new BattleAction
        {
            Slot = slot
        };
    }

    private bool IsValidSlot(
        ActionSlot slot)
    {
        if (slot == null ||
            slot.Owner == null ||
            slot.Skill == null)
        {
            return false;
        }

        if (slot.Owner.IsDead)
            return false;

        if (slot.TargetCharacter != null &&
            slot.TargetCharacter.IsDead)
        {
            return false;
        }

        if (slot.Part != null &&
            slot.Part.IsBroken)
        {
            return false;
        }

        // OwnerPart == null:
        // 독립 위세와 이후 일반몹 행동을 위해 허용한다.
        //
        // TargetPart == null:
        // 단일 HP 대상을 위해 허용한다.
        //
        // TargetPart.IsBroken:
        // 파괴 부위 재공격 규칙 때문에 허용한다.
        return true;
    }

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
            Debug.Log(
                "[CLASH SKIP] 양쪽 행동 모두 실행 불가");

            return null;
        }

        if (canA && !canB)
        {
            Debug.Log(
                $"[CLASH -> ONESIDE] " +
                $"{GetActionName(first)} 행동만 실행");

            return ResolveOneSide(first);
        }

        if (!canA && canB)
        {
            Debug.Log(
                $"[CLASH -> ONESIDE] " +
                $"{GetActionName(second)} 행동만 실행");

            return ResolveOneSide(second);
        }

        battleContext._battleEvent
            .RaiseClashStart(
                first.Owner,
                second.Owner);

        List<ClashRollVisualStep> steps =
            new();

        int firstClash;
        int secondClash;
        int roundIndex = 0;

        do
        {
            roundIndex++;

            RollClashPower(
                first,
                second,
                roundIndex > 1);

            RollClashPower(
                second,
                first,
                roundIndex > 1);

            firstClash =
                CalculateClashPower(first);

            secondClash =
                CalculateClashPower(second);

            Debug.Log(
                $"[ClashManager] Clash Step / " +
                $"Round={roundIndex}, " +
                $"First={first.Owner?.Data?.CharacterName}, " +
                $"FirstRoll={first.LastRollResult?.GetShortDisplayText()}, " +
                $"FirstFinal={first.RolledPower}, " +
                $"FirstSpeed={first.SpeedModifier}, " +
                $"FirstMomentum={first.MomentumModifier}, " +
                $"FirstClash={firstClash}, " +
                $"FirstCritical={first.Critical}, " +
                $"Second={second.Owner?.Data?.CharacterName}, " +
                $"SecondRoll={second.LastRollResult?.GetShortDisplayText()}, " +
                $"SecondFinal={second.RolledPower}, " +
                $"SecondSpeed={second.SpeedModifier}, " +
                $"SecondMomentum={second.MomentumModifier}, " +
                $"SecondClash={secondClash}, " +
                $"SecondCritical={second.Critical}");

            steps.Add(
                new ClashRollVisualStep(
                    roundIndex,
                    firstClash,
                    secondClash,
                    first.LastRollResult,
                    second.LastRollResult,
                    first.SpeedModifier,
                    second.SpeedModifier,
                    first.MomentumModifier,
                    second.MomentumModifier,
                    first.Critical,
                    second.Critical));

        } while (firstClash == secondClash);

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

        ClashResultContext result =
            new()
            {
                IsClash = true,
                WinnerAction = winner,
                LoserAction = loser,
                WinnerClashPower = winnerClash,
                LoserClashPower = loserClash,
                Gap = Mathf.Abs(
                    winnerClash -
                    loserClash),
                WinnerWasCritical = winner.Critical,
                LoserWasCritical = loser.Critical
            };

        foreach (ClashRollVisualStep step in steps)
        {
            result.ClashSteps.Add(
                firstWin
                    ? step
                    : step.Swapped());
        }

        battleContext._battleEvent
            .RaiseClashWin(
                winner,
                loser);

        battleContext._battleEvent
            .RaiseClashLose(
                loser,
                winner);

        // 속도와 합 전용 기세 보정은 기세 이동량/위세 획득량에도
        // 직접 섞지 않는다. 기존 밸런스를 유지하며 순수 위력 차이만 사용한다.
        int purePowerGap =
            Mathf.Abs(
                first.RolledPower -
                second.RolledPower);

        bool wasOverwhelm =
            momentumManager.IsOverwhelm(
                winner.Owner);

        int momentumBonus =
            winner.Skill == null
                ? 0
                : winner.Skill
                    .GetMomentumPushBonus(
                        winner);

        momentumManager.ApplyClashResult(
            winner.Owner,
            purePowerGap,
            momentumBonus);

        int prestigeGain =
            ApplyPrestigeGain(
                winner,
                purePowerGap,
                wasOverwhelm);

        result.PrestigeGain =
            prestigeGain;

        ExecuteSkill(winner);

        DamageContext damageContext =
            damageManager.ApplyDamageContext(
                winner,
                isClashDamage: true,
                targetLostClash: true);

        winner.SetDamageContext(
            damageContext);

        FillDamageResult(
            result,
            damageContext);

        battleContext._battleEvent
            .RaiseClashResolved(result);

        int beforeHP =
            damageContext?.GetPrimaryHpBefore() ?? 0;

        int afterHP =
            damageContext?.GetPrimaryHpAfter() ?? 0;

        int damage =
            damageContext?.GetDisplayDamage() ?? 0;

        bool targetPartWasBrokenBeforeDamage =
            damageContext != null &&
            damageContext.HasTargetPartSnapshot &&
            damageContext.TargetPartStateBefore ==
                BodyPartState.Broken;

        battleContext.battleManager
            .BattleLogger
            .LogClashResult(
                winner,
                true,
                winnerClash,
                loserClash,
                damage,
                prestigeGain,
                beforeHP,
                afterHP,
                targetPartWasBrokenBeforeDamage);

        battleContext.battleManager
            .BattleLogger
            .LogClashResult(
                loser,
                false,
                loserClash,
                winnerClash);

        return result;
    }

    private ClashResultContext ResolveOneSide(
        BattleAction action)
    {
        if (!CanExecuteAction(action))
        {
            Debug.Log(
                $"[ONESIDE SKIP] " +
                $"{GetActionName(action)} 실행 불가");

            return null;
        }

        if (!action.HasRolled)
            action.RollPower();

        // 일방 공격에는 합 전용 속도/기세 보정을 적용하지 않는다.
        action.ClearClashModifiers();

        ExecuteSkill(action);

        DamageContext damageContext =
            damageManager.ApplyDamageContext(
                action,
                isClashDamage: false,
                targetLostClash: false);

        action.SetDamageContext(
            damageContext);

        int damage =
            damageContext?.GetDisplayDamage() ?? 0;

        int beforeHP =
            damageContext?.GetPrimaryHpBefore() ?? 0;

        int afterHP =
            damageContext?.GetPrimaryHpAfter() ?? 0;

        bool targetPartWasBrokenBeforeDamage =
            damageContext != null &&
            damageContext.HasTargetPartSnapshot &&
            damageContext.TargetPartStateBefore ==
                BodyPartState.Broken;

        battleContext.battleManager
            .BattleLogger
            .LogOneSideResult(
                action,
                damage,
                beforeHP,
                afterHP,
                targetPartWasBrokenBeforeDamage);

        ClashResultContext result =
            new()
            {
                IsClash = false,
                WinnerAction = action,
                WinnerClashPower =
                    action.RolledPower,
                LoserClashPower = 0,
                Gap = 0,
                WinnerWasCritical = action.Critical,
                LoserWasCritical = false
            };

        FillDamageResult(
            result,
            damageContext);

        return result;
    }

    private void FillDamageResult(
        ClashResultContext result,
        DamageContext damageContext)
    {
        if (result == null)
            return;

        result.DamageContext =
            damageContext;

        result.DamageResult =
            damageContext?.Result;

        result.DamageEventResult =
            damageContext?.EventResult;

        int displayDamage =
            damageContext?.GetDisplayDamage() ?? 0;

        if (displayDamage > 0)
        {
            result.HitDamages.Add(
                displayDamage);
        }

        if (damageContext == null)
            return;

        result.FinalHpDamage =
            damageContext.FinalHpDamage;

        result.PartHpDamage =
            damageContext.PartHpDamage;

        result.DirectHpDamage =
            damageContext.DirectHpDamage;

        result.WasCritical =
            damageContext.WasCritical;

        result.WasKilled =
            damageContext.WasKilled;

        result.BrokePart =
            damageContext.BrokePart;

        result.WeakenedPart =
            damageContext.WeakenedPart;

        result.HasTargetCharacterHpSnapshot = true;
        result.TargetCharacterHpBefore =
            damageContext.TargetHpBefore;
        result.TargetCharacterHpAfter =
            damageContext.TargetHpAfter;

        if (!damageContext.HasTargetPartSnapshot)
            return;

        result.HasTargetPartHpSnapshot = true;
        result.TargetPartHpBefore =
            damageContext.TargetPartHpBefore;
        result.TargetPartHpAfter =
            damageContext.TargetPartHpAfter;
    }

    private int ApplyPrestigeGain(
        BattleAction winner,
        int purePowerGap,
        bool wasOverwhelm)
    {
        int prestigeGain = 0;

        if (winner.Skill != null &&
            winner.Skill.GainPrestige)
        {
            prestigeGain =
                momentumManager
                    .CalculatePrestigeGain(
                        purePowerGap);

            prestigeGain +=
                winner.Skill
                    .GetPrestigeGainBonus(
                        winner);

            if (prestigeGain > 0)
            {
                AddPrestigeThroughResolver(
                    winner.Owner,
                    winner.Owner,
                    prestigeGain);
            }
        }

        if (!wasOverwhelm &&
            momentumManager.IsOverwhelm(
                winner.Owner))
        {
            SetPrestigeToMaxThroughResolver(
                winner.Owner,
                winner.Owner);
        }

        return prestigeGain;
    }

    private int CalculateClashPower(
        BattleAction action)
    {
        return action?.ClashPower ?? 0;
    }

    private int CalculateSpeedModifier(
        BattleAction self,
        BattleAction opponent)
    {
        if (self == null ||
            opponent == null)
        {
            return 0;
        }

        return
            (self.Speed -
             opponent.Speed) *
            SpeedWeight;
    }

    private void ExecuteSkill(
        BattleAction action)
    {
        if (!CanExecuteAction(action) ||
            action.Skill == null)
        {
            return;
        }

        action.Skill.Execute(action);

        action.Skill.ConsumeResource(
            action.Owner);
    }

    private bool CanExecuteAction(
        BattleAction action)
    {
        if (action == null ||
            action.Owner == null ||
            action.Skill == null)
        {
            return false;
        }

        if (action.Owner.IsDead)
            return false;

        if (action.Target != null &&
            action.Target.IsDead)
        {
            return false;
        }

        if (action.OwnerPart != null &&
            action.OwnerPart.IsBroken)
        {
            Debug.Log(
                $"[ACTION INVALID - BROKEN OWNER PART] " +
                $"ActionId={action.ActionId}, " +
                $"{action.Owner.Data?.CharacterName} / " +
                $"{action.OwnerPart.Type} / " +
                $"{action.Skill?.SkillName}");

            return false;
        }

        return true;
    }

    private void RollClashPower(
        BattleAction action,
        BattleAction opponent,
        bool wasRerolled)
    {
        if (action == null)
            return;

        int finalPower =
            action.RollPower();

        if (action.LastRollResult != null)
            action.LastRollResult.WasRerolled = wasRerolled;

        int afterMomentum =
            momentumManager.ApplyLastStand(
                action.Owner,
                finalPower);

        int momentumModifier =
            afterMomentum - finalPower;

        int speedModifier =
            CalculateSpeedModifier(
                action,
                opponent);

        action.ApplyClashModifiers(
            speedModifier,
            momentumModifier);
    }

    private void AddPrestigeThroughResolver(
        Character source,
        Character target,
        int amount)
    {
        if (source == null ||
            target == null ||
            amount <= 0)
        {
            return;
        }

        battleContext?.EffectResolver?.AddPrestige(
                EffectRequest.Prestige(
                    source,
                    target,
                    amount));
    }

    private void SetPrestigeToMaxThroughResolver(
        Character source,
        Character target)
    {
        if (source == null ||
            target == null)
        {
            return;
        }

        battleContext?.EffectResolver?.SetPrestigeToMax(
                EffectRequest.PrestigeToMax(
                    source,
                    target));
    }

    private string GetActionName(
        BattleAction action)
    {
        if (action == null)
            return "NULL";

        string ownerName =
            action.Owner?.Data?.CharacterName ??
            "NULL_OWNER";

        string partName =
            action.OwnerPart == null
                ? "NONE"
                : action.OwnerPart.Type.ToString();

        string skillName =
            action.Skill?.SkillName ??
            "NULL_SKILL";

        return
            $"Id={action.ActionId}, " +
            $"Index={action.ActionIndex}, " +
            $"{ownerName} {partName} / {skillName}";
    }
}
