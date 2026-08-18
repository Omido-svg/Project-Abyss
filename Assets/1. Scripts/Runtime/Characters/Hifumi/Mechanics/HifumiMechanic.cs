using System.Collections.Generic;
using UnityEngine;

public readonly struct HifumiCounterPreview
{
    public HifumiCounterPreview(
        bool enabled,
        int counterCount,
        int counterPower,
        int heal,
        bool consumeAllBone,
        bool weaken,
        bool breakPart,
        int momentumPush)
    {
        Enabled = enabled;
        CounterCount = counterCount;
        CounterPower = counterPower;
        Heal = heal;
        ConsumeAllBone = consumeAllBone;
        Weaken = weaken;
        BreakPart = breakPart;
        MomentumPush = momentumPush;
    }

    public bool Enabled { get; }
    public int CounterCount { get; }
    public int CounterPower { get; }
    public int Heal { get; }
    public bool ConsumeAllBone { get; }
    public bool Weaken { get; }
    public bool BreakPart { get; }
    public int MomentumPush { get; }
}

/// <summary>
/// 히후미 코어: 뼈 0~500 / 짓눌림 생존 / 친치로 / 반격.
/// 기획 미확정값은 PATCH_NOTES의 Provisional Decisions에 기록한다.
/// </summary>
public sealed class HifumiMechanic : CombatMechanic, ICharacterUniqueGaugeProvider
{
    public const int MaxBone = 500;
    public const int BloomThreshold = 500;
    public const int HifumiSelfDamage = 60;

    private int bone;
    private bool pokerFaceActive;
    private bool engraveBoneActive;
    private bool nextTurnSpeedPenalty;
    private bool selfDamageTriggeredThisTurn;
    private bool trickActive;
    private bool forceTrickFailure;

    private readonly HashSet<long> boldJudgmentRewardedActions = new();

    public int Bone => bone;
    public int BoneBand => Mathf.Min(4, bone / 100);
    public bool IsBloom => bone >= BloomThreshold;

    public string GaugeLabel => "뼈";
    public float GaugeNormalized => (float)bone / MaxBone;
    public string GaugeValueText => $"{bone}/{MaxBone}" + (IsBloom ? " · 만개" : string.Empty);

