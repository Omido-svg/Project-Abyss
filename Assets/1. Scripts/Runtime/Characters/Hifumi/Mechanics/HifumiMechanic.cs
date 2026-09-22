using System.Collections.Generic;
using UnityEngine;

public readonly struct HifumiCounterPreview
{
    public HifumiCounterPreview(
        bool enabled,
        int counterCount,
        int counterPower,
        int boneGain,
        int heal,
        bool consumeAllBone,
        bool weaken,
        bool breakPart,
        int momentumPush)
    {
        Enabled = enabled;
        CounterCount = counterCount;
        CounterPower = counterPower;
        BoneGain = boneGain;
        Heal = heal;
        ConsumeAllBone = consumeAllBone;
        Weaken = weaken;
        BreakPart = breakPart;
        MomentumPush = momentumPush;
    }

    public bool Enabled { get; }
    public int CounterCount { get; }
    public int CounterPower { get; }
    public int BoneGain { get; }
    public int Heal { get; }
    public bool ConsumeAllBone { get; }
    public bool Weaken { get; }
    public bool BreakPart { get; }
    public int MomentumPush { get; }
}

/// <summary>
/// Phase D-3 Hifumi core runtime.
/// 0916 Source of Truth:
/// - Bone 0..500 / tier 0..5 / bloom=500
/// - FinalHpDamage 1:1 bone gain
/// - Duel/Normal counter gates split
/// - Canonical counter Chinchiro resolver
/// - 1·2·3 catastrophe = forced loss + self 60 + counter +1
/// - Bold Judgment OnUse + OnClashEnd
/// - Next-turn non-stacking Duel governor (amount is data/Unset)
/// </summary>
public sealed class HifumiMechanic :
    CombatMechanic,
    ICharacterUniqueGaugeProvider,
    IChinchiroOutcomeOverride,
    IFervorTurnEndGainModifier,
    IForcedRollFailureRule,
    IForcedRollJudgmentRule,
    IExchangeContinuationRule
{
    public const int MaxBone = 500;
    public const int BonePerTier = 100;
    public const int MaxBoneTier = 5;
    public const int BloomThreshold = 500;
    public const int HifumiSelfDamage = 60;

    private const int DuelCounterBasePower = 8;
    private const int NormalCounterBasePower = 4;
    private const int HifumiBasePowerBonus = 1;
    private const int CounterSafetyLimit = 64;

    private int bone;
    // 0922 §19.1: 가불은 실제 뼈와 완전히 분리된 턴 한정 자원이다.
    // BoneBand에는 들어가지 않고, 비용/보유 조건에는 먼저 사용하며, 만개 조건에는 합산한다.
    private int advanceBone;

    private readonly HifumiEngraveRunProgression localEngraveProgress = new();

    // E 판돈을 올리다 — 전투 동안 영구 누적, 전투 종료 시 Mechanic 수명과 함께 초기화.
    private int raiseStakePowerBonus;
    private int raiseStakeEnergyIncrease;

    private bool pokerFaceActive;
    private bool engraveBoneActive;
    private bool nextTurnSpeedPenalty;
    private bool boldJudgmentTracking;
    private int boldJudgmentHpDamageTaken;
    private bool engraveCompleteBlockTracking;
    private int engraveHpDamageTaken;

    // [0917_CONFIRMED_GAP:HIFUMI_C_FIELDS]
    // 무모한 베팅(C): 뼈<70 사용 1회당 다음 턴 받는 HP 피해 +3.
    // 같은 턴 여러 번 발동하면 공통 상태 문법처럼 합산한다.
    private int recklessBetIncomingDamagePenaltyQueued;
    private int recklessBetIncomingDamagePenaltyActive;

    private bool trickActive;
    private bool forceTrickFailure;

    private bool governorQueued;
    private int governorActivePenalty;
    private int governorVerificationPenalty = -1;

    private readonly Dictionary<long, int>
        catastropheCounterBonusByAction = new();

    // J 굴림 도감: 같은 합에서 나온 대실패 하나당 그 합의 모든 반격 굴림 위력 +1.
    private readonly Dictionary<long, int>
        counterPowerBonusByAction = new();

    private readonly HashSet<string>
        processedCatastrophes = new();

    public int Bone => bone;
    public int AdvanceBone => advanceBone;
    public int SpendableBone => Mathf.Max(0, bone) + Mathf.Max(0, advanceBone);
    public int BoneBand => Mathf.Clamp(bone / BonePerTier, 0, MaxBoneTier);
    public int BoneTier => BoneBand;
    public bool IsBloom => SpendableBone >= BloomThreshold;

    private HifumiEngraveRunProgression EngraveProgress =>
        battleContext?.RunProgression?.HifumiEngrave ??
        localEngraveProgress;

    public int EngraveStage => EngraveProgress?.Stage ?? 1;
    public HifumiEngraveBranch EngraveBranch =>
        EngraveProgress?.Branch ?? HifumiEngraveBranch.None;
    public int EngraveRollCount => Mathf.Clamp(EngraveStage, 1, 4);
    public int EngraveEnergyCost => EngraveStage >= 3 ? 1 : 0;
    public bool EngraveCounterEnabled =>
        EngraveStage <= 1 || EngraveBranch != HifumiEngraveBranch.Victory;

    public int RaiseStakePowerBonus => raiseStakePowerBonus;
    public int RaiseStakeEnergyIncrease => raiseStakeEnergyIncrease;
    public bool RaiseStakeLocked => raiseStakeEnergyIncrease >= 4;

    public bool GovernorQueued => governorQueued;
    public int ActiveGovernorPenalty => governorActivePenalty;
    public bool NextTurnSpeedPenaltyQueued => nextTurnSpeedPenalty;

    public string GaugeLabel => "뼈";
    public float GaugeNormalized => (float)bone / MaxBone;
    public string GaugeValueText =>
        $"{bone}/{MaxBone}" +
        (advanceBone > 0 ? $" · 가불 {advanceBone}" : string.Empty) +
        (IsBloom ? " · 만개" : string.Empty);
    public int GaugeStateVersion =>
        bone ^ (advanceBone << 10) ^ (EngraveStage << 24);

    public override string MechanicName =>
        "Hifumi Bone / Chinchiro / Counter";

    public override void OnRegister()
    {
        SubscribeToBattleEvent(
            () => battleEvent.OnTurnStart += OnTurnStart,
            () => battleEvent.OnTurnStart -= OnTurnStart,
            "OnTurnStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnTurnEnd += OnTurnEnd,
            () => battleEvent.OnTurnEnd -= OnTurnEnd,
            "OnTurnEnd");

        SubscribeToBattleEvent(
            () => battleEvent.OnActionStart += OnActionStart,
            () => battleEvent.OnActionStart -= OnActionStart,
            "OnActionStart");

        SubscribeToBattleEvent(
            () => battleEvent.OnExchangeResolved += OnExchangeResolved,
            () => battleEvent.OnExchangeResolved -= OnExchangeResolved,
            "OnExchangeResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnClashResolved += OnClashResolved,
            () => battleEvent.OnClashResolved -= OnClashResolved,
            "OnClashResolved");

        SubscribeToBattleEvent(
            () => battleEvent.OnDamageEventResolved += OnDamageResolved,
            () => battleEvent.OnDamageEventResolved -= OnDamageResolved,
            "OnDamageEventResolved");
    }

    public override void OnUnregister()
    {
        bone = 0;
        advanceBone = 0;
        raiseStakePowerBonus = 0;
        raiseStakeEnergyIncrease = 0;
        localEngraveProgress.Reset();
        pokerFaceActive = false;
        engraveBoneActive = false;
        nextTurnSpeedPenalty = false;
        boldJudgmentTracking = false;
        boldJudgmentHpDamageTaken = 0;
        engraveCompleteBlockTracking = false;
        engraveHpDamageTaken = 0;
        recklessBetIncomingDamagePenaltyQueued = 0;
        recklessBetIncomingDamagePenaltyActive = 0;
        trickActive = false; // [0917_CONFIRMED_GAP:HIFUMI_C_RESET]
        forceTrickFailure = false;
        governorQueued = false;
        governorActivePenalty = 0;
        governorVerificationPenalty = -1;
        catastropheCounterBonusByAction.Clear();
        counterPowerBonusByAction.Clear();
        processedCatastrophes.Clear();
    }

    /// <summary>
    /// Hifumi pure power is resolved in HifumiRuntimeSkills so it affects both
    /// judgment and damage. This hook intentionally does not add the old +1 or
    /// Goldan +tier*4 judgment-only modifier.
    /// </summary>
    public override int ModifyRoll(
        BattleAction action,
        int roll) => roll;

    public int ResolveDuelBasePowerAdjustment(
        string skillId)
    {
        int adjustment = -Mathf.Max(0, governorActivePenalty);

        if (skillId == HifumiSkillIds.Goldan)
            adjustment -= BoneBand * 4;

        if (skillId == HifumiSkillIds.RaiseStake)
            adjustment += Mathf.Max(0, raiseStakePowerBonus);

        return adjustment;
    }

    public override int ModifyDamageTaken(
        DamageContext context,
        int damage)
    {
        if (context == null ||
            context.Target != owner ||
            damage <= 0)
        {
            return damage;
        }

        // [0917_CONFIRMED_GAP:HIFUMI_C_DAMAGE]
        // 두 효과는 같은 flat HP damage 축이므로 먼저 대수합한 뒤 한 번만 clamp한다.
        // 무모한 베팅의 +3은 '교환당' 규칙이므로 Action 기반 피해에만 적용한다.
        int flatModifier = 0;
        if (context.Action != null)
        {
            if (pokerFaceActive)
                flatModifier -= 4;

            flatModifier +=
                Mathf.Max(0, recklessBetIncomingDamagePenaltyActive);
        }

        return Mathf.Max(0, damage + flatModifier);
    }

    public int ModifyTurnEndFervorGain(
        MomentumState finalState,
        int currentGain)
    {
        if (finalState == MomentumState.Disadvantage ||
            finalState == MomentumState.LastStand)
        {
            return Mathf.Max(currentGain, 3);
        }

        return currentGain;
    }

    bool IForcedRollFailureRule.IsForcedRollFailure(
        BattleAction action,
        RollResult rollResult,
        out string reason)
    {
        reason = string.Empty;

        if (action?.Owner != owner ||
            rollResult == null ||
            rollResult.ResolverType != SkillResolverType.Chinchiro ||
            rollResult.ChinchiroCombination != ChinchiroCombination.Hifumi)
        {
            return false;
        }

        // 0922 H 운명을 흔들다는 1·2·3을 공통 대실패로 처리하지 않는다.
        // 판정값 0의 일반 비교만 수행한다.
        if (action.Skill?.Definition?.SkillId == HifumiSkillIds.ShakeFate)
            return false;

        reason = "Hifumi Chinchiro 1·2·3 catastrophe";
        return true;
    }

    bool IForcedRollJudgmentRule.TryGetForcedRollJudgment(
        BattleAction action,
        RollResult rollResult,
        out ForcedRollJudgmentDirective directive,
        out string reason)
    {
        directive = ForcedRollJudgmentDirective.None;
        reason = string.Empty;

        if (action?.Owner != owner ||
            action.Skill?.Definition?.SkillId != HifumiSkillIds.ShakeFate ||
            rollResult?.ResolverType != SkillResolverType.Chinchiro)
        {
            return false;
        }

        if (rollResult.ChinchiroCombination == ChinchiroCombination.Arashi ||
            rollResult.ChinchiroCombination == ChinchiroCombination.Shigoro)
        {
            directive = ForcedRollJudgmentDirective.ForceWin;
            reason = "0922 H 운명을 흔들다: Arashi/Shigoro auto-win";
            return true;
        }

        // Moku/Blank/1·2·3은 정상 비교.
        return false;
    }

    int IExchangeContinuationRule.ModifyOpponentRemainingRollCount(
        BattleAction winnerAction,
        BattleAction opponentAction,
        int currentRemainingRollCount)
    {
        if (winnerAction?.Owner != owner ||
            winnerAction.Skill?.Definition?.SkillId != HifumiSkillIds.ShakeFate ||
            winnerAction.LastRollResult?.ChinchiroCombination != ChinchiroCombination.Arashi)
        {
            return currentRemainingRollCount;
        }

        // 0922 H: Arashi 자동 승리 뒤 상대의 다음 굴림 하나를 무효화.
        return Mathf.Max(0, currentRemainingRollCount - 1);
    }

    public override bool CanUseSkill(BodyPart part, Skill skill)
    {
        if (skill == null)
            return false;

        string id = skill.Definition?.SkillId;

        // 0922 K/L/M은 슬롯 구조는 확정됐지만 수치 핵심이 아직 (미정).
        // default Duel cost/base로 조용히 플레이되는 것을 막는다.
        if (HifumiSkillIds.HasPendingCanonicalRuntimeNumbers(id))
            return false;

        if (id == HifumiSkillIds.RaiseStake && RaiseStakeLocked)
            return false;

        return true;
    }

    public void ExecuteSkill(BattleAction action)
    {
        if (action?.Owner != owner)
            return;

        ExecuteSkillId(
            action.Skill?.Definition?.SkillId,
            action);
    }

    public void ExecuteSkillForVerification(string skillId)
    {
        ExecuteSkillId(skillId, null);
    }

    private void ExecuteSkillId(
        string id,
        BattleAction action)
    {
        if (id == HifumiSkillIds.PokerFace)
        {
            bool before = pokerFaceActive;
            pokerFaceActive = true;

            action?.Slot?.PlanningUndo?.Record(
                () => pokerFaceActive = before);
        }
        else if (id == HifumiSkillIds.EngraveBone)
        {
            bool before = engraveBoneActive;
            engraveBoneActive = true;

            action?.Slot?.PlanningUndo?.Record(
                () => engraveBoneActive = before);
        }
        else if (id == HifumiSkillIds.GiveFlesh)
        {
            int boneBefore = bone;

            DeferredStatusEffect deferredBefore =
                FindDeferredStatus(StatusEffectId.Rupture);

            int deferredStackBefore =
                deferredBefore?.Stack ?? 0;

            int deferredDurationBefore =
                deferredBefore?.PendingDuration ?? 0;

            AddBone(100);
            QueueNextTurnRupture();

            action?.Slot?.PlanningUndo?.Record(
                () =>
                {
                    bone = boneBefore;

                    DeferredStatusEffect current =
                        FindDeferredStatus(StatusEffectId.Rupture);

                    if (deferredStackBefore <= 0)
                    {
                        if (current != null)
                            owner?.RemoveStatus(current);
                    }
                    else if (current != null)
                    {
                        current.RestorePendingStateForPlanning(
                            deferredStackBefore,
                            deferredDurationBefore);
                    }
                });
        }
        else if (id == HifumiSkillIds.FoldHand)
        {
            if (SpendableBone >= 100)
            {
                int boneBefore = bone;
                int advanceBefore = advanceBone;
                int hpBefore = owner?.CurrentHP ?? 0;

                List<BodyPart> parts = new();
                List<float> partHp = new();
                List<float> partMaxHp = new();
                List<BodyPartState> partStates = new();

                if (owner?.BodyParts != null)
                {
                    foreach (BodyPart part in owner.BodyParts)
                    {
                        if (part == null)
                            continue;

                        parts.Add(part);
                        partHp.Add(part.PartHP);
                        partMaxHp.Add(part.MaxPartHP);
                        partStates.Add(part.State);
                    }
                }

                ConsumeBone(100);
                owner?.RestoreCurrentHP(70);

                action?.Slot?.PlanningUndo?.Record(
                    () =>
                    {
                        bone = boneBefore;
                        advanceBone = advanceBefore;

                        if (owner?.RuntimeStatus != null)
                        {
                            owner.RuntimeStatus.currentHP =
                                Mathf.Clamp(
                                    hpBefore,
                                    0,
                                    owner.MaxCombatHP);
                        }

                        for (int i = 0; i < parts.Count; i++)
                        {
                            owner?.SetBodyPartStateForDebug(
                                parts[i],
                                partHp[i],
                                partMaxHp[i],
                                partStates[i],
                                clearNonStructuralStatuses: false);
                        }
                    });
            }
        }
        else if (id == HifumiSkillIds.GamblerMove)
        {
            int burned = bone;
            bone = 0;
            owner?.AddTurnClashPowerBonus((burned / 100) * 6);
        }
        else if (id == HifumiSkillIds.AllIn)
        {
            ResolveAllIn();
        }
        else if (id == HifumiSkillIds.Trick)
        {
            trickActive = true;
            forceTrickFailure = false;
        }
        else if (id == HifumiSkillIds.CleanseAll)
        {
            // 0922 M 구조: 캐릭터 상태만 전부 제거하고 부위 상태는 건드리지 않는다.
            // 실제 사용은 뼈 비용/빛/굴림 수가 (미정)이므로 CanUseSkill에서 잠겨 있다.
            if (owner?.StatusEffects != null)
            {
                List<StatusEffect> remove = new();
                foreach (StatusEffect status in owner.StatusEffects)
                {
                    if (status != null)
                        remove.Add(status);
                }

                foreach (StatusEffect status in remove)
                {
                    owner.RemoveStatus(
                        status,
                        StatusEffectRemoveReason.Cleared);
                }
            }
        }
    }

    public void SetTrickOutcomeForTurn(bool forceFailure)
    {
        if (!trickActive)
            return;

        forceTrickFailure = forceFailure;
    }

    public bool TryGetForcedChinchiro(
        out ChinchiroCombination combination)
    {
        if (!trickActive)
        {
            combination = ChinchiroCombination.None;
            return false;
        }

        combination = forceTrickFailure
            ? ChinchiroCombination.Hifumi
            : ChinchiroCombination.Arashi;
        return true;
    }

    public void AddBone(int amount)
    {
        if (amount <= 0)
            return;

        bone = Mathf.Clamp(bone + amount, 0, MaxBone);
    }

    public void AddAdvanceBone(int amount)
    {
        if (amount <= 0)
            return;

        // 0922: 가불 자체에는 상한이 없다.
        advanceBone = Mathf.Max(0, advanceBone + amount);
    }

    /// <summary>
    /// 모든 뼈 비용은 가불을 먼저 지불하고 부족분만 실제 뼈에서 낸다.
    /// </summary>
    public bool ConsumeBone(int amount)
    {
        int value = Mathf.Max(0, amount);
        if (SpendableBone < value)
            return false;

        int advancePaid = Mathf.Min(advanceBone, value);
        advanceBone -= advancePaid;
        value -= advancePaid;

        if (value > 0)
            bone = Mathf.Max(0, bone - value);

        return true;
    }

    public int ConvertAllAdvanceToBone()
    {
        int converted = advanceBone;
        advanceBone = 0;
        if (converted > 0)
            bone = Mathf.Clamp(bone + converted, 0, MaxBone);
        return converted;
    }

    private void ClearAdvanceBone()
    {
        advanceBone = 0;
    }

    private void ConsumeAllBonePaymentResources()
    {
        bone = 0;
        advanceBone = 0;
    }

    public void SetBoneForVerification(int value)
    {
        bone = Mathf.Clamp(value, 0, MaxBone);
    }

    public void SetAdvanceBoneForVerification(int value)
    {
        advanceBone = Mathf.Max(0, value);
    }

    public void ClearAdvanceForVerification() => ClearAdvanceBone();

    public void SetEngraveProgressForVerification(
        int stage,
        HifumiEngraveBranch branch,
        int victoryProgress = 0,
        int defeatProgress = 0)
    {
        EngraveProgress?.SetForVerification(
            stage, branch, victoryProgress, defeatProgress);
    }

    public int GetEngraveRollCountForVerification() => EngraveRollCount;
    public int GetEngraveEnergyCostForVerification() => EngraveEnergyCost;

    public bool IsCompleteBlockForVerification(int hpDamageTaken, int staggerDamageTaken) =>
        hpDamageTaken <= 0 && staggerDamageTaken <= 0;

    public bool ResolveEngraveStage4CompleteBlockForVerification(
        int hpDamageTaken,
        int staggerDamageTaken)
    {
        if (EngraveStage < 4 ||
            EngraveBranch != HifumiEngraveBranch.Victory ||
            hpDamageTaken > 0 ||
            staggerDamageTaken > 0)
        {
            return false;
        }

        ConvertAllAdvanceToBone();
        return true;
    }

    public int ResolveRaiseStakeEnergyCost(int baseCost) =>
        Mathf.Max(0, baseCost) + Mathf.Max(0, raiseStakeEnergyIncrease);

    public int ResolveIncomingDamageForVerification(
        int damage,
        bool lastStand,
        bool pokerFace)
    {
        int result = Mathf.Max(0, damage);

        // LastStand never changes Hifumi damage in 0916.
        if (pokerFace)
            result = Mathf.Max(0, result - 4);

        return result;
    }

    public int ResolveBoneGainForVerification(
        int finalHpDamage,
        bool lastStand,
        bool selfCost,
        bool pokerFaceHit)
    {
        int gain = Mathf.Max(0, finalHpDamage);
        if (pokerFaceHit)
            gain += 4;
        return gain;
    }

    public void ApplyActionStartCostForVerification(string skillId)
    {
        ApplyActionStartCost(skillId);
    }

    public bool ApplyBoldJudgmentClashEndForVerification(
        int lostExchanges)
    {
        // Legacy oracle helper only. 0922 runtime no longer uses exchange loss count.
        return lostExchanges == 0;
    }

    public bool ApplyBoldJudgmentCompleteBlockForVerification(
        int hpDamageTaken,
        int staggerDamageTaken)
    {
        return TryApplyBoldJudgmentCompleteBlockReward(
            Mathf.Max(0, hpDamageTaken),
            Mathf.Max(0, staggerDamageTaken));
    }

    public void QueueGovernorForVerification(int penalty)
    {
        governorVerificationPenalty = Mathf.Max(0, penalty);
        governorQueued = true;
    }

    public void ActivateGovernorForVerification()
    {
        ActivateQueuedGovernor();
    }

    public HifumiCounterPreview BuildCounterPreviewForVerification(
        string skillId,
        int lostExchanges,
        int boneValue,
        bool sourcePartBroken,
        bool targetAlreadyWeakened)
    {
        bool enabled =
            IsCounterSkillForCurrentRun(skillId) &&
            !sourcePartBroken &&
            lostExchanges > 0;

        if (!enabled)
        {
            return new HifumiCounterPreview(
                false, 0, 0, 0, 0,
                false, false, false, 0);
        }

        int snapshot = Mathf.Clamp(boneValue, 0, MaxBone);
        int tier = Mathf.Clamp(snapshot / BonePerTier, 0, MaxBoneTier);
        bool bloom = snapshot + advanceBone >= BloomThreshold;
        bool normal = HifumiSkillIds.IsNormalCounterSkill(skillId);
        bool yukcham = skillId == HifumiSkillIds.Yukcham;
        bool goldan = skillId == HifumiSkillIds.Goldan;

        int baseCounterPower = normal
            ? NormalCounterBasePower + HifumiBasePowerBonus
            : DuelCounterBasePower + tier + HifumiBasePowerBonus;

        int previewCounterCount =
            goldan && bloom
                ? lostExchanges * 2
                : lostExchanges;

        return new HifumiCounterPreview(
            true,
            previewCounterCount,
            baseCounterPower,
            yukcham ? (tier + 1) * 20 : 0,
            goldan && bloom ? 350 : 0,
            goldan,
            goldan && bloom && !targetAlreadyWeakened,
            goldan && bloom,
            goldan && bloom ? 25 : 0);
    }

    public void ResolveAllInForVerification(
        ChinchiroCombination first,
        ChinchiroCombination second,
        ChinchiroCombination third)
    {
        ResolveAllIn(new[] { first, second, third });
    }

    public int GetCounterPoolForVerification(
        string skillId,
        int taggedLosses,
        int catastropheCount)
    {
        int tagged = IsCounterSkillForCurrentRun(skillId)
            ? Mathf.Max(0, taggedLosses)
            : 0;
        return tagged + Mathf.Max(0, catastropheCount);
    }

    public int GetCounterAttemptsAfterCatastrophesForVerification(
        int initialCounterCount,
        int catastropheCount)
    {
        return Mathf.Max(0, initialCounterCount) +
               Mathf.Max(0, catastropheCount);
    }

    public bool IsShakeFateCommonCatastropheSuppressedForVerification() => true;

    private void OnTurnStart(int turn)
    {
        pokerFaceActive = false;
        engraveBoneActive = false;
        trickActive = false;
        forceTrickFailure = false;
        catastropheCounterBonusByAction.Clear();
        counterPowerBonusByAction.Clear();
        processedCatastrophes.Clear();

        // [0917_CONFIRMED_GAP:HIFUMI_C_TURN_START]
        recklessBetIncomingDamagePenaltyActive =
            Mathf.Max(0, recklessBetIncomingDamagePenaltyQueued);
        recklessBetIncomingDamagePenaltyQueued = 0;

        ActivateQueuedGovernor();

        if (nextTurnSpeedPenalty)
        {
            owner.AddStatus(
                new HifumiSpeedPenaltyStatus(),
                owner);
            nextTurnSpeedPenalty = false;
        }
    }

    private void ActivateQueuedGovernor()
    {
        governorActivePenalty =
            governorQueued
                ? ResolveConfiguredGovernorPenalty()
                : 0;
        governorQueued = false;
    }

    private void OnTurnEnd(int turn)
    {
        pokerFaceActive = false;
        engraveBoneActive = false;
        trickActive = false;
        forceTrickFailure = false;
        recklessBetIncomingDamagePenaltyActive = 0; // [0917_CONFIRMED_GAP:HIFUMI_C_TURN_END]
        governorActivePenalty = 0;
        boldJudgmentTracking = false;
        boldJudgmentHpDamageTaken = 0;
        engraveCompleteBlockTracking = false;
        engraveHpDamageTaken = 0;
        ClearAdvanceBone();
        catastropheCounterBonusByAction.Clear();
        counterPowerBonusByAction.Clear();
        processedCatastrophes.Clear();
    }

    private void OnActionStart(BattleAction action)
    {
        if (action?.Owner != owner)
            return;

        string skillId = action.Skill?.Definition?.SkillId;

        if (skillId == HifumiSkillIds.BoldJudgment)
        {
            boldJudgmentTracking = true;
            boldJudgmentHpDamageTaken = 0;
        }

        if (skillId == HifumiSkillIds.EngraveBody &&
            EngraveStage >= 4 &&
            EngraveBranch == HifumiEngraveBranch.Victory)
        {
            engraveCompleteBlockTracking = true;
            engraveHpDamageTaken = 0;
        }

        ApplyActionStartCost(skillId);
    }

    private void ApplyActionStartCost(string id)
    {
        if (id == HifumiSkillIds.BoldJudgment)
        {
            if (!ConsumeBone(40))
                AddBone(20);

            // 0916: both branches schedule next-turn speed -1.
            nextTurnSpeedPenalty = true;
        }
        else if (id == HifumiSkillIds.RecklessBet)
        {
            if (SpendableBone >= 70)
            {
                ConsumeBone(40);
            }
            else
            {
                AddBone(100);

                // [0917_CONFIRMED_GAP:HIFUMI_C_QUEUE]
                // 0917 확정: 다음 턴 받는 피해 +3/교환.
                recklessBetIncomingDamagePenaltyQueued += 3;
            }
        }
    }

    private void OnDamageResolved(DamageEventResult result)
    {
        DamageContext context = result?.Context;
        if (context == null || context.Target != owner)
            return;

        int finalHpDamage =
            result?.DamageResult?.FinalHpDamage ??
            context.FinalHpDamage;

        if (finalHpDamage <= 0)
            return;

        if (boldJudgmentTracking)
            boldJudgmentHpDamageTaken += finalHpDamage;
        if (engraveCompleteBlockTracking)
            engraveHpDamageTaken += finalHpDamage;

        // H-03: exact post-mitigation HP loss 1:1. No inverse/LastStand compensation.
        AddBone(finalHpDamage);

        // Poker Face buys back 4 bone per actual hit after its -4 reduction.
        if (pokerFaceActive && context.Action != null)
            AddBone(4);
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (exchange == null || exchange.WasCancelled)
            return;

        BattleAction myAction;
        RollResult myRoll;

        if (exchange.FirstAction?.Owner == owner)
        {
            myAction = exchange.FirstAction;
            myRoll = exchange.FirstRollResult;
        }
        else if (exchange.SecondAction?.Owner == owner)
        {
            myAction = exchange.SecondAction;
            myRoll = exchange.SecondRollResult;
        }
        else
        {
            return;
        }

        if (myAction == null)
            return;

        string skillId = myAction.Skill?.Definition?.SkillId;
        bool isCatastrophe =
            myRoll?.ResolverType == SkillResolverType.Chinchiro &&
            myRoll.ChinchiroCombination == ChinchiroCombination.Hifumi;

        if (isCatastrophe && skillId != HifumiSkillIds.ShakeFate)
        {
            ResolveCatastrophe(
                myAction,
                exchange);

            if (skillId == HifumiSkillIds.RollCompendium &&
                !exchange.IsOneSided &&
                exchange.IsDuelExchange)
            {
                counterPowerBonusByAction.TryGetValue(
                    myAction.ActionId,
                    out int currentBonus);
                counterPowerBonusByAction[myAction.ActionId] =
                    currentBonus + 1;
            }
        }

        if (engraveBoneActive &&
            exchange.LoserAction == myAction)
        {
            AddBone(30);
        }

        if (!exchange.IsOneSided &&
            !exchange.IsTie &&
            skillId == HifumiSkillIds.EngraveBody)
        {
            EngraveProgress?.RecordExchangeOutcome(
                exchange.WinnerAction == myAction);
        }

        if (!exchange.IsOneSided &&
            !exchange.IsTie &&
            skillId == HifumiSkillIds.RaiseStake)
        {
            if (exchange.WinnerAction == myAction)
                raiseStakePowerBonus++;
            else if (exchange.LoserAction == myAction)
                raiseStakeEnergyIncrease++;
        }

        if (!exchange.IsOneSided &&
            !exchange.IsTie &&
            exchange.WinnerAction == myAction &&
            myAction.ActionType == ActionType.Duel)
        {
            // 0922 자기 제약은 이번 턴 여러 번 이겨도 다음 턴 상태 1개만 예약.
            governorQueued = true;
        }
    }

    private void ResolveCatastrophe(
        BattleAction action,
        ClashExchangeResult exchange)
    {
        if (action == null || exchange == null)
            return;

        string key =
            $"{action.ActionId}:{exchange.ExchangeIndex}:" +
            (exchange.IsOneSided ? "O" : "P");

        if (!processedCatastrophes.Add(key))
            return;

        ApplyCatastropheSelfDamage();

        if (owner == null || owner.IsDead)
            return;

        if (exchange.IsOneSided)
        {
            Character target = action.Target;
            BodyPart targetPart = action.TargetPart;

            ResolveCounterSequence(
                action,
                target,
                targetPart,
                1,
                applyTaggedSkillReward: false);
            return;
        }

        catastropheCounterBonusByAction.TryGetValue(
            action.ActionId,
            out int current);

        catastropheCounterBonusByAction[action.ActionId] =
            current + 1;
    }

    private void ApplyCatastropheSelfDamage()
    {
        if (owner == null || owner.IsDead)
            return;

        battleContext?.Services?.DamageManager
            ?.ApplyDamageContext(
                DamageRequest.SelfCost(
                    owner,
                    HifumiSelfDamage));
    }

    private void OnClashResolved(ClashResultContext clash)
    {
        if (clash?.Exchanges == null || owner == null)
            return;

        BattleAction myAction =
            clash.FirstAction?.Owner == owner
                ? clash.FirstAction
                : clash.SecondAction?.Owner == owner
                    ? clash.SecondAction
                    : null;

        if (myAction == null)
            return;

        ApplyBoldJudgmentClashEndReward(
            clash,
            myAction);

        ApplyEngraveStage4CompleteBlock(
            clash,
            myAction);

        int catastropheBonus = 0;
        catastropheCounterBonusByAction.TryGetValue(
            myAction.ActionId,
            out catastropheBonus);
        catastropheCounterBonusByAction.Remove(
            myAction.ActionId);

        counterPowerBonusByAction.TryGetValue(
            myAction.ActionId,
            out int counterPowerBonus);
        counterPowerBonusByAction.Remove(
            myAction.ActionId);

        if (owner.IsDead ||
            myAction.OwnerPart == null ||
            myAction.OwnerPart.IsBroken)
        {
            return;
        }

        int taggedLosses =
            CountTaggedCounterLosses(
                clash,
                myAction);

        int counterCount =
            Mathf.Max(0, taggedLosses) +
            Mathf.Max(0, catastropheBonus);

        // 0917 §19 Goldan: Bloom(뼈 500) 상태의 골단 반격 풀은 x2.
        // tagged loss + catastrophe bonus로 완성된 '이번 합의 반격 풀'을 두 배로 만든다.
        if (myAction.Skill?.Definition?.SkillId == HifumiSkillIds.Goldan &&
            IsBloom)
        {
            counterCount *= 2;
        }

        if (counterCount <= 0)
            return;

        BattleAction opponentAction =
            myAction == clash.FirstAction
                ? clash.SecondAction
                : clash.FirstAction;

        Character target =
            opponentAction?.Owner ??
            myAction.Target;

        BodyPart targetPart =
            opponentAction?.OwnerPart ??
            myAction.TargetPart;

        ResolveCounterSequence(
            myAction,
            target,
            targetPart,
            counterCount,
            applyTaggedSkillReward:
                taggedLosses > 0,
            counterPowerBonus:
                counterPowerBonus);
    }

    private void ApplyEngraveStage4CompleteBlock(
        ClashResultContext clash,
        BattleAction myAction)
    {
        if (!engraveCompleteBlockTracking ||
            myAction?.Skill?.Definition?.SkillId != HifumiSkillIds.EngraveBody ||
            EngraveStage < 4 ||
            EngraveBranch != HifumiEngraveBranch.Victory ||
            clash?.IsClash != true)
        {
            return;
        }

        int staggerDamageTaken = 0;
        foreach (ClashExchangeResult exchange in clash.Exchanges)
        {
            if (exchange == null || exchange.WasCancelled)
                continue;
            if (exchange.LoserAction == myAction)
                staggerDamageTaken += Mathf.Max(0, exchange.StaggerDamage);
        }

        if (engraveHpDamageTaken <= 0 && staggerDamageTaken <= 0)
            ConvertAllAdvanceToBone();

        engraveCompleteBlockTracking = false;
        engraveHpDamageTaken = 0;
    }

    private void ApplyBoldJudgmentClashEndReward(
        ClashResultContext clash,
        BattleAction myAction)
    {
        if (myAction?.Skill?.Definition?.SkillId !=
                HifumiSkillIds.BoldJudgment ||
            clash?.IsClash != true ||
            clash.PairedExchangeCount <= 0)
        {
            return;
        }

        int hpDamageTaken = Mathf.Max(0, boldJudgmentHpDamageTaken);
        int staggerDamageTaken = 0;

        foreach (ClashExchangeResult exchange in clash.Exchanges)
        {
            if (exchange == null || exchange.WasCancelled)
                continue;

            // StaggerDamage는 해당 교환 패자에게 적용되는 최종 감소량이다.
            if (exchange.LoserAction == myAction)
                staggerDamageTaken += Mathf.Max(0, exchange.StaggerDamage);
        }

        TryApplyBoldJudgmentCompleteBlockReward(
            hpDamageTaken,
            staggerDamageTaken);

        boldJudgmentTracking = false;
        boldJudgmentHpDamageTaken = 0;
    }

    private bool TryApplyBoldJudgmentCompleteBlockReward(
        int hpDamageTaken,
        int staggerDamageTaken)
    {
        if (hpDamageTaken > 0 || staggerDamageTaken > 0)
            return false;

        AddBone(50);
        return true;
    }

    private int CountTaggedCounterLosses(
        ClashResultContext clash,
        BattleAction myAction)
    {
        string skillId =
            myAction?.Skill?.Definition?.SkillId;

        bool normalCounter =
            HifumiSkillIds.IsNormalCounterSkill(skillId);

        bool duelCounter =
            IsDuelCounterSkillForCurrentRun(skillId);

        if (!normalCounter && !duelCounter)
            return 0;

        int lost = 0;

        foreach (ClashExchangeResult exchange in clash.Exchanges)
        {
            if (exchange == null ||
                exchange.WasCancelled ||
                exchange.IsOneSided ||
                exchange.IsTie ||
                exchange.LoserAction != myAction)
            {
                continue;
            }

            if (normalCounter)
            {
                // Small Change has no Duel×Duel gate.
                lost++;
            }
            else if (duelCounter && exchange.IsDuelExchange)
            {
                // Yukcham/Goldan counter loss merit is inside the Duel gate.
                lost++;
            }
        }

        return lost;
    }

    private void ResolveCounterSequence(
        BattleAction sourceAction,
        Character target,
        BodyPart targetPart,
        int initialCounterCount,
        bool applyTaggedSkillReward,
        int counterPowerBonus = 0)
    {
        if (sourceAction == null ||
            owner == null ||
            owner.IsDead ||
            target == null ||
            target.IsDead ||
            initialCounterCount <= 0)
        {
            return;
        }

        string skillId =
            sourceAction.Skill?.Definition?.SkillId;

        bool isNormalCounter =
            sourceAction.ActionType == ActionType.NormalAttack;

        bool isYukcham =
            applyTaggedSkillReward &&
            skillId == HifumiSkillIds.Yukcham;

        bool isGoldan =
            applyTaggedSkillReward &&
            skillId == HifumiSkillIds.Goldan;

        int boneSnapshot = bone;
        int tierSnapshot =
            Mathf.Clamp(
                boneSnapshot / BonePerTier,
                0,
                MaxBoneTier);

        bool bloomSnapshot =
            boneSnapshot + advanceBone >= BloomThreshold;

        int baseCounterPower =
            (isNormalCounter
                ? NormalCounterBasePower + HifumiBasePowerBonus
                : DuelCounterBasePower +
                  tierSnapshot +
                  HifumiBasePowerBonus) +
            Mathf.Max(0, counterPowerBonus);

        int pending = initialCounterCount;
        int sequenceIndex = 0;
        int attempted = 0;
        bool bloomBreakAttempted = false;

        while (pending > 0 &&
               attempted < CounterSafetyLimit)
        {
            pending--;
            attempted++;

            if (owner.IsDead ||
                sourceAction.OwnerPart == null ||
                sourceAction.OwnerPart.IsBroken ||
                target.IsDead)
            {
                break;
            }

            RollResult roll =
                HifumiChinchiroRuntime.RollStandalone(
                    owner,
                    baseCounterPower);

            bool catastrophe =
                roll?.ChinchiroCombination ==
                ChinchiroCombination.Hifumi;

            BattleReactiveRollEvent reactiveRoll =
                new BattleReactiveRollEvent
                {
                    SourceKind = BattleReactiveRollSourceKind.Counter,
                    DisplayName = catastrophe
                        ? "뼈 반격 · 대실패"
                        : "뼈 반격",
                    SourceAction = sourceAction,
                    Owner = owner,
                    OwnerPart = sourceAction.OwnerPart,
                    Target = target,
                    TargetPart = targetPart,
                    RollType = CombatRollType.Attack,
                    PhysicalType =
                        PhysicalDamageResolver.Resolve(sourceAction),
                    SequenceIndex = sequenceIndex,
                    SequenceCount = initialCounterCount,
                    Power = catastrophe
                        ? 0
                        : roll?.FinalPower ?? 0
                };

            battleEvent?.RaiseReactiveRollStarted(
                reactiveRoll);

            if (catastrophe)
            {
                // 0922: 반격 굴림 자체의 1·2·3도 공통 대실패를 그대로 적용한다.
                // 현재 반격은 실패하고 자해60 -> 뼈 적립 -> 추가 반격 +1을
                // 현재 큐의 뒤에 붙인다.
                reactiveRoll.Complete(null);
                battleEvent?.RaiseReactiveRollResolved(
                    reactiveRoll);

                ApplyCatastropheSelfDamage();
                if (owner != null && !owner.IsDead)
                    pending++;

                sequenceIndex++;
                continue;
            }

            int counterPower =
                Mathf.Max(1, roll?.FinalPower ?? 1);

            bool canBloomBreak =
                isGoldan &&
                bloomSnapshot &&
                !bloomBreakAttempted;

            if (canBloomBreak)
                bloomBreakAttempted = true;

            DamageRequest request =
                DamageRequest.Custom(
                    DamageType.Counter,
                    owner,
                    target,
                    targetPart,
                    counterPower,
                    1f,
                    canBreakPart: canBloomBreak,
                    applyMomentum: false,
                    applyGuard: true,
                    sourceAction: sourceAction);

            if (canBloomBreak)
            {
                // H-06: the sole prior-weaken bypass path.
                request.BreakMode =
                    PartBreakMode.IgnoreWeakenedPrerequisite;
            }

            DamageContext counterDamage =
                battleContext?.Services?.DamageManager
                    ?.ApplyDamageContext(request);

            reactiveRoll.Complete(counterDamage);
            battleEvent?.RaiseReactiveRollResolved(
                reactiveRoll);

            sequenceIndex++;
        }

        if (attempted <= 0)
            return;

        if (isYukcham)
        {
            AddBone((tierSnapshot + 1) * 20);
        }

        if (isGoldan)
        {
            if (bloomSnapshot)
            {
                owner.RestoreCurrentHP(350);

                if (targetPart != null &&
                    target != null &&
                    !target.IsDead &&
                    !targetPart.IsBroken)
                {
                    // If the bypass damage did not actually break the part,
                    // bloom Goldan still guarantees immediate weaken.
                    target.WeakenPart(
                        targetPart,
                        owner,
                        sourceAction);
                }

                battleContext?.Services?.MomentumManager
                    ?.ApplySkillShift(
                        owner,
                        25);
            }

            // 0922: Goldan tagged counter consumes the whole spendable Bone pool.
            ConsumeAllBonePaymentResources();
        }
    }

    public bool IsDuelCounterSkillForCurrentRun(string skillId)
    {
        if (HifumiSkillIds.IsDuelCounterSkill(skillId))
            return true;

        // 0922 §19.5: 몸에 새기다(I)는 공통 시작/패배 축에서는 (반격),
        // 승리 축 2굴림 이상에서는 (반격) 태그가 제거된다.
        return skillId == HifumiSkillIds.EngraveBody &&
               EngraveCounterEnabled;
    }

    public bool IsCounterSkillForCurrentRun(string skillId)
    {
        return HifumiSkillIds.IsNormalCounterSkill(skillId) ||
               IsDuelCounterSkillForCurrentRun(skillId);
    }

    public bool TryAdvanceEngraveProgress(
        int victoryThreshold,
        int defeatThreshold)
    {
        return EngraveProgress?.TryAdvance(
            victoryThreshold,
            defeatThreshold) == true;
    }

    private int ResolveConfiguredGovernorPenalty()
    {
        if (governorVerificationPenalty >= 0)
            return governorVerificationPenalty;

        return owner is Hifumi hifumi
            ? hifumi.DuelGovernorPowerPenalty
            : 0;
    }

    private DeferredStatusEffect FindDeferredStatus(
        StatusEffectId id)
    {
        if (owner?.StatusEffects == null)
            return null;

        foreach (StatusEffect effect in owner.StatusEffects)
        {
            if (effect is DeferredStatusEffect deferred &&
                deferred.DeferredStatusId == id)
            {
                return deferred;
            }
        }

        return null;
    }

    private void QueueNextTurnRupture()
    {
        // 0916: exact incoming-damage increase is still Unset.
        // Keep the legacy runtime slot for Phase E data migration, but do not
        // invent a new Phase D value here.
    }

    private void ResolveAllIn()
    {
        ResolveAllIn(
            new[]
            {
                RollCombination(),
                RollCombination(),
                RollCombination()
            });
    }

    private void ResolveAllIn(
        IReadOnlyList<ChinchiroCombination> rolls)
    {
        // 0922: 실제 뼈 전량을 먼저 wager한다. 가불은 별도 턴 자원이므로
        // prestige wager에 섞지 않는다.
        int wageredBone = bone;
        bone = 0;

        ChinchiroCombination best = ChinchiroCombination.None;

        if (rolls != null)
        {
            for (int i = 0; i < rolls.Count; i++)
            {
                ChinchiroCombination rolled = rolls[i];

                // 대실패 하나라도 있으면 최고 결과보다 우선하여 Bone=0.
                if (rolled == ChinchiroCombination.Hifumi)
                    return;

                if (Rank(rolled) > Rank(best))
                    best = rolled;
            }
        }

        switch (best)
        {
            case ChinchiroCombination.Arashi:
            case ChinchiroCombination.Shigoro:
                bone = MaxBone;
                break;

            case ChinchiroCombination.Moku:
                bone = Mathf.Clamp(wageredBone + 100, 0, MaxBone);
                break;

            case ChinchiroCombination.Blank:
            case ChinchiroCombination.None:
            default:
                bone = 0;
                break;
        }
    }

    private static ChinchiroCombination RollCombination()
    {
        int a = Random.Range(1, 7);
        int b = Random.Range(1, 7);
        int c = Random.Range(1, 7);

        int[] values = { a, b, c };
        System.Array.Sort(values);

        if (a == b && b == c)
            return ChinchiroCombination.Arashi;
        if (values[0] == 4 && values[1] == 5 && values[2] == 6)
            return ChinchiroCombination.Shigoro;
        if (values[0] == 1 && values[1] == 2 && values[2] == 3)
            return ChinchiroCombination.Hifumi;
        if (a == b || a == c || b == c)
            return ChinchiroCombination.Moku;
        return ChinchiroCombination.Blank;
    }

    private static int Rank(
        ChinchiroCombination combination) =>
        combination switch
        {
            ChinchiroCombination.Arashi => 4,
            ChinchiroCombination.Shigoro => 3,
            ChinchiroCombination.Moku => 2,
            ChinchiroCombination.Blank => 1,
            _ => 0
        };
}
