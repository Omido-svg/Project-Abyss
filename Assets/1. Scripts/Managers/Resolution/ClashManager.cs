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

        do
        {
            RollClashPower(first);
            RollClashPower(second);

            firstClash =
                CalculateClashPower(
                    first,
                    second);

            secondClash =
                CalculateClashPower(
                    second,
                    first);

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
                $"First={first.Owner?.Data?.CharacterName}, " +
                $"FirstRoll={first.LastRollResult?.GetShortDisplayText()}, " +
                $"FirstClash={firstClash}, " +
                $"Second={second.Owner?.Data?.CharacterName}, " +
                $"SecondRoll={second.LastRollResult?.GetShortDisplayText()}, " +
                $"SecondClash={secondClash}");

            steps.Add(
                new ClashRollVisualStep(
                    firstClash,
                    secondClash,
                    first.LastRollResult,
                    second.LastRollResult,
                    firstSpeedModifier,
                    secondSpeedModifier));

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
                    loserClash)
            };

        foreach (ClashRollVisualStep step in steps)
        {
            if (firstWin)
            {
                result.ClashSteps.Add(
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
                result.ClashSteps.Add(
                    new ClashRollVisualStep(
                        step.TargetValue,
                        step.AttackerValue,
                        step.TargetRollResult,
                        step.AttackerRollResult,
                        step.TargetSpeedModifier,
                        step.AttackerSpeedModifier));
            }
        }

        battleContext._battleEvent
            .RaiseClashWin(
                winner,
                loser);

        battleContext._battleEvent
            .RaiseClashLose(
                loser,
                winner);

        int rawPowerGap =
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
            rawPowerGap,
            momentumBonus);

        int prestigeGain =
            ApplyPrestigeGain(
                winner,
                rawPowerGap,
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
        {
            action.RolledPower =
                action.RollPower();

            action.finalPower =
                action.RolledPower;

            action.HasRolled = true;
        }

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
                    action.finalPower,
                LoserClashPower = 0,
                Gap = 0
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

        result.WinnerWasCritical =
            damageContext.WasCritical;

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
        int rawPowerGap,
        bool wasOverwhelm)
    {
        int prestigeGain = 0;

        if (winner.Skill != null &&
            winner.Skill.GainPrestige)
        {
            prestigeGain =
                momentumManager
                    .CalculatePrestigeGain(
                        rawPowerGap);

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
        BattleAction action)
    {
        if (action == null)
            return;

        int rolledPower =
            action.RollPower();

        int afterLastStand =
            momentumManager.ApplyLastStand(
                action.Owner,
                rolledPower);

        if (action.LastRollResult != null &&
            afterLastStand !=
            action.LastRollResult.FinalPower)
        {
            action.LastRollResult
                .ApplyExternalFinalPower(
                    afterLastStand);
        }

        action.RolledPower =
            afterLastStand;

        action.finalPower =
            afterLastStand;

        action.HasRolled = true;
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