    public override string MechanicName => "Hifumi Bone / Chinchiro / Counter";

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
            () => battleEvent.OnActionEnd += OnActionEnd,
            () => battleEvent.OnActionEnd -= OnActionEnd,
            "OnActionEnd");

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
        selfDamageTriggeredThisTurn = false;
        trickActive = false;
        forceTrickFailure = false;
        boldJudgmentRewardedActions.Clear();
    }

    public override int ModifyRoll(BattleAction action, int roll)
    {
        if (action?.Owner != owner || action.CurrentRollType != CombatRollType.Attack)
            return roll;

        // PPT의 친치로 평균 보정 +1을 캐릭터 단위로 유지한다.
        int result = roll + 1;

        // 최신 사용자 문서가 PPT의 "골단 -구간×4"를 덮어씀: +구간×4.
        if (action.Skill?.Definition?.SkillId == HifumiSkillIds.Goldan)
            result += BoneBand * 4;

        return result;
    }

    public override int ModifyDamageTaken(DamageContext context, int damage)
    {
        if (context == null || context.Target != owner || damage <= 0)
            return damage;

        int result = damage;

        // 짓눌림: 받는 피해 절반. 자해 비용은 캐릭터 외부 공격이 아니므로 제외한다.
        if (context.DamageType != DamageType.SelfCost && IsLastStand())
            result = Mathf.CeilToInt(result * 0.5f);

        // 최신 사용자 문서 우선: 포커페이스는 "교환당 -1".
        if (pokerFaceActive && context.Action != null)
            result = Mathf.Max(0, result - 1);


        return result;
    }

    public void ExecuteSkill(BattleAction action)
    {
        if (action?.Owner != owner)
            return;

        ExecuteSkillId(action.Skill?.Definition?.SkillId);
    }

    public void ExecuteSkillForVerification(string skillId)
    {
        ExecuteSkillId(skillId);
    }

    private void ExecuteSkillId(string id)
    {
        if (id == HifumiSkillIds.PokerFace)
        {
            pokerFaceActive = true;
        }
        else if (id == HifumiSkillIds.EngraveBone)
        {
            engraveBoneActive = true;
        }
        else if (id == HifumiSkillIds.GiveFlesh)
        {
            AddBone(100);
            QueueNextTurnRupture();
        }
        else if (id == HifumiSkillIds.FoldHand)
        {
            if (bone >= 100)
            {
                ConsumeBone(100);
                owner?.RestoreCurrentHP(70);
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

    public bool TryGetForcedChinchiro(out ChinchiroCombination combination)
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

        if (lastStand)
            result = Mathf.CeilToInt(result * 0.5f);

        if (pokerFace)
            result = Mathf.Max(0, result - 1);

        return result;
    }

    public int ResolveBoneGainForVerification(
        int appliedDamage,
        bool lastStand,
        bool selfCost,
        bool pokerFaceHit)
    {
        int gain = Mathf.Max(0, appliedDamage);

        if (!selfCost && lastStand)
            gain *= 2;

        if (pokerFaceHit)
            gain += 4;

        return gain;
    }

    public void ApplyActionStartCostForVerification(string skillId)
    {
        ApplyActionStartCost(skillId);
    }

    public HifumiCounterPreview BuildCounterPreviewForVerification(
        string skillId,
        int lostExchanges,
        int boneValue,
        bool sourcePartBroken,
        bool targetAlreadyWeakened)
    {
        bool enabled =
            HifumiSkillIds.IsCounterEnabled(skillId) &&
            !sourcePartBroken &&
            lostExchanges > 0;

        if (!enabled)
        {
            return new HifumiCounterPreview(
                false, 0, 0, 0, false, false, false, 0);
        }

        int snapshot = Mathf.Clamp(boneValue, 0, MaxBone);
        int band = Mathf.Min(4, snapshot / 100);
        bool bloom = snapshot >= BloomThreshold;

        int heal = bloom
            ? 350
            : band == 2
                ? 30
                : band >= 3
                    ? 60
                    : 0;

        return new HifumiCounterPreview(
            true,
            lostExchanges,
            8 + band,
            heal,
            bloom,
            bloom && !targetAlreadyWeakened,
            bloom && targetAlreadyWeakened,
            bloom ? 25 : 0);
    }

    public void ResolveAllInForVerification(
        ChinchiroCombination first,
        ChinchiroCombination second,
        ChinchiroCombination third)
    {
        ResolveAllIn(
            new[] { first, second, third });
    }

    private void OnTurnStart(int turn)
    {
        pokerFaceActive = false;
        engraveBoneActive = false;
        selfDamageTriggeredThisTurn = false;
        trickActive = false;
        forceTrickFailure = false;
        boldJudgmentRewardedActions.Clear();


        if (nextTurnSpeedPenalty)
        {
            owner.AddStatus(new HifumiSpeedPenaltyStatus(), owner);
            nextTurnSpeedPenalty = false;
        }
    }

    private void OnTurnEnd(int turn)
    {
        pokerFaceActive = false;
        engraveBoneActive = false;
        trickActive = false;
        forceTrickFailure = false;
        boldJudgmentRewardedActions.Clear();
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
            {
                AddBone(20);
                nextTurnSpeedPenalty = true;
            }
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

    private void OnActionEnd(BattleAction action)
    {
        if (action?.Owner != owner || selfDamageTriggeredThisTurn || action.RollHistory == null)
            return;

        foreach (RollResult result in action.RollHistory)
        {
            if (result?.ChinchiroCombination != ChinchiroCombination.Hifumi)
                continue;

            selfDamageTriggeredThisTurn = true;
            DamageRequest request = DamageRequest.SelfCost(owner, HifumiSelfDamage);
            battleContext?.Services?.DamageManager?.ApplyDamageContext(request);
            break;
        }
    }

    private void OnDamageResolved(DamageEventResult result)
    {
        DamageContext context = result?.Context;
        if (context == null || context.Target != owner)
            return;

        int applied = context.GetDisplayDamage();
        if (applied <= 0)
            return;

        int gain = applied;
        // PPT의 친치로 대실패는 "60 자해 → 같은 양 60 뼈"로 명시되어 있으므로
        // SelfCost는 짓눌림의 ×2 획득 보정에서 제외한다.
        if (context.DamageType != DamageType.SelfCost && IsLastStand())
            gain *= 2;

        AddBone(gain);

        // 포커페이스의 +4는 실제 피격 이벤트당 추가로 적립한다.
        if (pokerFaceActive && context.Action != null)
            AddBone(4);
    }

    private void OnExchangeResolved(ClashExchangeResult exchange)
    {
        if (exchange == null || exchange.WasCancelled || exchange.IsTie)
            return;

        BattleAction myAction = exchange.FirstAction?.Owner == owner
            ? exchange.FirstAction
            : exchange.SecondAction?.Owner == owner
                ? exchange.SecondAction
                : null;

        if (myAction == null)
            return;

        if (engraveBoneActive && exchange.LoserAction == myAction)
            AddBone(30);

        if (myAction.Skill?.Definition?.SkillId == HifumiSkillIds.BoldJudgment &&
            exchange.WinnerAction == myAction &&
            exchange.LoserAction != null &&
            exchange.LoserAction.CurrentRollType == CombatRollType.Attack &&
            !boldJudgmentRewardedActions.Contains(myAction.ActionId))
        {
            AddBone(50);
            boldJudgmentRewardedActions.Add(myAction.ActionId);
        }
    }

    private void OnClashResolved(ClashResultContext clash)
    {
        if (clash?.Exchanges == null || owner == null || owner.IsDead)
            return;

        BattleAction myAction = clash.FirstAction?.Owner == owner
            ? clash.FirstAction
            : clash.SecondAction?.Owner == owner
                ? clash.SecondAction
                : null;

        BattleAction opponentAction = myAction == clash.FirstAction
            ? clash.SecondAction
            : clash.FirstAction;

        if (myAction == null || opponentAction == null ||
            myAction.ActionType != ActionType.Duel || opponentAction.ActionType != ActionType.Duel ||
            !HifumiSkillIds.IsCounterEnabled(myAction.Skill?.Definition?.SkillId) ||
            myAction.OwnerPart == null || myAction.OwnerPart.IsBroken)
        {
            return;
        }

        int lost = 0;
        foreach (ClashExchangeResult exchange in clash.Exchanges)
        {
            if (exchange != null && !exchange.WasCancelled && !exchange.IsTie &&
                exchange.IsDuelExchange && exchange.LoserAction == myAction)
            {
                lost++;
            }
        }

        if (lost <= 0)
            return;

        int boneSnapshot = bone;
        int bandSnapshot = Mathf.Min(4, boneSnapshot / 100);
        bool bloomSnapshot = boneSnapshot >= BloomThreshold;

        BodyPart targetPart = opponentAction.OwnerPart ?? myAction.TargetPart;
        Character target = opponentAction.Owner;
        int executedCounters = 0;

        for (int i = 0; i < lost; i++)
        {
            if (owner.IsDead || myAction.OwnerPart.IsBroken || target == null || target.IsDead)
                break;

            // TODO2 우선: 반격 위력은 BASE 8 + 당시 뼈 구간.
            // 같은 합에서 만들어진 반격은 모두 합 종료 시점의 동일한 뼈 스냅샷을 사용한다.
            int counterPower = 8 + bandSnapshot;

            DamageRequest request = DamageRequest.Custom(
                DamageType.Counter,
                owner,
                target,
                targetPart,
                counterPower,
                1f,
                canBreakPart: false,
                applyMomentum: false,
                applyGuard: true,
                sourceAction: myAction);

            battleContext?.Services?.DamageManager?.ApplyDamageContext(request);
            executedCounters++;
        }

        if (executedCounters > 0)
        {
            // "첫 번째로 실제 사용되는 반격 굴림에 뼈 스택이 사용"은
            // 효과 산정 스냅샷을 한 번만 확정한다는 의미로 처리한다.
            // TODO2에서 전량 소모가 명시된 것은 만개 반격뿐이므로,
            // 0~499 구간의 일반 반격에서는 뼈를 소모하지 않는다.
            if (bloomSnapshot)
                bone = 0;

            ApplyCounterBandReward(
                bandSnapshot,
                bloomSnapshot,
                target,
                targetPart,
                myAction);
        }
    }

    private void ApplyCounterBandReward(
        int band,
        bool bloom,
        Character target,
        BodyPart targetPart,
        BattleAction sourceAction)
    {
        if (bloom)
        {
            owner.RestoreCurrentHP(350);

            if (targetPart != null && target != null)
            {
                if (targetPart.IsWeakened)
                    target.ForceBreakPart(targetPart, owner, sourceAction);
                else if (!targetPart.IsBroken)
                    target.WeakenPart(targetPart, owner, sourceAction);
            }

            battleContext?.Services?.MomentumManager?.ApplySkillShift(owner, 25);
            return;
        }

        if (band == 2)
            owner.RestoreCurrentHP(30);
        else if (band >= 3)
            owner.RestoreCurrentHP(60);
    }

    private void QueueNextTurnRupture()
    {
        // TODO2는 "다음 턴 받는 피해 증가"의 정확한 수치를 정하지 않았다.
        // 공용 상태이상 체계와 충돌 없이 확장할 수 있도록 최소 단위인
        // 균열 +1을 다음 턴 1턴 상태로 예약한다.
        owner?.AddStatus(
            new DeferredStatusEffect(StatusEffectId.Rupture, 1, 1),
            owner);
    }

    private bool IsLastStand()
    {
        return battleContext?.Services?.MomentumManager?.IsLastStand(owner) == true;
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
        ChinchiroCombination best = ChinchiroCombination.None;
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
            case ChinchiroCombination.Blank:
            case ChinchiroCombination.None:
            default:
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

    private static int Rank(ChinchiroCombination combination) => combination switch
    {
        ChinchiroCombination.Arashi => 4,
        ChinchiroCombination.Shigoro => 3,
        ChinchiroCombination.Moku => 2,
        ChinchiroCombination.Blank => 1,
        _ => 0
    };
}