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
    IForcedRollFailureRule
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
    private bool pokerFaceActive;
    private bool engraveBoneActive;
    private bool nextTurnSpeedPenalty;
    private bool trickActive;
    private bool forceTrickFailure;

    private bool governorQueued;
    private int governorActivePenalty;
    private int governorVerificationPenalty = -1;

    private readonly Dictionary<long, int>
        catastropheCounterBonusByAction = new();

    private readonly HashSet<string>
        processedCatastrophes = new();

    public int Bone => bone;
    public int BoneBand => Mathf.Clamp(bone / BonePerTier, 0, MaxBoneTier);
    public int BoneTier => BoneBand;
    public bool IsBloom => bone >= BloomThreshold;

    public bool GovernorQueued => governorQueued;
    public int ActiveGovernorPenalty => governorActivePenalty;
    public bool NextTurnSpeedPenaltyQueued => nextTurnSpeedPenalty;

    public string GaugeLabel => "뼈";
    public float GaugeNormalized => (float)bone / MaxBone;
    public string GaugeValueText =>
        $"{bone}/{MaxBone}" +
        (IsBloom ? " · 만개" : string.Empty);
    public int GaugeStateVersion => bone;

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
        pokerFaceActive = false;
        engraveBoneActive = false;
        nextTurnSpeedPenalty = false;
        trickActive = false;
        forceTrickFailure = false;
        governorQueued = false;
        governorActivePenalty = 0;
        governorVerificationPenalty = -1;
        catastropheCounterBonusByAction.Clear();
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

        int result = damage;

        // 0916 Poker Face: incoming HP damage -4 per hit/exchange.
        if (pokerFaceActive && context.Action != null)
            result = Mathf.Max(0, result - 4);

        return result;
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

        reason = "Hifumi Chinchiro 1·2·3 catastrophe";
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
            if (bone >= 100)
            {
                int boneBefore = bone;
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

    public bool ConsumeBone(int amount)
    {
        int value = Mathf.Max(0, amount);
        if (bone < value)
            return false;

        bone -= value;
        return true;
    }

    public void SetBoneForVerification(int value)
    {
        bone = Mathf.Clamp(value, 0, MaxBone);
    }

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
        return TryApplyBoldJudgmentPerfectBlockReward(
            Mathf.Max(0, lostExchanges));
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
            HifumiSkillIds.IsCounterSkill(skillId) &&
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
        bool bloom = snapshot >= BloomThreshold;
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

    private void OnTurnStart(int turn)
    {
        pokerFaceActive = false;
        engraveBoneActive = false;
        trickActive = false;
        forceTrickFailure = false;
        catastropheCounterBonusByAction.Clear();
        processedCatastrophes.Clear();

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
        governorActivePenalty = 0;
        catastropheCounterBonusByAction.Clear();
        processedCatastrophes.Clear();
    }

    private void OnActionStart(BattleAction action)
    {
        if (action?.Owner != owner)
            return;

        ApplyActionStartCost(
            action.Skill?.Definition?.SkillId);
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
            if (bone >= 70)
            {
                ConsumeBone(40);
            }
            else
            {
                AddBone(100);
                QueueNextTurnRupture();
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

        if (myRoll?.ResolverType == SkillResolverType.Chinchiro &&
            myRoll.ChinchiroCombination == ChinchiroCombination.Hifumi)
        {
            ResolveCatastrophe(
                myAction,
                exchange);
        }

        if (engraveBoneActive &&
            exchange.LoserAction == myAction)
        {
            AddBone(30);
        }

        if (!exchange.IsOneSided &&
            !exchange.IsTie &&
            exchange.WinnerAction == myAction &&
            myAction.ActionType == ActionType.Duel)
        {
            // H-10: wins in the same turn only refresh one queued state.
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

        int catastropheBonus = 0;
        catastropheCounterBonusByAction.TryGetValue(
            myAction.ActionId,
            out catastropheBonus);
        catastropheCounterBonusByAction.Remove(
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
                taggedLosses > 0);
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

        int lost = 0;
        foreach (ClashExchangeResult exchange in clash.Exchanges)
        {
            if (exchange == null ||
                exchange.WasCancelled ||
                exchange.IsOneSided ||
                exchange.IsTie)
            {
                continue;
            }

            if (exchange.LoserAction == myAction)
                lost++;
        }

        TryApplyBoldJudgmentPerfectBlockReward(lost);
    }

    private bool TryApplyBoldJudgmentPerfectBlockReward(
        int lostExchanges)
    {
        if (lostExchanges != 0)
            return false;

        AddBone(50);
        return true;
    }

    private static int CountTaggedCounterLosses(
        ClashResultContext clash,
        BattleAction myAction)
    {
        string skillId =
            myAction?.Skill?.Definition?.SkillId;

        bool normalCounter =
            HifumiSkillIds.IsNormalCounterSkill(skillId);

        bool duelCounter =
            HifumiSkillIds.IsDuelCounterSkill(skillId);

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
        bool applyTaggedSkillReward)
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
            boneSnapshot >= BloomThreshold;

        int baseCounterPower =
            isNormalCounter
                ? NormalCounterBasePower + HifumiBasePowerBonus
                : DuelCounterBasePower +
                  tierSnapshot +
                  HifumiBasePowerBonus;

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
                // 0916 §19.3: 반격 굴림 자체에서 1·2·3이 나왔을 때의
                // 추가 처리(자해/추가 반격 등)는 아직 (미정)이다.
                // Phase D에서는 값을 발명하지 않고 해당 반격 타격만 무효화한다.
                // 일반 공격/결투 굴림의 대실패는 ResolveCatastrophe에서
                // 확정 규칙(강제패배 + 자해60 + 반격굴림+1)을 처리한다.
                reactiveRoll.Complete(null);
                battleEvent?.RaiseReactiveRollResolved(
                    reactiveRoll);
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

            // Goldan consumes all bone whenever its tagged counter triggers.
            bone = 0;
        }
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
        ChinchiroCombination best =
            ChinchiroCombination.None;
        bool failure = false;

        if (rolls != null)
        {
            for (int i = 0; i < rolls.Count; i++)
            {
                ChinchiroCombination rolled = rolls[i];

                if (rolled == ChinchiroCombination.Hifumi)
                {
                    failure = true;
                    break;
                }

                if (Rank(rolled) > Rank(best))
                    best = rolled;
            }
        }

        if (failure)
        {
            bone = 0;
            return;
        }

        // All-In payout is still (미정). Preserve the legacy preview behavior
        // until Phase E data migration owns these values.
        switch (best)
        {
            case ChinchiroCombination.Arashi:
                bone = Mathf.Clamp(bone * 3, 0, MaxBone);
                break;
            case ChinchiroCombination.Shigoro:
                bone = Mathf.Clamp(bone * 2, 0, MaxBone);
                break;
            case ChinchiroCombination.Moku:
                AddBone(50);
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
